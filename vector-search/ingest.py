#!/usr/bin/env python3
"""
Ingest markdown files into ChromaDB for vector search.

Usage:
    python ingest.py [REPO_ROOT] [--db-path PATH] [--dry-run] [--force] [--verbose]

Scans for markdown files, chunks them, generates embeddings, and stores
in a persistent ChromaDB database.
"""

import argparse
import hashlib
import json
import os
import re
import sys
import time
from datetime import datetime
from pathlib import Path

import chromadb
from langchain_text_splitters import MarkdownTextSplitter


# Directories to skip during scanning
SKIP_DIRS = {
    ".git", ".vectordb", "node_modules", ".venv", "__pycache__",
    "bin", "obj", ".vs", ".idea", ".claude"
}

# File patterns to skip
SKIP_FILES = {"TEMPLATE.md", "README.md"}

# Default chunk settings
DEFAULT_CHUNK_SIZE = 1000  # characters (~250 tokens)
DEFAULT_CHUNK_OVERLAP = 200  # characters overlap between chunks


def find_markdown_files(repo_root: Path) -> list[Path]:
    """Find all markdown files in the repo, skipping excluded directories."""
    md_files = []
    for root, dirs, files in os.walk(repo_root):
        # Filter out skip directories in-place
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]

        for f in files:
            if f.endswith(".md") and f not in SKIP_FILES:
                md_files.append(Path(root) / f)

    return sorted(md_files)


def extract_metadata(file_path: Path, repo_root: Path) -> dict:
    """Extract metadata from a markdown file's frontmatter and path."""
    content = file_path.read_text(encoding="utf-8")
    rel_path = str(file_path.relative_to(repo_root))

    # Determine area from path
    parts = rel_path.split("/")
    area = parts[0] if parts else "unknown"
    sub_area = parts[1] if len(parts) > 2 else ""

    # Extract title from first H1
    title_match = re.search(r"^#\s+(.+)$", content, re.MULTILINE)
    title = title_match.group(1).strip() if title_match else file_path.stem

    # Extract date from frontmatter or filename
    date = ""
    date_match = re.search(
        r"\*\*(?:Added|Date|Started):\*\*\s*(\d{4}-\d{2}-\d{2})",
        content
    )
    if date_match:
        date = date_match.group(1)
    else:
        # Try filename date pattern (e.g., 2025-02-01-something.md)
        fname_match = re.match(r"(\d{4}-\d{2}-\d{2})", file_path.name)
        if fname_match:
            date = fname_match.group(1)

    # Extract status if present
    status = ""
    status_match = re.search(
        r"\*\*Status:\*\*\s*(\w+)",
        content
    )
    if status_match:
        status = status_match.group(1)

    return {
        "file_path": rel_path,
        "area": area,
        "sub_area": sub_area,
        "title": title,
        "date": date,
        "status": status,
        "file_size": len(content),
    }


def chunk_markdown(content: str, chunk_size: int = DEFAULT_CHUNK_SIZE,
                   chunk_overlap: int = DEFAULT_CHUNK_OVERLAP) -> list[str]:
    """Split markdown content into chunks using markdown-aware splitter."""
    splitter = MarkdownTextSplitter(
        chunk_size=chunk_size,
        chunk_overlap=chunk_overlap,
    )
    chunks = splitter.split_text(content)
    # Filter out very short chunks (less than 50 chars)
    return [c for c in chunks if len(c.strip()) >= 50]


def compute_file_hash(file_path: Path) -> str:
    """Compute MD5 hash of file contents for change detection."""
    return hashlib.md5(file_path.read_bytes()).hexdigest()


def load_hash_cache(cache_path: Path) -> dict:
    """Load the file hash cache for incremental updates."""
    if cache_path.exists():
        return json.loads(cache_path.read_text())
    return {}


def save_hash_cache(cache_path: Path, cache: dict):
    """Save the file hash cache."""
    cache_path.parent.mkdir(parents=True, exist_ok=True)
    cache_path.write_text(json.dumps(cache, indent=2))


