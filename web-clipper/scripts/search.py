#!/usr/bin/env python3
"""Full-text search across saved web clips.

Usage:
    search.py <query> [--clips-dir DIR] [--limit N] [--format text|json]
"""

import argparse
import json
from pathlib import Path

import yaml

DEFAULT_CLIPS_DIR = Path.home() / "web-clips"


def parse_clip(filepath: Path) -> dict | None:
    """Parse a clip file into frontmatter + body."""
    try:
        text = filepath.read_text(encoding="utf-8")
    except (OSError, UnicodeDecodeError):
        return None

    fm = {}
    body = text
    if text.startswith("---"):
        parts = text.split("---\n", 2)
        if len(parts) >= 3:
            try:
                fm = yaml.safe_load(parts[1]) or {}
            except yaml.YAMLError:
                pass
            body = parts[2]

    fm["filename"] = filepath.name
    fm["body"] = body
    return fm


def search_clips(clips_dir: Path, query: str, limit: int = 20) -> list[dict]:
    """Search clips by matching query words against title and body."""
    if not clips_dir.exists():
        return []

    query_words = query.lower().split()
    results = []

    for f in clips_dir.glob("*.md"):
        clip = parse_clip(f)
        if clip is None:
            continue

        searchable = (
            (clip.get("title") or "") + " " + (clip.get("body") or "")
        ).lower()

        if all(word in searchable for word in query_words):
            results.append({
                "filename": clip["filename"],
                "title": clip.get("title", "Untitled"),
                "url": clip.get("url", ""),
            })

    return results[:limit]


def main():
    parser = argparse.ArgumentParser(description="Search saved web clips")
    parser.add_argument("query", help="Search query")
    parser.add_argument("--clips-dir", type=Path, default=DEFAULT_CLIPS_DIR)
    parser.add_argument("--limit", type=int, default=20)
    parser.add_argument("--format", "-f", choices=["text", "json"], default="text")
    args = parser.parse_args()

    results = search_clips(args.clips_dir, args.query, args.limit)

    if args.format == "json":
        print(json.dumps(results, indent=2))
    else:
        if not results:
            print(f"No clips match '{args.query}'.")
            return
        print(f"Found {len(results)} clip(s):")
        for r in results:
            print(f"  {r['title']}")
            print(f"    {r['url']}")


if __name__ == "__main__":
    main()
