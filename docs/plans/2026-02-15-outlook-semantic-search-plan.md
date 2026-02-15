# Outlook Semantic Search Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Extend email-search to ingest emails from Microsoft 365 Graph API, with cross-source deduplication and incremental sync.

**Architecture:** New `graph_parser.py` module yields `ParsedEmail` objects (same as `pst_parser.py`), feeding into the unchanged classify → store pipeline. Deterministic IDs from RFC Message-ID prevent duplicates across PST and Graph API sources.

**Tech Stack:** Python 3.12, `requests` (new), `chromadb`, `click`, `rich` (existing)

---

### Task 1: Deterministic Email IDs

Replace random UUID `email.id` with a deterministic hash of `message_id` so the same email from PST and Graph API produces the same ChromaDB document ID.

**Files:**
- Modify: `email-search/email_search/models.py:35` (id field default_factory)
- Modify: `email-search/tests_py/test_models.py` (add dedup test)

**Step 1: Write the failing test**

Add to `email-search/tests_py/test_models.py`:

```python
def test_deterministic_id_from_message_id():
    """Same message_id should produce same email.id."""
    email1 = Email(
        message_id="<abc123@example.com>",
        date=datetime(2024, 1, 1),
        sender="alice@example.com",
        sender_name="Alice",
        recipients=["bob@example.com"],
        subject="Test",
        body_text="Hello",
    )
    email2 = Email(
        message_id="<abc123@example.com>",
        date=datetime(2024, 1, 1),
        sender="alice@example.com",
        sender_name="Alice",
        recipients=["bob@example.com"],
        subject="Test",
        body_text="Hello",
    )
    assert email1.id == email2.id


def test_different_message_ids_produce_different_ids():
    email1 = Email(
        message_id="<abc@example.com>",
        date=datetime(2024, 1, 1),
        sender="a@b.com",
        sender_name="A",
        recipients=[],
        subject="X",
        body_text="Y",
    )
    email2 = Email(
        message_id="<xyz@example.com>",
        date=datetime(2024, 1, 1),
        sender="a@b.com",
        sender_name="A",
        recipients=[],
        subject="X",
        body_text="Y",
    )
    assert email1.id != email2.id
```

**Step 2: Run tests to verify they fail**

Run: `cd /home/dan/source/claude-skills/email-search && .venv/bin/python -m pytest tests_py/test_models.py::test_deterministic_id_from_message_id tests_py/test_models.py::test_different_message_ids_produce_different_ids -v`

Expected: FAIL — `email1.id != email2.id` because IDs are random UUIDs.

**Step 3: Implement deterministic ID**

In `email-search/email_search/models.py`, the `Email` dataclass currently has:

```python
id: str = field(default_factory=lambda: str(uuid.uuid4()))
```

This needs to change. The `id` can no longer be a `default_factory` because it depends on `message_id`. Use `__post_init__` instead:

```python
import hashlib

@dataclass
class Email:
    """Represents a parsed email."""

    message_id: str
    date: datetime
    sender: str
    sender_name: str
    recipients: list[str]
    subject: str
    body_text: str
    is_sent: bool = False
    has_attachments: bool = False
    tier: Tier = Tier.UNCLASSIFIED
    thread_id: str | None = None
    source: str = ""
    id: str = field(default="")
    created_at: datetime = field(default_factory=lambda: datetime.now(timezone.utc))

    def __post_init__(self):
        if not self.id:
            self.id = hashlib.sha256(self.message_id.encode()).hexdigest()
```

Note: also adds the `source` field (empty string default, will be set to `"pst"` or `"outlook"` by parsers).

**Step 4: Run tests to verify they pass**

Run: `cd /home/dan/source/claude-skills/email-search && .venv/bin/python -m pytest tests_py/test_models.py -v`

Expected: ALL PASS. Check existing tests too — `test_email_defaults` tests `email.id` is non-empty, which still holds since sha256 hex is non-empty.

**Step 5: Commit**

```bash
cd /home/dan/source/claude-skills && git add email-search/email_search/models.py email-search/tests_py/test_models.py
git commit -m "feat(email-search): deterministic email IDs from message_id for cross-source dedup"
```

---

### Task 2: Add Source Field to Store Metadata

Include the `source` field in ChromaDB metadata so we can track whether an email came from PST or Graph API.

**Files:**
- Modify: `email-search/email_search/store.py:621-639` (`_build_email_metadata`)
- Modify: `email-search/tests_py/test_store.py` (add source metadata test)

**Step 1: Write the failing test**

Add to `email-search/tests_py/test_store.py`:

