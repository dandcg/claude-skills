# Outlook Skill: Alternate Sender (`--from`) Flag

## Problem

The Outlook skill can only send emails from the authenticated user's primary email address. Users with configured aliases (e.g. `support@company.com`) cannot send from those addresses.

## Scope

This feature targets **alias-based send-as only** — email aliases configured on the user's own Microsoft 365 account. It does not cover shared mailbox delegation (which would require `Mail.Send.Shared` permissions).

## Solution

Add an optional `--from <email>` flag to all email-sending commands. When omitted, behaviour is unchanged (Graph API defaults to the primary address). When provided, the specified alias is set as the sender in the Graph API payload.

The `--from` flag can appear anywhere in the argument list after the command name. A helper function extracts it before positional args are read.

## Affected Commands

| Command | How `--from` is applied |
|---------|------------------------|
| `draft` | Added to the JSON payload in `POST /me/messages` |
| `mddraft` | Added to the JSON payload in `POST /me/messages` |
| `reply` | New conditional PATCH after `createReply` (extra API call only when `--from` is used) |
| `mdreply` | Added to the existing PATCH payload after `createReply` |
| `followup` | Added to the existing PATCH payload after `createReply` |

The `send` command is unchanged — it just sends an existing draft (the `from` address is already persisted on the draft at that point).

Note: The script already has a `from` command (filters emails by sender). There is no naming collision because the flag uses a `--from` double-dash prefix.

## Usage

```bash
# With --from (optional, can appear anywhere after the command name)
outlook-mail.sh draft --from "support@company.com" "to@example.com" "Subject" "Body"
outlook-mail.sh mddraft --from "support@company.com" "to@example.com" "Subject" "# Markdown"
outlook-mail.sh reply --from "support@company.com" <message-id> "Reply body"
outlook-mail.sh mdreply --from "support@company.com" <message-id> "Reply body"
outlook-mail.sh followup --from "support@company.com" <sent-message-id> "Follow-up body"

# Change sender on existing draft
outlook-mail.sh update <draft-id> from "support@company.com"

# Without --from (unchanged behaviour)
outlook-mail.sh draft "to@example.com" "Subject" "Body"
```

## Implementation Details

### 1. Helper function: `parse_from_flag()`

**Defined** near the top of `outlook-mail.sh` (with other utility functions). **Called** inside each affected `case` branch — not before the `case` block, since `$1` (the command name) must be dispatched first.

```bash
FROM_ADDRESS=""
REMAINING_ARGS=()

parse_from_flag() {
    FROM_ADDRESS=""
    local args=()
    while [[ $# -gt 0 ]]; do
        case "$1" in
            --from)
                if [[ -z "${2:-}" ]]; then
                    echo "Error: --from requires an email address" >&2
                    exit 1
                fi
                FROM_ADDRESS="$2"
                shift 2
                ;;
            *)
                args+=("$1")
                shift
                ;;
        esac
    done
    REMAINING_ARGS=("${args[@]}")
}
```

A helper to build the `from` JSON fragment, used by multiple commands:

```bash
build_from_json() {
    if [[ -n "$FROM_ADDRESS" ]]; then
        echo ', "from": {"emailAddress": {"address": "'"$FROM_ADDRESS"'"}}'
    fi
}
```

### 2. `draft` command — integration example

This shows the full pattern for how a `case` branch consumes `REMAINING_ARGS`:

```bash
draft)
    parse_from_flag "${@:2}"  # pass everything after the command name
    to="${REMAINING_ARGS[0]}"
    subject="${REMAINING_ARGS[1]}"
    body="${REMAINING_ARGS[2]:-}"
    if [ -z "$to" ] || [ -z "$subject" ]; then
        echo "Usage: outlook-mail.sh draft [--from <email>] <to-email> <subject> <body>"
        exit 1
    fi

    echo "Creating draft..."
    if [[ -n "$FROM_ADDRESS" ]]; then
        payload=$(jq -n \
            --arg to "$to" \
            --arg subject "$subject" \
            --arg body "$body" \
            --arg from "$FROM_ADDRESS" \
            '{
                subject: $subject,
                body: { contentType: "Text", content: $body },
                toRecipients: [{ emailAddress: { address: $to } }],
                from: { emailAddress: { address: $from } }
            }')
    else
        payload=$(jq -n \
            --arg to "$to" \
            --arg subject "$subject" \
            --arg body "$body" \
            '{
                subject: $subject,
                body: { contentType: "Text", content: $body },
                toRecipients: [{ emailAddress: { address: $to } }]
            }')
    fi
    # ... rest unchanged
```

