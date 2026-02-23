# Design: Garmin Import Skill

**Date:** 2026-02-22
**Status:** Approved

## Overview

A Python-native Claude skill for querying and importing Garmin Connect health and fitness data. Supports on-demand live queries and periodic markdown imports into the second brain.

## Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Data scope | Everything — training, vitals, sleep, activities | Full picture enables cross-domain correlation (TRT, CPAP, HYROX) |
| Use case | On-demand queries + periodic imports | Quick checks daily, structured snapshots for reviews |
| Auth method | Garmin Connect web session via `garminconnect` Python lib | Unofficial but mature, no partner agreement needed, full data access |
| Snapshot cadence | Daily files + weekly rollups | Dailies for granular data, rollups feed weekly review cadence |
| Credentials | `~/.garmin/config.json` | Consistent with Outlook/Trello skill pattern |
| Language | Python-native (like repo-search) | `garminconnect` is Python — no language boundary overhead |

## Skill Structure

```
garmin/
├── SKILL.md                  # Metadata + command reference
├── README.md                 # Quick start guide
├── requirements.txt          # garminconnect, etc.
├── scripts/
│   ├── setup.sh              # Bash: prompts for creds, creates venv, installs deps
│   ├── garmin_client.py      # Shared: session management, auth, token caching
│   ├── garmin_health.py      # Query: daily vitals (Body Battery, stress, HR, HRV)
│   ├── garmin_sleep.py       # Query: sleep data (score, stages, duration)
│   ├── garmin_activities.py  # Query: activities + training metrics (VO2, load, readiness)
│   ├── garmin_snapshot.py    # Import: pull a day's data → markdown file
│   └── garmin_rollup.py      # Import: aggregate a week's dailies → weekly rollup markdown
├── references/
│   └── setup.md              # Manual setup walkthrough
└── .venv/                    # Created by setup.sh
```

## Authentication & Session Management

### Credentials

Stored at `~/.garmin/config.json` (chmod 600):

```json
{
  "email": "user@example.com",
  "password": "..."
}
```

### Session Caching

Session tokens cached at `~/.garmin/session.json`. On each call:

1. Load cached session from `~/.garmin/session.json`
2. If valid, use it (avoids re-login)
3. If expired/missing, re-authenticate with email/password from config
4. Save new session back to `~/.garmin/session.json`

### MFA Handling

Garmin Connect sometimes prompts for MFA. The `garminconnect` library handles this — if MFA is triggered, the script prompts for the code interactively. Only happens on first login or full session expiry.

### setup.sh

1. Prompt for email + password, write to `~/.garmin/config.json` (chmod 600)
2. Create Python venv in `.venv/`
3. Install requirements
4. Test authentication — trial login and confirm success

## Commands

### On-Demand Queries

| Command | Script | Returns |
|---------|--------|---------|
| `today` | `garmin_health.py today` | Today's Body Battery, stress, resting HR, HRV, steps, calories |
| `sleep [date]` | `garmin_sleep.py <date>` | Sleep score, stages breakdown, duration, start/end times |
| `activities [days]` | `garmin_activities.py <days>` | Recent activities — type, duration, distance, HR zones, training effect |
| `training` | `garmin_activities.py training` | VO2 max, training status, training load, training readiness |
| `health [date]` | `garmin_health.py <date>` | Full vitals for a specific date |
| `week` | `garmin_health.py week` | Last 7 days of vitals as a summary table |

### Periodic Import

| Command | Script | Produces |
|---------|--------|----------|
| `snapshot [date]` | `garmin_snapshot.py <date>` | `areas/health/garmin/YYYY-MM-DD.md` — all data for that day |
| `rollup [week]` | `garmin_rollup.py <week>` | `areas/health/garmin/weekly/YYYY-WXX.md` — aggregated weekly trends |

All commands default to today/current when no date specified. Dates accept `YYYY-MM-DD` or `yesterday`.

### SKILL.md Trigger Phrases

```yaml
description: >
  Use for Garmin health and fitness data - body battery, sleep, VO2 max,
  training load, heart rate, HRV, stress, activities. Trigger on phrases
  like "garmin", "body battery", "sleep score", "vo2 max", "training load",
  "fitness data", "pull garmin", "garmin snapshot".
```

