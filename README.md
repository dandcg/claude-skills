# Claude Code Skills

Custom skills for [Claude Code](https://docs.anthropic.com/en/docs/claude-code) that extend its capabilities with external service integrations and local tooling.

## Skills

### [Outlook](./outlook/) - Email & Calendar
Microsoft 365 email and calendar via Microsoft Graph API. Read inbox, send emails (with markdown formatting), manage drafts, handle attachments up to 150MB, view calendar, check availability, and create events.

### [Trello](./trello/) - Board Management
Trello boards, lists, and cards via the REST API. List boards, create/update/move/archive cards, add comments, manage positions, and smart-sort cards by category.

### [Vector Search](./vector-search/) - Semantic Search
ChromaDB-powered semantic search across a directory of markdown files. Find information by meaning rather than keywords, filter by area or date range, and build summaries from relevant chunks. Great for personal knowledge bases / second brains.

### [Email Archive](./email-archive/) - PST Processing
.NET CLI tool for ingesting PST email archives into PostgreSQL with pgvector. Semantic search across historical emails, analytics (timelines, top contacts, activity patterns), and export to markdown.

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
| Vector Search | None (local only) | `vector-search/setup.sh` |
| Email Archive | `appsettings.*.json` / env vars | See README |

## Requirements

| Skill | Dependencies |
|-------|-------------|
| Outlook | azure-cli, jq, curl, pandoc (optional) |
| Trello | jq, curl |
| Vector Search | Python 3, pip |
| Email Archive | .NET 10, PostgreSQL with pgvector |

## License

MIT
