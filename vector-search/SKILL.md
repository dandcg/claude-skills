---
name: vector-search
description: Semantic search and summarisation across a markdown corpus. Use when needing to find information across many files, build timelines, aggregate knowledge, or answer questions about content. Trigger on phrases like "search brain", "find in my notes", "what do I know about", "summarise", "timeline of", "aggregate".
---

# Vector Search & Summarisation

Semantic search across a directory of markdown files using ChromaDB vector embeddings. Retrieves relevant chunks without loading entire files into context. Designed for use with a "second brain" or personal knowledge base, but works with any collection of markdown files.

## Prerequisites

- Python virtual environment set up (run setup.sh if not done)
- Index built (run ingest if no `.vectordb/` directory exists)

### First-Time Setup

```bash
# Set up Python environment (one-time)
~/.claude/skills/vector-search/setup.sh

# Build the index (run from brain repo root)
~/.claude/skills/vector-search/.venv/bin/python ~/.claude/skills/vector-search/ingest.py /path/to/your/markdown-repo --verbose
```

### Rebuild Index (after adding/changing files)

```bash
# Incremental update (only changed files)
~/.claude/skills/vector-search/.venv/bin/python ~/.claude/skills/vector-search/ingest.py /path/to/your/markdown-repo

# Full rebuild
~/.claude/skills/vector-search/.venv/bin/python ~/.claude/skills/vector-search/ingest.py /path/to/your/markdown-repo --force --verbose
```

## Search Operations

### Semantic Search (most common)

Find content semantically related to a query:

```bash
# Basic search (returns top 10 chunks)
~/.claude/skills/vector-search/.venv/bin/python ~/.claude/skills/vector-search/query.py --db-path /path/to/your/markdown-repo/.vectordb search "query text here"

# More results
~/.claude/skills/vector-search/.venv/bin/python ~/.claude/skills/vector-search/query.py --db-path /path/to/your/markdown-repo/.vectordb search "query text here" -k 20

# Filter by area
~/.claude/skills/vector-search/.venv/bin/python ~/.claude/skills/vector-search/query.py --db-path /path/to/your/markdown-repo/.vectordb search "query text" --area finance

# JSON output (for programmatic use)
~/.claude/skills/vector-search/.venv/bin/python ~/.claude/skills/vector-search/query.py --db-path /path/to/your/markdown-repo/.vectordb -f json search "query text" -k 5
```

### Browse by Area

Retrieve all chunks for an area (useful for summarisation):

```bash
~/.claude/skills/vector-search/.venv/bin/python ~/.claude/skills/vector-search/query.py --db-path /path/to/your/markdown-repo/.vectordb area finance
~/.claude/skills/vector-search/.venv/bin/python ~/.claude/skills/vector-search/query.py --db-path /path/to/your/markdown-repo/.vectordb area health -k 100
```

### Browse by File

Get all chunks for a specific file:

```bash
~/.claude/skills/vector-search/.venv/bin/python ~/.claude/skills/vector-search/query.py --db-path /path/to/your/markdown-repo/.vectordb file "areas/finance/index.md"
```

### Date Range Query

Retrieve chunks within a date range (for timelines):

```bash
~/.claude/skills/vector-search/.venv/bin/python ~/.claude/skills/vector-search/query.py --db-path /path/to/your/markdown-repo/.vectordb date-range 2025-01-01 2025-12-31
```

### Database Info

```bash
# Statistics
~/.claude/skills/vector-search/.venv/bin/python ~/.claude/skills/vector-search/query.py --db-path /path/to/your/markdown-repo/.vectordb stats

# List all indexed files
~/.claude/skills/vector-search/.venv/bin/python ~/.claude/skills/vector-search/query.py --db-path /path/to/your/markdown-repo/.vectordb list
```

## Summarisation Workflow

For large aggregation tasks (timelines, domain summaries, cross-cutting analysis):

1. **Retrieve** relevant chunks using search or area/date-range queries with JSON output
2. **Batch** chunks into manageable groups (by file, date, or topic)
3. **Summarise** each batch using Claude
4. **Synthesise** batch summaries into final output

Example workflow for "summarise my financial position":
```bash
# Step 1: Get all finance chunks as JSON
~/.claude/skills/vector-search/.venv/bin/python ~/.claude/skills/vector-search/query.py --db-path /path/to/your/markdown-repo/.vectordb -f json area finance -k 100

# Step 2: Read the JSON output and synthesise with Claude
# (Claude does this step naturally after reading the chunks)
```

## Available Areas

The brain is organised into these areas:
- `areas` → business, technical, health, relationships, finance, philosophy, mental, career, income
- `projects` → Active initiatives
- `decisions` → Decision logs
- `resources` → Reference material
- `reviews` → Daily/weekly/monthly reflections
- `outputs` → Finished content
- `docs` → Plans and design documents

## Error Handling

- **"Database not found"**: Run the ingest script first
- **"No results"**: Try broader query terms, remove area filter, or increase -k
- **Stale results**: Re-run ingest to pick up file changes (incremental, fast)
- **Slow first query**: ChromaDB loads the embedding model on first use (~10-20s), subsequent queries are fast
