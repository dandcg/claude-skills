<div align="center">

# 🧩 Claude Code Skills

**Extend [Claude Code](https://docs.anthropic.com/en/docs/claude-code) with external service integrations and local tooling.**

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Claude Code](https://img.shields.io/badge/Claude_Code-Skills-blueviolet?logo=anthropic)](https://docs.anthropic.com/en/docs/claude-code)
[![Platform](https://img.shields.io/badge/Platform-Linux%20%7C%20macOS%20%7C%20WSL-lightgrey)]()

</div>

---

## 📦 Skills

| Skill | Description |
|-------|-------------|
| 📬 **[Outlook](./outlook/)** | Microsoft 365 email & calendar via Graph API — inbox, send, drafts, attachments (up to 150 MB), calendar & availability |
| 📋 **[Trello](./trello/)** | Board management via REST API — boards, lists, cards, comments, positions, smart-sort |
| 🔍 **[Repo Search](./repo-search/)** | ChromaDB semantic search across markdown, PDF, DOCX & XLSX — find by meaning, filter by area/date, build summaries |
| 📨 **[PST to Markdown](./pst-to-markdown/)** | Extract Outlook PST archives into organised markdown with YAML frontmatter & integrity verification |
| 📧 **[Email Search](./email-search/)** | Ingest PST archives into ChromaDB — semantic search, analytics, timelines, top contacts, export |
| 🛡️ **[FlareSolverr](./flaresolverr/)** | Bypass Cloudflare & anti-bot protection via a real browser in Docker — scrape any protected page |
| 📎 **[Web Clipper](./web-clipper/)** | Clip web pages to markdown with YAML frontmatter — clean extraction, Cloudflare bypass, tagging, full-text search, repo-search integration |
| ⌚ **[Garmin](./garmin/)** | Garmin Connect health & fitness data — Body Battery, HRV, sleep, activities, VO2 max, training load/readiness, daily snapshots & weekly rollups |

## 🚀 Installation

```bash
git clone https://github.com/dandcg/claude-skills.git
cd claude-skills

# Install all skills (symlinks into ~/.claude/skills/)
./install.sh --all

# Or pick specific ones
./install.sh outlook trello

# Or interactive mode
./install.sh
```

> Skills are **symlinked** — edits to this repo are immediately live in Claude Code. No re-install needed after `git pull`.

Each skill's `SKILL.md` uses [Claude Code's skill format](https://docs.anthropic.com/en/docs/claude-code) with YAML frontmatter for automatic discovery.

## 🗂️ Skill Structure

All skills follow a consistent layout:

```
skill-name/
  SKILL.md            # Skill definition (YAML frontmatter + usage docs)
  README.md           # Human-readable documentation
  setup.sh            # Automated first-time setup
  scripts/            # Executable scripts
  references/         # Setup guides & manual instructions
```

## 🔐 Credentials

No secrets are stored in this repo. Each skill externalises credentials:

| Skill | Credential Location | Setup |
|-------|---------------------|-------|
| 📬 Outlook | `~/.outlook/` | `outlook/scripts/outlook-setup.sh` |
| 📋 Trello | `~/.trello/` | `trello/scripts/trello-setup.sh` |
| 🔍 Repo Search | None (local only) | `repo-search/setup.sh` |
| 📨 PST to Markdown | None (local only) | `pst-to-markdown/setup.sh` |
| 📧 Email Search | None (local only) | `email-search/setup.sh` |
| 🛡️ FlareSolverr | None (Docker) | Auto-managed |
| 📎 Web Clipper | None (local only) | `web-clipper/setup.sh` |
| ⌚ Garmin | `~/.garmin/` | `garmin/scripts/setup.sh` |

## ⚙️ Requirements

| Skill | Dependencies |
|-------|-------------|
| 📬 Outlook | `azure-cli` · `jq` · `curl` · `pandoc` (optional) |
| 📋 Trello | `jq` · `curl` |
| 🔍 Repo Search | Python 3 · pip |
| 📨 PST to Markdown | Python 3 · pip · `readpst` (optional fallback) |
| 📧 Email Search | Python 3 · pip |
| 🛡️ FlareSolverr | Docker |
| 📎 Web Clipper | Python 3 · pip · Docker (optional) |
| ⌚ Garmin | Python 3 · pip |

## 📄 License

[MIT](LICENSE)
