# Repo-Search Testing & Improvements Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a comprehensive test suite to repo-search, then incrementally improve chunking, embeddings, search quality, and performance — each validated by tests.

**Architecture:** Test-first approach. Build fixtures programmatically (no committed binaries). Refactor ingest.py and query.py in-place, keeping the same CLI interface. New capabilities (hybrid search, named collections, prune) are additive.

**Tech Stack:** Python 3, pytest, ChromaDB, langchain-text-splitters, rank-bm25, pypdf, python-docx, openpyxl, reportlab (test fixtures only)

---

## Phase 1: Test Infrastructure & Unit Tests

### Task 1: Test dependencies and pytest setup

**Files:**
- Modify: `repo-search/requirements.txt`
- Create: `repo-search/requirements-dev.txt`
- Create: `repo-search/pytest.ini`

**Step 1: Create dev requirements file**

Create `repo-search/requirements-dev.txt`:
```
-r requirements.txt
pytest>=8.0,<9.0
reportlab>=4.0,<5.0
```

**Step 2: Create pytest.ini**

Create `repo-search/pytest.ini`:
```ini
[pytest]
testpaths = tests
pythonpath = .
```

**Step 3: Install dev dependencies**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/pip install -r requirements-dev.txt -q`
Expected: Clean install, no errors

**Step 4: Verify pytest runs**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest --co -q`
Expected: "no tests ran" (empty test dir)

**Step 5: Commit**

```bash
cd /home/dan/source/claude-skills
git add repo-search/requirements-dev.txt repo-search/pytest.ini
git commit -m "test: add pytest setup and dev dependencies for repo-search"
```

---

### Task 2: Test fixtures via conftest.py

**Files:**
- Create: `repo-search/tests/__init__.py`
- Create: `repo-search/tests/conftest.py`

**Step 1: Create test package and conftest**

Create `repo-search/tests/__init__.py` (empty).

Create `repo-search/tests/conftest.py` with fixtures that programmatically generate test documents in a temp directory tree:

```python
import pytest
from pathlib import Path


@pytest.fixture
def repo_root(tmp_path):
    """Create a mock repo with known documents across all supported formats."""
    # Markdown files
    finance_dir = tmp_path / "finance" / "reports"
    finance_dir.mkdir(parents=True)

    (finance_dir / "2025-01-15-q4-revenue.md").write_text(
        "# Q4 Revenue Report\n\n"
        "**Date:** 2025-01-15\n"
        "**Status:** Final\n\n"
        "## Summary\n\n"
        "Total revenue for Q4 was $2.3M, up 15% from Q3.\n\n"
        "## Regional Breakdown\n\n"
        "North America contributed 60% of total revenue.\n"
        "Europe contributed 25% with strong growth in Germany.\n"
        "Asia Pacific made up the remaining 15%.\n\n"
        "## Outlook\n\n"
        "Q1 projections suggest continued growth driven by new product launches.\n"
    )

    health_dir = tmp_path / "health"
    health_dir.mkdir(parents=True)

    (health_dir / "exercise-routine.md").write_text(
        "# Exercise Routine\n\n"
        "**Added:** 2025-03-01\n\n"
        "## Morning Workout\n\n"
        "Start with 20 minutes of cardio, followed by strength training.\n"
        "Focus on compound movements: squats, deadlifts, bench press.\n\n"
        "## Evening Stretching\n\n"
        "15 minutes of yoga-style stretching to improve flexibility.\n"
    )

    # A long markdown file that will produce multiple chunks
    tech_dir = tmp_path / "technical" / "guides"
    tech_dir.mkdir(parents=True)

    long_content = "# Python Best Practices Guide\n\n**Date:** 2025-06-01\n\n"
    for i in range(20):
        long_content += f"## Section {i+1}: Topic {i+1}\n\n"
        long_content += f"This is detailed content about topic {i+1}. " * 20 + "\n\n"
    (tech_dir / "python-best-practices.md").write_text(long_content)

    return tmp_path


@pytest.fixture
def repo_with_pdf(repo_root):
    """Extend repo_root with a PDF fixture."""
    from reportlab.lib.pagesizes import letter
    from reportlab.pdfgen import canvas

    pdf_dir = repo_root / "finance" / "invoices"
    pdf_dir.mkdir(parents=True, exist_ok=True)
    pdf_path = pdf_dir / "2025-02-invoice.pdf"

    c = canvas.Canvas(str(pdf_path), pagesize=letter)
    c.drawString(72, 720, "Invoice #12345")
    c.drawString(72, 700, "Date: 2025-02-15")
    c.drawString(72, 680, "Amount: $5,000.00")
    c.drawString(72, 660, "Client: Acme Corporation")
    c.drawString(72, 640, "Description: Consulting services for Q1 2025")
    c.showPage()
    c.save()

    return repo_root


@pytest.fixture
def repo_with_docx(repo_root):
    """Extend repo_root with a DOCX fixture."""
    from docx import Document

    docx_dir = repo_root / "technical" / "specs"
    docx_dir.mkdir(parents=True, exist_ok=True)
    docx_path = docx_dir / "api-specification.docx"

    doc = Document()
    doc.add_heading("API Specification", level=1)
    doc.add_paragraph("This document describes the REST API endpoints.")
    doc.add_heading("Authentication", level=2)
    doc.add_paragraph("All requests require a Bearer token in the Authorization header.")
    doc.add_heading("Endpoints", level=2)
    doc.add_paragraph("GET /users - Returns a list of all users.")
    doc.add_paragraph("POST /users - Creates a new user account.")
    doc.save(str(docx_path))

    return repo_root


@pytest.fixture
def repo_with_xlsx(repo_root):
    """Extend repo_root with an XLSX fixture."""
    from openpyxl import Workbook

    xlsx_dir = repo_root / "finance" / "data"
    xlsx_dir.mkdir(parents=True, exist_ok=True)
    xlsx_path = xlsx_dir / "budget-2025.xlsx"

    wb = Workbook()
    ws = wb.active
    ws.title = "Q1 Budget"
    ws.append(["Category", "Amount", "Notes"])
    ws.append(["Marketing", 50000, "Digital campaigns"])
    ws.append(["Engineering", 120000, "New hires"])
    ws.append(["Operations", 30000, "Office supplies"])

    ws2 = wb.create_sheet("Q2 Budget")
    ws2.append(["Category", "Amount", "Notes"])
    ws2.append(["Marketing", 55000, "Conference sponsorship"])
    ws2.append(["Engineering", 130000, "Infrastructure"])
    wb.save(str(xlsx_path))

    return repo_root


@pytest.fixture
def repo_all_formats(repo_with_pdf, repo_with_docx, repo_with_xlsx):
    """Repo with all supported file formats. All fixtures share the same tmp_path."""
    # repo_with_pdf, repo_with_docx, repo_with_xlsx all extend the same repo_root
    # but pytest creates separate tmp_paths. We need a combined fixture.
    # Since they all use repo_root which uses tmp_path, we need a different approach.
    return repo_with_pdf  # This won't work - need to combine in one fixture


@pytest.fixture
def repo_all_formats(tmp_path):
    """Repo with all supported file formats, built in a single tmp_path."""
    from reportlab.lib.pagesizes import letter
    from reportlab.pdfgen import canvas
    from docx import Document as DocxDocument
    from openpyxl import Workbook

    # --- Markdown files ---
    finance_dir = tmp_path / "finance" / "reports"
    finance_dir.mkdir(parents=True)

    (finance_dir / "2025-01-15-q4-revenue.md").write_text(
        "# Q4 Revenue Report\n\n"
        "**Date:** 2025-01-15\n"
        "**Status:** Final\n\n"
        "## Summary\n\n"
        "Total revenue for Q4 was $2.3M, up 15% from Q3.\n\n"
        "## Regional Breakdown\n\n"
        "North America contributed 60% of total revenue.\n"
        "Europe contributed 25% with strong growth in Germany.\n"
        "Asia Pacific made up the remaining 15%.\n\n"
        "## Outlook\n\n"
        "Q1 projections suggest continued growth driven by new product launches.\n"
    )

    health_dir = tmp_path / "health"
    health_dir.mkdir(parents=True)

    (health_dir / "exercise-routine.md").write_text(
        "# Exercise Routine\n\n"
        "**Added:** 2025-03-01\n\n"
        "## Morning Workout\n\n"
        "Start with 20 minutes of cardio, followed by strength training.\n"
        "Focus on compound movements: squats, deadlifts, bench press.\n\n"
        "## Evening Stretching\n\n"
        "15 minutes of yoga-style stretching to improve flexibility.\n"
    )

    tech_dir = tmp_path / "technical" / "guides"
    tech_dir.mkdir(parents=True)

    long_content = "# Python Best Practices Guide\n\n**Date:** 2025-06-01\n\n"
    for i in range(20):
        long_content += f"## Section {i+1}: Topic {i+1}\n\n"
        long_content += f"This is detailed content about topic {i+1}. " * 20 + "\n\n"
    (tech_dir / "python-best-practices.md").write_text(long_content)

    # --- PDF ---
    pdf_dir = tmp_path / "finance" / "invoices"
    pdf_dir.mkdir(parents=True, exist_ok=True)
    pdf_path = pdf_dir / "2025-02-invoice.pdf"

    c = canvas.Canvas(str(pdf_path), pagesize=letter)
    c.drawString(72, 720, "Invoice #12345")
    c.drawString(72, 700, "Date: 2025-02-15")
    c.drawString(72, 680, "Amount: $5,000.00")
    c.drawString(72, 640, "Description: Consulting services for Q1 2025")
    c.showPage()
    c.save()

    # --- DOCX ---
    docx_dir = tmp_path / "technical" / "specs"
    docx_dir.mkdir(parents=True, exist_ok=True)
    docx_path = docx_dir / "api-specification.docx"

    doc = DocxDocument()
    doc.add_heading("API Specification", level=1)
    doc.add_paragraph("This document describes the REST API endpoints.")
    doc.add_heading("Authentication", level=2)
    doc.add_paragraph("All requests require a Bearer token in the Authorization header.")
    doc.add_heading("Endpoints", level=2)
    doc.add_paragraph("GET /users - Returns a list of all users.")
    doc.add_paragraph("POST /users - Creates a new user account.")
    doc.save(str(docx_path))

    # --- XLSX ---
    xlsx_dir = tmp_path / "finance" / "data"
    xlsx_dir.mkdir(parents=True, exist_ok=True)
    xlsx_path = xlsx_dir / "budget-2025.xlsx"

    wb = Workbook()
    ws = wb.active
    ws.title = "Q1 Budget"
    ws.append(["Category", "Amount", "Notes"])
    ws.append(["Marketing", 50000, "Digital campaigns"])
    ws.append(["Engineering", 120000, "New hires"])
    ws.append(["Operations", 30000, "Office supplies"])

    ws2 = wb.create_sheet("Q2 Budget")
    ws2.append(["Category", "Amount", "Notes"])
    ws2.append(["Marketing", 55000, "Conference sponsorship"])
    ws2.append(["Engineering", 130000, "Infrastructure"])
    wb.save(str(xlsx_path))

    return tmp_path


@pytest.fixture
def db_path(tmp_path):
    """Temporary database path for ChromaDB."""
    return tmp_path / ".vectordb"


@pytest.fixture
def ingested_db(repo_all_formats, tmp_path):
    """A fully ingested database from the all-formats repo, ready for querying."""
    from ingest import ingest

    db_path = tmp_path / "test-vectordb"
    ingest(
        repo_root=repo_all_formats,
        db_path=db_path,
        force=True,
        verbose=False,
    )
    return db_path
```

