---
name: email-archive
description: Process email archives (PST files) into a searchable ChromaDB vector database with automatic semantic embeddings. Ingest, classify, search, analyse, and export to markdown. Trigger on phrases like "email archive", "ingest pst", "search emails", "email analytics", "export contacts", "email timeline".
---

# Email Archive CLI

Process email archives (PST files) into a searchable ChromaDB vector database with automatic semantic embeddings. Ingest, classify, search, analyse, and export to markdown.

## Prerequisites

- Python 3.11+
- Package installed in a virtual environment

### First-Time Setup

```bash
# Set up Python environment (one-time)
~/.claude/skills/email-archive/setup.sh
```

## Usage

All commands use the `email-archive` CLI, which should be run from the skill's virtual environment:

```bash
~/.claude/skills/email-archive/.venv/bin/email-archive <command>
```

### Ingest a PST file

```bash
email-archive ingest /path/to/archive.pst
```

Emails are classified into three tiers:
- **Tier 1 (Excluded)**: Calendar invites, delivery notifications, password resets - skipped
- **Tier 2 (Metadata Only)**: Short emails, automated senders - stored but not vectorised
- **Tier 3 (Vectorise)**: Real conversations - stored and auto-embedded

### Search emails

```bash
email-archive search "budget meeting notes"
email-archive search "project update" --from 2023-01-01 --to 2023-12-31
email-archive search "invoice" --sender "accounting"
email-archive search "contract terms" --emails-only
email-archive search "spreadsheet data" --attachments-only --limit 5
```

### Analytics

```bash
email-archive analytics summary
email-archive analytics timeline
email-archive analytics timeline --monthly --year 2020
email-archive analytics contacts --limit 50
```

### Export to markdown

```bash
email-archive export contacts -o areas/relationships/email-contacts.md
email-archive export review -p week -d 2023-01-15
email-archive export review -p month -o reviews/monthly/2023-01-email.md
```

### Status

```bash
email-archive status
```

## Configuration

```bash
# Default data directory: ./email-archive-data
export EMAIL_ARCHIVE_DATA_DIR="/path/to/data"
```

## Key Details

- No API keys or external services needed - ChromaDB runs locally with built-in embeddings (all-MiniLM-L6-v2)
- Text extraction from PDF, Word, Excel, and plain text attachments
- No data leaves the machine
