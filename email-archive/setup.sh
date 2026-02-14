#!/bin/bash
# Set up Python virtual environment for email-archive skill
set -e

SKILL_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "==> Setting up email-archive Python environment..."

# Create venv if it doesn't exist
if [ ! -d "$SKILL_DIR/.venv" ]; then
    python3 -m venv "$SKILL_DIR/.venv"
    echo "Created virtual environment"
fi

# Install/upgrade dependencies
echo "Installing dependencies..."
"$SKILL_DIR/.venv/bin/pip" install --upgrade pip setuptools wheel -q
"$SKILL_DIR/.venv/bin/pip" install -e "$SKILL_DIR" -q

echo "==> email-archive environment ready"
echo
echo "CLI available at: $SKILL_DIR/.venv/bin/email-archive"
echo
