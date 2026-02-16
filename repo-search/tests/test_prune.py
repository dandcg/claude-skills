"""Tests for the prune command."""
import chromadb
from pathlib import Path
from ingest import ingest


class TestPrune:
    def test_prune_removes_orphaned_chunks(self, repo_all_formats, tmp_path_factory):
        db_path = tmp_path_factory.mktemp("db")
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)
        # Delete a file from disk
        (repo_all_formats / "health" / "exercise-routine.md").unlink()
        from query import cmd_prune
        client = chromadb.PersistentClient(path=str(db_path))
        collection = client.get_collection("brain")
        removed = cmd_prune(collection, repo_all_formats)
        assert removed > 0
        # Verify chunks are gone
        results = collection.get(where={"file_path": "health/exercise-routine.md"}, include=["metadatas"])
        assert len(results["ids"]) == 0

    def test_prune_keeps_existing_files(self, repo_all_formats, tmp_path_factory):
        db_path = tmp_path_factory.mktemp("db")
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)
        client = chromadb.PersistentClient(path=str(db_path))
        collection = client.get_collection("brain")
        count_before = collection.count()
        from query import cmd_prune
        removed = cmd_prune(collection, repo_all_formats)
        assert removed == 0
        assert collection.count() == count_before