```python
class TestSourceMetadata:
    def test_source_stored_in_metadata(self, store):
        email = _make_email()
        email.source = "outlook"
        store.insert_email(email)

        result = store._emails.get(ids=[email.id], include=["metadatas"])
        meta = result["metadatas"][0]
        assert meta["source"] == "outlook"

    def test_source_defaults_to_empty(self, store):
        email = _make_email()
        store.insert_email(email)

        result = store._emails.get(ids=[email.id], include=["metadatas"])
        meta = result["metadatas"][0]
        assert meta["source"] == ""
```

**Step 2: Run tests to verify they fail**

Run: `cd /home/dan/source/claude-skills/email-search && .venv/bin/python -m pytest tests_py/test_store.py::TestSourceMetadata -v`

Expected: FAIL — KeyError on `meta["source"]`.

**Step 3: Add source to metadata builder**

In `email-search/email_search/store.py`, modify `_build_email_metadata` (line 621). Add after `"tier": int(email.tier),`:

```python
"source": email.source,
```

**Step 4: Run all store tests**

Run: `cd /home/dan/source/claude-skills/email-search && .venv/bin/python -m pytest tests_py/test_store.py -v`

Expected: ALL PASS.

**Step 5: Commit**

```bash
cd /home/dan/source/claude-skills && git add email-search/email_search/store.py email-search/tests_py/test_store.py
git commit -m "feat(email-search): track email source in ChromaDB metadata"
```

---

### Task 3: Set Source on PST Parser

Tag emails from PST with `source="pst"`.

**Files:**
- Modify: `email-search/email_search/pst_parser.py:142-151`

**Step 1: Write the failing test**

This is hard to unit test without a real PST file, so we'll verify by reading the code change. No new test needed — the existing integration works and Task 2's test already validates the source field flows through the store.

**Step 2: Set source in pst_parser.py**

In `email-search/email_search/pst_parser.py`, at line 142 where `Email(...)` is constructed, add the `source` kwarg:

```python
        email = Email(
            message_id=message_id,
            date=submit_time,
            sender=sender_email,
            sender_name=sender_name,
            recipients=recipients,
            subject=subject,
            body_text=body,
            has_attachments=has_attachments,
            source="pst",
        )
```

**Step 3: Run full test suite to verify nothing breaks**

Run: `cd /home/dan/source/claude-skills/email-search && .venv/bin/python -m pytest tests_py/ -v`

Expected: ALL PASS.

**Step 4: Commit**

```bash
cd /home/dan/source/claude-skills && git add email-search/email_search/pst_parser.py
git commit -m "feat(email-search): tag PST-sourced emails with source='pst'"
```

---

### Task 4: Graph API Auth Module

Create the token management for Graph API in Python. Reads existing Outlook credentials, handles refresh.

**Files:**
- Create: `email-search/email_search/graph_auth.py`
- Create: `email-search/tests_py/test_graph_auth.py`

**Step 1: Write the failing test**

Create `email-search/tests_py/test_graph_auth.py`:

```python
"""Tests for Graph API authentication."""

import json
import time
from unittest.mock import patch, MagicMock

import pytest

from email_search.graph_auth import GraphAuth, GraphAuthError


def test_load_config_missing_file():
    with pytest.raises(GraphAuthError, match="config.json not found"):
        GraphAuth(config_dir="/nonexistent/path")


def test_load_config_success(tmp_path):
    config = {"client_id": "test-id", "client_secret": "test-secret"}
    creds = {"access_token": "tok123", "refresh_token": "ref456"}
    (tmp_path / "config.json").write_text(json.dumps(config))
    (tmp_path / "credentials.json").write_text(json.dumps(creds))

    auth = GraphAuth(config_dir=str(tmp_path))
    assert auth.client_id == "test-id"
    assert auth.access_token == "tok123"


def test_refresh_token(tmp_path):
    config = {"client_id": "test-id", "client_secret": "test-secret"}
    creds = {"access_token": "old-tok", "refresh_token": "ref456"}
    (tmp_path / "config.json").write_text(json.dumps(config))
    (tmp_path / "credentials.json").write_text(json.dumps(creds))

    auth = GraphAuth(config_dir=str(tmp_path))

    mock_response = MagicMock()
    mock_response.status_code = 200
    mock_response.json.return_value = {
        "access_token": "new-tok",
        "refresh_token": "new-ref",
    }

    with patch("email_search.graph_auth.requests.post", return_value=mock_response):
        auth.refresh()

    assert auth.access_token == "new-tok"
    # Check it was persisted
    saved = json.loads((tmp_path / "credentials.json").read_text())
    assert saved["access_token"] == "new-tok"


def test_get_headers(tmp_path):
    config = {"client_id": "test-id", "client_secret": "test-secret"}
    creds = {"access_token": "tok123", "refresh_token": "ref456"}
    (tmp_path / "config.json").write_text(json.dumps(config))
    (tmp_path / "credentials.json").write_text(json.dumps(creds))

    auth = GraphAuth(config_dir=str(tmp_path))
    headers = auth.get_headers()
    assert headers["Authorization"] == "Bearer tok123"
```

