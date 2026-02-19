import json
import yaml
import pytest
from pathlib import Path
from unittest.mock import patch, MagicMock

import sys
import os

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "scripts"))


class TestExtractArticle:
    """Test article extraction from HTML."""

    def test_extracts_text_from_html(self, sample_html):
        from clip import extract_article

        result = extract_article(sample_html, "https://example.com/article")
        assert result is not None
        assert result["text"]
        assert len(result["text"]) > 50

    def test_extracts_title(self, sample_html):
        from clip import extract_article

        result = extract_article(sample_html, "https://example.com/article")
        assert result["title"]

    def test_returns_none_for_empty_html(self):
        from clip import extract_article

        result = extract_article("", "https://example.com")
        assert result is None

    def test_returns_none_for_non_article_html(self):
        from clip import extract_article

        result = extract_article("<html><body><nav>Menu</nav></body></html>", "https://example.com")
        assert result is None


class TestGenerateMarkdown:
    """Test markdown generation with YAML frontmatter."""

    def test_generates_valid_yaml_frontmatter(self):
        from clip import generate_markdown

        article = {
            "title": "Test Title",
            "text": "Article body text.",
            "author": "John",
            "date": "2026-02-19",
            "description": "A test article",
        }
        md = generate_markdown(article, "https://example.com/test", tags=["python"])

        # Parse frontmatter
        parts = md.split("---\n")
        assert len(parts) >= 3
        fm = yaml.safe_load(parts[1])
        assert fm["title"] == "Test Title"
        assert fm["url"] == "https://example.com/test"
        assert fm["domain"] == "example.com"
        assert "python" in fm["tags"]

    def test_includes_article_body(self):
        from clip import generate_markdown

        article = {
            "title": "Test",
            "text": "Body content here.",
            "author": None,
            "date": None,
            "description": None,
        }
        md = generate_markdown(article, "https://example.com/test", tags=[])
        assert "Body content here." in md


class TestGenerateFilename:
    """Test filename generation from article title."""

    def test_slugifies_title(self):
        from clip import generate_filename

        name = generate_filename("My Great Article!", "2026-02-19")
        assert name == "2026-02-19-my-great-article.md"

    def test_handles_long_titles(self):
        from clip import generate_filename

        long_title = "A" * 200
        name = generate_filename(long_title, "2026-02-19")
        assert len(name) <= 120

    def test_handles_special_characters(self):
        from clip import generate_filename

        name = generate_filename("What's the deal with C++/C#?", "2026-02-19")
        assert "/" not in name
        assert "'" not in name
        assert name.endswith(".md")


class TestSaveClip:
    """Test saving clips to disk."""

    def test_saves_markdown_file(self, tmp_clips_dir):
        from clip import save_clip

        filepath = save_clip(
            "---\ntitle: Test\n---\nBody",
            "Test Title",
            tmp_clips_dir,
        )
        assert filepath.exists()
        assert filepath.suffix == ".md"
        assert filepath.read_text().startswith("---")

    def test_handles_filename_collision(self, tmp_clips_dir):
        from clip import save_clip

        path1 = save_clip("content1", "Same Title", tmp_clips_dir)
        path2 = save_clip("content2", "Same Title", tmp_clips_dir)
        assert path1 != path2
        assert path2.exists()


class TestDetectCloudflare:
    """Test Cloudflare challenge detection."""

    def test_detects_cloudflare_title(self, cloudflare_html):
        from clip import is_cloudflare_challenge

        assert is_cloudflare_challenge(cloudflare_html, 200) is True

    def test_detects_403_status(self):
        from clip import is_cloudflare_challenge

        assert is_cloudflare_challenge("<html></html>", 403) is True

    def test_normal_page_not_detected(self, sample_html):
        from clip import is_cloudflare_challenge

        assert is_cloudflare_challenge(sample_html, 200) is False