def ingest(
    repo_root: Path,
    db_path: Path,
    dry_run: bool = False,
    force: bool = False,
    verbose: bool = False,
    chunk_size: int = DEFAULT_CHUNK_SIZE,
    chunk_overlap: int = DEFAULT_CHUNK_OVERLAP,
):
    """Main ingestion pipeline."""

    print(f"=== Vector Search Ingestion ===")
    print(f"Repo root: {repo_root}")
    print(f"DB path:   {db_path}")
    print(f"Chunk size: {chunk_size} chars, overlap: {chunk_overlap} chars")
    print()

    # Find all markdown files
    md_files = find_markdown_files(repo_root)
    print(f"Found {len(md_files)} markdown files")

    if not md_files:
        print("No files to process")
        return

    # Load hash cache for incremental updates
    cache_path = db_path / "file_hashes.json"
    hash_cache = {} if force else load_hash_cache(cache_path)

    # Determine which files need processing
    files_to_process = []
    files_unchanged = 0
    for f in md_files:
        file_hash = compute_file_hash(f)
        rel_path = str(f.relative_to(repo_root))
        if not force and hash_cache.get(rel_path) == file_hash:
            files_unchanged += 1
            if verbose:
                print(f"  SKIP (unchanged): {rel_path}")
        else:
            files_to_process.append((f, file_hash))
            if verbose:
                action = "NEW" if rel_path not in hash_cache else "CHANGED"
                print(f"  {action}: {rel_path}")

    print(f"Files to process: {len(files_to_process)} (unchanged: {files_unchanged})")
    print()

    if dry_run:
        print("=== DRY RUN — no changes made ===")
        total_chunks = 0
        for f, _ in files_to_process:
            content = f.read_text(encoding="utf-8")
            chunks = chunk_markdown(content, chunk_size, chunk_overlap)
            total_chunks += len(chunks)
            meta = extract_metadata(f, repo_root)
            print(f"  {meta['file_path']}: {len(chunks)} chunks, "
                  f"area={meta['area']}, title={meta['title']}")
        print(f"\nTotal chunks: {total_chunks}")
        return

    if not files_to_process:
        print("Nothing to update — all files are current")
        return

    # Initialize ChromaDB
    print("Initialising ChromaDB...")
    db_path.mkdir(parents=True, exist_ok=True)
    client = chromadb.PersistentClient(path=str(db_path))
    collection = client.get_or_create_collection(
        name="brain",
        metadata={"hnsw:space": "cosine"},
    )

    # Process each file
    total_chunks = 0
    start_time = time.time()

    for i, (f, file_hash) in enumerate(files_to_process, 1):
        rel_path = str(f.relative_to(repo_root))
        content = f.read_text(encoding="utf-8")
        metadata = extract_metadata(f, repo_root)
        chunks = chunk_markdown(content, chunk_size, chunk_overlap)

        if not chunks:
            if verbose:
                print(f"  [{i}/{len(files_to_process)}] {rel_path}: no chunks (too short)")
            continue

        # Delete existing chunks for this file (in case of update)
        try:
            existing = collection.get(where={"file_path": rel_path})
            if existing["ids"]:
                collection.delete(ids=existing["ids"])
                if verbose:
                    print(f"  Deleted {len(existing['ids'])} old chunks for {rel_path}")
        except Exception:
            pass  # Collection might be empty

        # Prepare batch data
        ids = []
        documents = []
        metadatas = []

        for j, chunk in enumerate(chunks):
            chunk_id = f"{rel_path}::chunk_{j}"
            ids.append(chunk_id)
            documents.append(chunk)
            metadatas.append({
                **metadata,
                "chunk_index": j,
                "chunk_count": len(chunks),
                "chunk_length": len(chunk),
                "ingested_at": datetime.now().isoformat(),
            })

        # Add to collection
        collection.add(
            ids=ids,
            documents=documents,
            metadatas=metadatas,
        )

        total_chunks += len(chunks)
        # Update hash cache
        hash_cache[rel_path] = file_hash

        if verbose or i % 10 == 0 or i == len(files_to_process):
            print(f"  [{i}/{len(files_to_process)}] {rel_path}: "
                  f"{len(chunks)} chunks")

    elapsed = time.time() - start_time
    save_hash_cache(cache_path, hash_cache)

    # Print summary
    print()
    print(f"=== Ingestion Complete ===")
    print(f"Files processed: {len(files_to_process)}")
    print(f"Total chunks added: {total_chunks}")
    print(f"Total chunks in DB: {collection.count()}")
    print(f"Time: {elapsed:.1f}s")
    print(f"DB location: {db_path}")


def main():
    parser = argparse.ArgumentParser(description="Ingest markdown files into vector DB")
    parser.add_argument(
        "repo_root",
        nargs="?",
        default=os.getcwd(),
        help="Root directory to scan (default: cwd)",
    )
    parser.add_argument(
        "--db-path",
        default=None,
        help="Path for ChromaDB storage (default: REPO_ROOT/.vectordb)",
    )
    parser.add_argument("--dry-run", action="store_true", help="Show what would be done")
    parser.add_argument("--force", action="store_true", help="Re-ingest all files")
    parser.add_argument("--verbose", "-v", action="store_true", help="Verbose output")
    parser.add_argument("--chunk-size", type=int, default=DEFAULT_CHUNK_SIZE,
                        help=f"Chunk size in chars (default: {DEFAULT_CHUNK_SIZE})")
    parser.add_argument("--chunk-overlap", type=int, default=DEFAULT_CHUNK_OVERLAP,
                        help=f"Chunk overlap in chars (default: {DEFAULT_CHUNK_OVERLAP})")

    args = parser.parse_args()
    repo_root = Path(args.repo_root).resolve()
    db_path = Path(args.db_path) if args.db_path else repo_root / ".vectordb"

    ingest(
        repo_root=repo_root,
        db_path=db_path,
        dry_run=args.dry_run,
        force=args.force,
        verbose=args.verbose,
        chunk_size=args.chunk_size,
        chunk_overlap=args.chunk_overlap,
    )


if __name__ == "__main__":
    main()
