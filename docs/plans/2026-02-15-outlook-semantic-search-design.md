# Outlook Semantic Search — Design

**Date:** 2026-02-15
**Status:** Approved

## Goal

Extend the `email-search` skill to ingest emails from Microsoft 365 via the Graph API, enabling semantic search across live Outlook mailboxes. Supports a hybrid workflow: PST bulk import for historical email, then Graph API for incremental sync.

## Architecture

New module `graph_parser.py` sits alongside `pst_parser.py` as an alternative data source. Both yield `ParsedEmail` objects into the same downstream pipeline:

```
PST file  ──→  pst_parser.py   ──→  ParsedEmail  ──┐
                                                     ├──→  classify()  ──→  store.py (ChromaDB)
Graph API ──→  graph_parser.py  ──→  ParsedEmail  ──┘
```

Classification, attachment extraction, and ChromaDB storage are untouched.

## Deduplication

**Problem:** The current `email.id` is a random UUID. Same email ingested from PST and Graph API would create duplicates.

**Fix:** Derive `email.id` deterministically from `message_id` (RFC Message-ID header) via `hashlib.sha256(message_id.encode()).hexdigest()`. Both sources produce the same ID for the same email. ChromaDB `.add()` silently skips existing IDs.

Add a `source` metadata field (`"pst"` or `"outlook"`) to track provenance.

## Graph API Parser (`graph_parser.py`)

- **Auth:** Reads tokens from `~/.outlook/credentials.json`, refreshes via `~/.outlook/config.json` (client_id + refresh_token). Writes refreshed tokens back.
- **Fetching:** Paginates `/me/mailFolders` then `/me/mailFolders/{id}/messages` with `$select` for needed fields. Follows `@odata.nextLink`.
- **Attachments:** Fetches `/me/messages/{id}/attachments` for emails with `hasAttachments=true`. Maps to `RawAttachment` (base64 decode content).
- **Sent detection:** Emails from "Sent Items" folder get `is_sent=True`.
- **Rate limiting:** Backoff on 429 responses.
- **Output:** Yields `ParsedEmail` — identical to PST parser output.

### Field Mapping (Graph API → Email dataclass)

| Email field | Graph API source |
|-------------|------------------|
| `message_id` | `internetMessageId` |
| `date` | `receivedDateTime` |
| `sender` | `from.emailAddress.address` |
| `sender_name` | `from.emailAddress.name` |
| `recipients` | `toRecipients[].emailAddress.address` |
| `subject` | `subject` |
| `body_text` | `body.content` (HTML stripped to plain text) |
| `is_sent` | Inferred from folder name |
| `has_attachments` | `hasAttachments` |
| `thread_id` | `conversationId` |

## CLI Command

```
email-search ingest-outlook [--since DATE] [--folders Inbox,Sent] [--batch-size 100] [--db-path PATH]
```

- Without `--since`: checks sync state for last sync timestamp. If none, fetches everything.
- After success, writes timestamp to sync state file.
- Progress via `rich` — folder name, count, rate.

## Sync State

File: `~/.email-search/outlook_sync_state.json`

```json
{
  "last_sync": "2026-02-15T10:30:00Z",
  "folders_synced": ["Inbox", "Sent Items", "Archive"],
  "total_synced": 15234
}
```

## Intended Workflow

1. Export PST from Outlook (fast, offline, no API limits)
2. `email-search ingest archive.pst` — bulk load into ChromaDB
3. `email-search ingest-outlook` — picks up newer emails via Graph API
4. Run `ingest-outlook` periodically to stay current

## Files Changed

| File | Change |
|------|--------|
| `email_search/models.py` | Deterministic ID from `message_id`, add `source` field |
| `email_search/store.py` | Include `source` in metadata |
| `email_search/graph_parser.py` | **New** — Graph API fetcher |
| `email_search/cli.py` | New `ingest-outlook` command |
| `requirements.txt` | Add `requests` |
| `SKILL.md` | Document `ingest-outlook` command |

## Files Untouched

`pst_parser.py`, `email_filter.py`, `attachment_extractor.py`, `markdown_formatter.py` — entire downstream pipeline unchanged.

## Dependencies

Only new dependency: `requests`. Everything else already present.
