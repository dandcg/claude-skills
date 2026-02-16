#!/usr/bin/env python3
"""
Query the vector search database.

Usage:
    # Similarity search
    python query.py search "what investments does Dan have?"

    # Search with filters
    python query.py search "health routines" --area health --top-k 10

    # List all indexed files
    python query.py list

    # Get stats about the database
    python query.py stats

    # Retrieve all chunks for a specific file
    python query.py file "areas/finance/index.md"

    # Retrieve chunks by area (for summarisation)
    python query.py area finance --top-k 50

    # Retrieve chunks by date range
    python query.py date-range 2025-01-01 2025-12-31 --top-k 50
"""

import argparse
import json
import os
import pickle
import sys
from pathlib import Path

import chromadb


def get_collection(db_path: Path):
    """Get the brain collection from ChromaDB."""
    if not db_path.exists():
        print(f"Error: Database not found at {db_path}", file=sys.stderr)
        print("Run ingest.py first to build the index.", file=sys.stderr)
        sys.exit(1)

    client = chromadb.PersistentClient(path=str(db_path))
    try:
        return client.get_collection("brain")
    except Exception:
        print("Error: 'brain' collection not found. Run ingest.py first.", file=sys.stderr)
        sys.exit(1)


def cmd_search(collection, query: str, top_k: int = 10, area: str = None,
               sub_area: str = None, output_format: str = "text"):
    """Similarity search against the vector DB."""
    where_filter = None
    conditions = []
    if area:
        conditions.append({"area": area})
    if sub_area:
        conditions.append({"sub_area": sub_area})

    if len(conditions) == 1:
        where_filter = conditions[0]
    elif len(conditions) > 1:
        where_filter = {"$and": conditions}

    results = collection.query(
        query_texts=[query],
        n_results=top_k,
        where=where_filter,
        include=["documents", "metadatas", "distances"],
    )

    if output_format == "json":
        output = []
        for i in range(len(results["ids"][0])):
            output.append({
                "id": results["ids"][0][i],
                "distance": results["distances"][0][i],
                "metadata": results["metadatas"][0][i],
                "content": results["documents"][0][i],
            })
        print(json.dumps(output, indent=2))
    else:
        if not results["ids"][0]:
            print("No results found.")
            return

        print(f"Query: {query}")
        if where_filter:
            print(f"Filter: {where_filter}")
        print(f"Results: {len(results['ids'][0])}")
        print("=" * 60)

        seen_files = {}
        for i in range(len(results["ids"][0])):
            meta = results["metadatas"][0][i]
            distance = results["distances"][0][i]
            content = results["documents"][0][i]
            file_path = meta["file_path"]

            # Track which files appear and their best score
            if file_path not in seen_files:
                seen_files[file_path] = distance

            similarity = 1 - distance  # cosine distance to similarity
            print(f"\n--- Result {i+1} [similarity: {similarity:.3f}] ---")
            print(f"File: {file_path}")
            print(f"Title: {meta.get('title', 'N/A')}")
            print(f"Area: {meta.get('area', 'N/A')}/{meta.get('sub_area', '')}")
            if meta.get("date"):
                print(f"Date: {meta['date']}")
            print(f"Chunk: {meta.get('chunk_index', '?')}/{meta.get('chunk_count', '?')}")
            print()
            # Truncate long content for display
            if len(content) > 500:
                print(content[:500] + "...")
            else:
                print(content)

        print("\n" + "=" * 60)
        print(f"\nFiles referenced (by best match):")
        for fp, dist in sorted(seen_files.items(), key=lambda x: x[1]):
            print(f"  {1-dist:.3f}  {fp}")


def cmd_area(collection, area: str, top_k: int = 50,
             output_format: str = "text"):
    """Retrieve all chunks for a given area."""
    results = collection.get(
        where={"area": area},
        include=["documents", "metadatas"],
        limit=top_k,
    )

    if output_format == "json":
        output = []
        for i in range(len(results["ids"])):
            output.append({
                "id": results["ids"][i],
                "metadata": results["metadatas"][i],
                "content": results["documents"][i],
            })
        print(json.dumps(output, indent=2))
    else:
        print(f"Area: {area}")
        print(f"Chunks: {len(results['ids'])}")
        print("=" * 60)
        for i in range(len(results["ids"])):
            meta = results["metadatas"][i]
            content = results["documents"][i]
            print(f"\n--- {meta['file_path']} [chunk {meta.get('chunk_index', '?')}] ---")
            if meta.get("title"):
                print(f"Title: {meta['title']}")
            print()
            print(content)