**Step 2: Verify fixtures load**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest --co -q`
Expected: "no tests ran" but no import errors

**Step 3: Commit**

```bash
cd /home/dan/source/claude-skills
git add repo-search/tests/
git commit -m "test: add conftest with programmatic fixtures for all doc formats"
```

---

### Task 3: Unit tests for text extraction

**Files:**
- Create: `repo-search/tests/test_extraction.py`

**Step 1: Write the failing tests**

Create `repo-search/tests/test_extraction.py`:

```python
"""Tests for text extraction across all supported formats."""
from pathlib import Path
from ingest import extract_text, find_files, SUPPORTED_EXTENSIONS


class TestFindFiles:
    def test_finds_markdown_files(self, repo_all_formats):
        files = find_files(repo_all_formats)
        md_files = [f for f in files if f.suffix == ".md"]
        assert len(md_files) == 3

    def test_finds_pdf_files(self, repo_all_formats):
        files = find_files(repo_all_formats)
        pdf_files = [f for f in files if f.suffix == ".pdf"]
        assert len(pdf_files) == 1

    def test_finds_docx_files(self, repo_all_formats):
        files = find_files(repo_all_formats)
        docx_files = [f for f in files if f.suffix == ".docx"]
        assert len(docx_files) == 1

    def test_finds_xlsx_files(self, repo_all_formats):
        files = find_files(repo_all_formats)
        xlsx_files = [f for f in files if f.suffix == ".xlsx"]
        assert len(xlsx_files) == 1

    def test_skips_readme(self, repo_all_formats):
        (repo_all_formats / "README.md").write_text("# Readme")
        files = find_files(repo_all_formats)
        names = [f.name for f in files]
        assert "README.md" not in names

    def test_skips_git_dir(self, repo_all_formats):
        git_dir = repo_all_formats / ".git"
        git_dir.mkdir()
        (git_dir / "notes.md").write_text("# git notes")
        files = find_files(repo_all_formats)
        paths = [str(f) for f in files]
        assert not any(".git" in p for p in paths)

    def test_returns_sorted(self, repo_all_formats):
        files = find_files(repo_all_formats)
        assert files == sorted(files)


class TestExtractText:
    def test_markdown_extraction(self, repo_all_formats):
        md_file = repo_all_formats / "health" / "exercise-routine.md"
        text = extract_text(md_file)
        assert "Morning Workout" in text
        assert "cardio" in text

    def test_pdf_extraction(self, repo_all_formats):
        pdf_file = repo_all_formats / "finance" / "invoices" / "2025-02-invoice.pdf"
        text = extract_text(pdf_file)
        assert "Invoice" in text
        assert "5,000" in text

    def test_docx_extraction(self, repo_all_formats):
        docx_file = repo_all_formats / "technical" / "specs" / "api-specification.docx"
        text = extract_text(docx_file)
        assert "API Specification" in text
        assert "Bearer token" in text

    def test_xlsx_extraction(self, repo_all_formats):
        xlsx_file = repo_all_formats / "finance" / "data" / "budget-2025.xlsx"
        text = extract_text(xlsx_file)
        assert "Marketing" in text
        assert "50000" in text
        assert "Q1 Budget" in text

    def test_empty_markdown(self, tmp_path):
        empty = tmp_path / "empty.md"
        empty.write_text("")
        text = extract_text(empty)
        assert text == ""

    def test_unicode_markdown(self, tmp_path):
        f = tmp_path / "unicode.md"
        f.write_text("# Sprachführer\n\nGrüße und Umlaute: äöü ß")
        text = extract_text(f)
        assert "Grüße" in text
        assert "äöü" in text

    def test_unsupported_extension(self, tmp_path):
        f = tmp_path / "data.csv"
        f.write_text("a,b,c")
        import pytest as pt
        with pt.raises(ValueError, match="Unsupported"):
            extract_text(f)
```

**Step 2: Run tests**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest tests/test_extraction.py -v`
Expected: All PASS

**Step 3: Commit**

```bash
cd /home/dan/source/claude-skills
git add repo-search/tests/test_extraction.py
git commit -m "test: add text extraction tests for all doc formats"
```

---

### Task 4: Unit tests for metadata extraction

**Files:**
- Create: `repo-search/tests/test_metadata.py`

**Step 1: Write the tests**

Create `repo-search/tests/test_metadata.py`:

