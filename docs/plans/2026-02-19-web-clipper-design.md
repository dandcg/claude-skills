# Web Clipper Skill Design

**Date:** 2026-02-19
**Status:** Approved
**Approach:** Python skill with FlareSolverr fallback and repo-search integration

## Context

The claude-skills repo has strong document search (repo-search) and web scraping (FlareSolverr) capabilities, but no way to capture web content for later retrieval. A web clipper skill bridges these — saving any URL as clean markdown with YAML frontmatter, stored locally, and optionally ingested into the existing ChromaDB index for semantic search.

---

## 1. Skill Structure

```
web-clipper/
  SKILL.md            # Skill definition (YAML frontmatter + usage docs)
  README.md           # Human-readable documentation
  setup.sh            # Creates .venv, installs deps
  scripts/
    clip.py           # Fetch URL → extract article → save markdown
    list.py           # List saved clips with optional filters
    search.py         # Full-text search across saved clips
    delete.py         # Remove a clip by filename or URL
    ingest.py         # Push clips into repo-search ChromaDB
```

---

## 2. Clipping Pipeline

1. **Fetch** — `requests.get()` with a browser-like User-Agent
2. **Fallback** — If response is 403 or contains Cloudflare challenge markers, automatically call FlareSolverr (`http://localhost:8191/v1/request`) to get the rendered HTML
3. **Extract** — Use `trafilatura` to pull clean article text, title, author, date, and description from the HTML
4. **Convert** — Output as markdown with YAML frontmatter
5. **Save** — Write to `~/web-clips/<YYYY-MM-DD>-<slugified-title>.md`

### Markdown Output Format

```markdown
---
title: "Article Title"
url: "https://example.com/article"
domain: "example.com"
author: "Author Name"
date_published: "2026-02-19"
date_clipped: "2026-02-19T14:30:00"
tags: []
---

# Article Title

Extracted article content in clean markdown...
```

### CLI Interface

```bash
# Basic clip
clip.py <url>

# Clip with tags
clip.py <url> --tags "python,web-scraping"

# Clip and immediately ingest into ChromaDB
clip.py <url> --ingest

# Force FlareSolverr (skip direct fetch)
clip.py <url> --force-flaresolverr
```

---

## 3. List & Search

### list.py

```bash
# List all clips (newest first)
list.py

# Filter by domain
list.py --domain "example.com"

# Filter by tag
list.py --tag "python"

# Filter by date range
list.py --after 2026-01-01 --before 2026-02-01
```

Output: JSON array of `{filename, title, url, domain, date_clipped, tags}`.

### search.py

```bash
# Full-text search across clip content
search.py "transformer architecture"

# Search with limit
search.py "transformer architecture" --limit 5
```

Simple grep-based full-text search for standalone use. For semantic search, use repo-search after ingesting clips.

### delete.py

```bash
# Delete by filename
delete.py 2026-02-19-some-article.md

# Delete by URL
delete.py --url "https://example.com/article"
```

---

## 4. Repo-Search Integration

### ingest.py

```bash
# Ingest all clips into repo-search ChromaDB
ingest.py

# Ingest only new clips (not already in the collection)
ingest.py --new-only
```

- Calls repo-search's ingestion with `~/web-clips/` as the source directory
- Clips appear in repo-search results alongside other documents
- Uses the `web-clips` area in ChromaDB metadata for filtering

This is optional — the skill works standalone without repo-search.

---

## 5. FlareSolverr Integration

- Check if FlareSolverr container is running before attempting fallback
- If not running and `--force-flaresolverr` is set, start it automatically via `docker run`
- Timeout: 30 seconds for FlareSolverr requests
- If both direct fetch and FlareSolverr fail, report error with details

---

## 6. Storage

- Default directory: `~/web-clips/`
- Configurable via `WEB_CLIPS_DIR` environment variable
- Filenames: `<YYYY-MM-DD>-<slugified-title>.md`
- Filename collisions: append `-2`, `-3`, etc.
- No database — the markdown files with YAML frontmatter ARE the data store

---

## 7. Dependencies

New `requirements.txt`:
- `trafilatura` — article extraction (handles readability + metadata)
- `requests` — HTTP fetching
- `python-slugify` — URL-safe filename generation
- `pyyaml` — YAML frontmatter parsing/writing

No heavyweight dependencies. trafilatura bundles its own HTML parsing.

---

## 8. Implementation Order

1. **Scaffold** — skill directory, SKILL.md, setup.sh, requirements.txt
2. **clip.py** — core clipping pipeline (fetch → extract → save)
3. **FlareSolverr fallback** — automatic 403 detection and fallback
4. **list.py** — listing with filters
5. **search.py** — full-text search
6. **delete.py** — clip removal
7. **ingest.py** — repo-search ChromaDB integration
8. **install.sh** — add web-clipper to the installer
9. **README.md** — documentation
10. **Tests** — unit tests for extraction, frontmatter, filename generation
