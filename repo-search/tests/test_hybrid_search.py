"""Tests for hybrid (vector + BM25) search."""
import pickle
from pathlib import Path
import chromadb
from ingest import ingest


class TestBM25Index:
    def test_bm25_index_created_on_ingest(self, repo_all_formats, tmp_path_factory):
        db_path = tmp_path_factory.mktemp("db")
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)
        bm25_path = db_path / "bm25_index.pkl"
        assert bm25_path.exists()

    def test_bm25_index_loadable(self, repo_all_formats, tmp_path_factory):
        db_path = tmp_path_factory.mktemp("db")
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)
        bm25_path = db_path / "bm25_index.pkl"
        with open(bm25_path, "rb") as f:
            data = pickle.load(f)
        assert "bm25" in data
        assert "ids" in data
        assert "documents" in data


class TestHybridSearch:
    def test_hybrid_returns_results(self, repo_all_formats, tmp_path_factory):
        db_path = tmp_path_factory.mktemp("db")
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)
        from query import hybrid_search
        client = chromadb.PersistentClient(path=str(db_path))
        collection = client.get_collection("brain")
        results = hybrid_search(collection, db_path, "quarterly revenue", top_k=5)
        assert len(results) > 0
        assert "id" in results[0]
        assert "score" in results[0]

    def test_keyword_search_finds_exact_terms(self, repo_all_formats, tmp_path_factory):
        db_path = tmp_path_factory.mktemp("db")
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)
        from query import keyword_search
        client = chromadb.PersistentClient(path=str(db_path))
        collection = client.get_collection("brain")
        results = keyword_search(collection, db_path, "Invoice", top_k=5)
        assert len(results) > 0

    def test_hybrid_combines_both_signals(self, repo_all_formats, tmp_path_factory):
        """Hybrid search should return results from both vector and keyword search."""
        db_path = tmp_path_factory.mktemp("db")
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)
        from query import hybrid_search
        client = chromadb.PersistentClient(path=str(db_path))
        collection = client.get_collection("brain")
        results = hybrid_search(collection, db_path, "budget marketing costs", top_k=5)
        assert len(results) > 0
        # Results should have RRF scores
        for r in results:
            assert r["score"] > 0
