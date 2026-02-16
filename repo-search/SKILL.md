---
name: repo-search
description: Semantic search and summarisation across a document corpus (markdown, PDF, DOCX, XLSX). Use when needing to find information across many files, build timelines, aggregate knowledge, or answer questions about content. Trigger on phrases like "search brain", "find in my notes", "what do I know about", "summarise", "timeline of", "aggregate".
---

# Repo Search & Summarisation

Semantic search across a directory of documents using ChromaDB vector embeddings. Supports **markdown**, **PDF**, **DOCX**, and **XLSX** files. Retrieves relevant chunks without loading entire files into context. Designed for use with a "second brain" or personal knowledge base, but works with any collection of documents.

## Prerequisites

- Python virtual environment set up (run setup.sh if not done)
- Index built (run ingest if no `.vectordb/` directory exists)

### First-Time Setup

```bash
# Set up Python environment (one-time)
~/.claude/skills/repo-search/setup.sh

# Build the index (run from brain repo root)
~/.claude/skills/repo-search/.venv/bin/python ~/.claude/skills/repo-search/ingest.py /path/to/your/markdown-repo --verbose
```

### Rebuild Index (after adding/changing files)

```bash
# Incremental update (only changed files)
~/.claude/skills/repo-search/.venv/bin/python ~/.claude/skills/repo-search/ingest.py /path/to/your/markdown-repo

# Full rebuild
~/.claude/skills/repo-search/.venv/bin/python ~/.claude/skills/repo-search/ingest.py /path/to/your/markdown-repo --force --verbose
```

## Search Operations

### Semantic Search (default)

Find content semantically related to a query:

```bash
# Basic search (returns top 10 chunks)
~/.claude/skills/repo-search/.venv/bin/python ~/.claude/skills/repo-search/query.py --db-path /path/to/your/markdown-repo/.vectordb search "query text here"

# More results
~/.claude/skills/repo-search/.venv/bin/python ~/.claude/skills/repo-search/query.py --db-path /path/to/your/markdown-repo/.vectordb search "query text here" -k 20

# Filter by area
~/.claude/skills/repo-search/.venv/bin/python ~/.claude/skills/repo-search/query.py --db-path /path/to/your/markdown-repo/.vectordb search "query text" --area finance

# JSON output (for programmatic use)
~/.claude/skills/repo-search/.venv/bin/python ~/.claude/skills/repo-search/query.py --db-path /path/to/your/markdown-repo/.vectordb -f json search "query text" -k 5
```

### Hybrid Search (vector + keyword)

Combines semantic similarity with BM25 keyword matching for better precision, especially with exact terms, names, or acronyms:

```bash
# Hybrid search (recommended for most queries)
~/.claude/skills/repo-search/.venv/bin/python ~/.claude/skills/repo-search/query.py --db-path /path/to/your/markdown-repo/.vectordb search "query text" --mode hybrid

# Keyword-only search (BM25)
~/.claude/skills/repo-search/.venv/bin/python ~/.claude/skills/repo-search/query.py --db-path /path/to/your/markdown-repo/.vectordb search "exact phrase" --mode keyword
```

Search modes: `semantic` (default), `hybrid` (vector + BM25 via Reciprocal Rank Fusion), `keyword` (BM25 only).

### Browse by Area

Retrieve all chunks for an area (useful for summarisation):

```bash
~/.claude/skills/repo-search/.venv/bin/python ~/.claude/skills/repo-search/query.py --db-path /path/to/your/markdown-repo/.vectordb area finance
~/.claude/skills/repo-search/.venv/bin/python ~/.claude/skills/repo-search/query.py --db-path /path/to/your/markdown-repo/.vectordb area health -k 100
```

### Browse by File

Get all chunks for a specific file:

```bash
~/.claude/skills/repo-search/.venv/bin/python ~/.claude/skills/repo-search/query.py --db-path /path/to/your/markdown-repo/.vectordb file "areas/finance/index.md"
```

### Date Range Query

Retrieve chunks within a date range (for timelines):

```bash
~/.claude/skills/repo-search/.venv/bin/python ~/.claude/skills/repo-search/query.py --db-path /path/to/your/markdown-repo/.vectordb date-range 2025-01-01 2025-12-31
```

### Database Info

```bash
# Statistics
~/.claude/skills/repo-search/.venv/bin/python ~/.claude/skills/repo-search/query.py --db-path /path/to/your/markdown-repo/.vectordb stats

# List all indexed files
~/.claude/skills/repo-search/.venv/bin/python ~/.claude/skills/repo-search/query.py --db-path /path/to/your/markdown-repo/.vectordb list

# Prune orphaned chunks (for files deleted from disk)
~/.claude/skills/repo-search/.venv/bin/python ~/.claude/skills/repo-search/query.py --db-path /path/to/your/markdown-repo/.vectordb prune /path/to/your/markdown-repo
```

### Named Collections

Use `--collection` to manage separate indexes for different corpora:

```bash
# Ingest into a named collection
~/.claude/skills/repo-search/.venv/bin/python ~/.claude/skills/repo-search/ingest.py /path/to/work-docs --collection work

# Search a named collection
~/.claude/skills/repo-search/.venv/bin/python ~/.claude/skills/repo-search/query.py --db-path /path/to/work-docs/.vectordb --collection work search "query"
```

Default collection name is `brain`.

## Summarisation Workflow

For large aggregation tasks (timelines, domain summaries, cross-cutting analysis):

1. **Retrieve** relevant chunks using search or area/date-range queries with JSON output
2. **Batch** chunks into manageable groups (by file, date, or topic)
3. **Summarise** each batch using Claude
4. **Synthesise** batch summaries into final output

Example workflow for "summarise my financial position":
```bash
# Step 1: Get all finance chunks as JSON
~/.claude/skills/repo-search/.venv/bin/python ~/.claude/skills/repo-search/query.py --db-path /path/to/your/markdown-repo/.vectordb -f json area finance -k 100

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

## Chunking & Embedding Details

- **Markdown:** Heading-aware chunking (respects `#`, `##`, `###` boundaries). Each chunk is enriched with its heading chain (e.g. `[Title > Section > Subsection]`) and document title for better embedding context.
- **PDF:** Page-aware chunking at 1000 chars default.
- **DOCX:** Paragraph-aware chunking at 1500 chars default.
- **XLSX:** Row-group chunking at 2000 chars default with sheet names preserved.
- **Embedding model:** `all-MiniLM-L6-v2` (ChromaDB default). Model name is stored in collection metadata.
- **BM25 index:** Built automatically during ingestion for hybrid search support.

## Error Handling

- **"Database not found"**: Run the ingest script first
- **"No results"**: Try broader query terms, remove area filter, increase -k, or try `--mode hybrid`
- **Stale results**: Re-run ingest to pick up file changes (incremental, fast)
- **Orphaned chunks**: Use `prune` command to remove chunks for deleted files
- **Slow first query**: ChromaDB loads the embedding model on first use (~10-20s), subsequent queries are fast
- **"Failed to extract"**: The file may be corrupted or password-protected; check stderr for details
