# Repo-Search: Testing & Improvements Design

**Date:** 2026-02-15
**Status:** Approved
**Approach:** Test-first with incremental, measured improvements

## Context

The repo-search skill provides semantic search over a mixed document corpus (markdown, PDF, DOCX, XLSX) using ChromaDB with sentence-transformer embeddings. It works but has no tests, uses basic character-count chunking, relies on ChromaDB's implicit default embedding model, and only supports vector similarity search.

This design covers two workstreams: a comprehensive test suite, and incremental improvements to chunking, embeddings, search, and performance — each validated by the test suite.

---

## 1. Test Suite

**Location:** `repo-search/tests/` using `pytest`

### Unit Tests

**`tests/test_chunking.py`**
- Markdown chunks respect heading boundaries
- Code blocks are never split mid-block
- Overlap works correctly between chunks
- Minimum length filtering (< 50 chars) applies
- Per-format chunk sizes are respected

**`tests/test_extraction.py`**
- Each format (md, pdf, docx, xlsx) extracts text correctly
- Programmatically-generated fixtures — no binary files committed
- Edge cases: empty files, unicode content, large files

**`tests/test_metadata.py`**
- Area/sub_area parsing from directory paths
- Date extraction from markdown frontmatter and filenames
- Title extraction from headings and fallback to filename
- Edge cases: missing fields, deeply nested dirs, non-markdown files

**`tests/test_hashing.py`**
- Unchanged files are skipped on re-ingestion
- Changed files are re-processed
- Deleted files are detected (for future prune command)
- Hash cache load/save round-trips correctly

### Integration Tests

**`tests/test_ingest_query.py`**
- Full round-trip: create fixtures, ingest, query, verify results
- Known document is top result for its obvious query
- Area/sub_area filtering returns correct subsets
- Date-range queries with known dates return expected results
- Re-ingestion after file modification updates correctly
- Deleted chunks are cleaned up on re-ingest

### Fixtures

**`tests/conftest.py`**
- Generates a temp directory tree mimicking the brain structure
- Creates markdown files with frontmatter, headings, varied content
- Generates PDF fixtures using `pypdf` (or `reportlab` if needed)
- Generates DOCX fixtures using `python-docx`
- Generates XLSX fixtures using `openpyxl`
- All fixtures created in `tmp_path` — nothing committed to repo

### Quality Benchmarks

**`tests/test_search_quality.py`**
- Synthetic corpus with known ground-truth query-document pairs
- Measures Mean Reciprocal Rank (MRR) across the test corpus
- Not a hard pass/fail gate, but logs scores for regression detection
- Baseline measured before improvements, tracked after each change

---

## 2. Chunking Improvements

### Problem

Current chunking is character-count-based (1000 chars, 200 overlap) with `MarkdownTextSplitter` for .md and `RecursiveCharacterTextSplitter` for everything else. This regularly splits mid-sentence, mid-paragraph, and ignores document structure in non-markdown formats.

### Semantic-Aware Chunking

**Markdown:**
- Split on `##` and `###` headings first, keeping each section as a candidate chunk
- If a section exceeds chunk_size, split on paragraph boundaries (`\n\n`)
- Fall back to character splitting only for truly massive paragraphs
- Prepend heading chain to each chunk (e.g. `## Finance > ### Q4 Revenue`)

**PDF:**
- Split on page boundaries first, then paragraph boundaries within pages
- Detect and merge sentences split across page breaks
- Detect repeating headers/footers and strip them

**DOCX:**
- Use heading styles (Heading 1, Heading 2) as natural split points
- Never split mid-paragraph
- Prepend heading context like markdown

**XLSX:**
- Chunk by row groups (~50 rows per chunk)
- Preserve column headers in every chunk for self-contained context
- Include sheet name in each chunk

### Per-Format Default Chunk Sizes

| Format | Default Size | Rationale |
|--------|-------------|-----------|
| Markdown | 1500 chars | Sections are naturally larger |
| PDF | 1000 chars | Denser text |
| DOCX | 1500 chars | Similar structure to markdown |
| XLSX | 2000 chars | Tabular data needs more context |

Still overridable via `--chunk-size` CLI flag.

---

## 3. Embedding & Search Improvements

### Explicit & Configurable Embedding Model

- Default to `all-MiniLM-L6-v2` explicitly rather than relying on ChromaDB's implicit default
- Add `--model` flag to both `ingest.py` and `query.py`
- Recommended upgrade: `sentence-transformers/all-mpnet-base-v2` (better quality, ~2x slower)
- Store model name in collection metadata so query auto-detects the ingest-time model (prevents embedding mismatch)

### Hybrid Search (Vector + BM25)

- Add BM25 keyword scoring alongside vector similarity
- Build BM25 index during ingestion, stored as pickle alongside vectordb
- Combine scores via Reciprocal Rank Fusion: `score = 1/(k + vector_rank) + 1/(k + bm25_rank)` where `k=60`
- New `--mode` flag: `semantic` (default), `keyword`, `hybrid`

### Chunk Context Enrichment

- Prepend document title + heading chain to each chunk before embedding
- For each document, generate a one-line summary (first ~200 chars or title) prepended to every chunk from that document

### Lightweight Reranking

- Deduplicate: multiple chunks from same file → keep best, boost file score
- Metadata boost: if query matches chunk area or title, boost score
- No external API calls or cross-encoder models

---

## 4. Scale & Performance

### Ingestion

- **Parallel text extraction:** `concurrent.futures.ThreadPoolExecutor` for I/O-bound file reading
- **Batch embedding:** Accumulate chunks, batch-add in groups of 100-500 to ChromaDB
- **Progress reporting:** File X of Y, chunks processed count

### Query

- **Warmup command:** Pre-load embedding model to avoid first-query lag
- **Result caching:** Cache last N query results in memory within a session

### Collection Management

- **Named collections:** `--collection` flag instead of hardcoded `"brain"`
- **Prune command:** Remove chunks for files that no longer exist on disk
- **Verbose stats:** Embedding dimensions, HNSW parameters, memory usage

---

## 5. Dependencies

New additions to `requirements.txt`:
- `pytest` (test runner)
- `rank-bm25` (BM25 scoring for hybrid search)
- `reportlab` (PDF fixture generation in tests, test-only)

No new heavyweight dependencies. All improvements use existing libraries or lightweight additions.

---

## 6. Implementation Order

1. **Test suite** — unit tests, integration tests, fixtures, quality benchmarks
2. **Chunking improvements** — semantic-aware splitting, per-format strategies
3. **Embedding improvements** — explicit model config, context enrichment
4. **Hybrid search** — BM25 index, RRF scoring, mode flag
5. **Performance** — parallel ingestion, batch embedding, collection management

Each step is validated against the test suite before proceeding.
