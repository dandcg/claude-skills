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
        assert len(h) == 32


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
