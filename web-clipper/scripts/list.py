#!/usr/bin/env python3
"""List saved web clips with optional filters.

Usage:
    list.py [--domain DOMAIN] [--tag TAG] [--after DATE] [--before DATE] [--format text|json]
"""

import argparse
import json
import sys
from pathlib import Path

import yaml

DEFAULT_CLIPS_DIR = Path.home() / "web-clips"


def parse_frontmatter(filepath: Path) -> dict | None:
    """Parse YAML frontmatter from a markdown file."""
    try:
        text = filepath.read_text(encoding="utf-8")
    except (OSError, UnicodeDecodeError):
        return None

    if not text.startswith("---"):
        return None

    parts = text.split("---\n", 2)
    if len(parts) < 3:
        return None

    try:
        fm = yaml.safe_load(parts[1])
    except yaml.YAMLError:
        return None

    if not isinstance(fm, dict):
        return None

    fm["filename"] = filepath.name
    return fm


def list_clips(
    clips_dir: Path,
    domain: str | None = None,
    tag: str | None = None,
    after: str | None = None,
    before: str | None = None,
) -> list[dict]:
    """List clips with optional filters. Returns list of frontmatter dicts."""
    if not clips_dir.exists():
        return []

    clips = []
    for f in clips_dir.glob("*.md"):
        fm = parse_frontmatter(f)
        if fm is None:
            continue

        # Apply filters
        if domain and fm.get("domain") != domain:
            continue
        if tag and tag not in (fm.get("tags") or []):
            continue
        if after:
            clipped = (fm.get("date_clipped") or "")[:10]
            if clipped < after:
                continue
        if before:
            clipped = (fm.get("date_clipped") or "")[:10]
            if clipped > before:
                continue

        clips.append(fm)

    # Sort newest first
    clips.sort(key=lambda c: c.get("date_clipped", ""), reverse=True)
    return clips


def main():
    parser = argparse.ArgumentParser(description="List saved web clips")
    parser.add_argument("--clips-dir", type=Path, default=DEFAULT_CLIPS_DIR)
    parser.add_argument("--domain", help="Filter by domain")
    parser.add_argument("--tag", help="Filter by tag")
    parser.add_argument("--after", help="Only clips after this date (YYYY-MM-DD)")
    parser.add_argument("--before", help="Only clips before this date (YYYY-MM-DD)")
    parser.add_argument("--format", "-f", choices=["text", "json"], default="text")
    args = parser.parse_args()

    clips = list_clips(args.clips_dir, args.domain, args.tag, args.after, args.before)

    if args.format == "json":
        print(json.dumps(clips, indent=2))
    else:
        if not clips:
            print("No clips found.")
            return
        for c in clips:
            tags = ", ".join(c.get("tags") or [])
            tag_str = f" [{tags}]" if tags else ""
            print(f"  {c.get('date_clipped', '?')[:10]}  {c.get('title', 'Untitled')}{tag_str}")
            print(f"             {c.get('url', '')}")


if __name__ == "__main__":
    main()
