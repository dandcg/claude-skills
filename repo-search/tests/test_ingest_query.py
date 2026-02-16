"""Integration tests: ingest documents then query them."""
import chromadb
from ingest import ingest


class TestIngestPipeline:
    def test_ingest_creates_db(self, repo_all_formats, tmp_path_factory):
        db_path = tmp_path_factory.mktemp("db")
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)
        assert db_path.exists()

    def test_ingest_populates_collection(self, repo_all_formats, tmp_path_factory):
        db_path = tmp_path_factory.mktemp("db")
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)
        client = chromadb.PersistentClient(path=str(db_path))
        collection = client.get_collection("brain")
        assert collection.count() > 0

    def test_ingest_all_files_indexed(self, repo_all_formats, tmp_path_factory):
        db_path = tmp_path_factory.mktemp("db")
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)
        client = chromadb.PersistentClient(path=str(db_path))
        collection = client.get_collection("brain")
        results = collection.get(include=["metadatas"])
        file_paths = {m["file_path"] for m in results["metadatas"]}
        assert len(file_paths) >= 5  # 3 md + pdf + docx + xlsx (xlsx might be too short)

    def test_incremental_skips_unchanged(self, repo_all_formats, tmp_path_factory):
        db_path = tmp_path_factory.mktemp("db")
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)
        client = chromadb.PersistentClient(path=str(db_path))
        collection = client.get_collection("brain")
        count_after_first = collection.count()
        # Second ingest should skip everything
        ingest(repo_root=repo_all_formats, db_path=db_path)
        collection = client.get_collection("brain")
        assert collection.count() == count_after_first

    def test_incremental_reprocesses_changed(self, repo_all_formats, tmp_path_factory):
        db_path = tmp_path_factory.mktemp("db")
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)
        # Modify a file
        f = repo_all_formats / "health" / "exercise-routine.md"
        f.write_text(f.read_text() + "\n\n## New Section\n\nBrand new content added here for testing purposes to be long enough.\n")
        # Re-ingest
        ingest(repo_root=repo_all_formats, db_path=db_path)
        client = chromadb.PersistentClient(path=str(db_path))
        collection = client.get_collection("brain")
        results = collection.get(
            where={"file_path": "health/exercise-routine.md"},
            include=["documents"],
        )
        all_text = " ".join(results["documents"])
        assert "Brand new content" in all_text


class TestBatchIngestion:
    def test_large_batch_ingestion(self, repo_all_formats, tmp_path_factory):
        """Ingestion should handle batched adds without error."""
        # Add many small files to force batching
        for i in range(50):
            area_dir = repo_all_formats / "batch_test"
            area_dir.mkdir(exist_ok=True)
            (area_dir / f"doc_{i:03d}.md").write_text(
                f"# Document {i}\n\n" +
                f"Content for document number {i} with enough text to be a valid chunk. " * 10 + "\n"
            )
        db_path = tmp_path_factory.mktemp("db")
        from ingest import ingest
        ingest(repo_root=repo_all_formats, db_path=db_path, force=True)
        import chromadb
        client = chromadb.PersistentClient(path=str(db_path))
        collection = client.get_collection("brain")
        assert collection.count() > 50


class TestQueryRoundTrip:
    def test_semantic_search_returns_relevant(self, ingested_db):
        client = chromadb.PersistentClient(path=str(ingested_db))
        collection = client.get_collection("brain")
        results = collection.query(
            query_texts=["quarterly revenue financial results"],
            n_results=5,
            include=["metadatas", "distances"],
        )
        file_paths = [m["file_path"] for m in results["metadatas"][0]]
        assert any("q4-revenue" in fp for fp in file_paths)

    def test_area_filter_restricts_results(self, ingested_db):
        client = chromadb.PersistentClient(path=str(ingested_db))
        collection = client.get_collection("brain")
        results = collection.query(
            query_texts=["report"],
            n_results=50,
            where={"area": "health"},
            include=["metadatas"],
        )
        for meta in results["metadatas"][0]:
            assert meta["area"] == "health"

    def test_file_retrieval_ordered_by_chunk(self, ingested_db):
        client = chromadb.PersistentClient(path=str(ingested_db))
        collection = client.get_collection("brain")
        results = collection.get(
            where={"file_path": "technical/guides/python-best-practices.md"},
            include=["metadatas"],
        )
        indices = sorted([m["chunk_index"] for m in results["metadatas"]])
        assert indices == list(range(len(indices)))

    def test_date_filter_query(self, ingested_db):
        """Filter by exact date value (ChromaDB string metadata)."""
        client = chromadb.PersistentClient(path=str(ingested_db))
        collection = client.get_collection("brain")
        # Query for a specific known date
        results = collection.get(
            where={"date": "2025-01-15"},
            include=["metadatas"],
        )
        assert len(results["metadatas"]) > 0
        for meta in results["metadatas"]:
            assert meta["date"] == "2025-01-15"
            assert "q4-revenue" in meta["file_path"]