```python
"""Tests for metadata extraction from files and paths."""
from pathlib import Path
from ingest import extract_metadata


class TestAreaParsing:
    def test_top_level_area(self, repo_all_formats):
        content = "# Test"
        f = repo_all_formats / "health" / "exercise-routine.md"
        meta = extract_metadata(f, repo_all_formats, content)
        assert meta["area"] == "health"

    def test_sub_area(self, repo_all_formats):
        f = repo_all_formats / "finance" / "reports" / "2025-01-15-q4-revenue.md"
        content = (f).read_text()
        meta = extract_metadata(f, repo_all_formats, content)
        assert meta["area"] == "finance"
        assert meta["sub_area"] == "reports"

    def test_no_sub_area(self, repo_all_formats):
        f = repo_all_formats / "health" / "exercise-routine.md"
        content = f.read_text()
        meta = extract_metadata(f, repo_all_formats, content)
        assert meta["sub_area"] == ""


class TestTitleExtraction:
    def test_title_from_heading(self, repo_all_formats):
        f = repo_all_formats / "health" / "exercise-routine.md"
        content = f.read_text()
        meta = extract_metadata(f, repo_all_formats, content)
        assert meta["title"] == "Exercise Routine"

    def test_title_fallback_to_stem(self, repo_all_formats):
        f = repo_all_formats / "finance" / "data" / "budget-2025.xlsx"
        meta = extract_metadata(f, repo_all_formats, "spreadsheet content")
        assert meta["title"] == "budget-2025"


class TestDateExtraction:
    def test_date_from_frontmatter(self, repo_all_formats):
        f = repo_all_formats / "finance" / "reports" / "2025-01-15-q4-revenue.md"
        content = f.read_text()
        meta = extract_metadata(f, repo_all_formats, content)
        assert meta["date"] == "2025-01-15"

    def test_date_from_added_field(self, repo_all_formats):
        f = repo_all_formats / "health" / "exercise-routine.md"
        content = f.read_text()
        meta = extract_metadata(f, repo_all_formats, content)
        assert meta["date"] == "2025-03-01"

    def test_date_from_filename(self, tmp_path):
        f = tmp_path / "area" / "2025-07-04-independence.md"
        f.parent.mkdir(parents=True)
        f.write_text("No frontmatter date here.")
        meta = extract_metadata(f, tmp_path, f.read_text())
        assert meta["date"] == "2025-07-04"

    def test_no_date(self, tmp_path):
        f = tmp_path / "area" / "notes.md"
        f.parent.mkdir(parents=True)
        f.write_text("# Just notes\n\nNo date anywhere.")
        meta = extract_metadata(f, tmp_path, f.read_text())
        assert meta["date"] == ""


class TestStatusExtraction:
    def test_status_from_content(self, repo_all_formats):
        f = repo_all_formats / "finance" / "reports" / "2025-01-15-q4-revenue.md"
        content = f.read_text()
        meta = extract_metadata(f, repo_all_formats, content)
        assert meta["status"] == "Final"

    def test_no_status(self, repo_all_formats):
        f = repo_all_formats / "health" / "exercise-routine.md"
        content = f.read_text()
        meta = extract_metadata(f, repo_all_formats, content)
        assert meta["status"] == ""


class TestFileMetadata:
    def test_file_type(self, repo_all_formats):
        f = repo_all_formats / "health" / "exercise-routine.md"
        meta = extract_metadata(f, repo_all_formats, "")
        assert meta["file_type"] == "md"

    def test_file_size(self, repo_all_formats):
        f = repo_all_formats / "health" / "exercise-routine.md"
        meta = extract_metadata(f, repo_all_formats, "")
        assert meta["file_size"] > 0

    def test_relative_file_path(self, repo_all_formats):
        f = repo_all_formats / "finance" / "reports" / "2025-01-15-q4-revenue.md"
        meta = extract_metadata(f, repo_all_formats, "")
        assert meta["file_path"] == "finance/reports/2025-01-15-q4-revenue.md"
```

**Step 2: Run tests**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest tests/test_metadata.py -v`
Expected: All PASS

**Step 3: Commit**

```bash
cd /home/dan/source/claude-skills
git add repo-search/tests/test_metadata.py
git commit -m "test: add metadata extraction tests for paths, dates, titles"
```

---

### Task 5: Unit tests for chunking

**Files:**
- Create: `repo-search/tests/test_chunking.py`

**Step 1: Write the tests**

Create `repo-search/tests/test_chunking.py`:

```python
"""Tests for text chunking logic."""
from pathlib import Path
from ingest import chunk_text, DEFAULT_CHUNK_SIZE, DEFAULT_CHUNK_OVERLAP


class TestMarkdownChunking:
    def test_short_file_single_chunk(self, tmp_path):
        """A short markdown file should produce a single chunk."""
        f = tmp_path / "short.md"
        content = "# Title\n\nA short paragraph that fits in one chunk."
        chunks = chunk_text(content, f)
        assert len(chunks) == 1
        assert "short paragraph" in chunks[0]

    def test_long_file_multiple_chunks(self, repo_all_formats):
        """The long Python best practices doc should produce many chunks."""
        f = repo_all_formats / "technical" / "guides" / "python-best-practices.md"
        content = f.read_text()
        chunks = chunk_text(content, f)
        assert len(chunks) > 5

    def test_chunks_do_not_exceed_size(self, repo_all_formats):
        """No chunk should be much larger than chunk_size (some tolerance for splitter)."""
        f = repo_all_formats / "technical" / "guides" / "python-best-practices.md"
        content = f.read_text()
        chunks = chunk_text(content, f, chunk_size=500, chunk_overlap=100)
        # Allow 50% tolerance - splitters don't guarantee hard limits
        for chunk in chunks:
            assert len(chunk) < 500 * 1.5, f"Chunk too large: {len(chunk)} chars"

    def test_minimum_chunk_filter(self, tmp_path):
        """Chunks shorter than 50 chars after strip should be filtered out."""
        f = tmp_path / "tiny.md"
        # Content that will produce tiny fragments
        content = "# H\n\n" + "x " * 25 + "\n\n" + "A substantial paragraph with enough content to pass the minimum length filter easily."
        chunks = chunk_text(content, f)
        for chunk in chunks:
            assert len(chunk.strip()) >= 50

    def test_custom_chunk_size(self, repo_all_formats):
        """Smaller chunk_size should produce more chunks."""
        f = repo_all_formats / "technical" / "guides" / "python-best-practices.md"
        content = f.read_text()
        chunks_default = chunk_text(content, f, chunk_size=1000)
        chunks_small = chunk_text(content, f, chunk_size=500)
        assert len(chunks_small) > len(chunks_default)

    def test_overlap_preserves_context(self, repo_all_formats):
        """Adjacent chunks should share some overlapping content."""
        f = repo_all_formats / "technical" / "guides" / "python-best-practices.md"
        content = f.read_text()
        chunks = chunk_text(content, f, chunk_size=500, chunk_overlap=200)
        if len(chunks) >= 2:
            # Check at least some pairs overlap
            overlaps_found = 0
            for i in range(len(chunks) - 1):
                # Take last 100 chars of current chunk and check if any appear in next
                tail = chunks[i][-100:]
                if any(word in chunks[i + 1] for word in tail.split() if len(word) > 4):
                    overlaps_found += 1
            assert overlaps_found > 0, "Expected some overlapping content between adjacent chunks"


class TestNonMarkdownChunking:
    def test_pdf_text_chunking(self, repo_all_formats):
        """PDF text should be chunked with RecursiveCharacterTextSplitter."""
        f = repo_all_formats / "finance" / "invoices" / "2025-02-invoice.pdf"
        from ingest import extract_text
        content = extract_text(f)
        chunks = chunk_text(content, f)
        # PDF fixture is short, may be 1 chunk or filtered out if < 50 chars
        assert isinstance(chunks, list)

    def test_docx_text_chunking(self, repo_all_formats):
        f = repo_all_formats / "technical" / "specs" / "api-specification.docx"
        from ingest import extract_text
        content = extract_text(f)
        chunks = chunk_text(content, f)
        assert len(chunks) >= 1
        assert any("API" in c for c in chunks)

    def test_xlsx_text_chunking(self, repo_all_formats):
        f = repo_all_formats / "finance" / "data" / "budget-2025.xlsx"
        from ingest import extract_text
        content = extract_text(f)
        chunks = chunk_text(content, f)
        assert isinstance(chunks, list)
```

**Step 2: Run tests**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest tests/test_chunking.py -v`
Expected: All PASS

**Step 3: Commit**

```bash
cd /home/dan/source/claude-skills
git add repo-search/tests/test_chunking.py
git commit -m "test: add chunking unit tests for markdown and non-markdown formats"
```

---

### Task 6: Unit tests for hash caching

**Files:**
- Create: `repo-search/tests/test_hashing.py`

**Step 1: Write the tests**

Create `repo-search/tests/test_hashing.py`:

```python
"""Tests for file hash computation and cache management."""
from pathlib import Path
from ingest import compute_file_hash, load_hash_cache, save_hash_cache


class TestComputeFileHash:
    def test_same_content_same_hash(self, tmp_path):
        f1 = tmp_path / "a.md"
        f2 = tmp_path / "b.md"
        f1.write_text("identical content")
        f2.write_text("identical content")
        assert compute_file_hash(f1) == compute_file_hash(f2)

    def test_different_content_different_hash(self, tmp_path):
        f1 = tmp_path / "a.md"
        f2 = tmp_path / "b.md"
        f1.write_text("content A")
        f2.write_text("content B")
        assert compute_file_hash(f1) != compute_file_hash(f2)

    def test_hash_changes_on_modification(self, tmp_path):
        f = tmp_path / "doc.md"
        f.write_text("original")
        hash1 = compute_file_hash(f)
        f.write_text("modified")
        hash2 = compute_file_hash(f)
        assert hash1 != hash2

    def test_returns_hex_string(self, tmp_path):
        f = tmp_path / "doc.md"
        f.write_text("test")
        h = compute_file_hash(f)
        assert isinstance(h, str)
        assert len(h) == 32  # MD5 hex


class TestHashCache:
    def test_save_and_load_roundtrip(self, tmp_path):
        cache_path = tmp_path / "hashes.json"
        data = {"file1.md": "abc123", "file2.pdf": "def456"}
        save_hash_cache(cache_path, data)
        loaded = load_hash_cache(cache_path)
        assert loaded == data

    def test_load_nonexistent_returns_empty(self, tmp_path):
        cache_path = tmp_path / "nonexistent.json"
        assert load_hash_cache(cache_path) == {}

    def test_save_creates_parent_dirs(self, tmp_path):
        cache_path = tmp_path / "deep" / "nested" / "hashes.json"
        save_hash_cache(cache_path, {"a": "b"})
        assert cache_path.exists()
        assert load_hash_cache(cache_path) == {"a": "b"}
```