**Step 2: Run tests to verify they fail**

Run: `cd /home/dan/source/claude-skills/email-search && .venv/bin/python -m pytest tests_py/test_graph_auth.py -v`

Expected: FAIL — `ModuleNotFoundError: No module named 'email_search.graph_auth'`

**Step 3: Implement graph_auth.py**

Create `email-search/email_search/graph_auth.py`:

```python
"""Microsoft Graph API authentication using existing Outlook credentials."""

from __future__ import annotations

import json
from pathlib import Path

import requests

TOKEN_URL = "https://login.microsoftonline.com/common/oauth2/v2.0/token"
SCOPE = "offline_access Mail.ReadWrite Mail.Send Calendars.ReadWrite User.Read"
DEFAULT_CONFIG_DIR = str(Path.home() / ".outlook")


class GraphAuthError(Exception):
    pass


class GraphAuth:
    """Manages Graph API tokens using existing ~/.outlook/ credentials."""

    def __init__(self, config_dir: str = DEFAULT_CONFIG_DIR):
        self._config_dir = Path(config_dir)
        self._config_file = self._config_dir / "config.json"
        self._creds_file = self._config_dir / "credentials.json"

        if not self._config_file.exists():
            raise GraphAuthError(
                f"config.json not found at {self._config_file}. "
                "Run the Outlook skill setup first."
            )
        if not self._creds_file.exists():
            raise GraphAuthError(
                f"credentials.json not found at {self._creds_file}. "
                "Run the Outlook skill setup first."
            )

        config = json.loads(self._config_file.read_text())
        self.client_id: str = config["client_id"]
        self._client_secret: str = config["client_secret"]

        creds = json.loads(self._creds_file.read_text())
        self.access_token: str = creds["access_token"]
        self._refresh_token: str = creds["refresh_token"]

    def refresh(self) -> None:
        """Refresh the access token using the refresh token."""
        resp = requests.post(
            TOKEN_URL,
            data={
                "client_id": self.client_id,
                "client_secret": self._client_secret,
                "refresh_token": self._refresh_token,
                "grant_type": "refresh_token",
                "scope": SCOPE,
            },
        )

        if resp.status_code != 200:
            raise GraphAuthError(f"Token refresh failed: {resp.text}")

        data = resp.json()
        if "error" in data:
            raise GraphAuthError(f"Token refresh error: {data.get('error_description', data['error'])}")

        self.access_token = data["access_token"]
        self._refresh_token = data.get("refresh_token", self._refresh_token)

        # Persist refreshed credentials
        self._creds_file.write_text(json.dumps(data, indent=2))

    def get_headers(self) -> dict[str, str]:
        """Return authorization headers for Graph API requests."""
        return {
            "Authorization": f"Bearer {self.access_token}",
            "Content-Type": "application/json",
        }
```

**Step 4: Install requests dependency**

In `email-search/pyproject.toml`, add `"requests>=2.31"` to the `dependencies` list.

Then: `cd /home/dan/source/claude-skills/email-search && .venv/bin/pip install -e ".[dev]"`

**Step 5: Run tests to verify they pass**

Run: `cd /home/dan/source/claude-skills/email-search && .venv/bin/python -m pytest tests_py/test_graph_auth.py -v`

Expected: ALL PASS.

**Step 6: Commit**

```bash
cd /home/dan/source/claude-skills && git add email-search/email_search/graph_auth.py email-search/tests_py/test_graph_auth.py email-search/pyproject.toml
git commit -m "feat(email-search): Graph API auth module using existing Outlook credentials"
```

---

### Task 5: Graph API Parser

The core module that fetches emails from Graph API and yields `ParsedEmail` objects.

**Files:**
- Create: `email-search/email_search/graph_parser.py`
- Create: `email-search/tests_py/test_graph_parser.py`

**Step 1: Write the failing tests**

Create `email-search/tests_py/test_graph_parser.py`:

