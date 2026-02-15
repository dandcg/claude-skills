# Email Search CLI

Process email archives (PST files) into a searchable ChromaDB vector database with automatic semantic embeddings. Ingest, classify, search, analyse, and export to markdown.

## Features

- **Ingest** PST files with automatic email classification into tiers
- **Extract** text from PDF, Word, Excel, and plain text attachments
- **Auto-embed** emails and attachments using ChromaDB's built-in model (all-MiniLM-L6-v2) — no API keys needed
- **Search** with natural language queries, date ranges, and sender filters
- **Analytics** — email volume timelines, top contacts, activity patterns
- **Export** contacts and review sections to second brain markdown

## Prerequisites

- **Python 3.11+**

That's it. No PostgreSQL, no OpenAI API key, no Docker. ChromaDB runs embedded with local persistence.

## Installation

```bash
cd email-search
pip install -e .
```

For development (with tests):
```bash
pip install -e ".[dev]"
```

## Quick Start

```bash
# 1. Ingest a PST file (auto-embeds Tier 3 emails)
email-search ingest /path/to/archive.pst

# 2. Check what was imported
email-search status

# 3. Search (no separate embed step needed!)
email-search search "budget meeting notes"
```

## Configuration

The only configuration is the data directory for ChromaDB storage:

```bash
# Default: ./email-search-data
export EMAIL_SEARCH_DATA_DIR="/path/to/data"
```

## Commands

### `ingest <pst-file>`

Parse a PST file, classify emails by tier, and store in ChromaDB. Tier 3 emails are automatically embedded on ingest — no separate embed step.

Emails are classified into three tiers:

| Tier | Name | Description | Action |
|------|------|-------------|--------|
| 1 | Excluded | Calendar invites, delivery notifications, password resets | Skipped entirely |
| 2 | Metadata Only | Short emails, automated senders, one-word replies | Stored but not vectorised |
| 3 | Vectorise | Real conversations with substantive content | Stored and auto-embedded |

For Tier 3 emails, text is automatically extracted from attachments:
- **PDF** (via pdfplumber)
- **Word** (.docx via python-docx)
- **Excel** (.xlsx via openpyxl)
- **Plain text** (.txt, .csv)

### `status`

Show counts of emails by tier, embedding status, and attachment statistics.

### `search <query>`

Search emails and attachments using natural language queries.

```bash
email-search search "budget meeting notes"
email-search search "project update" --from 2023-01-01 --to 2023-12-31
email-search search "invoice" --sender "accounting"
email-search search "contract terms" --emails-only
email-search search "spreadsheet data" --attachments-only --limit 5
```

| Option | Description |
|--------|-------------|
| `--limit N` | Maximum results (default: 10) |
| `--from DATE` | Filter from date (YYYY-MM-DD) |
| `--to DATE` | Filter until date (YYYY-MM-DD) |
| `--sender TEXT` | Filter by sender name or email |
| `--emails-only` | Only search emails |
| `--attachments-only` | Only search attachments |

### `analytics`

Analyse email patterns and statistics.

```bash
email-search analytics summary
email-search analytics timeline
email-search analytics timeline --monthly --year 2020
email-search analytics contacts --limit 50
```

| Subcommand | Description |
|------------|-------------|
| `summary` | Archive overview with activity-by-hour and activity-by-day charts |
| `timeline` | Email volume over time (bar chart + table, yearly or `--monthly`) |
| `contacts` | Top contacts ranked by email volume |

### `export`

Export email data to second brain markdown files.

```bash
email-search export contacts -o areas/relationships/email-contacts.md
email-search export contacts -n 10 --min-emails 10
email-search export review -p week -d 2023-01-15
email-search export review -p month -o reviews/monthly/2023-01-email.md
```

| Subcommand | Options | Description |
|------------|---------|-------------|
| `contacts` | `-o`, `-n`, `--min-emails` | Export top contacts for relationships area |
| `review` | `-p` (week/month), `-d`, `-o` | Export email activity for weekly/monthly reviews |

## Architecture

```
email_search/
├── cli.py                  # Click CLI commands
├── models.py               # Email, Attachment, Tier dataclasses
├── store.py                # ChromaDB storage (replaces PostgreSQL + pgvector)
├── email_filter.py         # Three-tier email classification
├── pst_parser.py           # PST file parsing (libpff)
├── attachment_extractor.py # PDF/Word/Excel/text extraction
└── markdown_formatter.py   # Export formatting
```

### Key Design Changes from v1 (C#/.NET)

| v1 (C#) | v2 (Python) |
|---------|-------------|
| PostgreSQL + pgvector | ChromaDB (embedded, local persistence) |
| OpenAI `text-embedding-3-small` (1536d) | ChromaDB built-in `all-MiniLM-L6-v2` (384d) |
| Separate `embed` command | Auto-embedded on ingest |
| Requires running database server | Zero-config local storage |
| `Npgsql` + `Pgvector` packages | Single `chromadb` package |
| 12 NuGet packages | 7 PyPI packages |

## Development

```bash
# Run tests
python3 -m pytest tests_py/ -v

# Run tests with coverage
python3 -m pytest tests_py/ --cov=email_search --cov-report=term-missing
```

## Troubleshooting

### PST file won't parse
- Ensure the file is a valid Outlook PST (not OST)
- Large PST files (>5GB) work but take longer — ingestion shows progress
- Corrupted PST files may partially parse (successful emails are still stored)

### ChromaDB data location
Data is stored in `./email-search-data/` by default. Set `EMAIL_SEARCH_DATA_DIR` to change.

### First search is slow
ChromaDB downloads the all-MiniLM-L6-v2 model on first use (~80MB). Subsequent runs use the cached model.

## Security Notes

- Email content is stored in the local ChromaDB directory — secure your filesystem accordingly
- No API keys or external services required
- No data leaves your machine
