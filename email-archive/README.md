# Email Archive CLI

Process email archives (PST files) into a searchable PostgreSQL database with pgvector for semantic search. Ingest, classify, embed, search, analyse, and export to markdown.

## Features

- **Ingest** PST files with automatic email classification into tiers
- **Extract** text from PDF, Word, Excel, and plain text attachments
- **Embed** emails and attachments using OpenAI `text-embedding-3-small`
- **Search** with natural language queries, date ranges, and sender filters
- **Analytics** — email volume timelines, top contacts, activity patterns
- **Export** contacts and review sections to second brain markdown

## Prerequisites

- **.NET 10 SDK** (or .NET 8+)
- **PostgreSQL** with the [pgvector](https://github.com/pgvector/pgvector) extension
- **OpenAI API key** (for embedding and search — not needed for ingestion/analytics)

## Installation

```bash
cd email-archive
dotnet build
```

## Database Setup

### 1. Install pgvector

```bash
# macOS (Homebrew)
brew install pgvector

# Ubuntu/Debian
sudo apt install postgresql-16-pgvector

# Or build from source: https://github.com/pgvector/pgvector#installation
```

### 2. Create Database

```bash
createdb email_archive
psql -d email_archive -c "CREATE EXTENSION vector;"
```

The application creates the required tables automatically on first run. Alternatively, you can initialise manually:

```bash
psql -d email_archive -f scripts/init-db.sql
```

## Configuration

Configure via environment variables or `appsettings.json`:

### Environment Variables

```bash
export EMAIL_ARCHIVE_CONNECTION_STRING="Host=localhost;Database=email_archive;Username=postgres;Password=yourpassword"
export EMAIL_ARCHIVE_OPENAI_API_KEY="sk-..."
```

### appsettings.json

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Database=email_archive;Username=postgres;Password=yourpassword"
  },
  "OpenAI": {
    "ApiKey": "sk-..."
  }
}
```

For local development, use `appsettings.Development.json` (gitignored) to keep secrets out of version control.

## Quick Start

```bash
# 1. Ingest a PST file
dotnet run --project src/EmailArchive -- ingest /path/to/archive.pst

# 2. Check what was imported
dotnet run --project src/EmailArchive -- status

# 3. Generate embeddings (requires OpenAI key)
dotnet run --project src/EmailArchive -- embed

# 4. Search
dotnet run --project src/EmailArchive -- search "budget meeting notes"
```

## Commands

### `ingest <pst-file>`

Parse a PST file, classify emails by tier, and store in PostgreSQL.

Emails are classified into three tiers:

| Tier | Name | Description | Action |
|------|------|-------------|--------|
| 1 | Excluded | Calendar invites, delivery notifications, password resets | Skipped entirely |
| 2 | Metadata Only | Short emails, automated senders, one-word replies | Stored but not vectorised |
| 3 | Vectorise | Real conversations with substantive content | Stored and queued for embedding |

For Tier 3 emails, text is automatically extracted from attachments:
- **PDF** (via PdfPig)
- **Word** (.docx via OpenXml)
- **Excel** (.xlsx via ClosedXML)
- **Plain text** (.txt, .csv, .log)

### `status`

Show counts of emails by tier, embedding status, and attachment statistics.

### `embed`

Generate OpenAI embeddings for Tier 3 emails and attachments with extracted text.

```bash
dotnet run --project src/EmailArchive -- embed                     # All pending
dotnet run --project src/EmailArchive -- embed --emails-only       # Emails only
dotnet run --project src/EmailArchive -- embed --attachments-only  # Attachments only
dotnet run --project src/EmailArchive -- embed --batch-size 50     # Custom batch size
```

Uses `text-embedding-3-small` (1536 dimensions). Requires `EMAIL_ARCHIVE_OPENAI_API_KEY` environment variable.

### `search <query>`

Search emails and attachments using natural language queries.

```bash
dotnet run --project src/EmailArchive -- search "budget meeting notes"
dotnet run --project src/EmailArchive -- search "project update" --from 2023-01-01 --to 2023-12-31
dotnet run --project src/EmailArchive -- search "invoice" --sender "accounting"
dotnet run --project src/EmailArchive -- search "contract terms" --emails-only
dotnet run --project src/EmailArchive -- search "spreadsheet data" --attachments-only --limit 5
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
dotnet run --project src/EmailArchive -- analytics summary
dotnet run --project src/EmailArchive -- analytics timeline
dotnet run --project src/EmailArchive -- analytics timeline --monthly --year 2020
dotnet run --project src/EmailArchive -- analytics contacts --limit 50
```

| Subcommand | Description |
|------------|-------------|
| `summary` | Archive overview with activity-by-hour and activity-by-day charts |
| `timeline` | Email volume over time (bar chart + table, yearly or `--monthly`) |
| `contacts` | Top contacts ranked by email volume |

### `export`

Export email data to second brain markdown files.

```bash
dotnet run --project src/EmailArchive -- export contacts -o areas/relationships/email-contacts.md
dotnet run --project src/EmailArchive -- export contacts -n 10 --min-emails 10
dotnet run --project src/EmailArchive -- export review -p week -d 2023-01-15
dotnet run --project src/EmailArchive -- export review -p month -o reviews/monthly/2023-01-email.md
```

| Subcommand | Options | Description |
|------------|---------|-------------|
| `contacts` | `-o`, `-n`, `--min-emails` | Export top contacts for relationships area |
| `review` | `-p` (week/month), `-d`, `-o` | Export email activity for weekly/monthly reviews |

## Architecture

```
src/EmailArchive/
├── Commands/           # CLI command handlers
│   ├── Analytics/      # summary, timeline, contacts
│   └── Export/         # contacts, review
├── Configuration/      # appsettings loader
├── Embedding/          # OpenAI embedding service
├── Ingest/             # PST parser, email filter, attachment extractor
├── Models/             # Email, Attachment, Tier
├── Search/             # Vector similarity search
└── Storage/            # PostgreSQL repositories
```

## Development

```bash
# Run tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Test Database

```bash
createdb email_archive_test
psql -d email_archive_test -c "CREATE EXTENSION vector;"
export EMAIL_ARCHIVE_TEST_DB="Host=localhost;Database=email_archive_test;Username=postgres;Password=yourpassword"
dotnet test
```

## Troubleshooting

### "relation does not exist" or table errors
The application creates tables automatically. If this fails, run the init script manually:
```bash
psql -d email_archive -f scripts/init-db.sql
```

### "extension vector does not exist"
Install the pgvector extension for your PostgreSQL version. See [pgvector installation](https://github.com/pgvector/pgvector#installation).

### Embedding is slow
- Default batch size is 100 — lower it with `--batch-size` if you hit rate limits
- Only Tier 3 emails are embedded (real conversations), keeping costs proportional
- Progress is displayed during embedding

### PST file won't parse
- Ensure the file is a valid Outlook PST (not OST)
- Large PST files (>5GB) work but take longer — ingestion shows progress
- Corrupted PST files may partially parse (successful emails are still stored)

## Security Notes

- Store credentials in `appsettings.Development.json` (gitignored) or environment variables
- The template `appsettings.json` contains placeholder values only
- Email content is stored in PostgreSQL — secure your database accordingly