def cmd_file(collection, file_path: str, output_format: str = "text"):
    """Retrieve all chunks for a specific file."""
    results = collection.get(
        where={"file_path": file_path},
        include=["documents", "metadatas"],
    )

    if output_format == "json":
        output = []
        for i in range(len(results["ids"])):
            output.append({
                "id": results["ids"][i],
                "metadata": results["metadatas"][i],
                "content": results["documents"][i],
            })
        print(json.dumps(output, indent=2))
    else:
        if not results["ids"]:
            print(f"No chunks found for: {file_path}")
            return

        print(f"File: {file_path}")
        print(f"Chunks: {len(results['ids'])}")
        print("=" * 60)

        # Sort by chunk index
        indexed = list(zip(results["ids"], results["documents"], results["metadatas"]))
        indexed.sort(key=lambda x: x[2].get("chunk_index", 0))

        for chunk_id, content, meta in indexed:
            print(f"\n--- Chunk {meta.get('chunk_index', '?')}/{meta.get('chunk_count', '?')} ---")
            print(content)


def cmd_date_range(collection, start_date: str, end_date: str, top_k: int = 50,
                   output_format: str = "text"):
    """Retrieve chunks within a date range."""
    results = collection.get(
        where={
            "$and": [
                {"date": {"$gte": start_date}},
                {"date": {"$lte": end_date}},
            ]
        },
        include=["documents", "metadatas"],
        limit=top_k,
    )

    if output_format == "json":
        output = []
        for i in range(len(results["ids"])):
            output.append({
                "id": results["ids"][i],
                "metadata": results["metadatas"][i],
                "content": results["documents"][i],
            })
        print(json.dumps(output, indent=2))
    else:
        print(f"Date range: {start_date} to {end_date}")
        print(f"Chunks: {len(results['ids'])}")
        print("=" * 60)
        for i in range(len(results["ids"])):
            meta = results["metadatas"][i]
            content = results["documents"][i]
            print(f"\n--- {meta['file_path']} [{meta.get('date', 'no date')}] ---")
            print(content)


def cmd_list(collection, output_format: str = "text"):
    """List all indexed files with metadata."""
    results = collection.get(
        include=["metadatas"],
    )

    # Aggregate by file
    files = {}
    for meta in results["metadatas"]:
        fp = meta["file_path"]
        if fp not in files:
            files[fp] = {
                "title": meta.get("title", ""),
                "area": meta.get("area", ""),
                "sub_area": meta.get("sub_area", ""),
                "date": meta.get("date", ""),
                "chunks": 0,
            }
        files[fp]["chunks"] += 1

    if output_format == "json":
        print(json.dumps(files, indent=2))
    else:
        print(f"Indexed files: {len(files)}")
        print("=" * 60)
        for fp in sorted(files.keys()):
            info = files[fp]
            print(f"  {fp}")
            print(f"    Title: {info['title']} | Area: {info['area']}/{info['sub_area']} | "
                  f"Chunks: {info['chunks']} | Date: {info['date']}")


def cmd_stats(collection, output_format: str = "text"):
    """Show database statistics."""
    total_chunks = collection.count()

    results = collection.get(include=["metadatas"])

    # Aggregate stats
    areas = {}
    files = set()
    dates = []
    total_size = 0

    for meta in results["metadatas"]:
        area = meta.get("area", "unknown")
        areas[area] = areas.get(area, 0) + 1
        files.add(meta["file_path"])
        if meta.get("date"):
            dates.append(meta["date"])
        total_size += meta.get("chunk_length", 0)

    if output_format == "json":
        print(json.dumps({
            "total_chunks": total_chunks,
            "total_files": len(files),
            "total_content_size": total_size,
            "areas": areas,
            "date_range": {
                "earliest": min(dates) if dates else None,
                "latest": max(dates) if dates else None,
            },
        }, indent=2))
    else:
        print(f"=== Vector DB Stats ===")
        print(f"Total chunks: {total_chunks}")
        print(f"Total files: {len(files)}")
        print(f"Total content: {total_size:,} characters")
        if dates:
            print(f"Date range: {min(dates)} to {max(dates)}")
        print(f"\nChunks by area:")
        for area in sorted(areas.keys()):
            print(f"  {area}: {areas[area]}")


