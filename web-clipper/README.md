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
