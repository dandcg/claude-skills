"""Tests for named collection support."""
import chromadb
from ingest import ingest


class TestNamedCollections:
    def test_custom_collection_name(self, repo_all_formats, tmp_path_factory):
        db_path = tmp_path_factory.mktemp("db")
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True, collection_name="work")
        client = chromadb.PersistentClient(path=str(db_path))
        collection = client.get_collection("work")
        assert collection.count() > 0

    def test_default_collection_is_brain(self, repo_all_formats, tmp_path_factory):
        db_path = tmp_path_factory.mktemp("db")
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)
        client = chromadb.PersistentClient(path=str(db_path))
        collection = client.get_collection("brain")
        assert collection.count() > 0
