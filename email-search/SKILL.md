---
name: email-search
description: Process email archives (PST files) into a searchable ChromaDB vector database with automatic semantic embeddings. Ingest, classify, search, analyse, and export to markdown. Trigger on phrases like "email archive", "ingest pst", "search emails", "email analytics", "export contacts", "email timeline".
---

# Email Search CLI

Process email archives (PST files) into a searchable ChromaDB vector database with automatic semantic embeddings. Ingest, classify, search, analyse, and export to markdown.

## Prerequisites

- Python 3.11+
- Package installed in a virtual environment

### First-Time Setup

```bash
# Set up Python environment (one-time)
~/.claude/skills/email-search/setup.sh
```

## Usage

All commands use the `email-search` CLI, which should be run from the skill's virtual environment:

```bash
~/.claude/skills/email-search/.venv/bin/email-search <command>
```

### Ingest a PST file

```bash
email-search ingest /path/to/archive.pst
```

Emails are classified into three tiers:
- **Tier 1 (Excluded)**: Calendar invites, delivery notifications, password resets - skipped
- **Tier 2 (Metadata Only)**: Short emails, automated senders - stored but not vectorised
- **Tier 3 (Vectorise)**: Real conversations - stored and auto-embedded

### Search emails

```bash
email-search search "budget meeting notes"
email-search search "project update" --from 2023-01-01 --to 2023-12-31
email-search search "invoice" --sender "accounting"
email-search search "contract terms" --emails-only
email-search search "spreadsheet data" --attachments-only --limit 5
```

### Analytics

```bash
email-search analytics summary
email-search analytics timeline
email-search analytics timeline --monthly --year 2020
email-search analytics contacts --limit 50
```

### Export to markdown

```bash
email-search export contacts -o areas/relationships/email-contacts.md
email-search export review -p week -d 2023-01-15
email-search export review -p month -o reviews/monthly/2023-01-email.md
```

### Status

```bash
email-search status
```

## Configuration

```bash
# Default data directory: ./email-search-data
export EMAIL_SEARCH_DATA_DIR="/path/to/data"
```

## Key Details

- No API keys or external services needed - ChromaDB runs locally with built-in embeddings (all-MiniLM-L6-v2)
- Text extraction from PDF, Word, Excel, and plain text attachments
- No data leaves the machine
