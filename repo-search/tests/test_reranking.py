"""Tests for search result reranking."""
from query import rerank_results


class TestReranking:
    def test_dedup_keeps_best_per_file(self):
        results = [
            {"id": "a.md::chunk_0", "score": 0.9, "metadata": {"file_path": "a.md", "title": "Doc A", "area": "test"}, "content": "x"},
            {"id": "a.md::chunk_1", "score": 0.7, "metadata": {"file_path": "a.md", "title": "Doc A", "area": "test"}, "content": "y"},
            {"id": "b.md::chunk_0", "score": 0.8, "metadata": {"file_path": "b.md", "title": "Doc B", "area": "test"}, "content": "z"},
        ]
        reranked = rerank_results(results, query="test", deduplicate=True)
        file_paths = [r["metadata"]["file_path"] for r in reranked]
        assert len(set(file_paths)) == len(file_paths)

    def test_metadata_boost_for_title_match(self):
        results = [
            {"id": "a.md::chunk_0", "score": 0.5, "metadata": {"file_path": "a.md", "title": "Budget Report", "area": "finance"}, "content": "x"},
            {"id": "b.md::chunk_0", "score": 0.6, "metadata": {"file_path": "b.md", "title": "Exercise", "area": "health"}, "content": "y"},
        ]
        reranked = rerank_results(results, query="budget report", deduplicate=False)
        assert reranked[0]["metadata"]["title"] == "Budget Report"

    def test_rerank_empty_list(self):
        assert rerank_results([], query="test") == []

    def test_area_boost(self):
        results = [
            {"id": "a.md::chunk_0", "score": 0.5, "metadata": {"file_path": "a.md", "title": "Notes", "area": "finance"}, "content": "x"},
            {"id": "b.md::chunk_0", "score": 0.5, "metadata": {"file_path": "b.md", "title": "Notes", "area": "health"}, "content": "y"},
        ]
        reranked = rerank_results(results, query="finance notes", deduplicate=False)
        assert reranked[0]["metadata"]["area"] == "finance"
