# FlareSolverr Skill

Bypass Cloudflare and similar anti-bot protection when scraping URLs from Claude Code.

## What it does

Runs [FlareSolverr](https://github.com/FlareSolverr/FlareSolverr) as a Docker container, providing a local API that solves Cloudflare challenges using a real browser. When a URL returns 403 or a challenge page, this skill fetches the content through FlareSolverr instead.

## Requirements

- Docker (or Docker Desktop with WSL integration)

## Installation

```bash
cd ~/source/claude-skills
./install.sh flaresolverr
```

## Usage

Claude Code will automatically use this skill when encountering blocked URLs. You can also use the scripts directly:

```bash
# Ensure the container is running
~/.claude/skills/flaresolverr/scripts/flaresolverr-ensure.sh

# Fetch a protected page
~/.claude/skills/flaresolverr/scripts/flaresolverr-fetch.sh "https://example.com/protected"
```

## How it works

1. Docker container runs a real Chrome browser
2. Browser navigates to the URL and solves any Cloudflare challenges
3. Page HTML is returned via HTTP API on `localhost:8191`
4. Scripts wrap the API for easy command-line use

## Resource usage

- ~300–500MB RAM while running
- Container auto-restarts with Docker (set to `--restart unless-stopped`)
- ~5–10 seconds per request (real browser navigation)
