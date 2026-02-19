import json
import pytest
from pathlib import Path

import sys
import os

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "scripts"))

CLIP_TEMPLATE = """---
title: "{title}"
url: "{url}"
domain: "{domain}"
date_clipped: "{date_clipped}"
tags: {tags}
---

# {title}

Some content here.
"""


def _write_clip(clips_dir: Path, filename: str, title: str, url: str, domain: str, tags: list, date_clipped: str):
    tag_str = json.dumps(tags)
    content = CLIP_TEMPLATE.format(
        title=title, url=url, domain=domain, tags=tag_str, date_clipped=date_clipped,
    )
    (clips_dir / filename).write_text(content)


@pytest.fixture
def populated_clips_dir(tmp_clips_dir):
    """Create a clips dir with 3 test clips."""
    _write_clip(
        tmp_clips_dir, "2026-01-10-first.md",
        "First Article", "https://blog.example.com/first", "blog.example.com",
        ["python"], "2026-01-10T10:00:00",
    )
    _write_clip(
        tmp_clips_dir, "2026-02-15-second.md",
        "Second Article", "https://news.example.com/second", "news.example.com",
        ["rust", "python"], "2026-02-15T12:00:00",
    )
    _write_clip(
        tmp_clips_dir, "2026-02-19-third.md",
        "Third Article", "https://blog.example.com/third", "blog.example.com",
        [], "2026-02-19T08:00:00",
    )
    return tmp_clips_dir


class TestListClips:
    def test_lists_all_clips(self, populated_clips_dir):
        from list import list_clips

        results = list_clips(populated_clips_dir)
        assert len(results) == 3

    def test_newest_first(self, populated_clips_dir):
        from list import list_clips

        results = list_clips(populated_clips_dir)
        dates = [r["date_clipped"] for r in results]
        assert dates == sorted(dates, reverse=True)

    def test_filter_by_domain(self, populated_clips_dir):
        from list import list_clips

        results = list_clips(populated_clips_dir, domain="blog.example.com")
        assert len(results) == 2
        assert all(r["domain"] == "blog.example.com" for r in results)

    def test_filter_by_tag(self, populated_clips_dir):
        from list import list_clips

        results = list_clips(populated_clips_dir, tag="python")
        assert len(results) == 2

    def test_filter_by_date_range(self, populated_clips_dir):
        from list import list_clips

        results = list_clips(populated_clips_dir, after="2026-02-01")
        assert len(results) == 2

    def test_empty_dir_returns_empty(self, tmp_clips_dir):
        from list import list_clips

        results = list_clips(tmp_clips_dir)
        assert results == []