### 3. `mddraft` command

Same pattern as `draft` — call `parse_from_flag "${@:2}"`, use `REMAINING_ARGS`, conditionally add `from` to the payload.

### 4. `reply` command

Currently uses `createReply` with a `{comment: $body}` payload and never PATCHes. When `--from` is set, add a new PATCH call after `createReply`:

```bash
if [[ -n "$FROM_ADDRESS" ]]; then
    from_payload=$(jq -n --arg from "$FROM_ADDRESS" '{from: {emailAddress: {address: $from}}}')
    api_call PATCH "/me/messages/$draft_id" "$from_payload" > /dev/null
fi
```

This is a conditional extra API round-trip that only fires when `--from` is used.

### 5. `mdreply` command

Already PATCHes the draft to set the HTML body. Add the `from` field to that same PATCH payload when `FROM_ADDRESS` is set — no extra API call.

### 6. `followup` command

Same as `mdreply` — add `from` to the existing PATCH payload.

### 7. `update` command

Add `from` as a new field in the `case` block (alongside `subject`, `body`, `to`, `cc`, `bcc`):

```bash
from)
    if [ -z "$value" ]; then
        echo "Error: Email address required"
        exit 1
    fi
    echo "Updating sender address..."
    payload=$(jq -n --arg email "$value" '{from: {emailAddress: {address: $email}}}')
    ;;
```

Also update the usage help text (lines 452-458) to list `from` as a valid field.

### 8. Documentation updates

- **SKILL.md**: Update the "Sending Email" section and workflow examples to show the `--from` flag. Add a note about alias requirements.
- **README.md**: Mention alternate sender alias support in the Outlook feature description.

## Error Handling

- **Missing `--from` value**: `parse_from_flag` exits with an error if `--from` is passed with no argument
- **Invalid alias**: The Graph API accepts any address at draft creation time but **rejects unauthorised addresses at send time**. When `--from` is used, the draft output should note: "From: support@company.com (validated on send)"
- **Non-sending commands**: If `--from` is accidentally passed to a read-only command, it will be ignored (not parsed since only sending commands call `parse_from_flag`)

## Prerequisites

The alias must be configured on the user's Microsoft 365 account by an admin. The Graph API will reject `from` addresses the account is not authorised to send as — this rejection happens at send time, not draft creation.

No new OAuth scopes or permissions are required — `Mail.Send` already covers send-as for configured aliases.

## What doesn't change

- `send` command (no payload, just triggers send on an existing draft)
- Token management and OAuth flow
- Config files (`~/.outlook/config.json`, `~/.outlook/credentials.json`)
- All read-only commands (inbox, unread, read, etc.)
- Default behaviour when `--from` is omitted
- The existing `from` command (filter by sender)

## Test Plan

Manual verification for each affected command, with and without `--from`:

- [ ] `draft` without `--from` — creates draft from primary (unchanged)
- [ ] `draft --from alias@co.com` — creates draft with `from` set to alias
- [ ] `mddraft --from alias@co.com` — creates HTML draft with `from` set
- [ ] `reply --from alias@co.com <id> "body"` — reply draft has `from` overridden
- [ ] `reply <id> "body"` — reply uses received-on address (unchanged)
- [ ] `mdreply --from alias@co.com <id> "body"` — reply draft has `from` set
- [ ] `followup --from alias@co.com <id>` — follow-up draft has `from` set
- [ ] `update <draft-id> from alias@co.com` — changes sender on existing draft
- [ ] `send <draft-id>` after `--from` draft — sends from alias successfully
- [ ] `--from` with no value — exits with error message
- [ ] `--from invalid-not-email` — draft created, error at send time
- [ ] Read-only commands unaffected (inbox, unread, read, etc.)