```python
"""Tests for Graph API parser."""

from datetime import datetime
from unittest.mock import patch, MagicMock

import pytest

from email_search.graph_parser import (
    _parse_message,
    _strip_html,
    parse_outlook,
)
from email_search.models import Tier


SAMPLE_MESSAGE = {
    "id": "AAMkAGI2",
    "internetMessageId": "<abc123@example.com>",
    "subject": "Q3 Budget Review",
    "from": {"emailAddress": {"name": "Alice Smith", "address": "alice@example.com"}},
    "toRecipients": [
        {"emailAddress": {"name": "Bob", "address": "bob@example.com"}},
        {"emailAddress": {"name": "Carol", "address": "carol@example.com"}},
    ],
    "receivedDateTime": "2024-06-15T10:30:00Z",
    "body": {"contentType": "text", "content": "Here are the Q3 budget numbers for review."},
    "hasAttachments": False,
    "conversationId": "conv-123",
}


def test_parse_message_basic():
    parsed = _parse_message(SAMPLE_MESSAGE, folder_name="Inbox")
    email = parsed.email

    assert email.message_id == "<abc123@example.com>"
    assert email.subject == "Q3 Budget Review"
    assert email.sender == "alice@example.com"
    assert email.sender_name == "Alice Smith"
    assert email.recipients == ["bob@example.com", "carol@example.com"]
    assert email.date == datetime(2024, 6, 15, 10, 30, tzinfo=None) or email.date.year == 2024
    assert email.body_text == "Here are the Q3 budget numbers for review."
    assert email.has_attachments is False
    assert email.is_sent is False
    assert email.source == "outlook"
    assert email.thread_id == "conv-123"


def test_parse_message_sent_folder():
    parsed = _parse_message(SAMPLE_MESSAGE, folder_name="Sent Items")
    assert parsed.email.is_sent is True


def test_parse_message_html_body():
    msg = {**SAMPLE_MESSAGE, "body": {"contentType": "html", "content": "<p>Hello <b>world</b></p>"}}
    parsed = _parse_message(msg, folder_name="Inbox")
    assert "<p>" not in parsed.email.body_text
    assert "Hello" in parsed.email.body_text
    assert "world" in parsed.email.body_text


def test_parse_message_missing_internet_message_id():
    msg = {**SAMPLE_MESSAGE}
    del msg["internetMessageId"]
    parsed = _parse_message(msg, folder_name="Inbox")
    # Should fall back to Graph API id
    assert parsed.email.message_id == "<graph-AAMkAGI2@outlook>"


def test_strip_html():
    assert _strip_html("<p>Hello <b>world</b></p>") == "Hello world"
    assert _strip_html("plain text") == "plain text"
    assert _strip_html("<div>a</div><div>b</div>").strip() in ("a\nb", "a b", "a\n\nb")
```

**Step 2: Run tests to verify they fail**

Run: `cd /home/dan/source/claude-skills/email-search && .venv/bin/python -m pytest tests_py/test_graph_parser.py -v`

Expected: FAIL — `ModuleNotFoundError`.

**Step 3: Implement graph_parser.py**

Create `email-search/email_search/graph_parser.py`:

```python
"""Microsoft Graph API email parser.

Fetches emails from Outlook via Graph API and yields ParsedEmail objects,
compatible with the same pipeline used by pst_parser.py.
"""

from __future__ import annotations

import base64
import re
import time
from datetime import datetime
from typing import Iterator

import requests

from .graph_auth import GraphAuth, GraphAuthError
from .models import Email, ParsedEmail, RawAttachment

GRAPH_BASE = "https://graph.microsoft.com/v1.0"

# Fields to request from Graph API
MESSAGE_FIELDS = (
    "id,internetMessageId,subject,from,toRecipients,receivedDateTime,"
    "body,hasAttachments,conversationId"
)

SENT_FOLDER_NAMES = frozenset({"sent items", "sent", "sentitems"})


def parse_outlook(
    auth: GraphAuth,
    *,
    since: datetime | None = None,
    folders: list[str] | None = None,
    batch_size: int = 100,
) -> Iterator[tuple[str, ParsedEmail]]:
    """Fetch emails from Outlook and yield (folder_name, ParsedEmail) tuples.

    Args:
        auth: Authenticated GraphAuth instance.
        since: Only fetch emails after this date.
        folders: Folder names to include (None = all).
        batch_size: Number of messages per API page (max 1000).
    """
    mail_folders = _list_folders(auth)

    for folder_id, folder_name in mail_folders:
        if folders and folder_name.lower() not in {f.lower() for f in folders}:
            continue

        yield from _fetch_folder_messages(
            auth, folder_id, folder_name, since, batch_size
        )


def _list_folders(auth: GraphAuth) -> list[tuple[str, str]]:
    """List all mail folders (including nested) as (id, displayName) pairs."""
    result: list[tuple[str, str]] = []
    url = f"{GRAPH_BASE}/me/mailFolders?$top=100"

    while url:
        resp = _api_get(auth, url)
        data = resp.json()

        for folder in data.get("value", []):
            result.append((folder["id"], folder["displayName"]))
            # Fetch child folders
            if folder.get("childFolderCount", 0) > 0:
                result.extend(_list_child_folders(auth, folder["id"]))

        url = data.get("@odata.nextLink")

    return result


def _list_child_folders(auth: GraphAuth, parent_id: str) -> list[tuple[str, str]]:
    """Recursively list child folders."""
    result: list[tuple[str, str]] = []
    url = f"{GRAPH_BASE}/me/mailFolders/{parent_id}/childFolders?$top=100"

    while url:
        resp = _api_get(auth, url)
        data = resp.json()

        for folder in data.get("value", []):
            result.append((folder["id"], folder["displayName"]))
            if folder.get("childFolderCount", 0) > 0:
                result.extend(_list_child_folders(auth, folder["id"]))

        url = data.get("@odata.nextLink")

    return result


def _fetch_folder_messages(
    auth: GraphAuth,
    folder_id: str,
    folder_name: str,
    since: datetime | None,
    batch_size: int,
) -> Iterator[tuple[str, ParsedEmail]]:
    """Fetch all messages from a folder, paginating as needed."""
    url = (
        f"{GRAPH_BASE}/me/mailFolders/{folder_id}/messages"
        f"?$select={MESSAGE_FIELDS}"
        f"&$top={min(batch_size, 1000)}"
        f"&$orderby=receivedDateTime asc"
    )

    if since:
        date_filter = since.strftime("%Y-%m-%dT%H:%M:%SZ")
        url += f"&$filter=receivedDateTime ge {date_filter}"

    while url:
        resp = _api_get(auth, url)
        data = resp.json()

        for msg in data.get("value", []):
            parsed = _parse_message(msg, folder_name)

            # Fetch attachments if needed
            if msg.get("hasAttachments"):
                parsed.attachments = _fetch_attachments(auth, msg["id"])

            yield (folder_name, parsed)

        url = data.get("@odata.nextLink")


def _parse_message(msg: dict, folder_name: str) -> ParsedEmail:
    """Convert a Graph API message dict to a ParsedEmail."""
    # Message ID: prefer internetMessageId, fall back to Graph id
    message_id = msg.get("internetMessageId") or f"<graph-{msg['id']}@outlook>"

    # Date
    date_str = msg.get("receivedDateTime", "")
    try:
        date = datetime.fromisoformat(date_str.replace("Z", "+00:00")).replace(tzinfo=None)
    except (ValueError, AttributeError):
        date = datetime.utcnow()

    # Sender
    from_field = msg.get("from", {}).get("emailAddress", {})
    sender = from_field.get("address", "")
    sender_name = from_field.get("name", "")

    # Recipients
    recipients = [
        r["emailAddress"]["address"]
        for r in msg.get("toRecipients", [])
        if r.get("emailAddress", {}).get("address")
    ]

    # Body
    body_obj = msg.get("body", {})
    body_text = body_obj.get("content", "")
    if body_obj.get("contentType", "").lower() == "html":
        body_text = _strip_html(body_text)

    # Sent detection
    is_sent = folder_name.lower() in SENT_FOLDER_NAMES

    email = Email(
        message_id=message_id,
        date=date,
        sender=sender,
        sender_name=sender_name,
        recipients=recipients,
        subject=msg.get("subject", ""),
        body_text=body_text,
        has_attachments=msg.get("hasAttachments", False),
        is_sent=is_sent,
        thread_id=msg.get("conversationId"),
        source="outlook",
    )

    return ParsedEmail(email=email)


def _fetch_attachments(auth: GraphAuth, message_id: str) -> list[RawAttachment]:
    """Fetch attachments for a message."""
    url = f"{GRAPH_BASE}/me/messages/{message_id}/attachments"
    resp = _api_get(auth, url)
    data = resp.json()

    attachments: list[RawAttachment] = []
    for att in data.get("value", []):
        # Only process file attachments (skip item attachments, reference attachments)
        if att.get("@odata.type") != "#microsoft.graph.fileAttachment":
            continue

        content_bytes = base64.b64decode(att.get("contentBytes", "")) if att.get("contentBytes") else b""

        attachments.append(
            RawAttachment(
                filename=att.get("name", "unknown"),
                mime_type=att.get("contentType"),
                content=content_bytes,
                size_bytes=att.get("size", len(content_bytes)),
            )
        )

    return attachments


def _strip_html(html: str) -> str:
    """Strip HTML tags and decode entities. Simple regex-based approach."""
    # Replace block-level tags with newlines
    text = re.sub(r"<br\s*/?>", "\n", html, flags=re.IGNORECASE)
    text = re.sub(r"</?(div|p|tr|li|h[1-6])[^>]*>", "\n", text, flags=re.IGNORECASE)
    # Strip remaining tags
    text = re.sub(r"<[^>]+>", "", text)
    # Decode common entities
    text = text.replace("&nbsp;", " ").replace("&amp;", "&")
    text = text.replace("&lt;", "<").replace("&gt;", ">").replace("&quot;", '"')
    # Collapse whitespace
    text = re.sub(r"\n{3,}", "\n\n", text)
    return text.strip()


def _api_get(auth: GraphAuth, url: str, retries: int = 3) -> requests.Response:
    """Make a GET request with retry on 401 (token expired) and 429 (throttled)."""
    for attempt in range(retries):
        resp = requests.get(url, headers=auth.get_headers())

        if resp.status_code == 200:
            return resp

        if resp.status_code == 401 and attempt < retries - 1:
            auth.refresh()
            continue

        if resp.status_code == 429:
            retry_after = int(resp.headers.get("Retry-After", 5))
            time.sleep(retry_after)
            continue

        resp.raise_for_status()

    return resp
```

