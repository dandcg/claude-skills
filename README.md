# Claude Code Skills

Custom skills for [Claude Code](https://docs.anthropic.com/en/docs/claude-code) that extend its capabilities with external service integrations and local tooling.

## Skills

### [Outlook](./outlook/) - Email & Calendar
Microsoft 365 email and calendar via Microsoft Graph API. Read inbox, send emails (with markdown formatting), manage drafts, handle attachments up to 150MB, view calendar, check availability, and create events.

### [Trello](./trello/) - Board Management
Trello boards, lists, and cards via the REST API. List boards, create/update/move/archive cards, add comments, manage positions, and smart-sort cards by category.

### [Repo Search](./repo-search/) - Semantic Search
ChromaDB-powered semantic search across a directory of markdown files. Find information by meaning rather than keywords, filter by area or date range, and build summaries from relevant chunks. Great for personal knowledge bases / second brains.

### [PST to Markdown](./pst-to-markdown/) - PST Extraction
Python tool for extracting Outlook PST files into organised markdown archives with integrity verification. Supports full and incremental extraction, attachment handling, and produces a searchable directory of emails with YAML frontmatter. Pairs well with Repo Search for semantic search across extracted archives.

### [Email Search](./email-search/) - Email Archive & Search
Python CLI tool for ingesting PST email archives into ChromaDB with automatic semantic embeddings. Search, analytics (timelines, top contacts, activity patterns), and export to markdown.

## Installation

Clone the repo and run the install script:

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

Skills are **symlinked** — edits to this repo are immediately live in Claude Code, no re-install needed after `git pull`.

Each skill's `SKILL.md` uses [Claude Code's skill format](https://docs.anthropic.com/en/docs/claude-code) with YAML frontmatter for automatic discovery.

## Skill Structure

All skills follow a consistent layout:

```
skill-name/
  SKILL.md            # Skill definition (YAML frontmatter + usage docs)
  README.md           # Human-readable documentation
  install.sh          # Automated installer
  scripts/            # Executable scripts
  references/         # Setup guides & manual instructions
```

## Credentials

No secrets are stored in this repo. Each skill externalises credentials:

| Skill | Credential Location | Setup |
|-------|-------------------|-------|
| Outlook | `~/.outlook/` | `outlook/scripts/outlook-setup.sh` |
| Trello | `~/.trello/` | `trello/scripts/trello-setup.sh` |
| Repo Search | None (local only) | `repo-search/setup.sh` |
| PST to Markdown | None (local only) | `pst-to-markdown/setup.sh` |
| Email Search | None (local only) | `email-search/setup.sh` |

## Requirements

| Skill | Dependencies |
|-------|-------------|
| Outlook | azure-cli, jq, curl, pandoc (optional) |
| Trello | jq, curl |
| Repo Search | Python 3, pip |
| PST to Markdown | Python 3, pip, readpst (optional fallback) |
| Email Search | Python 3, pip |

## License

MIT
