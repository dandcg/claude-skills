# Web Clipper Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a web clipper skill that saves URLs as clean markdown with YAML frontmatter, with FlareSolverr fallback and optional repo-search ChromaDB integration.

**Architecture:** Python scripts in `web-clipper/scripts/`, requirements.txt-based venv (like repo-search). Each script is standalone with argparse. Clips stored as markdown files in `~/web-clips/`. FlareSolverr integration via HTTP POST to localhost:8191.

**Tech Stack:** Python 3, trafilatura (extraction), requests (HTTP), python-slugify (filenames), PyYAML (frontmatter)

---

### Task 1: Scaffold skill directory and dependencies

**Files:**
- Create: `web-clipper/requirements.txt`
- Create: `web-clipper/setup.sh`

**Step 1: Create requirements.txt**

```
trafilatura>=1.6,<2.0
requests>=2.31,<3.0
python-slugify>=8.0,<9.0
pyyaml>=6.0,<7.0
```

**Step 2: Create setup.sh**

```bash
#!/bin/bash
set -e

SKILL_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VENV_DIR="$SKILL_DIR/.venv"

echo "=== Web Clipper Setup ==="

if ! command -v python3 &> /dev/null; then
    echo "Error: python3 not found" >&2
    exit 1
fi

PYTHON_VERSION=$(python3 -c "import sys; print(f'{sys.version_info.major}.{sys.version_info.minor}')")
echo "Python version: $PYTHON_VERSION"

if [ ! -d "$VENV_DIR" ]; then
    echo "Creating virtual environment..."
    python3 -m venv "$VENV_DIR"
else
    echo "Virtual environment already exists"
fi

echo "Installing dependencies..."
"$VENV_DIR/bin/pip" install --upgrade pip -q
"$VENV_DIR/bin/pip" install -r "$SKILL_DIR/requirements.txt" -q

echo
echo "=== Setup Complete ==="
echo "Virtual environment: $VENV_DIR"
echo "Python: $VENV_DIR/bin/python"
```

**Step 3: Create scripts directory**

```bash
mkdir -p web-clipper/scripts
```

**Step 4: Run setup and verify**

Run: `chmod +x web-clipper/setup.sh && web-clipper/setup.sh`
Expected: "=== Setup Complete ===" with venv created

**Step 5: Commit**

```bash
git add web-clipper/requirements.txt web-clipper/setup.sh
git commit -m "feat(web-clipper): scaffold skill directory with dependencies"
```

---

### Task 2: Core clipping — write failing tests

**Files:**
- Create: `web-clipper/tests/conftest.py`
- Create: `web-clipper/tests/test_clip.py`

**Step 1: Install test dependencies**

Run: `web-clipper/.venv/bin/pip install pytest -q`

**Step 2: Create conftest.py with shared fixtures**

```python
import os
import pytest
from pathlib import Path


@pytest.fixture
def tmp_clips_dir(tmp_path):
    """Temporary directory for clip output."""
    clips_dir = tmp_path / "web-clips"
    clips_dir.mkdir()
    return clips_dir


@pytest.fixture
def sample_html():
    """Minimal HTML article page."""
    return """<!DOCTYPE html>
<html>
<head><title>Test Article Title</title></head>
<body>
<article>
<h1>Test Article Title</h1>
<p>By John Author</p>
<p>This is the first paragraph of a test article about Python programming.
It contains enough text to be recognised as real article content by extraction
libraries. We need several sentences to ensure the content is not dismissed
as boilerplate or navigation text.</p>
<p>The second paragraph continues with more substantial content about software
development practices. This helps ensure trafilatura identifies this as the
main content of the page rather than sidebar or footer material.</p>
<p>A third paragraph rounds out the article with concluding thoughts on the
topic. Having multiple paragraphs of reasonable length is important for
content extraction quality.</p>
</article>
</body>
</html>"""


@pytest.fixture
def cloudflare_html():
    """HTML that looks like a Cloudflare challenge page."""
    return """<!DOCTYPE html>
<html>
<head><title>Just a moment...</title></head>
<body>
<div class="challenge-running">Checking your browser before accessing the site.</div>
</body>
</html>"""
```

**Step 3: Create test_clip.py with tests for extraction and saving**