**Step 2: Run tests**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest tests/test_hashing.py -v`
Expected: All PASS

**Step 3: Commit**

```bash
cd /home/dan/source/claude-skills
git add repo-search/tests/test_hashing.py
git commit -m "test: add hash caching unit tests"
```

---

### Task 7: Integration tests for ingest + query round-trip

**Files:**
- Create: `repo-search/tests/test_ingest_query.py`

**Step 1: Write the tests**

Create `repo-search/tests/test_ingest_query.py`:

```python
"""Integration tests: ingest documents then query them."""
import chromadb
from ingest import ingest
from query import get_collection, cmd_search


class TestIngestPipeline:
    def test_ingest_creates_db(self, repo_all_formats, tmp_path):
        db_path = tmp_path / "testdb"
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)
        assert db_path.exists()

    def test_ingest_populates_collection(self, repo_all_formats, tmp_path):
        db_path = tmp_path / "testdb"
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)
        client = chromadb.PersistentClient(path=str(db_path))
        collection = client.get_collection("brain")
        assert collection.count() > 0

    def test_ingest_all_files_indexed(self, repo_all_formats, tmp_path):
        db_path = tmp_path / "testdb"
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)
        client = chromadb.PersistentClient(path=str(db_path))
        collection = client.get_collection("brain")
        results = collection.get(include=["metadatas"])
        file_paths = {m["file_path"] for m in results["metadatas"]}
        # Should have at least the 3 markdown files + pdf + docx + xlsx
        assert len(file_paths) >= 5

    def test_incremental_skips_unchanged(self, repo_all_formats, tmp_path):
        db_path = tmp_path / "testdb"
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)
        client = chromadb.PersistentClient(path=str(db_path))
        collection = client.get_collection("brain")
        count_after_first = collection.count()

        # Second ingest should skip everything
        ingest(repo_root=repo_all_formats, db_path=db_path)
        collection = client.get_collection("brain")
        assert collection.count() == count_after_first

    def test_incremental_reprocesses_changed(self, repo_all_formats, tmp_path):
        db_path = tmp_path / "testdb"
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)

        # Modify a file
        f = repo_all_formats / "health" / "exercise-routine.md"
        f.write_text(f.read_text() + "\n\n## New Section\n\nBrand new content added here for testing.\n")

        # Re-ingest
        ingest(repo_root=repo_all_formats, db_path=db_path)

        client = chromadb.PersistentClient(path=str(db_path))
        collection = client.get_collection("brain")
        results = collection.get(
            where={"file_path": "health/exercise-routine.md"},
            include=["documents"],
        )
        all_text = " ".join(results["documents"])
        assert "Brand new content" in all_text


class TestQueryRoundTrip:
    def test_semantic_search_returns_relevant(self, ingested_db):
        client = chromadb.PersistentClient(path=str(ingested_db))
        collection = client.get_collection("brain")
        results = collection.query(
            query_texts=["quarterly revenue financial results"],
            n_results=5,
            include=["metadatas", "distances"],
        )
        # The Q4 revenue report should be in the results
        file_paths = [m["file_path"] for m in results["metadatas"][0]]
        assert any("q4-revenue" in fp for fp in file_paths)

    def test_area_filter_restricts_results(self, ingested_db):
        client = chromadb.PersistentClient(path=str(ingested_db))
        collection = client.get_collection("brain")
        results = collection.query(
            query_texts=["report"],
            n_results=50,
            where={"area": "health"},
            include=["metadatas"],
        )
        for meta in results["metadatas"][0]:
            assert meta["area"] == "health"

    def test_file_retrieval_ordered_by_chunk(self, ingested_db):
        client = chromadb.PersistentClient(path=str(ingested_db))
        collection = client.get_collection("brain")
        results = collection.get(
            where={"file_path": "technical/guides/python-best-practices.md"},
            include=["metadatas"],
        )
        indices = [m["chunk_index"] for m in results["metadatas"]]
        assert sorted(indices) == list(range(len(indices)))

    def test_date_range_query(self, ingested_db):
        client = chromadb.PersistentClient(path=str(ingested_db))
        collection = client.get_collection("brain")
        results = collection.get(
            where={
                "$and": [
                    {"date": {"$gte": "2025-01-01"}},
                    {"date": {"$lte": "2025-02-28"}},
                ]
            },
            include=["metadatas"],
        )
        for meta in results["metadatas"]:
            if meta["date"]:
                assert "2025-01" <= meta["date"] <= "2025-02-28"
```

**Step 2: Run tests**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest tests/test_ingest_query.py -v`
Expected: All PASS

**Step 3: Commit**

```bash
cd /home/dan/source/claude-skills
git add repo-search/tests/test_ingest_query.py
git commit -m "test: add ingest/query integration tests with round-trip validation"
```

---

### Task 8: Search quality benchmarks

**Files:**
- Create: `repo-search/tests/test_search_quality.py`

**Step 1: Write the benchmark tests**

Create `repo-search/tests/test_search_quality.py`:

```python
"""Search quality benchmarks - measures MRR to detect regressions.

These tests use a synthetic corpus with known ground-truth query-document pairs.
They are not hard pass/fail gates but log quality metrics.
"""
import chromadb
from ingest import ingest


# Ground truth: (query, expected_file_substring)
GROUND_TRUTH = [
    ("quarterly revenue financial results", "q4-revenue"),
    ("exercise workout cardio", "exercise-routine"),
    ("python programming best practices", "python-best-practices"),
    ("API endpoints authentication", "api-specification"),
    ("budget marketing engineering costs", "budget-2025"),
]


def _compute_mrr(collection, ground_truth, top_k=10):
    """Compute Mean Reciprocal Rank over ground truth queries."""
    reciprocal_ranks = []
    for query, expected_substring in ground_truth:
        results = collection.query(
            query_texts=[query],
            n_results=top_k,
            include=["metadatas"],
        )
        rr = 0.0
        for rank, meta in enumerate(results["metadatas"][0], start=1):
            if expected_substring in meta["file_path"]:
                rr = 1.0 / rank
                break
        reciprocal_ranks.append(rr)
    return sum(reciprocal_ranks) / len(reciprocal_ranks)


class TestSearchQuality:
    def test_mrr_above_threshold(self, ingested_db):
        """MRR should be at least 0.5 (expected doc in top 2 on average)."""
        client = chromadb.PersistentClient(path=str(ingested_db))
        collection = client.get_collection("brain")
        mrr = _compute_mrr(collection, GROUND_TRUTH)
        print(f"\n>>> MRR Score: {mrr:.3f} (threshold: 0.5)")
        # Soft threshold - log the score, fail only if very bad
        assert mrr >= 0.5, f"MRR {mrr:.3f} is below minimum threshold 0.5"

    def test_top1_accuracy(self, ingested_db):
        """At least 60% of ground truth queries should return correct doc at rank 1."""
        client = chromadb.PersistentClient(path=str(ingested_db))
        collection = client.get_collection("brain")
        hits = 0
        for query, expected_substring in GROUND_TRUTH:
            results = collection.query(
                query_texts=[query],
                n_results=1,
                include=["metadatas"],
            )
            if results["metadatas"][0]:
                if expected_substring in results["metadatas"][0][0]["file_path"]:
                    hits += 1
        accuracy = hits / len(GROUND_TRUTH)
        print(f"\n>>> Top-1 Accuracy: {accuracy:.1%} ({hits}/{len(GROUND_TRUTH)})")
        assert accuracy >= 0.6, f"Top-1 accuracy {accuracy:.1%} below 60%"
```

