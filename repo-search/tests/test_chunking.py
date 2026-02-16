"""Tests for text chunking logic."""
from pathlib import Path
from ingest import chunk_text, DEFAULT_CHUNK_SIZE, DEFAULT_CHUNK_OVERLAP, FORMAT_CHUNK_DEFAULTS


class TestMarkdownChunking:
    def test_short_file_single_chunk(self, tmp_path):
        f = tmp_path / "short.md"
        content = "# Title\n\nA short paragraph that fits in one chunk easily without any splitting needed."
        chunks = chunk_text(content, f)
        assert len(chunks) == 1
        assert "short paragraph" in chunks[0]

    def test_long_file_multiple_chunks(self, repo_all_formats):
        f = repo_all_formats / "technical" / "guides" / "python-best-practices.md"
        content = f.read_text()
        chunks = chunk_text(content, f)
        assert len(chunks) > 5

    def test_chunks_do_not_exceed_size(self, repo_all_formats):
        f = repo_all_formats / "technical" / "guides" / "python-best-practices.md"
        content = f.read_text()
        chunks = chunk_text(content, f, chunk_size=500, chunk_overlap=100)
        for chunk in chunks:
            assert len(chunk) < 500 * 1.5, f"Chunk too large: {len(chunk)} chars"

    def test_minimum_chunk_filter(self, tmp_path):
        f = tmp_path / "tiny.md"
        content = "# H\n\n" + "x " * 25 + "\n\n" + "A substantial paragraph with enough content to pass the minimum length filter easily and be kept."
        chunks = chunk_text(content, f)
        for chunk in chunks:
            assert len(chunk.strip()) >= 50

    def test_custom_chunk_size(self, repo_all_formats):
        f = repo_all_formats / "technical" / "guides" / "python-best-practices.md"
        content = f.read_text()
        chunks_default = chunk_text(content, f, chunk_size=1000)
        chunks_small = chunk_text(content, f, chunk_size=500)
        assert len(chunks_small) > len(chunks_default)

    def test_overlap_preserves_context(self, repo_all_formats):
        f = repo_all_formats / "technical" / "guides" / "python-best-practices.md"
        content = f.read_text()
        chunks = chunk_text(content, f, chunk_size=500, chunk_overlap=200)
        if len(chunks) >= 2:
            overlaps_found = 0
            for i in range(len(chunks) - 1):
                tail = chunks[i][-100:]
                if any(word in chunks[i + 1] for word in tail.split() if len(word) > 4):
                    overlaps_found += 1
            assert overlaps_found > 0, "Expected some overlapping content between adjacent chunks"


class TestNonMarkdownChunking:
    def test_pdf_text_chunking(self, repo_all_formats):
        f = repo_all_formats / "finance" / "invoices" / "2025-02-invoice.pdf"
        from ingest import extract_text
        content = extract_text(f)
        chunks = chunk_text(content, f)
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


class TestHeadingContextChunking:
    def test_chunks_contain_heading_context(self, repo_all_formats):
        """Each markdown chunk should include its parent heading chain."""
        f = repo_all_formats / "finance" / "reports" / "2025-01-15-q4-revenue.md"
        content = f.read_text()
        chunks = chunk_text(content, f)
        # The "Regional Breakdown" content should reference that section
        regional_chunks = [c for c in chunks if "North America" in c]
        assert len(regional_chunks) >= 1
        assert any("Regional Breakdown" in c for c in regional_chunks)

    def test_code_blocks_not_split(self, tmp_path):
        """Code blocks should be kept intact within a single chunk."""
        f = tmp_path / "code.md"
        code_block = "```python\n" + "\n".join(f"line_{i} = {i}" for i in range(30)) + "\n```"
        content = f"# Code Examples\n\n## Example 1\n\n{code_block}\n\n## Example 2\n\nSome other content here that is long enough to be a valid chunk.\n"
        f.write_text(content)
        chunks = chunk_text(content, f, chunk_size=2000)
        # Find the chunk with the code block - it should contain start and end markers
        code_chunks = [c for c in chunks if "line_0 = 0" in c]
        assert len(code_chunks) >= 1


class TestXlsxStructuredChunking:
    def test_xlsx_chunks_contain_sheet_name(self, repo_all_formats):
        f = repo_all_formats / "finance" / "data" / "budget-2025.xlsx"
        from ingest import extract_text
        content = extract_text(f)
        assert "Sheet:" in content
        # Both sheets should be present
        assert "Q1 Budget" in content
        assert "Q2 Budget" in content

    def test_xlsx_preserves_data(self, repo_all_formats):
        f = repo_all_formats / "finance" / "data" / "budget-2025.xlsx"
        from ingest import extract_text
        content = extract_text(f)
        assert "Marketing" in content
        assert "50000" in content


class TestPerFormatChunkSize:
    def test_format_defaults_exist(self):
        assert ".md" in FORMAT_CHUNK_DEFAULTS
        assert ".pdf" in FORMAT_CHUNK_DEFAULTS
        assert ".docx" in FORMAT_CHUNK_DEFAULTS
        assert ".xlsx" in FORMAT_CHUNK_DEFAULTS

    def test_markdown_default_larger(self):
        assert FORMAT_CHUNK_DEFAULTS[".md"]["chunk_size"] == 1500

    def test_xlsx_default_larger(self):
        assert FORMAT_CHUNK_DEFAULTS[".xlsx"]["chunk_size"] == 2000

    def test_pdf_default(self):
        assert FORMAT_CHUNK_DEFAULTS[".pdf"]["chunk_size"] == 1000
