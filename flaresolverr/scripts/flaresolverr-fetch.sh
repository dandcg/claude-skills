#!/bin/bash
# Fetch a URL via FlareSolverr, bypassing Cloudflare protection.
# Outputs full page HTML to stdout.
#
# Usage: flaresolverr-fetch.sh <url> [timeout_ms]
set -e

URL="${1:?Usage: flaresolverr-fetch.sh <url> [timeout_ms]}"
TIMEOUT="${2:-30000}"
PORT=8191
ENDPOINT="http://localhost:${PORT}/v1"

# Check FlareSolverr is running
if ! curl -s "http://localhost:${PORT}/" | grep -q "FlareSolverr is ready" 2>/dev/null; then
    echo "Error: FlareSolverr is not running. Run: ~/.claude/skills/flaresolverr/scripts/flaresolverr-ensure.sh" >&2
    exit 1
fi

# Fetch the URL
RESPONSE=$(curl -s -X POST "$ENDPOINT" \
    -H "Content-Type: application/json" \
    -d "{\"cmd\":\"request.get\",\"url\":\"${URL}\",\"maxTimeout\":${TIMEOUT}}")

# Check for success
STATUS=$(echo "$RESPONSE" | python3 -c "import sys,json; print(json.load(sys.stdin).get('status',''))" 2>/dev/null)

if [ "$STATUS" != "ok" ]; then
    echo "Error: FlareSolverr returned status '$STATUS'" >&2
    echo "$RESPONSE" | python3 -m json.tool >&2 2>/dev/null || echo "$RESPONSE" >&2
    exit 1
fi

# Output the HTML
echo "$RESPONSE" | python3 -c "import sys,json; print(json.load(sys.stdin)['solution']['response'])"