def _load_bm25(db_path: Path):
    """Load the BM25 index from disk."""
    bm25_path = db_path / "bm25_index.pkl"
    if not bm25_path.exists():
        return None
    with open(bm25_path, "rb") as f:
        return pickle.load(f)


def keyword_search(collection, db_path: Path, query: str, top_k: int = 10):
    """BM25 keyword search."""
    bm25_data = _load_bm25(db_path)
    if not bm25_data:
        return []
    tokenized_query = query.lower().split()
    scores = bm25_data["bm25"].get_scores(tokenized_query)
    top_indices = sorted(range(len(scores)), key=lambda i: scores[i], reverse=True)[:top_k]
    results = []
    for idx in top_indices:
        if scores[idx] > 0:
            results.append({
                "id": bm25_data["ids"][idx],
                "score": float(scores[idx]),
                "metadata": bm25_data["metadatas"][idx],
                "content": bm25_data["documents"][idx],
            })
    return results


def hybrid_search(collection, db_path: Path, query: str, top_k: int = 10,
                  area: str = None, sub_area: str = None):
    """Hybrid search combining vector similarity and BM25 via Reciprocal Rank Fusion."""
    # Vector search
    where_filter = None
    conditions = []
    if area:
        conditions.append({"area": area})
    if sub_area:
        conditions.append({"sub_area": sub_area})
    if len(conditions) == 1:
        where_filter = conditions[0]
    elif len(conditions) > 1:
        where_filter = {"$and": conditions}

    vector_results = collection.query(
        query_texts=[query],
        n_results=min(top_k * 2, collection.count()),
        where=where_filter,
        include=["documents", "metadatas", "distances"],
    )

    # BM25 search
    bm25_results = keyword_search(collection, db_path, query, top_k=top_k * 2)

    # Reciprocal Rank Fusion (k=60 is standard)
    k = 60
    rrf_scores = {}

    for rank, chunk_id in enumerate(vector_results["ids"][0], start=1):
        rrf_scores[chunk_id] = rrf_scores.get(chunk_id, 0) + 1.0 / (k + rank)

    for rank, result in enumerate(bm25_results, start=1):
        chunk_id = result["id"]
        rrf_scores[chunk_id] = rrf_scores.get(chunk_id, 0) + 1.0 / (k + rank)

    sorted_ids = sorted(rrf_scores.keys(), key=lambda x: rrf_scores[x], reverse=True)[:top_k]

    # Build result list
    all_vector_ids = vector_results["ids"][0]
    bm25_by_id = {r["id"]: r for r in bm25_results}
    results = []
    for chunk_id in sorted_ids:
        if chunk_id in all_vector_ids:
            idx = all_vector_ids.index(chunk_id)
            results.append({
                "id": chunk_id,
                "score": rrf_scores[chunk_id],
                "metadata": vector_results["metadatas"][0][idx],
                "content": vector_results["documents"][0][idx],
            })
        elif chunk_id in bm25_by_id:
            br = bm25_by_id[chunk_id]
            results.append({
                "id": chunk_id,
                "score": rrf_scores[chunk_id],
                "metadata": br["metadata"],
                "content": br["content"],
            })

    return results


def rerank_results(results: list, query: str, deduplicate: bool = True) -> list:
    """Lightweight reranking: deduplication and metadata boosting."""
    if not results:
        return results

    query_terms = set(query.lower().split())

    boosted = []
    for r in results:
        boost = 0.0
        title = r["metadata"].get("title", "").lower()
        area = r["metadata"].get("area", "").lower()
        title_words = set(title.split())
        overlap = query_terms & title_words
        boost += len(overlap) * 0.05
        if area in query_terms:
            boost += 0.02
        boosted.append({**r, "score": r["score"] + boost})

    boosted.sort(key=lambda x: x["score"], reverse=True)

    if deduplicate:
        seen_files = set()
        deduped = []
        for r in boosted:
            fp = r["metadata"]["file_path"]
            if fp not in seen_files:
                seen_files.add(fp)
                deduped.append(r)
        return deduped

    return boosted


