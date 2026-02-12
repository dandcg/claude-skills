#!/bin/bash
# Install vector-search skill to ~/.claude/skills/vector-search
# Run from the tools/vector-search directory
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TARGET_DIR="$HOME/.claude/skills/vector-search"

echo "=== Vector Search Skill Installer ==="
echo

# Check dependencies
echo "Checking dependencies..."
MISSING=""

if ! command -v python3 &> /dev/null; then
    MISSING="$MISSING python3"
fi

if [ -n "$MISSING" ]; then
    echo "Missing required dependencies:$MISSING"
    exit 1
fi

echo "All required dependencies found"
echo

# Check for existing installation
if [ -d "$TARGET_DIR" ]; then
    echo "Existing installation found at $TARGET_DIR"
    read -p "Overwrite? (y/N): " overwrite
    if [[ ! "$overwrite" =~ ^[Yy]$ ]]; then
        echo "Installation cancelled"
        exit 0
    fi
    # Preserve .venv if it exists
    if [ -d "$TARGET_DIR/.venv" ]; then
        echo "Preserving existing virtual environment..."
        mv "$TARGET_DIR/.venv" /tmp/_vs_venv_backup
    fi
    rm -rf "$TARGET_DIR"
fi

# Create target directory
echo "Installing to $TARGET_DIR..."
mkdir -p "$TARGET_DIR"

# Copy files
cp "$SCRIPT_DIR/SKILL.md" "$TARGET_DIR/"
cp "$SCRIPT_DIR/requirements.txt" "$TARGET_DIR/"
cp "$SCRIPT_DIR/setup.sh" "$TARGET_DIR/"
cp "$SCRIPT_DIR/ingest.py" "$TARGET_DIR/"
cp "$SCRIPT_DIR/query.py" "$TARGET_DIR/"

# Make scripts executable
chmod +x "$TARGET_DIR/setup.sh"

# Restore .venv if backed up
if [ -d /tmp/_vs_venv_backup ]; then
    echo "Restoring virtual environment..."
    mv /tmp/_vs_venv_backup "$TARGET_DIR/.venv"
fi

echo "Skill files installed!"
echo

# Set up Python environment
if [ ! -d "$TARGET_DIR/.venv" ]; then
    echo "Setting up Python environment..."
    "$TARGET_DIR/setup.sh"
else
    echo "Virtual environment already exists"
    echo "Checking dependencies are up to date..."
    "$TARGET_DIR/.venv/bin/pip" install -r "$TARGET_DIR/requirements.txt" -q
fi

echo
echo "=== Installation Complete ==="
echo
echo "Build the index (run once):"
echo "  $TARGET_DIR/.venv/bin/python $TARGET_DIR/ingest.py /path/to/your/markdown-repo --verbose"
echo
echo "Or use natural language in Claude Code:"
echo "  'search my brain for investment info'"
echo "  'what do I know about health routines'"
echo "  'rebuild the vector index'"
echo
