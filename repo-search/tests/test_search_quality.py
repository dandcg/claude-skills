"""Search quality benchmarks - measures MRR to detect regressions."""
import chromadb
from ingest import ingest


GROUND_TRUTH = [
    ("quarterly revenue financial results", "q4-revenue"),
    ("exercise workout fitness routine", "exercise-routine"),
    ("python programming best practices", "python-best-practices"),
    ("API endpoints authentication REST", "api-specification"),
    ("budget marketing engineering costs", "budget-2025"),
]


def _compute_mrr(collection, ground_truth, top_k=10):
    reciprocal_ranks = []
    for query, expected_substring in ground_truth:
        results = collection.query(
            query_texts=[query],
            n_results=top_k,
            include=["metadatas"],
        )
        rr = 0.0
        for rank, meta in enumerate(results["metadatas"][0], start=1):
            if expected_substring in meta["file_path"]:
                rr = 1.0 / rank
                break
        reciprocal_ranks.append(rr)
    return sum(reciprocal_ranks) / len(reciprocal_ranks)


class TestSearchQuality:
    def test_mrr_above_threshold(self, ingested_db):
        client = chromadb.PersistentClient(path=str(ingested_db))
        collection = client.get_collection("brain")
        mrr = _compute_mrr(collection, GROUND_TRUTH)
        print(f"\n>>> MRR Score: {mrr:.3f} (threshold: 0.5)")
        assert mrr >= 0.5, f"MRR {mrr:.3f} is below minimum threshold 0.5"

    def test_top1_accuracy(self, ingested_db):
        client = chromadb.PersistentClient(path=str(ingested_db))
        collection = client.get_collection("brain")
        hits = 0
        for query, expected_substring in GROUND_TRUTH:
            results = collection.query(
                query_texts=[query],
                n_results=1,
                include=["metadatas"],
            )
            if results["metadatas"][0]:
                if expected_substring in results["metadatas"][0][0]["file_path"]:
                    hits += 1
        accuracy = hits / len(GROUND_TRUTH)
        print(f"\n>>> Top-1 Accuracy: {accuracy:.1%} ({hits}/{len(GROUND_TRUTH)})")
        assert accuracy >= 0.6, f"Top-1 accuracy {accuracy:.1%} below 60%"