**Step 2: Run tests**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest tests/test_search_quality.py -v -s`
Expected: PASS, with MRR and accuracy scores printed

**Step 3: Commit**

```bash
cd /home/dan/source/claude-skills
git add repo-search/tests/test_search_quality.py
git commit -m "test: add search quality benchmarks with MRR and top-1 accuracy"
```

---

## Phase 2: Chunking Improvements

### Task 9: Semantic markdown chunking with heading context

**Files:**
- Modify: `repo-search/ingest.py:165-181` (chunk_text function)
- Modify: `repo-search/tests/test_chunking.py` (add new tests)

**Step 1: Write failing tests for heading-aware chunking**

Add to `repo-search/tests/test_chunking.py`:

```python
class TestHeadingContextChunking:
    def test_chunks_contain_heading_context(self, repo_all_formats):
        """Each markdown chunk should include its parent heading chain."""
        f = repo_all_formats / "finance" / "reports" / "2025-01-15-q4-revenue.md"
        content = f.read_text()
        chunks = chunk_text(content, f)
        # The "Regional Breakdown" content chunk should reference the section heading
        regional_chunks = [c for c in chunks if "North America" in c]
        assert len(regional_chunks) >= 1
        assert any("Regional Breakdown" in c for c in regional_chunks)

    def test_code_blocks_not_split(self, tmp_path):
        """Code blocks should be kept intact within a single chunk."""
        f = tmp_path / "code.md"
        code_block = "```python\n" + "\n".join(f"line_{i} = {i}" for i in range(30)) + "\n```"
        content = f"# Code Examples\n\n## Example 1\n\n{code_block}\n\n## Example 2\n\nSome other content here.\n"
        f.write_text(content)
        chunks = chunk_text(content, f, chunk_size=2000)
        # Find the chunk with the code block
        code_chunks = [c for c in chunks if "line_0 = 0" in c]
        assert len(code_chunks) >= 1
        # The code block should be intact (has both start and end markers)
        for cc in code_chunks:
            if "```python" in cc:
                assert "```" in cc[cc.index("```python") + 10:], "Code block split mid-block"
```

**Step 2: Run tests — they should fail**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest tests/test_chunking.py::TestHeadingContextChunking -v`
Expected: FAIL (heading context not yet prepended)

**Step 3: Implement semantic markdown chunking**

Modify `repo-search/ingest.py` — replace `chunk_text()` with heading-aware logic:

```python
def _get_heading_chain(content: str, position: int) -> str:
    """Extract the heading chain (h1 > h2 > h3) that applies at a given position."""
    lines = content[:position].split("\n")
    headings = {}
    for line in lines:
        if line.startswith("### "):
            headings[3] = line[4:].strip()
        elif line.startswith("## "):
            headings[2] = line[3:].strip()
            headings.pop(3, None)  # Reset lower levels
        elif line.startswith("# "):
            headings[1] = line[2:].strip()
            headings.pop(2, None)
            headings.pop(3, None)
    parts = []
    for level in sorted(headings.keys()):
        parts.append(headings[level])
    return " > ".join(parts) if parts else ""


def chunk_text(content: str, file_path: Path,
               chunk_size: int = DEFAULT_CHUNK_SIZE,
               chunk_overlap: int = DEFAULT_CHUNK_OVERLAP) -> list[str]:
    """Split content into chunks with heading context for markdown files."""
    if file_path.suffix.lower() == ".md":
        splitter = MarkdownTextSplitter(
            chunk_size=chunk_size,
            chunk_overlap=chunk_overlap,
        )
        raw_chunks = splitter.split_text(content)
        # Enrich each chunk with heading context
        enriched = []
        for chunk in raw_chunks:
            pos = content.find(chunk[:80])  # Find chunk position in original
            if pos > 0:
                heading_chain = _get_heading_chain(content, pos)
                if heading_chain and not chunk.startswith("# "):
                    chunk = f"[{heading_chain}]\n\n{chunk}"
            enriched.append(chunk)
        chunks = enriched
    else:
        splitter = RecursiveCharacterTextSplitter(
            chunk_size=chunk_size,
            chunk_overlap=chunk_overlap,
        )
        chunks = splitter.split_text(content)

    return [c for c in chunks if len(c.strip()) >= 50]
```

**Step 4: Run the new tests**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest tests/test_chunking.py -v`
Expected: All PASS

**Step 5: Run full test suite to check for regressions**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest -v`
Expected: All PASS

**Step 6: Commit**

```bash
cd /home/dan/source/claude-skills
git add repo-search/ingest.py repo-search/tests/test_chunking.py
git commit -m "feat: add heading-context enrichment to markdown chunking"
```

---

### Task 10: XLSX structure-aware chunking

**Files:**
- Modify: `repo-search/ingest.py:99-113` (_extract_xlsx function)
- Modify: `repo-search/tests/test_chunking.py` (add xlsx-specific tests)

**Step 1: Write failing tests**

Add to `repo-search/tests/test_chunking.py`:

```python
class TestXlsxStructuredChunking:
    def test_xlsx_chunks_contain_headers(self, repo_all_formats):
        """Each XLSX chunk should contain column headers for context."""
        f = repo_all_formats / "finance" / "data" / "budget-2025.xlsx"
        from ingest import extract_text
        content = extract_text(f)
        chunks = chunk_text(content, f)
        # Every chunk should have column context
        for chunk in chunks:
            if "Marketing" in chunk or "Engineering" in chunk:
                assert "Category" in chunk or "Sheet:" in chunk

    def test_xlsx_chunks_contain_sheet_name(self, repo_all_formats):
        f = repo_all_formats / "finance" / "data" / "budget-2025.xlsx"
        from ingest import extract_text
        content = extract_text(f)
        assert "Sheet:" in content
```

**Step 2: Run tests to see current behavior**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest tests/test_chunking.py::TestXlsxStructuredChunking -v`

**Step 3: Improve _extract_xlsx to preserve structure better**

Modify `repo-search/ingest.py` — update `_extract_xlsx()`:

```python
def _extract_xlsx(file_path: Path) -> str:
    """Extract text from an Excel XLSX file with structure preservation."""
    from openpyxl import load_workbook

    wb = load_workbook(file_path, read_only=True, data_only=True)
    parts = []
    for sheet in wb.worksheets:
        parts.append(f"Sheet: {sheet.title}")
        header_row = None
        for row_idx, row in enumerate(sheet.iter_rows(values_only=True)):
            cells = [str(c) if c is not None else "" for c in row]
            if not any(c for c in cells):
                continue
            if row_idx == 0:
                header_row = cells
            row_str = " | ".join(cells)
            parts.append(row_str)
        parts.append("")
    wb.close()
    return "\n".join(parts)
```

**Step 4: Run tests**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest tests/test_chunking.py -v`
Expected: All PASS

**Step 5: Commit**

```bash
cd /home/dan/source/claude-skills
git add repo-search/ingest.py repo-search/tests/test_chunking.py
git commit -m "feat: improve xlsx extraction with structure preservation"
```

---

### Task 11: Per-format chunk size defaults

**Files:**
- Modify: `repo-search/ingest.py:42-43` (constants) and `chunk_text()` function
- Modify: `repo-search/tests/test_chunking.py`

**Step 1: Write failing test**

Add to `repo-search/tests/test_chunking.py`:

```python
class TestPerFormatChunkSize:
    def test_markdown_default_larger(self, repo_all_formats):
        """Markdown should default to 1500 chars, not 1000."""
        from ingest import FORMAT_CHUNK_DEFAULTS
        assert FORMAT_CHUNK_DEFAULTS[".md"]["chunk_size"] == 1500

    def test_pdf_default(self):
        from ingest import FORMAT_CHUNK_DEFAULTS
        assert FORMAT_CHUNK_DEFAULTS[".pdf"]["chunk_size"] == 1000

    def test_xlsx_default_larger(self):
        from ingest import FORMAT_CHUNK_DEFAULTS
        assert FORMAT_CHUNK_DEFAULTS[".xlsx"]["chunk_size"] == 2000
```

**Step 2: Run tests — should fail**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest tests/test_chunking.py::TestPerFormatChunkSize -v`
Expected: FAIL (FORMAT_CHUNK_DEFAULTS doesn't exist yet)

**Step 3: Add per-format defaults to ingest.py**

Add after line 43 in `repo-search/ingest.py`:

```python
# Per-format chunk size defaults
FORMAT_CHUNK_DEFAULTS = {
    ".md": {"chunk_size": 1500, "chunk_overlap": 200},
    ".pdf": {"chunk_size": 1000, "chunk_overlap": 200},
    ".docx": {"chunk_size": 1500, "chunk_overlap": 200},
    ".xlsx": {"chunk_size": 2000, "chunk_overlap": 200},
}
```

Update `chunk_text()` to use per-format defaults when no explicit override is given:

```python
def chunk_text(content: str, file_path: Path,
               chunk_size: int = None,
               chunk_overlap: int = None) -> list[str]:
    ext = file_path.suffix.lower()
    defaults = FORMAT_CHUNK_DEFAULTS.get(ext, {"chunk_size": DEFAULT_CHUNK_SIZE, "chunk_overlap": DEFAULT_CHUNK_OVERLAP})
    chunk_size = chunk_size or defaults["chunk_size"]
    chunk_overlap = chunk_overlap or defaults["chunk_overlap"]
    # ... rest of function unchanged
