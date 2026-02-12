# Vector Search & Summarisation

Semantic search across a directory of markdown files using ChromaDB vector embeddings. Find information by meaning rather than keywords, filter by area or date range, and build summaries from relevant chunks.

Designed for use with a "second brain" or personal knowledge base, but works with any collection of markdown files.

## Features

- **Semantic search** — find content by meaning, not just keywords
- **Area filtering** — scope queries to a specific domain (e.g. finance, health)
- **Date range queries** — build timelines across your notes
- **File browsing** — retrieve all chunks for a specific file
- **Incremental ingestion** — only re-index changed files
- **JSON output** — pipe results into other tools or Claude for synthesis

## Quick Start

### 1. Install the Skill

From the repo root:

```bash
./install.sh vector-search
```

This installs the skill and sets up the Python virtual environment automatically.

### 2. Build the Index

Point the ingestion script at your markdown directory:

```bash
~/.claude/skills/vector-search/.venv/bin/python \
  ~/.claude/skills/vector-search/ingest.py \
  /path/to/your/markdown-repo --verbose
```

The database is stored in `/path/to/your/markdown-repo/.vectordb/`.

### 3. Search

```bash
~/.claude/skills/vector-search/.venv/bin/python \
  ~/.claude/skills/vector-search/query.py \
  --db-path /path/to/your/markdown-repo/.vectordb \
  search "your query here"
```

## How It Works

1. **Ingest** scans for `.md` files, splits them into ~250-token chunks (1000 chars with 200-char overlap), extracts metadata from frontmatter and file paths, and stores embeddings in a local ChromaDB database.
2. **Query** uses ChromaDB's built-in sentence-transformer embeddings to find the most semantically similar chunks to your query, with optional filtering by area, file, or date range.

No external APIs are needed — embeddings are generated locally using ChromaDB's default model.

## Usage

### Semantic Search

```bash
# Basic search (top 10 results)
query.py --db-path .vectordb search "investment strategy"

# More results
query.py --db-path .vectordb search "investment strategy" -k 20

# Filter by area
query.py --db-path .vectordb search "morning routine" --area health

# JSON output (for piping to other tools)
query.py --db-path .vectordb -f json search "investment strategy" -k 5
```

### Browse by Area

```bash
# All chunks in an area
query.py --db-path .vectordb area finance
query.py --db-path .vectordb area health -k 100
```

### Browse by File

```bash
query.py --db-path .vectordb file "areas/finance/index.md"
```

### Date Range

```bash
query.py --db-path .vectordb date-range 2025-01-01 2025-12-31
```

### Database Info

```bash
# Stats (total chunks, files, areas, date range)
query.py --db-path .vectordb stats

# List all indexed files
query.py --db-path .vectordb list
```

### Rebuilding the Index

```bash
# Incremental update (only changed files — fast)
ingest.py /path/to/your/markdown-repo

# Full rebuild
ingest.py /path/to/your/markdown-repo --force --verbose

# Dry run (see what would change)
ingest.py /path/to/your/markdown-repo --dry-run
```

## Natural Language (via Claude)

Once installed, Claude Code will automatically use this skill:

| You say | What happens |
|---------|--------------|
| "search my brain for investment info" | Semantic search |
| "what do I know about sleep?" | Search + summarise |
| "summarise my finance area" | Area retrieval + synthesis |
| "timeline of career decisions in 2024" | Date-range query |
| "rebuild the vector index" | Re-runs ingestion |

## Directory Structure

The ingestion script expects a markdown corpus. It works best with an organised structure like:

```
your-markdown-repo/
├── areas/
│   ├── finance/
│   ├── health/
│   └── career/
├── projects/
├── decisions/
├── reviews/
└── resources/
```

Top-level directories become searchable "areas". The script skips `.git`, `node_modules`, `.venv`, and other non-content directories automatically.

## Configuration

| Setting | Default | Description |
|---------|---------|-------------|
| Chunk size | 1000 chars | ~250 tokens per chunk |
| Chunk overlap | 200 chars | Context preserved between chunks |
| Skipped dirs | `.git`, `.venv`, `node_modules`, etc. | Directories excluded from scanning |
| Skipped files | `TEMPLATE.md`, `README.md` | Files excluded from indexing |

These can be adjusted in `ingest.py` constants.

## File Structure

```
~/.claude/skills/vector-search/
├── SKILL.md            # Skill definition
├── ingest.py           # Index builder
├── query.py            # Search interface
├── setup.sh            # Python venv setup
├── requirements.txt    # Python dependencies
└── .venv/              # Virtual environment (created by setup)
```

## Dependencies

- **Python 3.8+**
- **chromadb** — vector database with built-in embeddings
- **langchain-text-splitters** — markdown-aware chunking

All installed automatically by `setup.sh` into an isolated virtual environment.

## Troubleshooting

### "Database not found"
Run the ingest script first to build the index.

### No results
- Try broader query terms
- Remove the `--area` filter
- Increase `-k` (default is 10)
- Check `stats` to confirm files are indexed

### Slow first query
ChromaDB loads the embedding model on first use (~10-20 seconds). Subsequent queries are fast.

### Stale results
Re-run ingestion to pick up new or changed files. Incremental mode only processes changes, so it's fast.

## Uninstall

```bash
# Remove skill
rm -rf ~/.claude/skills/vector-search

# Remove vector database (in your markdown repo)
rm -rf /path/to/your/markdown-repo/.vectordb
```