## Markdown Output Formats

### Daily Snapshot (`areas/health/garmin/YYYY-MM-DD.md`)

```markdown
# Garmin Daily: YYYY-MM-DD

## Vitals
| Metric | Value |
|--------|-------|
| Resting HR | 58 bpm |
| HRV | 42 ms |
| Body Battery | 75 → 22 |
| Stress | Avg 34 |
| Steps | 8,432 |
| Calories | 2,180 |

## Sleep (previous night)
| Metric | Value |
|--------|-------|
| Score | 82 |
| Duration | 7h 24m |
| Deep | 1h 12m |
| Light | 3h 48m |
| REM | 2h 06m |
| Awake | 18m |

## Activities
### Morning — HYROX Training (58 min)
- Avg HR: 152 bpm | Max HR: 178 bpm
- Calories: 620
- Training Effect: Aerobic 3.8 / Anaerobic 2.1

## Training Status
| Metric | Value |
|--------|-------|
| VO2 Max | 44 |
| Training Load | 412 |
| Training Readiness | 62 |
| Training Status | Productive |
```

### Weekly Rollup (`areas/health/garmin/weekly/YYYY-WXX.md`)

```markdown
# Garmin Weekly: YYYY-WXX

## Trends
| Metric | Mon | Tue | Wed | Thu | Fri | Sat | Sun | Avg |
|--------|-----|-----|-----|-----|-----|-----|-----|-----|
| Resting HR | 58 | 56 | 59 | 57 | 55 | 58 | 56 | 57 |
| HRV | 42 | 45 | 38 | 44 | 48 | 41 | 46 | 43 |
| Body Battery Peak | 75 | 80 | 68 | 78 | 82 | 71 | 84 | 77 |
| Sleep Score | 82 | 78 | 71 | 85 | 88 | 74 | 80 | 80 |
| Steps | 8.4k | 10.1k | 6.2k | 9.3k | 11.0k | 7.8k | 5.1k | 8.3k |

## Activities (4 sessions)
- Mon: HYROX Training — 58 min, TE 3.8
- Wed: Running — 35 min, 5.2 km
- Fri: HYROX Training — 62 min, TE 4.0
- Sat: Walking — 45 min, 4.1 km

## Highlights
- **Best sleep:** Friday (88)
- **Highest HRV:** Friday (48)
- **Training load trend:** Maintaining (412)
- **VO2 Max:** 44 (stable)

## Notes
[Auto-generated — edit to add context during weekly review]
```

## Error Handling

| Scenario | Behaviour |
|----------|-----------|
| Session expired | Auto-refresh using stored credentials, transparent to user |
| Wrong credentials | Clear error, prompt to re-run `setup.sh` |
| MFA triggered | Interactive prompt for code, cache new session |
| Garmin service down | 30s timeout, clear error, suggest retry |
| No data for date | File created with "No data recorded" in empty sections |
| Partial data | Populate available sections, "No data" for the rest |
| Duplicate snapshot | Overwrites — always pulls fresh data (idempotent) |
| Missing daily files for rollup | Pulls directly from Garmin API — doesn't require prior snapshots |
| Rate limiting | Exponential backoff, 3 retries max |

## File Paths

| Component | Path |
|-----------|------|
| Skill source | `/home/devops/claude-skills/garmin/` |
| Symlink | `~/.claude/skills/garmin` → `/home/devops/claude-skills/garmin/` |
| Credentials | `~/.garmin/config.json` |
| Session cache | `~/.garmin/session.json` |
| Daily snapshots | `/home/devops/brain/areas/health/garmin/YYYY-MM-DD.md` |
| Weekly rollups | `/home/devops/brain/areas/health/garmin/weekly/YYYY-WXX.md` |

## Integration Points

- **Daily review:** Run `snapshot` to capture the day's data before writing the review
- **Weekly review:** Run `rollup` to generate the weekly summary, reference in `reviews/weekly/`
- **Health correlations:** Daily files sit alongside TRT, sleep apnoea, and fitness tracking data in `areas/health/`
- **repo-search:** All markdown output is indexable by the repo-search skill for semantic queries across health data