```

**Step 4: Run all tests**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest -v`
Expected: All PASS

**Step 5: Commit**

```bash
cd /home/dan/source/claude-skills
git add repo-search/ingest.py repo-search/tests/test_chunking.py
git commit -m "feat: add per-format chunk size defaults"
```

---

## Phase 3: Embedding & Search Improvements

### Task 12: Explicit and configurable embedding model

**Files:**
- Modify: `repo-search/ingest.py` (collection creation, add model config)
- Modify: `repo-search/query.py` (read model from collection metadata)
- Create: `repo-search/tests/test_embedding_config.py`

**Step 1: Write failing tests**

Create `repo-search/tests/test_embedding_config.py`:

```python
"""Tests for embedding model configuration."""
import chromadb
from ingest import ingest


class TestEmbeddingConfig:
    def test_collection_stores_model_name(self, repo_all_formats, tmp_path):
        """The collection metadata should record which embedding model was used."""
        db_path = tmp_path / "testdb"
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)
        client = chromadb.PersistentClient(path=str(db_path))
        collection = client.get_collection("brain")
        meta = collection.metadata
        assert "embedding_model" in meta
        assert meta["embedding_model"]  # not empty

    def test_default_model_is_minilm(self, repo_all_formats, tmp_path):
        db_path = tmp_path / "testdb"
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)
        client = chromadb.PersistentClient(path=str(db_path))
        collection = client.get_collection("brain")
        assert "MiniLM" in collection.metadata.get("embedding_model", "") or \
               "all-MiniLM" in collection.metadata.get("embedding_model", "")
```

**Step 2: Run tests — should fail**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest tests/test_embedding_config.py -v`
Expected: FAIL (no embedding_model in metadata)

**Step 3: Implement — store model name in collection metadata**

Modify `repo-search/ingest.py` collection creation (around line 278):

```python
DEFAULT_EMBEDDING_MODEL = "all-MiniLM-L6-v2"

# In ingest():
collection = client.get_or_create_collection(
    name="brain",
    metadata={
        "hnsw:space": "cosine",
        "embedding_model": DEFAULT_EMBEDDING_MODEL,
    },
)
```

**Step 4: Run tests**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest tests/test_embedding_config.py -v`
Expected: All PASS

**Step 5: Run full suite**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest -v`
Expected: All PASS

**Step 6: Commit**

```bash
cd /home/dan/source/claude-skills
git add repo-search/ingest.py repo-search/query.py repo-search/tests/test_embedding_config.py
git commit -m "feat: store embedding model name in collection metadata"
```

---

### Task 13: Chunk context enrichment (document title prepended)

**Files:**
- Modify: `repo-search/ingest.py` (in the processing loop, lines 288-340)
- Create: `repo-search/tests/test_context_enrichment.py`

**Step 1: Write failing tests**

Create `repo-search/tests/test_context_enrichment.py`:

```python
"""Tests for chunk context enrichment — title and summary prepended to chunks."""
import chromadb
from ingest import ingest


class TestContextEnrichment:
    def test_chunks_include_document_title(self, repo_all_formats, tmp_path):
        """Each chunk should have its document title prepended for embedding context."""
        db_path = tmp_path / "testdb"
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)
        client = chromadb.PersistentClient(path=str(db_path))
        collection = client.get_collection("brain")
        results = collection.get(
            where={"file_path": "finance/reports/2025-01-15-q4-revenue.md"},
            include=["documents"],
        )
        # Every chunk from this file should mention the doc title
        for doc in results["documents"]:
            assert "Q4 Revenue Report" in doc

    def test_non_first_chunks_have_title(self, repo_all_formats, tmp_path):
        """Even later chunks (not the first) should carry the document title."""
        db_path = tmp_path / "testdb"
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)
        client = chromadb.PersistentClient(path=str(db_path))
        collection = client.get_collection("brain")
        results = collection.get(
            where={"file_path": "technical/guides/python-best-practices.md"},
            include=["documents", "metadatas"],
        )
        later_chunks = [
            doc for doc, meta in zip(results["documents"], results["metadatas"])
            if meta["chunk_index"] > 0
        ]
        assert len(later_chunks) > 0
        for chunk in later_chunks:
            assert "Python Best Practices Guide" in chunk
```

**Step 2: Run — should fail**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest tests/test_context_enrichment.py -v`
Expected: FAIL

**Step 3: Implement — prepend title to all chunks in ingest pipeline**

In `repo-search/ingest.py`, in the processing loop (around line 323), after chunking:

```python
# Prepend document title to each chunk for embedding context
title = metadata["title"]
enriched_chunks = []
for chunk in chunks:
    if title and title not in chunk[:len(title) + 20]:
        enriched_chunks.append(f"[{title}]\n\n{chunk}")
    else:
        enriched_chunks.append(chunk)
chunks = enriched_chunks
```

**Step 4: Run tests**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest tests/test_context_enrichment.py -v`
Expected: All PASS

**Step 5: Run full suite + quality benchmarks**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest -v -s`
Expected: All PASS. Check if MRR improved.

**Step 6: Commit**

```bash
cd /home/dan/source/claude-skills
git add repo-search/ingest.py repo-search/tests/test_context_enrichment.py
git commit -m "feat: prepend document title to all chunks for better embedding context"
```

---

### Task 14: Hybrid search with BM25

**Files:**
- Modify: `repo-search/requirements.txt` (add rank-bm25)
- Modify: `repo-search/ingest.py` (build BM25 index during ingestion)
- Modify: `repo-search/query.py` (add hybrid search mode)
- Create: `repo-search/tests/test_hybrid_search.py`

**Step 1: Install rank-bm25**

Add `rank-bm25>=0.2,<1.0` to `repo-search/requirements.txt`.

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/pip install rank-bm25 -q`

**Step 2: Write failing tests**

Create `repo-search/tests/test_hybrid_search.py`:

```python
"""Tests for hybrid (vector + BM25) search."""
import pickle
from pathlib import Path
import chromadb
from ingest import ingest


class TestBM25Index:
    def test_bm25_index_created_on_ingest(self, repo_all_formats, tmp_path):
        db_path = tmp_path / "testdb"
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)
        bm25_path = db_path / "bm25_index.pkl"
        assert bm25_path.exists()

    def test_bm25_index_loadable(self, repo_all_formats, tmp_path):
        db_path = tmp_path / "testdb"
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)
        bm25_path = db_path / "bm25_index.pkl"
        with open(bm25_path, "rb") as f:
            data = pickle.load(f)
        assert "bm25" in data
        assert "ids" in data


class TestHybridSearch:
    def test_hybrid_mode_returns_results(self, repo_all_formats, tmp_path):
        db_path = tmp_path / "testdb"
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)

        from query import hybrid_search
        client = chromadb.PersistentClient(path=str(db_path))
        collection = client.get_collection("brain")
        results = hybrid_search(collection, db_path, "quarterly revenue", top_k=5)
        assert len(results) > 0
        assert "id" in results[0]
        assert "score" in results[0]

    def test_keyword_search_finds_exact_terms(self, repo_all_formats, tmp_path):
        """BM25 should find documents with exact keyword matches."""
        db_path = tmp_path / "testdb"
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)

        from query import keyword_search
        client = chromadb.PersistentClient(path=str(db_path))
        collection = client.get_collection("brain")
        results = keyword_search(collection, db_path, "Invoice #12345", top_k=5)
        assert len(results) > 0
        assert any("invoice" in r["metadata"]["file_path"] for r in results)
