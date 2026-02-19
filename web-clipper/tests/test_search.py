import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "scripts"))


@pytest.fixture
def searchable_clips_dir(tmp_clips_dir):
    """Clips with varied content for search testing."""
    (tmp_clips_dir / "2026-01-01-python-guide.md").write_text(
        "---\ntitle: Python Guide\nurl: https://example.com/py\n---\n\nPython is a great programming language for beginners.\n"
    )
    (tmp_clips_dir / "2026-01-02-rust-intro.md").write_text(
        "---\ntitle: Rust Introduction\nurl: https://example.com/rust\n---\n\nRust provides memory safety without garbage collection.\n"
    )
    (tmp_clips_dir / "2026-01-03-cooking.md").write_text(
        "---\ntitle: Best Pasta Recipes\nurl: https://example.com/pasta\n---\n\nBoil water, add pasta, cook for 10 minutes.\n"
    )
    return tmp_clips_dir


class TestSearchClips:
    def test_finds_matching_clips(self, searchable_clips_dir):
        from search import search_clips

        results = search_clips(searchable_clips_dir, "programming language")
        assert len(results) >= 1
        assert any("Python" in r["title"] for r in results)

    def test_no_results_for_unmatched_query(self, searchable_clips_dir):
        from search import search_clips

        results = search_clips(searchable_clips_dir, "quantum physics")
        assert len(results) == 0

    def test_search_is_case_insensitive(self, searchable_clips_dir):
        from search import search_clips

        results = search_clips(searchable_clips_dir, "PYTHON")
        assert len(results) >= 1

    def test_respects_limit(self, searchable_clips_dir):
        from search import search_clips

        results = search_clips(searchable_clips_dir, "a", limit=1)
        assert len(results) <= 1