**Step 4: Run tests to verify they pass**

Run: `cd /home/dan/source/claude-skills/email-search && .venv/bin/python -m pytest tests_py/test_graph_parser.py -v`

Expected: ALL PASS. The tests only exercise `_parse_message` and `_strip_html` (pure functions), no real API calls.

**Step 5: Commit**

```bash
cd /home/dan/source/claude-skills && git add email-search/email_search/graph_parser.py email-search/tests_py/test_graph_parser.py
git commit -m "feat(email-search): Graph API parser yielding ParsedEmail objects"
```

---

### Task 6: CLI `ingest-outlook` Command + Sync State

Wire everything together with a new CLI command and sync state tracking.

**Files:**
- Modify: `email-search/email_search/cli.py` (add `ingest-outlook` command)
- Create: `email-search/email_search/sync_state.py`
- Create: `email-search/tests_py/test_sync_state.py`

**Step 1: Write sync state tests**

Create `email-search/tests_py/test_sync_state.py`:

```python
"""Tests for sync state management."""

import json
from datetime import datetime, timezone

from email_search.sync_state import SyncState


def test_load_empty(tmp_path):
    state = SyncState(state_dir=str(tmp_path))
    assert state.last_sync is None
    assert state.total_synced == 0


def test_save_and_load(tmp_path):
    state = SyncState(state_dir=str(tmp_path))
    now = datetime(2024, 6, 15, 10, 30, tzinfo=timezone.utc)
    state.update(last_sync=now, folders_synced=["Inbox", "Sent Items"], emails_synced=1500)

    # Load fresh
    state2 = SyncState(state_dir=str(tmp_path))
    assert state2.last_sync.year == 2024
    assert state2.total_synced == 1500
    assert "Inbox" in state2.folders_synced


def test_incremental_update(tmp_path):
    state = SyncState(state_dir=str(tmp_path))
    t1 = datetime(2024, 1, 1, tzinfo=timezone.utc)
    state.update(last_sync=t1, folders_synced=["Inbox"], emails_synced=100)

    t2 = datetime(2024, 6, 1, tzinfo=timezone.utc)
    state.update(last_sync=t2, folders_synced=["Inbox", "Sent Items"], emails_synced=50)

    state2 = SyncState(state_dir=str(tmp_path))
    assert state2.last_sync.month == 6
    assert state2.total_synced == 150
```

**Step 2: Run tests to verify they fail**

Run: `cd /home/dan/source/claude-skills/email-search && .venv/bin/python -m pytest tests_py/test_sync_state.py -v`

Expected: FAIL — `ModuleNotFoundError`.

**Step 3: Implement sync_state.py**

Create `email-search/email_search/sync_state.py`:

```python
"""Outlook sync state management."""

from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path


DEFAULT_STATE_DIR = str(Path.home() / ".email-search")
STATE_FILENAME = "outlook_sync_state.json"


class SyncState:
    """Tracks Outlook sync progress between runs."""

    def __init__(self, state_dir: str = DEFAULT_STATE_DIR):
        self._state_dir = Path(state_dir)
        self._state_file = self._state_dir / STATE_FILENAME
        self.last_sync: datetime | None = None
        self.folders_synced: list[str] = []
        self.total_synced: int = 0
        self._load()

    def _load(self) -> None:
        if not self._state_file.exists():
            return
        data = json.loads(self._state_file.read_text())
        if data.get("last_sync"):
            self.last_sync = datetime.fromisoformat(data["last_sync"])
        self.folders_synced = data.get("folders_synced", [])
        self.total_synced = data.get("total_synced", 0)

    def update(
        self,
        last_sync: datetime,
        folders_synced: list[str],
        emails_synced: int,
    ) -> None:
        """Update sync state and persist to disk."""
        self.last_sync = last_sync
        self.folders_synced = folders_synced
        self.total_synced += emails_synced

        self._state_dir.mkdir(parents=True, exist_ok=True)
        self._state_file.write_text(
            json.dumps(
                {
                    "last_sync": self.last_sync.isoformat(),
                    "folders_synced": self.folders_synced,
                    "total_synced": self.total_synced,
                },
                indent=2,
            )
        )
```

**Step 4: Run sync state tests**

Run: `cd /home/dan/source/claude-skills/email-search && .venv/bin/python -m pytest tests_py/test_sync_state.py -v`

Expected: ALL PASS.

**Step 5: Add `ingest-outlook` CLI command**

In `email-search/email_search/cli.py`, add the following after the existing `ingest` command (after line 133):