```

**Step 3: Run tests — should fail**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest tests/test_hybrid_search.py -v`
Expected: FAIL (hybrid_search, keyword_search don't exist yet)

**Step 4: Implement BM25 index building in ingest.py**

Add to the end of the `ingest()` function, after saving hash cache:

```python
# Build BM25 index
import pickle
from rank_bm25 import BM25Okapi

all_results = collection.get(include=["documents", "metadatas"])
corpus = [doc.lower().split() for doc in all_results["documents"]]
bm25 = BM25Okapi(corpus)
bm25_data = {
    "bm25": bm25,
    "ids": all_results["ids"],
    "metadatas": all_results["metadatas"],
    "documents": all_results["documents"],
}
bm25_path = db_path / "bm25_index.pkl"
with open(bm25_path, "wb") as f:
    pickle.dump(bm25_data, f)
if verbose:
    print(f"BM25 index saved: {bm25_path}")
```

**Step 5: Implement hybrid_search and keyword_search in query.py**

Add to `repo-search/query.py`:

```python
def _load_bm25(db_path: Path):
    """Load the BM25 index from disk."""
    bm25_path = db_path / "bm25_index.pkl"
    if not bm25_path.exists():
        return None
    with open(bm25_path, "rb") as f:
        return pickle.load(f)


def keyword_search(collection, db_path: Path, query: str, top_k: int = 10):
    """BM25 keyword search."""
    bm25_data = _load_bm25(db_path)
    if not bm25_data:
        return []
    tokenized_query = query.lower().split()
    scores = bm25_data["bm25"].get_scores(tokenized_query)
    top_indices = sorted(range(len(scores)), key=lambda i: scores[i], reverse=True)[:top_k]
    results = []
    for idx in top_indices:
        if scores[idx] > 0:
            results.append({
                "id": bm25_data["ids"][idx],
                "score": float(scores[idx]),
                "metadata": bm25_data["metadatas"][idx],
                "content": bm25_data["documents"][idx],
            })
    return results


def hybrid_search(collection, db_path: Path, query: str, top_k: int = 10,
                  area: str = None, sub_area: str = None):
    """Hybrid search combining vector similarity and BM25 via Reciprocal Rank Fusion."""
    # Vector search
    where_filter = None
    conditions = []
    if area:
        conditions.append({"area": area})
    if sub_area:
        conditions.append({"sub_area": sub_area})
    if len(conditions) == 1:
        where_filter = conditions[0]
    elif len(conditions) > 1:
        where_filter = {"$and": conditions}

    vector_results = collection.query(
        query_texts=[query],
        n_results=top_k * 2,  # Over-fetch for fusion
        where=where_filter,
        include=["documents", "metadatas", "distances"],
    )

    # BM25 search
    bm25_results = keyword_search(collection, db_path, query, top_k=top_k * 2)

    # Reciprocal Rank Fusion
    k = 60
    rrf_scores = {}

    # Score from vector results
    for rank, chunk_id in enumerate(vector_results["ids"][0], start=1):
        rrf_scores[chunk_id] = rrf_scores.get(chunk_id, 0) + 1.0 / (k + rank)

    # Score from BM25 results
    for rank, result in enumerate(bm25_results, start=1):
        chunk_id = result["id"]
        rrf_scores[chunk_id] = rrf_scores.get(chunk_id, 0) + 1.0 / (k + rank)

    # Build combined results sorted by RRF score
    sorted_ids = sorted(rrf_scores.keys(), key=lambda x: rrf_scores[x], reverse=True)[:top_k]

    # Collect metadata and content
    all_ids = vector_results["ids"][0]
    results = []
    for chunk_id in sorted_ids:
        if chunk_id in all_ids:
            idx = all_ids.index(chunk_id)
            results.append({
                "id": chunk_id,
                "score": rrf_scores[chunk_id],
                "metadata": vector_results["metadatas"][0][idx],
                "content": vector_results["documents"][0][idx],
            })
        else:
            # From BM25 only — look up in BM25 data
            for br in bm25_results:
                if br["id"] == chunk_id:
                    results.append({
                        "id": chunk_id,
                        "score": rrf_scores[chunk_id],
                        "metadata": br["metadata"],
                        "content": br["content"],
                    })
                    break

    return results
```

Also add `import pickle` at the top of query.py.

**Step 6: Add `--mode` flag to query.py CLI**

In `query.py`'s `p_search` parser, add:
```python
p_search.add_argument("--mode", choices=["semantic", "keyword", "hybrid"],
                       default="semantic", help="Search mode")
```

Update the search command dispatch to use the mode flag.

**Step 7: Run tests**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest tests/test_hybrid_search.py -v`
Expected: All PASS

**Step 8: Run full suite**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest -v`
Expected: All PASS

**Step 9: Commit**

```bash
cd /home/dan/source/claude-skills
git add repo-search/requirements.txt repo-search/ingest.py repo-search/query.py repo-search/tests/test_hybrid_search.py
git commit -m "feat: add hybrid search combining vector similarity with BM25 keyword scoring"
```

---

### Task 15: Lightweight reranking (dedup + metadata boost)

**Files:**
- Modify: `repo-search/query.py` (add reranking to search output)
- Create: `repo-search/tests/test_reranking.py`

**Step 1: Write failing tests**

Create `repo-search/tests/test_reranking.py`:

```python
"""Tests for search result reranking."""
from query import rerank_results


class TestReranking:
    def test_dedup_keeps_best_per_file(self):
        """Multiple chunks from same file should be deduped to best one."""
        results = [
            {"id": "a.md::chunk_0", "score": 0.9, "metadata": {"file_path": "a.md", "title": "Doc A"}, "content": "x"},
            {"id": "a.md::chunk_1", "score": 0.7, "metadata": {"file_path": "a.md", "title": "Doc A"}, "content": "y"},
            {"id": "b.md::chunk_0", "score": 0.8, "metadata": {"file_path": "b.md", "title": "Doc B"}, "content": "z"},
        ]
        reranked = rerank_results(results, query="test", deduplicate=True)
        file_paths = [r["metadata"]["file_path"] for r in reranked]
        assert len(set(file_paths)) == len(file_paths), "Duplicates not removed"

    def test_metadata_boost_for_title_match(self):
        """Results whose title matches query terms should be boosted."""
        results = [
            {"id": "a.md::chunk_0", "score": 0.5, "metadata": {"file_path": "a.md", "title": "Budget Report", "area": "finance"}, "content": "x"},
            {"id": "b.md::chunk_0", "score": 0.6, "metadata": {"file_path": "b.md", "title": "Exercise", "area": "health"}, "content": "y"},
        ]
        reranked = rerank_results(results, query="budget report", deduplicate=False)
        # Budget report should be boosted above exercise
        assert reranked[0]["metadata"]["title"] == "Budget Report"

    def test_rerank_empty_list(self):
        assert rerank_results([], query="test") == []
```

**Step 2: Run tests — should fail**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest tests/test_reranking.py -v`
Expected: FAIL (rerank_results doesn't exist)

**Step 3: Implement reranking**

Add to `repo-search/query.py`:

```python
def rerank_results(results: list, query: str, deduplicate: bool = True) -> list:
    """Lightweight reranking: deduplication and metadata boosting."""
    if not results:
        return results

    query_terms = set(query.lower().split())

    # Metadata boost
    boosted = []
    for r in results:
        boost = 0.0
        title = r["metadata"].get("title", "").lower()
        area = r["metadata"].get("area", "").lower()
        # Boost if query terms appear in title
        title_words = set(title.split())
        overlap = query_terms & title_words
        boost += len(overlap) * 0.05
        # Boost if query terms match area
        if area in query_terms:
            boost += 0.02
        boosted.append({**r, "score": r["score"] + boost})

    # Sort by boosted score
    boosted.sort(key=lambda x: x["score"], reverse=True)

    # Deduplicate: keep best chunk per file
    if deduplicate:
        seen_files = set()
        deduped = []
        for r in boosted:
            fp = r["metadata"]["file_path"]
            if fp not in seen_files:
                seen_files.add(fp)
                deduped.append(r)
        return deduped

    return boosted
```

**Step 4: Run tests**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest tests/test_reranking.py -v`
Expected: All PASS

**Step 5: Integrate reranking into cmd_search and hybrid_search**

Wire `rerank_results()` into the search output path so results are reranked before display.

**Step 6: Run full suite**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest -v`
Expected: All PASS

**Step 7: Commit**

```bash
cd /home/dan/source/claude-skills
git add repo-search/query.py repo-search/tests/test_reranking.py
git commit -m "feat: add lightweight reranking with dedup and metadata boosting"
```

---

## Phase 4: Performance & Collection Management

### Task 16: Batch embedding during ingestion

**Files:**
- Modify: `repo-search/ingest.py` (accumulate chunks, batch-add)

**Step 1: Write failing test**

Add to `repo-search/tests/test_ingest_query.py`:

```python
class TestBatchIngestion:
    def test_large_batch_ingestion(self, repo_all_formats, tmp_path):
        """Ingestion should handle batched adds without error."""
        db_path = tmp_path / "testdb"
        # Add many small files to force batching
        for i in range(50):
            area_dir = repo_all_formats / "batch_test"
            area_dir.mkdir(exist_ok=True)
            (area_dir / f"doc_{i:03d}.md").write_text(
                f"# Document {i}\n\n" +
                f"Content for document number {i}. " * 30 + "\n"
            )
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)
        client = chromadb.PersistentClient(path=str(db_path))
        collection = client.get_collection("brain")
        assert collection.count() > 50