```python
import json
import yaml
import pytest
from pathlib import Path
from unittest.mock import patch, MagicMock

import sys
import os

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "scripts"))


class TestExtractArticle:
    """Test article extraction from HTML."""

    def test_extracts_text_from_html(self, sample_html):
        from clip import extract_article

        result = extract_article(sample_html, "https://example.com/article")
        assert result is not None
        assert result["text"]
        assert len(result["text"]) > 50

    def test_extracts_title(self, sample_html):
        from clip import extract_article

        result = extract_article(sample_html, "https://example.com/article")
        assert result["title"]

    def test_returns_none_for_empty_html(self):
        from clip import extract_article

        result = extract_article("", "https://example.com")
        assert result is None

    def test_returns_none_for_non_article_html(self):
        from clip import extract_article

        result = extract_article("<html><body><nav>Menu</nav></body></html>", "https://example.com")
        assert result is None


class TestGenerateMarkdown:
    """Test markdown generation with YAML frontmatter."""

    def test_generates_valid_yaml_frontmatter(self):
        from clip import generate_markdown

        article = {
            "title": "Test Title",
            "text": "Article body text.",
            "author": "John",
            "date": "2026-02-19",
            "description": "A test article",
        }
        md = generate_markdown(article, "https://example.com/test", tags=["python"])

        # Parse frontmatter
        parts = md.split("---\n")
        assert len(parts) >= 3
        fm = yaml.safe_load(parts[1])
        assert fm["title"] == "Test Title"
        assert fm["url"] == "https://example.com/test"
        assert fm["domain"] == "example.com"
        assert "python" in fm["tags"]

    def test_includes_article_body(self):
        from clip import generate_markdown

        article = {
            "title": "Test",
            "text": "Body content here.",
            "author": None,
            "date": None,
            "description": None,
        }
        md = generate_markdown(article, "https://example.com/test", tags=[])
        assert "Body content here." in md


class TestGenerateFilename:
    """Test filename generation from article title."""

    def test_slugifies_title(self):
        from clip import generate_filename

        name = generate_filename("My Great Article!", "2026-02-19")
        assert name == "2026-02-19-my-great-article.md"

    def test_handles_long_titles(self):
        from clip import generate_filename

        long_title = "A" * 200
        name = generate_filename(long_title, "2026-02-19")
        assert len(name) <= 120

    def test_handles_special_characters(self):
        from clip import generate_filename

        name = generate_filename("What's the deal with C++/C#?", "2026-02-19")
        assert "/" not in name
        assert "'" not in name
        assert name.endswith(".md")


class TestSaveClip:
    """Test saving clips to disk."""

    def test_saves_markdown_file(self, tmp_clips_dir):
        from clip import save_clip

        filepath = save_clip(
            "---\ntitle: Test\n---\nBody",
            "Test Title",
            tmp_clips_dir,
        )
        assert filepath.exists()
        assert filepath.suffix == ".md"
        assert filepath.read_text().startswith("---")

    def test_handles_filename_collision(self, tmp_clips_dir):
        from clip import save_clip

        path1 = save_clip("content1", "Same Title", tmp_clips_dir)
        path2 = save_clip("content2", "Same Title", tmp_clips_dir)
        assert path1 != path2
        assert path2.exists()


class TestDetectCloudflare:
    """Test Cloudflare challenge detection."""

    def test_detects_cloudflare_title(self, cloudflare_html):
        from clip import is_cloudflare_challenge

        assert is_cloudflare_challenge(cloudflare_html, 200) is True

    def test_detects_403_status(self):
        from clip import is_cloudflare_challenge

        assert is_cloudflare_challenge("<html></html>", 403) is True

    def test_normal_page_not_detected(self, sample_html):
        from clip import is_cloudflare_challenge

        assert is_cloudflare_challenge(sample_html, 200) is False
```

**Step 4: Run tests to verify they fail**

Run: `cd /home/dan/source/claude-skills && web-clipper/.venv/bin/python -m pytest web-clipper/tests/test_clip.py -v 2>&1 | head -30`
Expected: FAIL — `ModuleNotFoundError: No module named 'clip'`

**Step 5: Commit**

```bash
git add web-clipper/tests/
git commit -m "test(web-clipper): add failing tests for clip extraction and saving"
```

---

### Task 3: Implement clip.py to pass tests

**Files:**
- Create: `web-clipper/scripts/clip.py`

**Step 1: Implement clip.py**

