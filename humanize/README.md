# Humanize

Rewrite AI-generated text to sound natural and pass AI detection tools (GPTZero, Turnitin, Originality.ai) while preserving meaning exactly.

## Engines

### Claude Engine (default)

Uses Claude Code's own conversation to rewrite text via a multi-pass prompt workflow. No extra API calls or costs.

**4-pass process:**
1. **Structural deconstruction** - varies sentence/paragraph length to increase burstiness
2. **Diction & voice** - replaces predictable AI word choices with natural alternatives
3. **Banned patterns purge** - removes 60+ known AI-telltale words and phrases
4. **Self-audit** - verifies meaning preservation and catches remaining patterns

### Undetectable AI Engine (optional)

Commercial API at ~$10/month. Submit text and receive humanized version.

## Setup

No setup required for the Claude engine.

For the commercial API:

```bash
~/.claude/skills/humanize/scripts/setup.sh
```

You'll need an API key from [Undetectable AI](https://undetectable.ai/develop).

## Usage

```
"humanize this: [text]"
"humanize draft.md"
"humanize my clipboard"
"humanize draft.md in a casual tone"
"humanize draft.md with heavy rewriting"
"humanize draft.md using undetectable"
"humanize draft.md and save to output.md"
```

## Options

| Option | Values | Default |
|--------|--------|---------|
| Tone | neutral, casual, professional, academic | neutral |
| Aggressiveness | light, moderate, heavy | moderate |
| Engine | claude, undetectable | claude |
| Output | conversation, save to file | conversation |

## Credentials

| Item | Location |
|------|----------|
| Undetectable AI key | `~/.humanize/config.json` |

## Requirements

- Claude Code subscription (for Claude engine)
- python3, requests (for commercial API engine only)