```

**Step 2: Run test (should already pass with current code, this is a regression test)**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest tests/test_ingest_query.py::TestBatchIngestion -v`

**Step 3: Refactor ingest to use batch accumulation**

In `repo-search/ingest.py`, refactor the processing loop to accumulate chunks across files and batch-add to ChromaDB in groups of 500:

```python
BATCH_SIZE = 500

# In ingest(), replace the per-file collection.add() with batch accumulation:
batch_ids = []
batch_documents = []
batch_metadatas = []

for i, (f, file_hash) in enumerate(files_to_process, 1):
    # ... existing extraction logic ...

    # Accumulate
    batch_ids.extend(ids)
    batch_documents.extend(documents)
    batch_metadatas.extend(metadatas)

    # Flush batch when full
    if len(batch_ids) >= BATCH_SIZE:
        collection.add(ids=batch_ids, documents=batch_documents, metadatas=batch_metadatas)
        total_chunks += len(batch_ids)
        batch_ids, batch_documents, batch_metadatas = [], [], []

# Flush remaining
if batch_ids:
    collection.add(ids=batch_ids, documents=batch_documents, metadatas=batch_metadatas)
    total_chunks += len(batch_ids)
```

**Step 4: Run full suite**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest -v`
Expected: All PASS

**Step 5: Commit**

```bash
cd /home/dan/source/claude-skills
git add repo-search/ingest.py repo-search/tests/test_ingest_query.py
git commit -m "perf: batch chunk additions during ingestion for better performance"
```

---

### Task 17: Prune command for orphaned chunks

**Files:**
- Modify: `repo-search/query.py` (add prune subcommand)
- Create: `repo-search/tests/test_prune.py`

**Step 1: Write failing tests**

Create `repo-search/tests/test_prune.py`:

```python
"""Tests for the prune command — removing chunks for deleted files."""
import chromadb
from ingest import ingest


class TestPrune:
    def test_prune_removes_orphaned_chunks(self, repo_all_formats, tmp_path):
        db_path = tmp_path / "testdb"
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)

        # Delete a file from disk
        (repo_all_formats / "health" / "exercise-routine.md").unlink()

        from query import cmd_prune
        client = chromadb.PersistentClient(path=str(db_path))
        collection = client.get_collection("brain")
        removed = cmd_prune(collection, repo_all_formats)
        assert removed > 0

        # Verify chunks are gone
        results = collection.get(
            where={"file_path": "health/exercise-routine.md"},
            include=["metadatas"],
        )
        assert len(results["ids"]) == 0

    def test_prune_keeps_existing_files(self, repo_all_formats, tmp_path):
        db_path = tmp_path / "testdb"
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)
        client = chromadb.PersistentClient(path=str(db_path))
        collection = client.get_collection("brain")
        count_before = collection.count()

        from query import cmd_prune
        removed = cmd_prune(collection, repo_all_formats)
        assert removed == 0
        assert collection.count() == count_before
```

**Step 2: Run tests — should fail**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest tests/test_prune.py -v`
Expected: FAIL (cmd_prune doesn't exist)

**Step 3: Implement prune command**

Add to `repo-search/query.py`:

```python
def cmd_prune(collection, repo_root: Path, output_format: str = "text") -> int:
    """Remove chunks for files that no longer exist on disk."""
    results = collection.get(include=["metadatas"])
    file_paths = {m["file_path"] for m in results["metadatas"]}

    removed = 0
    for fp in file_paths:
        full_path = repo_root / fp
        if not full_path.exists():
            # Delete all chunks for this file
            chunks = collection.get(where={"file_path": fp})
            if chunks["ids"]:
                collection.delete(ids=chunks["ids"])
                removed += len(chunks["ids"])
                if output_format == "text":
                    print(f"  Pruned {len(chunks['ids'])} chunks: {fp}")

    if output_format == "text":
        print(f"\nTotal pruned: {removed} chunks")
    return removed
```

Add `prune` subcommand to the argparse setup:

```python
p_prune = subparsers.add_parser("prune", help="Remove chunks for deleted files")
p_prune.add_argument("repo_root", help="Repository root to check file existence against")
```

And the dispatch:
```python
elif args.command == "prune":
    cmd_prune(collection, Path(args.repo_root), args.format)
```

**Step 4: Run tests**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest tests/test_prune.py -v`
Expected: All PASS

**Step 5: Run full suite**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest -v`
Expected: All PASS

**Step 6: Commit**

```bash
cd /home/dan/source/claude-skills
git add repo-search/query.py repo-search/tests/test_prune.py
git commit -m "feat: add prune command to remove chunks for deleted files"
```

---

### Task 18: Named collections support

**Files:**
- Modify: `repo-search/ingest.py` (add `--collection` flag)
- Modify: `repo-search/query.py` (add `--collection` flag)
- Create: `repo-search/tests/test_collections.py`

**Step 1: Write failing tests**

Create `repo-search/tests/test_collections.py`:

```python
"""Tests for named collection support."""
import chromadb
from ingest import ingest


class TestNamedCollections:
    def test_custom_collection_name(self, repo_all_formats, tmp_path):
        db_path = tmp_path / "testdb"
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True,
               collection_name="work")
        client = chromadb.PersistentClient(path=str(db_path))
        collection = client.get_collection("work")
        assert collection.count() > 0

    def test_separate_collections_independent(self, repo_all_formats, tmp_path):
        db_path = tmp_path / "testdb"
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True,
               collection_name="col_a")
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True,
               collection_name="col_b")
        client = chromadb.PersistentClient(path=str(db_path))
        a = client.get_collection("col_a")
        b = client.get_collection("col_b")
        assert a.count() == b.count()
        assert a.count() > 0
```

**Step 2: Run tests — should fail**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest tests/test_collections.py -v`
Expected: FAIL (collection_name parameter doesn't exist)

**Step 3: Implement — add collection_name parameter**

In `repo-search/ingest.py`:
- Add `collection_name: str = "brain"` parameter to `ingest()`
- Replace hardcoded `"brain"` with `collection_name`
- Add `--collection` CLI arg

In `repo-search/query.py`:
- Add `--collection` CLI arg (default: "brain")
- Pass through to `get_collection()`

**Step 4: Run tests**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest tests/test_collections.py -v`
Expected: All PASS

**Step 5: Run full suite**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest -v`
Expected: All PASS

**Step 6: Commit**

```bash
cd /home/dan/source/claude-skills
git add repo-search/ingest.py repo-search/query.py repo-search/tests/test_collections.py
git commit -m "feat: add named collection support with --collection flag"
```

---

### Task 19: Progress reporting during ingestion

**Files:**
- Modify: `repo-search/ingest.py` (add progress output)

**Step 1: Implement progress reporting**

In `repo-search/ingest.py`, update the processing loop to always print progress:

```python
# Replace the conditional print at line 346-348 with:
elapsed_so_far = time.time() - start_time
rate = i / elapsed_so_far if elapsed_so_far > 0 else 0
print(f"\r  [{i}/{len(files_to_process)}] {rel_path}: "
      f"{len(chunks)} chunks ({rate:.1f} files/s)", end="", flush=True)
```

Add a newline after the loop completes: `print()`

**Step 2: Run full suite to verify no regressions**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest -v`
Expected: All PASS

**Step 3: Commit**

```bash
cd /home/dan/source/claude-skills
git add repo-search/ingest.py
git commit -m "feat: add progress reporting with rate display during ingestion"
```

---

### Task 20: Update SKILL.md with new features

**Files:**
- Modify: `repo-search/SKILL.md`

**Step 1: Update SKILL.md**

Add documentation for all new features:
- `--mode semantic|keyword|hybrid` search modes
- `--collection` flag for named collections
- `prune` command
- Per-format chunk sizes
- Heading context enrichment (automatic, no flag needed)
- Document title prepend (automatic)

**Step 2: Run full test suite one final time**

Run: `cd /home/dan/source/claude-skills/repo-search && .venv/bin/python -m pytest -v -s`
Expected: All PASS, with quality metrics printed

**Step 3: Commit**

```bash
cd /home/dan/source/claude-skills
git add repo-search/SKILL.md
git commit -m "docs: update SKILL.md with new search modes, collections, and prune command"
```

---

## Summary

| Phase | Tasks | What it delivers |
|-------|-------|------------------|
| 1: Tests | Tasks 1-8 | Full test suite: unit, integration, quality benchmarks |
| 2: Chunking | Tasks 9-11 | Heading context, XLSX structure, per-format sizes |
| 3: Search | Tasks 12-15 | Explicit embeddings, title enrichment, hybrid search, reranking |
| 4: Performance | Tasks 16-19 | Batch ingestion, prune command, named collections, progress |
| Docs | Task 20 | Updated SKILL.md |
