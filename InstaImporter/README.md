# InstaImporter

Extracts factual knowledge from Instagram posts/reels shared in DMs and routes them to a second brain repository. Uses GPT-4o for content analysis and Whisper for video transcription.

## How It Works

1. **Parses** your Instagram data export to find posts/reels shared by a target user in DMs
2. **Fetches** public post content (captions and videos)
3. **Transcribes** videos using OpenAI Whisper
4. **Extracts** factual knowledge using GPT-4o with structured output
5. **Routes** high-confidence items to `areas/{category}/instagram-imports.md`
6. **Routes** low-confidence items to `inbox/` for manual review
7. **Generates** a cost and summary report in `outputs/`

## Prerequisites

- **.NET 10 SDK** (or .NET 8+)
- **OpenAI API key** with access to GPT-4o and Whisper
- **Instagram data export** — request via Settings > Your Activity > Download Your Information (JSON format)

## Setup

### 1. Configure

Copy `appsettings.json` to `appsettings.Development.json` and fill in your values:

```json
{
  "OpenAI": {
    "ApiKey": "sk-your-api-key",
    "Model": "gpt-4o"
  },
  "Instagram": {
    "ExportPath": "./path/to/instagram-export.zip",
    "TargetUsername": "the_username_who_shared_posts",
    "RateLimitMs": 2000,
    "MaxVideoSizeMb": 100
  },
  "Brain": {
    "RepoPath": "/path/to/your/brain-repo",
    "ConfidenceThreshold": 0.8
  }
}
```

| Setting | Description |
|---------|-------------|
| `OpenAI.ApiKey` | Your OpenAI API key |
| `OpenAI.Model` | Model for knowledge extraction (default: `gpt-4o`) |
| `Instagram.ExportPath` | Path to your Instagram data export ZIP |
| `Instagram.TargetUsername` | The DM contact whose shared posts you want to extract |
| `Instagram.RateLimitMs` | Delay between Instagram fetches to avoid rate limits (default: 2000ms) |
| `Instagram.MaxVideoSizeMb` | Skip videos larger than this (default: 100MB) |
| `Brain.RepoPath` | Root of your second brain / markdown repository |
| `Brain.ConfidenceThreshold` | Items above this confidence go to `areas/`, below go to `inbox/` (default: 0.8) |

### 2. Build & Run

```bash
cd InstaImporter
dotnet build

# Run with config from appsettings.json
dotnet run

# Override export path via CLI
dotnet run -- --export ./instagram-data.zip
```

## Output

The tool writes markdown files to your brain repository:

| Output | Path | Description |
|--------|------|-------------|
| Auto-categorised knowledge | `areas/{category}/instagram-imports.md` | Facts routed by GPT-4o category |
| Needs review | `inbox/instagram-import-{date}.md` | Items below the confidence threshold |
| Summary report | `outputs/instagram-import-summary-{date}.md` | Import stats, costs, and failures |

### Categories

Knowledge is categorised into these brain areas:

`health`, `relationships`, `finance`, `business`, `technical`, `philosophy`, `mental`, `career`, `income`, `general`

Items categorised as `general` or with low confidence scores are routed to `inbox/` for manual review.

## Estimated Costs

Costs depend on the number of posts and video duration:

| Component | Rate | Example (50 posts, ~1min video each) |
|-----------|------|---------------------------------------|
| Whisper | $0.006/min | ~$0.50 |
| GPT-4o | ~$0.02/post | ~$1.00 |
| **Total** | | **~$1.50** |

Actual costs are logged in the summary report after each run.

## Architecture

```
Program.cs                          # Pipeline orchestrator
├── InstagramExportParser           # Parses Instagram data export ZIP
├── InstagramContentFetcher         # Fetches post content (captions, video URLs)
│   └── Uses Polly retry policies + AngleSharp HTML parsing
├── WhisperTranscriptionService     # Transcribes video via OpenAI Whisper API
├── GptKnowledgeExtractor           # Extracts structured facts via GPT-4o
└── BrainWriter                     # Writes markdown to brain repo
```

## Troubleshooting

### "No items to process"
- Check that `TargetUsername` matches the exact Instagram username (not display name)
- Make sure your export contains DM data (the `messages/` directory in the ZIP)
- The export format should be JSON, not HTML

### Posts showing as "Failed" or "PostDeleted"
- The post may have been deleted or set to private since it was shared
- Instagram may rate-limit fetches — increase `RateLimitMs` if you see many failures
- The tool logs the status of each post: `✓` success, `✗` failed, `⊘` deleted, `○` no content

### Video transcription issues
- Videos larger than `MaxVideoSizeMb` are skipped
- Temporary video files are downloaded to the system temp directory and cleaned up after
- Whisper works best with clear audio; background music may reduce quality

### API costs unexpectedly high
- Check the summary report for actual costs
- Reduce the number of posts by filtering your export or targeting a specific date range
- Use `--export` to point at a smaller test export first

## Security Notes

- Store your API key in `appsettings.Development.json` (gitignored) — **not** in `appsettings.json`
- The `.gitignore` excludes `appsettings.*.json` files by default
- Instagram data exports contain personal information — handle with care
