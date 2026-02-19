import os
import pytest
from pathlib import Path


@pytest.fixture
def tmp_clips_dir(tmp_path):
    """Temporary directory for clip output."""
    clips_dir = tmp_path / "web-clips"
    clips_dir.mkdir()
    return clips_dir


@pytest.fixture
def sample_html():
    """Minimal HTML article page."""
    return """<!DOCTYPE html>
<html>
<head><title>Test Article Title</title></head>
<body>
<article>
<h1>Test Article Title</h1>
<p>By John Author</p>
<p>This is the first paragraph of a test article about Python programming.
It contains enough text to be recognised as real article content by extraction
libraries. We need several sentences to ensure the content is not dismissed
as boilerplate or navigation text.</p>
<p>The second paragraph continues with more substantial content about software
development practices. This helps ensure trafilatura identifies this as the
main content of the page rather than sidebar or footer material.</p>
<p>A third paragraph rounds out the article with concluding thoughts on the
topic. Having multiple paragraphs of reasonable length is important for
content extraction quality.</p>
</article>
</body>
</html>"""


@pytest.fixture
def cloudflare_html():
    """HTML that looks like a Cloudflare challenge page."""
    return """<!DOCTYPE html>
<html>
<head><title>Just a moment...</title></head>
<body>
<div class="challenge-running">Checking your browser before accessing the site.</div>
</body>
</html>"""