```python
# ── ingest-outlook ─────────────────────────────────────────────────


@main.command("ingest-outlook")
@click.option("--since", type=click.DateTime(formats=["%Y-%m-%d"]), help="Only fetch emails after this date")
@click.option("--folders", help="Comma-separated folder names (default: all)")
@click.option("--batch-size", default=100, help="Messages per API page (max 1000)")
def ingest_outlook(since: datetime | None, folders: str | None, batch_size: int):
    """Ingest emails from Outlook via Microsoft Graph API."""
    from .graph_auth import GraphAuth, GraphAuthError
    from .graph_parser import parse_outlook
    from .sync_state import SyncState

    try:
        auth = GraphAuth()
    except GraphAuthError as e:
        console.print(f"[red]Auth error: {e}[/]")
        console.print("[dim]Make sure the Outlook skill is set up: ~/.claude/skills/outlook/scripts/outlook-setup.sh[/]")
        sys.exit(1)

    try:
        store = _get_store()
        sync = SyncState()

        # Determine start date
        effective_since = since
        if effective_since is None and sync.last_sync is not None:
            effective_since = sync.last_sync
            console.print(f"[dim]Resuming from last sync: {effective_since:%Y-%m-%d %H:%M}[/]")

        folder_list = [f.strip() for f in folders.split(",")] if folders else None

        counts = {
            "total": 0,
            "excluded": 0,
            "metadata_only": 0,
            "vectorize": 0,
            "attachments": 0,
            "attachments_with_text": 0,
            "skipped_existing": 0,
        }
        folders_seen: set[str] = set()

        console.print("[bold]Ingesting from Outlook...[/]")
        if effective_since:
            console.print(f"[dim]Fetching emails since {effective_since:%Y-%m-%d}[/]")
        else:
            console.print("[dim]Fetching all emails[/]")
        console.print()

        with Progress(console=console) as progress:
            task = progress.add_task("[cyan]Fetching emails...", total=None)

            for folder_name, parsed in parse_outlook(
                auth, since=effective_since, folders=folder_list, batch_size=batch_size
            ):
                counts["total"] += 1
                folders_seen.add(folder_name)
                email = parsed.email

                # Classify
                tier = email_filter.classify(email, email.has_attachments)
                email.tier = tier

                if tier == Tier.EXCLUDED:
                    counts["excluded"] += 1
                    progress.update(task, description=f"[cyan]{folder_name}: {counts['total']:,} emails...")
                    continue

                # Store (ChromaDB .add() skips existing IDs)
                store.insert_email(email)

                if tier == Tier.METADATA_ONLY:
                    counts["metadata_only"] += 1
                else:
                    counts["vectorize"] += 1

                    if email.has_attachments and parsed.attachments:
                        for raw_att in parsed.attachments:
                            counts["attachments"] += 1
                            extracted_text = None
                            if raw_att.content:
                                extracted_text = attachment_extractor.extract_text(
                                    raw_att.filename,
                                    raw_att.content,
                                    raw_att.mime_type,
                                )
                            if extracted_text:
                                counts["attachments_with_text"] += 1

                            att = Attachment(
                                email_id=email.id,
                                filename=raw_att.filename,
                                mime_type=raw_att.mime_type or attachment_extractor.get_mime_type(raw_att.filename),
                                size_bytes=raw_att.size_bytes,
                                extracted_text=extracted_text,
                            )
                            store.insert_attachment(att, email)

                progress.update(
                    task,
                    description=f"[cyan]{folder_name}: {counts['total']:,} emails, {counts['attachments']:,} attachments...",
                )

        # Update sync state
        from datetime import timezone
        sync.update(
            last_sync=datetime.now(timezone.utc),
            folders_synced=sorted(folders_seen),
            emails_synced=counts["total"],
        )

        console.print()
        console.print("[bold green]Outlook ingest complete![/]")
        console.print(f"  Total emails: [bold]{counts['total']:,}[/]")
        console.print(f"  Excluded (Tier 1): [grey50]{counts['excluded']:,}[/]")
        console.print(f"  Metadata only (Tier 2): {counts['metadata_only']:,}")
        console.print(f"  Vectorised (Tier 3): [green]{counts['vectorize']:,}[/]")
        console.print(
            f"  Attachments: [bold]{counts['attachments']:,}[/] "
            f"({counts['attachments_with_text']:,} with extracted text)"
        )
        console.print(f"  Folders: {', '.join(sorted(folders_seen))}")

    except Exception as e:
        console.print(f"[red]Error: {e}[/]")
        sys.exit(1)
```

**Step 6: Run full test suite**

Run: `cd /home/dan/source/claude-skills/email-search && .venv/bin/python -m pytest tests_py/ -v`

Expected: ALL PASS.

**Step 7: Commit**

