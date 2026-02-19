#!/usr/bin/env python3
"""Ingest web clips into repo-search ChromaDB for semantic search.

Usage:
    ingest.py [--clips-dir DIR] [--collection NAME]

Requires repo-search skill to be installed.
"""

import argparse
import subprocess
import sys
from pathlib import Path

DEFAULT_CLIPS_DIR = Path.home() / "web-clips"
REPO_SEARCH_INGEST = Path.home() / ".claude" / "skills" / "repo-search" / "ingest.py"
REPO_SEARCH_VENV_PYTHON = Path.home() / ".claude" / "skills" / "repo-search" / ".venv" / "bin" / "python"


def main():
    parser = argparse.ArgumentParser(description="Ingest clips into repo-search ChromaDB")
    parser.add_argument("--clips-dir", type=Path, default=DEFAULT_CLIPS_DIR)
    parser.add_argument("--collection", default="web-clips", help="ChromaDB collection name")
    args = parser.parse_args()

    if not args.clips_dir.exists():
        print(f"Error: Clips directory not found: {args.clips_dir}", file=sys.stderr)
        sys.exit(1)

    clip_count = len(list(args.clips_dir.glob("*.md")))
    if clip_count == 0:
        print("No clips to ingest.")
        return

    if not REPO_SEARCH_INGEST.exists():
        print("Error: repo-search skill not found. Install it first:", file=sys.stderr)
        print(f"  Expected: {REPO_SEARCH_INGEST}", file=sys.stderr)
        sys.exit(1)

    if not REPO_SEARCH_VENV_PYTHON.exists():
        print("Error: repo-search venv not found. Run repo-search/setup.sh first.", file=sys.stderr)
        sys.exit(1)

    print(f"Ingesting {clip_count} clip(s) from {args.clips_dir}...")

    result = subprocess.run(
        [
            str(REPO_SEARCH_VENV_PYTHON),
            str(REPO_SEARCH_INGEST),
            str(args.clips_dir),
            "--collection", args.collection,
        ],
        capture_output=True,
        text=True,
    )

    if result.returncode != 0:
        print(f"Error during ingestion:\n{result.stderr}", file=sys.stderr)
        sys.exit(1)

    print(result.stdout)
    print(f"Done. Clips are now searchable via repo-search with --collection {args.collection}")


if __name__ == "__main__":
    main()