def main():
    parser = argparse.ArgumentParser(description="Query the vector search database")
    parser.add_argument(
        "--db-path",
        default=None,
        help="Path to ChromaDB storage (default: auto-detect from cwd)",
    )
    parser.add_argument(
        "--format", "-f",
        choices=["text", "json"],
        default="text",
        help="Output format (default: text)",
    )

    subparsers = parser.add_subparsers(dest="command", required=True)

    # search
    p_search = subparsers.add_parser("search", help="Similarity search")
    p_search.add_argument("query", help="Search query")
    p_search.add_argument("--top-k", "-k", type=int, default=10, help="Number of results")
    p_search.add_argument("--area", help="Filter by area")
    p_search.add_argument("--sub-area", help="Filter by sub-area")
    p_search.add_argument("--mode", choices=["semantic", "keyword", "hybrid"],
                           default="semantic", help="Search mode (default: semantic)")

    # list
    subparsers.add_parser("list", help="List indexed files")

    # stats
    subparsers.add_parser("stats", help="Show database statistics")

    # file
    p_file = subparsers.add_parser("file", help="Get chunks for a file")
    p_file.add_argument("file_path", help="Relative file path")

    # area
    p_area = subparsers.add_parser("area", help="Get chunks by area")
    p_area.add_argument("area_name", help="Area name (e.g., finance, health)")
    p_area.add_argument("--top-k", "-k", type=int, default=50, help="Max chunks")

    # date-range
    p_date = subparsers.add_parser("date-range", help="Get chunks by date range")
    p_date.add_argument("start_date", help="Start date (YYYY-MM-DD)")
    p_date.add_argument("end_date", help="End date (YYYY-MM-DD)")
    p_date.add_argument("--top-k", "-k", type=int, default=50, help="Max chunks")

    args = parser.parse_args()

    # Auto-detect DB path
    if args.db_path:
        db_path = Path(args.db_path)
    else:
        # Walk up from cwd to find .vectordb
        cwd = Path.cwd()
        db_path = cwd / ".vectordb"
        if not db_path.exists():
            # Try repo root detection via git
            import subprocess
            try:
                result = subprocess.run(
                    ["git", "rev-parse", "--show-toplevel"],
                    capture_output=True, text=True, check=True
                )
                db_path = Path(result.stdout.strip()) / ".vectordb"
            except Exception:
                pass

    collection = get_collection(db_path)

    if args.command == "search":
        if args.mode == "semantic":
            cmd_search(collection, args.query, args.top_k, args.area,
                       getattr(args, "sub_area", None), args.format)
        elif args.mode == "keyword":
            results = keyword_search(collection, db_path, args.query, args.top_k)
            # Print results in similar format
            if args.format == "json":
                print(json.dumps(results, indent=2))
            else:
                print(f"Query: {args.query} (keyword mode)")
                print(f"Results: {len(results)}")
                print("=" * 60)
                for i, r in enumerate(results, 1):
                    print(f"\n--- Result {i} [BM25 score: {r['score']:.3f}] ---")
                    print(f"File: {r['metadata']['file_path']}")
                    content = r['content']
                    print(content[:500] + "..." if len(content) > 500 else content)
        elif args.mode == "hybrid":
            results = hybrid_search(collection, db_path, args.query, args.top_k,
                                    args.area, getattr(args, "sub_area", None))
            if args.format == "json":
                print(json.dumps(results, indent=2))
            else:
                print(f"Query: {args.query} (hybrid mode)")
                print(f"Results: {len(results)}")
                print("=" * 60)
                for i, r in enumerate(results, 1):
                    print(f"\n--- Result {i} [RRF score: {r['score']:.4f}] ---")
                    print(f"File: {r['metadata']['file_path']}")
                    content = r['content']
                    print(content[:500] + "..." if len(content) > 500 else content)
    elif args.command == "list":
        cmd_list(collection, args.format)
    elif args.command == "stats":
        cmd_stats(collection, args.format)
    elif args.command == "file":
        cmd_file(collection, args.file_path, args.format)
    elif args.command == "area":
        cmd_area(collection, args.area_name, args.top_k, args.format)
    elif args.command == "date-range":
        cmd_date_range(collection, args.start_date, args.end_date,
                       args.top_k, args.format)


if __name__ == "__main__":
    main()