```bash
cd /home/dan/source/claude-skills && git add email-search/email_search/sync_state.py email-search/tests_py/test_sync_state.py email-search/email_search/cli.py
git commit -m "feat(email-search): add ingest-outlook CLI command with sync state"
```

---

### Task 7: Update SKILL.md and Docs

Update the skill documentation to reflect the new Outlook ingest capability.

**Files:**
- Modify: `email-search/SKILL.md`

**Step 1: Update SKILL.md**

Update the YAML frontmatter description to mention Outlook:

```yaml
---
name: email-search
description: Process email archives (PST files) and Outlook mailboxes into a searchable ChromaDB vector database with automatic semantic embeddings. Ingest, classify, search, analyse, and export to markdown. Trigger on phrases like "email archive", "ingest pst", "search emails", "email analytics", "export contacts", "email timeline", "ingest outlook", "sync outlook".
---
```

Add after the PST ingest section (after line 39):

```markdown
### Ingest from Outlook (Microsoft 365)

Requires the Outlook skill to be set up first (`~/.claude/skills/outlook/scripts/outlook-setup.sh`).

```bash
# First time: ingest all emails
email-search ingest-outlook

# Incremental: only new emails since last sync
email-search ingest-outlook

# From a specific date
email-search ingest-outlook --since 2024-01-01

# Specific folders only
email-search ingest-outlook --folders "Inbox,Sent Items"
```

**Hybrid workflow** (recommended for large mailboxes):
1. Export PST from Outlook desktop client
2. `email-search ingest archive.pst` — fast bulk import
3. `email-search ingest-outlook` — picks up newer emails
4. Run `ingest-outlook` periodically to stay current

Cross-source deduplication is automatic — the same email from PST and Outlook produces the same vector DB entry.
```

Update the Key Details section to mention Outlook integration.

**Step 2: Commit**

```bash
cd /home/dan/source/claude-skills && git add email-search/SKILL.md
git commit -m "docs(email-search): document Outlook ingest command in SKILL.md"
```

---

### Task 8: Integration Test with Mocked Graph API

A higher-level test that exercises the full pipeline: Graph API → parse → classify → store.

**Files:**
- Modify: `email-search/tests_py/test_graph_parser.py` (add integration test)

**Step 1: Write the integration test**

Add to `email-search/tests_py/test_graph_parser.py`:

```python
from email_search import email_filter
from email_search.store import Store


class TestIngestPipeline:
    """Integration test: Graph API messages through the full classify+store pipeline."""

    def test_parsed_email_stores_in_chromadb(self, tmp_path):
        """A parsed Graph API email should classify and store correctly."""
        parsed = _parse_message(SAMPLE_MESSAGE, folder_name="Inbox")
        email = parsed.email

        # Classify
        tier = email_filter.classify(email, email.has_attachments)
        email.tier = tier

        # Store
        store = Store(data_dir=str(tmp_path / "test-data"))
        store.insert_email(email)

        counts = store.get_status_counts()
        assert counts.total == 1

        # Verify searchable
        results = store.search_emails("budget", limit=5)
        assert len(results) > 0
        assert "budget" in results[0].subject.lower()

    def test_dedup_across_sources(self, tmp_path):
        """Same message_id from PST and Outlook should produce one entry."""
        from email_search.models import Email

        store = Store(data_dir=str(tmp_path / "test-data"))

        # Simulate PST email
        pst_email = Email(
            message_id="<abc123@example.com>",
            date=datetime(2024, 6, 15, 10, 30),
            sender="alice@example.com",
            sender_name="Alice",
            recipients=["bob@example.com"],
            subject="Q3 Budget",
            body_text="Here are the Q3 budget numbers for review and discussion.",
            source="pst",
            tier=Tier.VECTORIZE,
        )
        store.insert_email(pst_email)

        # Simulate Outlook email (same message_id)
        outlook_parsed = _parse_message(SAMPLE_MESSAGE, folder_name="Inbox")
        outlook_email = outlook_parsed.email
        outlook_email.tier = Tier.VECTORIZE
        store.insert_email(outlook_email)

        # Should still be just 1 email (same deterministic ID)
        counts = store.get_status_counts()
        assert counts.total == 1
```

Add a `tmp_path` fixture import if needed (pytest provides it automatically).

**Step 2: Run the integration tests**

Run: `cd /home/dan/source/claude-skills/email-search && .venv/bin/python -m pytest tests_py/test_graph_parser.py::TestIngestPipeline -v`

Expected: ALL PASS.

**Step 3: Run full test suite one final time**

Run: `cd /home/dan/source/claude-skills/email-search && .venv/bin/python -m pytest tests_py/ -v`

Expected: ALL PASS.

**Step 4: Commit**

```bash
cd /home/dan/source/claude-skills && git add email-search/tests_py/test_graph_parser.py
git commit -m "test(email-search): integration test for Graph API ingest pipeline + cross-source dedup"
```