```python
#!/usr/bin/env python3
"""Clip a web page to clean markdown with YAML frontmatter.

Usage:
    clip.py <url> [--tags TAG,...] [--output-dir DIR] [--force-flaresolverr]
"""

import argparse
import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import urlparse

import requests
import trafilatura
import yaml
from slugify import slugify

DEFAULT_CLIPS_DIR = Path.home() / "web-clips"
FLARESOLVERR_URL = "http://localhost:8191/v1"
USER_AGENT = (
    "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
)


def fetch_url(url: str) -> tuple[str, int]:
    """Fetch a URL and return (html, status_code)."""
    resp = requests.get(url, headers={"User-Agent": USER_AGENT}, timeout=30)
    return resp.text, resp.status_code


def fetch_with_flaresolverr(url: str, timeout_ms: int = 30000) -> str:
    """Fetch a URL via FlareSolverr. Returns HTML or raises."""
    resp = requests.post(
        FLARESOLVERR_URL,
        json={"cmd": "request.get", "url": url, "maxTimeout": timeout_ms},
        timeout=60,
    )
    data = resp.json()
    if data.get("status") != "ok":
        raise RuntimeError(f"FlareSolverr error: {data}")
    return data["solution"]["response"]


def is_cloudflare_challenge(html: str, status_code: int) -> bool:
    """Detect Cloudflare challenge pages."""
    if status_code == 403:
        return True
    if "Just a moment..." in html[:2000]:
        return True
    if "challenge-running" in html[:5000]:
        return True
    return False


def extract_article(html: str, url: str) -> dict | None:
    """Extract article content from HTML using trafilatura.

    Returns dict with keys: title, text, author, date, description
    or None if extraction fails.
    """
    if not html or not html.strip():
        return None

    text = trafilatura.extract(
        html,
        url=url,
        include_comments=False,
        include_tables=True,
        output_format="txt",
    )
    if not text:
        return None

    metadata = trafilatura.extract_metadata(html, default_url=url)

    return {
        "title": (metadata.title if metadata and metadata.title else None)
            or _extract_title_fallback(html),
        "text": text,
        "author": metadata.author if metadata else None,
        "date": metadata.date if metadata else None,
        "description": metadata.description if metadata else None,
    }


def _extract_title_fallback(html: str) -> str:
    """Extract title from <title> tag as fallback."""
    match = re.search(r"<title[^>]*>([^<]+)</title>", html, re.IGNORECASE)
    return match.group(1).strip() if match else "Untitled"


def generate_markdown(article: dict, url: str, tags: list[str]) -> str:
    """Generate markdown with YAML frontmatter from extracted article."""
    domain = urlparse(url).netloc.removeprefix("www.")
    now = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S")

    frontmatter = {
        "title": article["title"] or "Untitled",
        "url": url,
        "domain": domain,
        "author": article.get("author"),
        "date_published": article.get("date"),
        "date_clipped": now,
        "tags": tags,
    }
    # Remove None values
    frontmatter = {k: v for k, v in frontmatter.items() if v is not None}

    fm_str = yaml.dump(frontmatter, default_flow_style=False, allow_unicode=True, sort_keys=False)
    title = article["title"] or "Untitled"

    return f"---\n{fm_str}---\n\n# {title}\n\n{article['text']}\n"


def generate_filename(title: str, date: str) -> str:
    """Generate a slugified filename from title and date."""
    slug = slugify(title, max_length=80)
    if not slug:
        slug = "untitled"
    name = f"{date}-{slug}.md"
    # Safety cap on total length
    if len(name) > 120:
        name = name[:116] + ".md"
    return name


def save_clip(content: str, title: str, output_dir: Path) -> Path:
    """Save markdown content to a file. Handles filename collisions."""
    output_dir.mkdir(parents=True, exist_ok=True)
    date_str = datetime.now(timezone.utc).strftime("%Y-%m-%d")
    filename = generate_filename(title, date_str)
    filepath = output_dir / filename

    # Handle collisions
    if filepath.exists():
        stem = filepath.stem
        suffix = filepath.suffix
        counter = 2
        while filepath.exists():
            filepath = output_dir / f"{stem}-{counter}{suffix}"
            counter += 1

    filepath.write_text(content, encoding="utf-8")
    return filepath


def main():
    parser = argparse.ArgumentParser(description="Clip a web page to markdown")
    parser.add_argument("url", help="URL to clip")
    parser.add_argument("--tags", default="", help="Comma-separated tags")
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=DEFAULT_CLIPS_DIR,
        help=f"Output directory (default: {DEFAULT_CLIPS_DIR})",
    )
    parser.add_argument(
        "--force-flaresolverr",
        action="store_true",
        help="Skip direct fetch, use FlareSolverr immediately",
    )
    parser.add_argument(
        "--format", "-f",
        choices=["text", "json"],
        default="text",
        help="Output format (default: text)",
    )
    args = parser.parse_args()

    tags = [t.strip() for t in args.tags.split(",") if t.strip()]

    # Fetch
    try:
        if args.force_flaresolverr:
            print("Fetching via FlareSolverr...", file=sys.stderr)
            html = fetch_with_flaresolverr(args.url)
            status_code = 200
        else:
            print(f"Fetching {args.url}...", file=sys.stderr)
            html, status_code = fetch_url(args.url)

            if is_cloudflare_challenge(html, status_code):
                print("Cloudflare detected, falling back to FlareSolverr...", file=sys.stderr)
                html = fetch_with_flaresolverr(args.url)
    except requests.RequestException as e:
        print(f"Error: Failed to fetch URL: {e}", file=sys.stderr)
        sys.exit(1)
    except RuntimeError as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)

    # Extract
    article = extract_article(html, args.url)
    if not article:
        print("Error: Could not extract article content from page", file=sys.stderr)
        sys.exit(1)

    # Generate and save
    markdown = generate_markdown(article, args.url, tags)
    filepath = save_clip(markdown, article["title"] or "Untitled", args.output_dir)

    # Output
    result = {
        "file": str(filepath),
        "title": article["title"],
        "url": args.url,
        "tags": tags,
        "size": len(markdown),
    }

    if args.format == "json":
        print(json.dumps(result, indent=2))
    else:
        print(f"Clipped: {article['title']}")
        print(f"Saved to: {filepath}")

    return 0


if __name__ == "__main__":
    sys.exit(main() or 0)
```

**Step 2: Run tests to verify they pass**

Run: `cd /home/dan/source/claude-skills && web-clipper/.venv/bin/python -m pytest web-clipper/tests/test_clip.py -v`
Expected: All tests PASS

Note: `test_extracts_title` and `test_returns_none_for_non_article_html` depend on trafilatura's behaviour — if they fail, adjust expectations to match what trafilatura actually returns for the given HTML. The key contract is: real articles return content, garbage returns None.

**Step 3: Commit**

```bash
git add web-clipper/scripts/clip.py
git commit -m "feat(web-clipper): implement core clipping pipeline"
```

---

### Task 4: Write and pass tests for list.py

**Files:**
- Create: `web-clipper/tests/test_list.py`
- Create: `web-clipper/scripts/list.py`

**Step 1: Create test_list.py**

