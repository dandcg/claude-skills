import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "scripts"))


@pytest.fixture
def clips_with_files(tmp_clips_dir):
    """Clips dir with files to delete."""
    (tmp_clips_dir / "2026-02-19-test-article.md").write_text(
        '---\ntitle: Test Article\nurl: "https://example.com/test"\n---\n\nContent.\n'
    )
    (tmp_clips_dir / "2026-02-19-other-article.md").write_text(
        '---\ntitle: Other Article\nurl: "https://example.com/other"\n---\n\nOther.\n'
    )
    return tmp_clips_dir


class TestDeleteClip:
    def test_delete_by_filename(self, clips_with_files):
        from delete import delete_clip

        result = delete_clip(clips_with_files, filename="2026-02-19-test-article.md")
        assert result is True
        assert not (clips_with_files / "2026-02-19-test-article.md").exists()
        # Other file untouched
        assert (clips_with_files / "2026-02-19-other-article.md").exists()

    def test_delete_by_url(self, clips_with_files):
        from delete import delete_clip

        result = delete_clip(clips_with_files, url="https://example.com/test")
        assert result is True
        assert not (clips_with_files / "2026-02-19-test-article.md").exists()

    def test_delete_nonexistent_returns_false(self, clips_with_files):
        from delete import delete_clip

        result = delete_clip(clips_with_files, filename="nonexistent.md")
        assert result is False

    def test_delete_by_url_no_match(self, clips_with_files):
        from delete import delete_clip

        result = delete_clip(clips_with_files, url="https://nope.com")
        assert result is False
