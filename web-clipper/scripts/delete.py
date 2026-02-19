#!/usr/bin/env python3
"""Delete a saved web clip by filename or URL.

Usage:
    delete.py <filename>
    delete.py --url <url> [--clips-dir DIR]
"""

import argparse
import sys
from pathlib import Path

import yaml

DEFAULT_CLIPS_DIR = Path.home() / "web-clips"


def delete_clip(
    clips_dir: Path,
    filename: str | None = None,
    url: str | None = None,
) -> bool:
    """Delete a clip by filename or URL. Returns True if deleted."""
    if filename:
        filepath = clips_dir / filename
        if filepath.exists() and filepath.is_file():
            filepath.unlink()
            return True
        return False

    if url:
        for f in clips_dir.glob("*.md"):
            try:
                text = f.read_text(encoding="utf-8")
            except (OSError, UnicodeDecodeError):
                continue

            if not text.startswith("---"):
                continue
            parts = text.split("---\n", 2)
            if len(parts) < 3:
                continue
            try:
                fm = yaml.safe_load(parts[1])
            except yaml.YAMLError:
                continue

            if isinstance(fm, dict) and fm.get("url") == url:
                f.unlink()
                return True
        return False

    return False


def main():
    parser = argparse.ArgumentParser(description="Delete a saved web clip")
    parser.add_argument("filename", nargs="?", help="Clip filename to delete")
    parser.add_argument("--url", help="Delete clip matching this URL")
    parser.add_argument("--clips-dir", type=Path, default=DEFAULT_CLIPS_DIR)
    args = parser.parse_args()

    if not args.filename and not args.url:
        parser.error("Provide a filename or --url")

    deleted = delete_clip(args.clips_dir, filename=args.filename, url=args.url)

    if deleted:
        print("Clip deleted.")
    else:
        print("Error: Clip not found.", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