```python
import json
import pytest
from pathlib import Path
from datetime import datetime

import sys
import os

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "scripts"))

CLIP_TEMPLATE = """---
title: "{title}"
url: "{url}"
domain: "{domain}"
date_clipped: "{date_clipped}"
tags: {tags}
---

# {title}

Some content here.
"""


def _write_clip(clips_dir: Path, filename: str, title: str, url: str, domain: str, tags: list, date_clipped: str):
    tag_str = json.dumps(tags)
    content = CLIP_TEMPLATE.format(
        title=title, url=url, domain=domain, tags=tag_str, date_clipped=date_clipped,
    )
    (clips_dir / filename).write_text(content)


@pytest.fixture
def populated_clips_dir(tmp_clips_dir):
    """Create a clips dir with 3 test clips."""
    _write_clip(
        tmp_clips_dir, "2026-01-10-first.md",
        "First Article", "https://blog.example.com/first", "blog.example.com",
        ["python"], "2026-01-10T10:00:00",
    )
    _write_clip(
        tmp_clips_dir, "2026-02-15-second.md",
        "Second Article", "https://news.example.com/second", "news.example.com",
        ["rust", "python"], "2026-02-15T12:00:00",
    )
    _write_clip(
        tmp_clips_dir, "2026-02-19-third.md",
        "Third Article", "https://blog.example.com/third", "blog.example.com",
        [], "2026-02-19T08:00:00",
    )
    return tmp_clips_dir


class TestListClips:
    def test_lists_all_clips(self, populated_clips_dir):
        from list import list_clips

        results = list_clips(populated_clips_dir)
        assert len(results) == 3

    def test_newest_first(self, populated_clips_dir):
        from list import list_clips

        results = list_clips(populated_clips_dir)
        dates = [r["date_clipped"] for r in results]
        assert dates == sorted(dates, reverse=True)

    def test_filter_by_domain(self, populated_clips_dir):
        from list import list_clips

        results = list_clips(populated_clips_dir, domain="blog.example.com")
        assert len(results) == 2
        assert all(r["domain"] == "blog.example.com" for r in results)

    def test_filter_by_tag(self, populated_clips_dir):
        from list import list_clips

        results = list_clips(populated_clips_dir, tag="python")
        assert len(results) == 2

    def test_filter_by_date_range(self, populated_clips_dir):
        from list import list_clips

        results = list_clips(populated_clips_dir, after="2026-02-01")
        assert len(results) == 2

    def test_empty_dir_returns_empty(self, tmp_clips_dir):
        from list import list_clips

        results = list_clips(tmp_clips_dir)
        assert results == []
```

**Step 2: Run tests to verify they fail**

Run: `cd /home/dan/source/claude-skills && web-clipper/.venv/bin/python -m pytest web-clipper/tests/test_list.py -v 2>&1 | head -20`
Expected: FAIL — `ModuleNotFoundError: No module named 'list'`

**Step 3: Implement list.py**

```python
#!/usr/bin/env python3
"""List saved web clips with optional filters.

Usage:
    list.py [--domain DOMAIN] [--tag TAG] [--after DATE] [--before DATE] [--format text|json]
"""

import argparse
import json
import sys
from pathlib import Path

import yaml

DEFAULT_CLIPS_DIR = Path.home() / "web-clips"


def parse_frontmatter(filepath: Path) -> dict | None:
    """Parse YAML frontmatter from a markdown file."""
    try:
        text = filepath.read_text(encoding="utf-8")
    except (OSError, UnicodeDecodeError):
        return None

    if not text.startswith("---"):
        return None

    parts = text.split("---\n", 2)
    if len(parts) < 3:
        return None

    try:
        fm = yaml.safe_load(parts[1])
    except yaml.YAMLError:
        return None

    if not isinstance(fm, dict):
        return None

    fm["filename"] = filepath.name
    return fm


def list_clips(
    clips_dir: Path,
    domain: str | None = None,
    tag: str | None = None,
    after: str | None = None,
    before: str | None = None,
) -> list[dict]:
    """List clips with optional filters. Returns list of frontmatter dicts."""
    if not clips_dir.exists():
        return []

    clips = []
    for f in clips_dir.glob("*.md"):
        fm = parse_frontmatter(f)
        if fm is None:
            continue

        # Apply filters
        if domain and fm.get("domain") != domain:
            continue
        if tag and tag not in (fm.get("tags") or []):
            continue
        if after:
            clipped = (fm.get("date_clipped") or "")[:10]
            if clipped < after:
                continue
        if before:
            clipped = (fm.get("date_clipped") or "")[:10]
            if clipped > before:
                continue

        clips.append(fm)

    # Sort newest first
    clips.sort(key=lambda c: c.get("date_clipped", ""), reverse=True)
    return clips


def main():
    parser = argparse.ArgumentParser(description="List saved web clips")
    parser.add_argument("--clips-dir", type=Path, default=DEFAULT_CLIPS_DIR)
    parser.add_argument("--domain", help="Filter by domain")
    parser.add_argument("--tag", help="Filter by tag")
    parser.add_argument("--after", help="Only clips after this date (YYYY-MM-DD)")
    parser.add_argument("--before", help="Only clips before this date (YYYY-MM-DD)")
    parser.add_argument("--format", "-f", choices=["text", "json"], default="text")
    args = parser.parse_args()

    clips = list_clips(args.clips_dir, args.domain, args.tag, args.after, args.before)

    if args.format == "json":
        print(json.dumps(clips, indent=2))
    else:
        if not clips:
            print("No clips found.")
            return
        for c in clips:
            tags = ", ".join(c.get("tags") or [])
            tag_str = f" [{tags}]" if tags else ""
            print(f"  {c.get('date_clipped', '?')[:10]}  {c.get('title', 'Untitled')}{tag_str}")
            print(f"             {c.get('url', '')}")


if __name__ == "__main__":
    main()
```

