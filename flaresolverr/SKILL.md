---
name: flaresolverr
description: Use when any URL returns 403, a Cloudflare challenge page, or "Just a moment..." - bypasses anti-bot protection via a real browser in Docker. Trigger on phrases like "scrape", "fetch blocked", "403", "cloudflare", "can't access site".
---

# FlareSolverr — Bypass Cloudflare & Anti-Bot Protection

Fetch content from sites protected by Cloudflare, Akamai, or similar anti-bot systems. Runs a real browser in Docker that solves challenges, then returns page HTML via a local API.

## Prerequisites

- Docker available in current shell

## Setup

```bash
# Ensure container is running (idempotent)
~/.claude/skills/flaresolverr/scripts/flaresolverr-ensure.sh
```

The script pulls and starts the container if needed, or confirms it's already running.

## Usage

### Fetch a protected URL

```bash
~/.claude/skills/flaresolverr/scripts/flaresolverr-fetch.sh "https://example.com/protected-page"
```

Returns the full page HTML to stdout. Use with pipes or redirect to file:

```bash
# Save to file
~/.claude/skills/flaresolverr/scripts/flaresolverr-fetch.sh "https://example.com" > /tmp/page.html

# Multiple URLs
for url in "$URL1" "$URL2" "$URL3"; do
  ~/.claude/skills/flaresolverr/scripts/flaresolverr-fetch.sh "$url" > "/tmp/$(echo "$url" | md5sum | cut -c1-8).html"
done
```

### Python (direct API)

```python
import requests

def fetch_protected(url, timeout=30000):
    r = requests.post("http://localhost:8191/v1", json={
        "cmd": "request.get",
        "url": url,
        "maxTimeout": timeout
    })
    data = r.json()
    if data["status"] == "ok":
        return data["solution"]["response"]
    raise Exception(f"FlareSolverr error: {data}")
```

### Check status

```bash
curl -s http://localhost:8191/ | python3 -m json.tool
```

## Parsing Tips

- **JSON-LD:** Many sites embed structured data in `<script type="application/ld+json">` — check for this first
- **Main content:** Strip `<script>` and `<style>` tags, then extract text from the body
- **Indeed specifically:** Job description is in `<div id="jobDescriptionText">`, structured data (salary, location, company) is in JSON-LD

## Management

```bash
docker stop flaresolverr     # Stop
docker start flaresolverr    # Restart
docker rm -f flaresolverr    # Remove (re-run ensure script to recreate)
docker logs flaresolverr --tail 20  # Debug
```

## Limitations

- Sequential requests (~5–10s per URL) — single browser instance
- ~300–500MB RAM
- First request after startup is slower (~10s)
- Some sites may still block beyond Cloudflare
