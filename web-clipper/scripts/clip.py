#!/usr/bin/env python3
"""Clip a web page to clean markdown with YAML frontmatter.

Usage:
    clip.py <url> [--tags TAG,...] [--output-dir DIR] [--force-flaresolverr]
"""

import argparse
import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import urlparse

import requests
import trafilatura
import yaml
from slugify import slugify

DEFAULT_CLIPS_DIR = Path.home() / "web-clips"
FLARESOLVERR_URL = "http://localhost:8191/v1"
USER_AGENT = (
    "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
)


def fetch_url(url: str) -> tuple:
    """Fetch a URL and return (html, status_code)."""
    resp = requests.get(url, headers={"User-Agent": USER_AGENT}, timeout=30)
    return resp.text, resp.status_code


def fetch_with_flaresolverr(url: str, timeout_ms: int = 30000) -> str:
    """Fetch a URL via FlareSolverr. Returns HTML or raises."""
    resp = requests.post(
        FLARESOLVERR_URL,
        json={"cmd": "request.get", "url": url, "maxTimeout": timeout_ms},
        timeout=60,
    )
    data = resp.json()
    if data.get("status") != "ok":
        raise RuntimeError(f"FlareSolverr error: {data}")
    return data["solution"]["response"]


def is_cloudflare_challenge(html: str, status_code: int) -> bool:
    """Detect Cloudflare challenge pages."""
    if status_code == 403:
        return True
    if "Just a moment..." in html[:2000]:
        return True
    if "challenge-running" in html[:5000]:
        return True
    return False


def extract_article(html: str, url: str) -> dict:
    """Extract article content from HTML using trafilatura.

    Returns dict with keys: title, text, author, date, description
    or None if extraction fails.
    """
    if not html or not html.strip():
        return None

    text = trafilatura.extract(
        html,
        url=url,
        include_comments=False,
        include_tables=True,
        output_format="txt",
    )
    if not text or len(text.strip()) < 50:
        return None

    metadata = trafilatura.extract_metadata(html, default_url=url)

    return {
        "title": (metadata.title if metadata and metadata.title else None)
            or _extract_title_fallback(html),
        "text": text,
        "author": metadata.author if metadata else None,
        "date": metadata.date if metadata else None,
        "description": metadata.description if metadata else None,
    }


def _extract_title_fallback(html: str) -> str:
    """Extract title from <title> tag as fallback."""
    match = re.search(r"<title[^>]*>([^<]+)</title>", html, re.IGNORECASE)
    return match.group(1).strip() if match else "Untitled"


def generate_markdown(article: dict, url: str, tags: list) -> str:
    """Generate markdown with YAML frontmatter from extracted article."""
    domain = urlparse(url).netloc.removeprefix("www.")
    now = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S")

    frontmatter = {
        "title": article["title"] or "Untitled",
        "url": url,
        "domain": domain,
        "author": article.get("author"),
        "date_published": article.get("date"),
        "date_clipped": now,
        "tags": tags,
    }
    # Remove None values
    frontmatter = {k: v for k, v in frontmatter.items() if v is not None}

    fm_str = yaml.dump(frontmatter, default_flow_style=False, allow_unicode=True, sort_keys=False)
    title = article["title"] or "Untitled"

    return f"---\n{fm_str}---\n\n# {title}\n\n{article['text']}\n"


def generate_filename(title: str, date: str) -> str:
    """Generate a slugified filename from title and date."""
    slug = slugify(title, max_length=80)
    if not slug:
        slug = "untitled"
    name = f"{date}-{slug}.md"
    # Safety cap on total length
    if len(name) > 120:
        name = name[:116] + ".md"
    return name


def save_clip(content: str, title: str, output_dir: Path) -> Path:
    """Save markdown content to a file. Handles filename collisions."""
    output_dir.mkdir(parents=True, exist_ok=True)
    date_str = datetime.now(timezone.utc).strftime("%Y-%m-%d")
    filename = generate_filename(title, date_str)
    filepath = output_dir / filename

    # Handle collisions
    if filepath.exists():
        stem = filepath.stem
        suffix = filepath.suffix
        counter = 2
        while filepath.exists():
            filepath = output_dir / f"{stem}-{counter}{suffix}"
            counter += 1

    filepath.write_text(content, encoding="utf-8")
    return filepath


def main():
    parser = argparse.ArgumentParser(description="Clip a web page to markdown")
    parser.add_argument("url", help="URL to clip")
    parser.add_argument("--tags", default="", help="Comma-separated tags")
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=DEFAULT_CLIPS_DIR,
        help=f"Output directory (default: {DEFAULT_CLIPS_DIR})",
    )
    parser.add_argument(
        "--force-flaresolverr",
        action="store_true",
        help="Skip direct fetch, use FlareSolverr immediately",
    )
    parser.add_argument(
        "--format", "-f",
        choices=["text", "json"],
        default="text",
        help="Output format (default: text)",
    )
    args = parser.parse_args()

    tags = [t.strip() for t in args.tags.split(",") if t.strip()]

    # Fetch
    try:
        if args.force_flaresolverr:
            print("Fetching via FlareSolverr...", file=sys.stderr)
            html = fetch_with_flaresolverr(args.url)
            status_code = 200
        else:
            print(f"Fetching {args.url}...", file=sys.stderr)
            html, status_code = fetch_url(args.url)

            if is_cloudflare_challenge(html, status_code):
                print("Cloudflare detected, falling back to FlareSolverr...", file=sys.stderr)
                html = fetch_with_flaresolverr(args.url)
    except requests.RequestException as e:
        print(f"Error: Failed to fetch URL: {e}", file=sys.stderr)
        sys.exit(1)
    except RuntimeError as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)

    # Extract
    article = extract_article(html, args.url)
    if not article:
        print("Error: Could not extract article content from page", file=sys.stderr)
        sys.exit(1)

    # Generate and save
    markdown = generate_markdown(article, args.url, tags)
    filepath = save_clip(markdown, article["title"] or "Untitled", args.output_dir)

    # Output
    result = {
        "file": str(filepath),
        "title": article["title"],
        "url": args.url,
        "tags": tags,
        "size": len(markdown),
    }

    if args.format == "json":
        print(json.dumps(result, indent=2))
    else:
        print(f"Clipped: {article['title']}")
        print(f"Saved to: {filepath}")

    return 0


if __name__ == "__main__":
    sys.exit(main() or 0)