**Step 4: Run tests to verify they pass**

Run: `cd /home/dan/source/claude-skills && web-clipper/.venv/bin/python -m pytest web-clipper/tests/test_list.py -v`
Expected: All PASS

**Step 5: Commit**

```bash
git add web-clipper/tests/test_list.py web-clipper/scripts/list.py
git commit -m "feat(web-clipper): add list command with domain/tag/date filtering"
```

---

### Task 5: Write and pass tests for search.py

**Files:**
- Create: `web-clipper/tests/test_search.py`
- Create: `web-clipper/scripts/search.py`

**Step 1: Create test_search.py**

```python
import pytest
from pathlib import Path

import sys
import os

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "scripts"))


@pytest.fixture
def searchable_clips_dir(tmp_clips_dir):
    """Clips with varied content for search testing."""
    (tmp_clips_dir / "2026-01-01-python-guide.md").write_text(
        "---\ntitle: Python Guide\nurl: https://example.com/py\n---\n\nPython is a great programming language for beginners.\n"
    )
    (tmp_clips_dir / "2026-01-02-rust-intro.md").write_text(
        "---\ntitle: Rust Introduction\nurl: https://example.com/rust\n---\n\nRust provides memory safety without garbage collection.\n"
    )
    (tmp_clips_dir / "2026-01-03-cooking.md").write_text(
        "---\ntitle: Best Pasta Recipes\nurl: https://example.com/pasta\n---\n\nBoil water, add pasta, cook for 10 minutes.\n"
    )
    return tmp_clips_dir


class TestSearchClips:
    def test_finds_matching_clips(self, searchable_clips_dir):
        from search import search_clips

        results = search_clips(searchable_clips_dir, "programming language")
        assert len(results) >= 1
        assert any("Python" in r["title"] for r in results)

    def test_no_results_for_unmatched_query(self, searchable_clips_dir):
        from search import search_clips

        results = search_clips(searchable_clips_dir, "quantum physics")
        assert len(results) == 0

    def test_search_is_case_insensitive(self, searchable_clips_dir):
        from search import search_clips

        results = search_clips(searchable_clips_dir, "PYTHON")
        assert len(results) >= 1

    def test_respects_limit(self, searchable_clips_dir):
        from search import search_clips

        results = search_clips(searchable_clips_dir, "a", limit=1)
        assert len(results) <= 1
```

**Step 2: Run tests to verify they fail**

Run: `cd /home/dan/source/claude-skills && web-clipper/.venv/bin/python -m pytest web-clipper/tests/test_search.py -v 2>&1 | head -20`
Expected: FAIL — `ModuleNotFoundError: No module named 'search'`

**Step 3: Implement search.py**

```python
#!/usr/bin/env python3
"""Full-text search across saved web clips.

Usage:
    search.py <query> [--clips-dir DIR] [--limit N] [--format text|json]
"""

import argparse
import json
import re
import sys
from pathlib import Path

import yaml

DEFAULT_CLIPS_DIR = Path.home() / "web-clips"


def parse_clip(filepath: Path) -> dict | None:
    """Parse a clip file into frontmatter + body."""
    try:
        text = filepath.read_text(encoding="utf-8")
    except (OSError, UnicodeDecodeError):
        return None

    fm = {}
    body = text
    if text.startswith("---"):
        parts = text.split("---\n", 2)
        if len(parts) >= 3:
            try:
                fm = yaml.safe_load(parts[1]) or {}
            except yaml.YAMLError:
                pass
            body = parts[2]

    fm["filename"] = filepath.name
    fm["body"] = body
    return fm


def search_clips(clips_dir: Path, query: str, limit: int = 20) -> list[dict]:
    """Search clips by matching query words against title and body."""
    if not clips_dir.exists():
        return []

    query_words = query.lower().split()
    results = []

    for f in clips_dir.glob("*.md"):
        clip = parse_clip(f)
        if clip is None:
            continue

        searchable = (
            (clip.get("title") or "") + " " + (clip.get("body") or "")
        ).lower()

        if all(word in searchable for word in query_words):
            # Return without body to keep output small
            results.append({
                "filename": clip["filename"],
                "title": clip.get("title", "Untitled"),
                "url": clip.get("url", ""),
            })

    return results[:limit]


def main():
    parser = argparse.ArgumentParser(description="Search saved web clips")
    parser.add_argument("query", help="Search query")
    parser.add_argument("--clips-dir", type=Path, default=DEFAULT_CLIPS_DIR)
    parser.add_argument("--limit", type=int, default=20)
    parser.add_argument("--format", "-f", choices=["text", "json"], default="text")
    args = parser.parse_args()

    results = search_clips(args.clips_dir, args.query, args.limit)

    if args.format == "json":
        print(json.dumps(results, indent=2))
    else:
        if not results:
            print(f"No clips match '{args.query}'.")
            return
        print(f"Found {len(results)} clip(s):")
        for r in results:
            print(f"  {r['title']}")
            print(f"    {r['url']}")


if __name__ == "__main__":
    main()
```

**Step 4: Run tests to verify they pass**

Run: `cd /home/dan/source/claude-skills && web-clipper/.venv/bin/python -m pytest web-clipper/tests/test_search.py -v`
Expected: All PASS

**Step 5: Commit**

```bash
git add web-clipper/tests/test_search.py web-clipper/scripts/search.py
git commit -m "feat(web-clipper): add full-text search across clips"
```

---

