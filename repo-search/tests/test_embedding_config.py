"""Tests for embedding model configuration."""
import chromadb
from ingest import ingest, DEFAULT_EMBEDDING_MODEL


class TestEmbeddingConfig:
    def test_default_model_constant_exists(self):
        assert DEFAULT_EMBEDDING_MODEL
        assert isinstance(DEFAULT_EMBEDDING_MODEL, str)

    def test_collection_stores_model_name(self, repo_all_formats, tmp_path_factory):
        db_path = tmp_path_factory.mktemp("db")
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)
        client = chromadb.PersistentClient(path=str(db_path))
        collection = client.get_collection("brain")
        meta = collection.metadata
        assert "embedding_model" in meta
        assert meta["embedding_model"] == DEFAULT_EMBEDDING_MODEL
