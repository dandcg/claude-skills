"""Tests for text extraction across all supported formats."""
from pathlib import Path
from ingest import extract_text, find_files, SUPPORTED_EXTENSIONS
import pytest


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
        assert "strength training" in text

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
        with pytest.raises(ValueError, match="Unsupported"):
            extract_text(f)