### Task 6: Write and pass tests for delete.py

**Files:**
- Create: `web-clipper/tests/test_delete.py`
- Create: `web-clipper/scripts/delete.py`

**Step 1: Create test_delete.py**

```python
import pytest
from pathlib import Path

import sys
import os

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "scripts"))


@pytest.fixture
def clips_with_files(tmp_clips_dir):
    """Clips dir with files to delete."""
    (tmp_clips_dir / "2026-02-19-test-article.md").write_text(
        '---\ntitle: Test Article\nurl: "https://example.com/test"\n---\n\nContent.\n'
    )
    (tmp_clips_dir / "2026-02-19-other-article.md").write_text(
        '---\ntitle: Other Article\nurl: "https://example.com/other"\n---\n\nOther.\n'
    )
    return tmp_clips_dir


class TestDeleteClip:
    def test_delete_by_filename(self, clips_with_files):
        from delete import delete_clip

        result = delete_clip(clips_with_files, filename="2026-02-19-test-article.md")
        assert result is True
        assert not (clips_with_files / "2026-02-19-test-article.md").exists()
        # Other file untouched
        assert (clips_with_files / "2026-02-19-other-article.md").exists()

    def test_delete_by_url(self, clips_with_files):
        from delete import delete_clip

        result = delete_clip(clips_with_files, url="https://example.com/test")
        assert result is True
        assert not (clips_with_files / "2026-02-19-test-article.md").exists()

    def test_delete_nonexistent_returns_false(self, clips_with_files):
        from delete import delete_clip

        result = delete_clip(clips_with_files, filename="nonexistent.md")
        assert result is False

    def test_delete_by_url_no_match(self, clips_with_files):
        from delete import delete_clip

        result = delete_clip(clips_with_files, url="https://nope.com")
        assert result is False
```

**Step 2: Run tests to verify they fail**

Run: `cd /home/dan/source/claude-skills && web-clipper/.venv/bin/python -m pytest web-clipper/tests/test_delete.py -v 2>&1 | head -20`
Expected: FAIL — `ModuleNotFoundError: No module named 'delete'`

**Step 3: Implement delete.py**

```python
#!/usr/bin/env python3
"""Delete a saved web clip by filename or URL.

Usage:
    delete.py <filename>
    delete.py --url <url> [--clips-dir DIR]
"""

import argparse
import sys
from pathlib import Path

import yaml

DEFAULT_CLIPS_DIR = Path.home() / "web-clips"


def delete_clip(
    clips_dir: Path,
    filename: str | None = None,
    url: str | None = None,
) -> bool:
    """Delete a clip by filename or URL. Returns True if deleted."""
    if filename:
        filepath = clips_dir / filename
        if filepath.exists() and filepath.is_file():
            filepath.unlink()
            return True
        return False

    if url:
        for f in clips_dir.glob("*.md"):
            try:
                text = f.read_text(encoding="utf-8")
            except (OSError, UnicodeDecodeError):
                continue

            if not text.startswith("---"):
                continue
            parts = text.split("---\n", 2)
            if len(parts) < 3:
                continue
            try:
                fm = yaml.safe_load(parts[1])
            except yaml.YAMLError:
                continue

            if isinstance(fm, dict) and fm.get("url") == url:
                f.unlink()
                return True
        return False

    return False


def main():
    parser = argparse.ArgumentParser(description="Delete a saved web clip")
    parser.add_argument("filename", nargs="?", help="Clip filename to delete")
    parser.add_argument("--url", help="Delete clip matching this URL")
    parser.add_argument("--clips-dir", type=Path, default=DEFAULT_CLIPS_DIR)
    args = parser.parse_args()

    if not args.filename and not args.url:
        parser.error("Provide a filename or --url")

    deleted = delete_clip(args.clips_dir, filename=args.filename, url=args.url)

    if deleted:
        print("Clip deleted.")
    else:
        print("Error: Clip not found.", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
```

**Step 4: Run tests to verify they pass**

Run: `cd /home/dan/source/claude-skills && web-clipper/.venv/bin/python -m pytest web-clipper/tests/test_delete.py -v`
Expected: All PASS

**Step 5: Commit**

```bash
git add web-clipper/tests/test_delete.py web-clipper/scripts/delete.py
git commit -m "feat(web-clipper): add delete command by filename or URL"
```

---

### Task 7: Implement ingest.py (repo-search integration)

**Files:**
- Create: `web-clipper/scripts/ingest.py`

**Step 1: Implement ingest.py**

This is a thin wrapper that calls repo-search's ingest on the clips directory. No dedicated tests needed — it delegates to repo-search which has its own tests.

