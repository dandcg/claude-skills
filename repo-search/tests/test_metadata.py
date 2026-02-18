"""Tests for metadata extraction from files and paths."""
from pathlib import Path
from ingest import extract_metadata


class TestAreaParsing:
    def test_top_level_area(self, repo_all_formats):
        f = repo_all_formats / "health" / "exercise-routine.md"
        content = f.read_text()
        meta = extract_metadata(f, repo_all_formats, content)
        assert meta["area"] == "health"

    def test_sub_area(self, repo_all_formats):
        f = repo_all_formats / "finance" / "reports" / "2025-01-15-q4-revenue.md"
        content = f.read_text()
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
        assert meta["date"] == "2025-03-10"

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
