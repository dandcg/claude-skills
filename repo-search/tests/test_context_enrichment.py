"""Tests for chunk context enrichment - title prepended to chunks."""
import chromadb
from ingest import ingest


class TestContextEnrichment:
    def test_chunks_include_document_title(self, repo_all_formats, tmp_path_factory):
        """Each chunk should have its document title prepended for embedding context."""
        db_path = tmp_path_factory.mktemp("db")
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

    def test_non_first_chunks_have_title(self, repo_all_formats, tmp_path_factory):
        """Even later chunks (not the first) should carry the document title."""
        db_path = tmp_path_factory.mktemp("db")
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
            assert "Python Best Practices" in chunk