```python
#!/usr/bin/env python3
"""Ingest web clips into repo-search ChromaDB for semantic search.

Usage:
    ingest.py [--clips-dir DIR] [--collection NAME]

Requires repo-search skill to be installed.
"""

import argparse
import subprocess
import sys
from pathlib import Path

DEFAULT_CLIPS_DIR = Path.home() / "web-clips"
REPO_SEARCH_INGEST = Path.home() / ".claude" / "skills" / "repo-search" / "ingest.py"
REPO_SEARCH_VENV_PYTHON = Path.home() / ".claude" / "skills" / "repo-search" / ".venv" / "bin" / "python"


def main():
    parser = argparse.ArgumentParser(description="Ingest clips into repo-search ChromaDB")
    parser.add_argument("--clips-dir", type=Path, default=DEFAULT_CLIPS_DIR)
    parser.add_argument("--collection", default="web-clips", help="ChromaDB collection name")
    args = parser.parse_args()

    if not args.clips_dir.exists():
        print(f"Error: Clips directory not found: {args.clips_dir}", file=sys.stderr)
        sys.exit(1)

    clip_count = len(list(args.clips_dir.glob("*.md")))
    if clip_count == 0:
        print("No clips to ingest.")
        return

    if not REPO_SEARCH_INGEST.exists():
        print("Error: repo-search skill not found. Install it first:", file=sys.stderr)
        print(f"  Expected: {REPO_SEARCH_INGEST}", file=sys.stderr)
        sys.exit(1)

    if not REPO_SEARCH_VENV_PYTHON.exists():
        print("Error: repo-search venv not found. Run repo-search/setup.sh first.", file=sys.stderr)
        sys.exit(1)

    print(f"Ingesting {clip_count} clip(s) from {args.clips_dir}...")

    result = subprocess.run(
        [
            str(REPO_SEARCH_VENV_PYTHON),
            str(REPO_SEARCH_INGEST),
            str(args.clips_dir),
            "--collection", args.collection,
        ],
        capture_output=True,
        text=True,
    )

    if result.returncode != 0:
        print(f"Error during ingestion:\n{result.stderr}", file=sys.stderr)
        sys.exit(1)

    print(result.stdout)
    print(f"Done. Clips are now searchable via repo-search with --collection {args.collection}")


if __name__ == "__main__":
    main()
```

**Step 2: Commit**

```bash
git add web-clipper/scripts/ingest.py
git commit -m "feat(web-clipper): add repo-search ChromaDB ingestion"
```

---

### Task 8: Create SKILL.md

**Files:**
- Create: `web-clipper/SKILL.md`

**Step 1: Write SKILL.md**

```markdown
---
name: web-clipper
description: Clip web pages to clean markdown for later reading and search. Use when needing to save a URL, bookmark an article, clip a page, build a read-later collection, or archive web content. Trigger on phrases like "clip this", "save this page", "bookmark", "read later", "web clip", "save article".
---

# Web Clipper

Save any web page as clean markdown with YAML frontmatter. Optionally search your clips or ingest them into repo-search for semantic search.

## Prerequisites

- Python 3 with venv
- Docker (optional, for FlareSolverr fallback on Cloudflare-protected sites)

## Setup

### First-Time Setup

```bash
~/.claude/skills/web-clipper/setup.sh
```

This creates a `.venv` and installs dependencies (trafilatura, requests, python-slugify, pyyaml).

## Usage

### Clip a URL

```bash
~/.claude/skills/web-clipper/.venv/bin/python ~/.claude/skills/web-clipper/scripts/clip.py <url>
```

With tags:

```bash
~/.claude/skills/web-clipper/.venv/bin/python ~/.claude/skills/web-clipper/scripts/clip.py <url> --tags "python,web-dev"
```

Force FlareSolverr (for Cloudflare-protected sites):

```bash
~/.claude/skills/web-clipper/.venv/bin/python ~/.claude/skills/web-clipper/scripts/clip.py <url> --force-flaresolverr
```

JSON output:

```bash
~/.claude/skills/web-clipper/.venv/bin/python ~/.claude/skills/web-clipper/scripts/clip.py <url> -f json
```

Clips are saved to `~/web-clips/` as markdown files with YAML frontmatter (title, url, domain, author, date, tags).

### List Clips

```bash
~/.claude/skills/web-clipper/.venv/bin/python ~/.claude/skills/web-clipper/scripts/list.py
```

Filter by domain, tag, or date:

```bash
~/.claude/skills/web-clipper/.venv/bin/python ~/.claude/skills/web-clipper/scripts/list.py --domain "example.com"
~/.claude/skills/web-clipper/.venv/bin/python ~/.claude/skills/web-clipper/scripts/list.py --tag "python"
~/.claude/skills/web-clipper/.venv/bin/python ~/.claude/skills/web-clipper/scripts/list.py --after 2026-01-01 --before 2026-02-01
~/.claude/skills/web-clipper/.venv/bin/python ~/.claude/skills/web-clipper/scripts/list.py -f json
```

### Search Clips

Full-text search across all clips:

```bash
~/.claude/skills/web-clipper/.venv/bin/python ~/.claude/skills/web-clipper/scripts/search.py "search terms"
~/.claude/skills/web-clipper/.venv/bin/python ~/.claude/skills/web-clipper/scripts/search.py "search terms" -f json
```

### Delete a Clip

```bash
~/.claude/skills/web-clipper/.venv/bin/python ~/.claude/skills/web-clipper/scripts/delete.py <filename>
~/.claude/skills/web-clipper/.venv/bin/python ~/.claude/skills/web-clipper/scripts/delete.py --url "https://example.com/article"
```

### Ingest into Repo Search (semantic search)

Requires the repo-search skill. Pushes all clips into ChromaDB:

```bash
~/.claude/skills/web-clipper/.venv/bin/python ~/.claude/skills/web-clipper/scripts/ingest.py
```

After ingestion, clips are searchable via repo-search:

```bash
~/.claude/skills/repo-search/.venv/bin/python ~/.claude/skills/repo-search/query.py "query" --collection web-clips
```

## Error Handling

| Error | Cause | Fix |
|-------|-------|-----|
| `Could not extract article content` | Page has no extractable article text (e.g., SPA, login wall) | Try `--force-flaresolverr` for JS-rendered pages |
| `FlareSolverr error` | FlareSolverr container not running | Run `~/.claude/skills/flaresolverr/scripts/flaresolverr-ensure.sh` |
| `repo-search skill not found` | repo-search not installed | Run `./install.sh repo-search` |

## Limitations

- Extracts article text only — does not preserve images, videos, or interactive elements
- JavaScript-rendered SPAs may need FlareSolverr for content extraction
- Login-walled content cannot be accessed
```

**Step 2: Commit**

```bash
git add web-clipper/SKILL.md
git commit -m "feat(web-clipper): add SKILL.md for Claude Code discovery"
```

---

### Task 9: Update install.sh

**Files:**
- Modify: `install.sh`

**Step 1: Add web-clipper to AVAILABLE_SKILLS array**

Find the line:
```bash
AVAILABLE_SKILLS=(outlook trello repo-search pst-to-markdown email-search flaresolverr)
```
Replace with:
```bash
AVAILABLE_SKILLS=(outlook trello repo-search pst-to-markdown email-search flaresolverr web-clipper)
```

**Step 2: Add check_deps_web_clipper function**

Add after the last `check_deps_*` function (before the `check_deps()` dispatcher):

```bash
check_deps_web_clipper() {
    if ! command -v python3 &>/dev/null; then
        err "Missing dependency for web-clipper: python3"
        return 1
    fi
    return 0
}
```

**Step 3: Add web-clipper to check_deps dispatcher**

Add a case line inside the `check_deps()` function:

```bash
        web-clipper)    check_deps_web_clipper ;;
```

**Step 4: Add post_install case for web-clipper**

Add inside `post_install()`:

```bash
        web-clipper)
            chmod +x "$REPO_DIR/web-clipper/setup.sh" 2>/dev/null || true
            if [ ! -d "$REPO_DIR/web-clipper/.venv" ]; then
                info "Setting up Python virtual environment..."
                "$REPO_DIR/web-clipper/setup.sh"
            else
                ok "Python venv already exists"
                "$REPO_DIR/web-clipper/.venv/bin/pip" install -r "$REPO_DIR/web-clipper/requirements.txt" -q 2>/dev/null || true
            fi
            ;;
```

**Step 5: Verify install works**

Run: `./install.sh web-clipper`
Expected: Symlink created, venv set up, "Setup Complete"

**Step 6: Commit**

```bash
git add install.sh
git commit -m "feat(web-clipper): register skill in install.sh"
```

---

### Task 10: Create README.md and update root README

**Files:**
- Create: `web-clipper/README.md`
- Modify: `README.md` (root)

**Step 1: Create web-clipper/README.md**

```markdown
# Web Clipper

Clip any web page to clean markdown with YAML frontmatter. Search your clips locally or ingest them into [repo-search](../repo-search/) for semantic search.

## Features

- **Clean extraction** — trafilatura pulls article text, title, author, and date from any page
- **Cloudflare bypass** — automatic fallback to FlareSolverr for protected pages
- **Local storage** — clips saved as markdown files in `~/web-clips/`
- **Tagging** — add tags when clipping for easy filtering
- **Search** — full-text search across all saved clips
- **Repo-search integration** — optional ChromaDB ingestion for semantic search

## Setup

```bash
./install.sh web-clipper
```

Or manually:

```bash
cd web-clipper && ./setup.sh
```

## Dependencies

- Python 3
- Docker (optional, for FlareSolverr)

## Clip Format

Each clip is a markdown file with YAML frontmatter:

```markdown
---
title: "Article Title"
url: "https://example.com/article"
domain: "example.com"
author: "Author Name"
date_published: "2026-02-19"
date_clipped: "2026-02-19T14:30:00"
tags:
- python
- web-dev
---

# Article Title

Extracted article content...
```

## Quick Reference

| Command | Description |
|---------|-------------|
| `clip.py <url>` | Clip a URL to markdown |
| `clip.py <url> --tags "a,b"` | Clip with tags |
| `clip.py <url> --force-flaresolverr` | Force FlareSolverr |
| `list.py` | List all clips |
| `list.py --domain "example.com"` | Filter by domain |
| `list.py --tag "python"` | Filter by tag |
| `search.py "query"` | Full-text search |
| `delete.py <filename>` | Delete by filename |
| `delete.py --url <url>` | Delete by URL |
| `ingest.py` | Push clips to repo-search ChromaDB |
```

**Step 2: Update root README.md skills table**

Add after the FlareSolverr row in the skills table:

```markdown
| 📎 **[Web Clipper](./web-clipper/)** | Clip web pages to markdown with YAML frontmatter — clean extraction, Cloudflare bypass, tagging, full-text search, repo-search integration |
```

Also add to the Credentials table:

```markdown
| 📎 Web Clipper | None (local only) | `web-clipper/setup.sh` |
```

And the Requirements table:

```markdown
| 📎 Web Clipper | Python 3 · pip · Docker (optional) |
```

**Step 3: Commit**

```bash
git add web-clipper/README.md README.md
git commit -m "docs(web-clipper): add README and update root documentation"
```

---

### Task 11: Run full test suite

**Step 1: Run all web-clipper tests**

Run: `cd /home/dan/source/claude-skills && web-clipper/.venv/bin/python -m pytest web-clipper/tests/ -v`
Expected: All tests PASS

**Step 2: Fix any failures, re-run, commit fixes if needed**
