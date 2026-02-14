#!/bin/bash
# Install Claude Code skills from this repo via symlinks
#
# Usage:
#   ./install.sh              # Install all skills (interactive)
#   ./install.sh --all        # Install all skills (no prompts)
#   ./install.sh outlook      # Install specific skill(s)
#   ./install.sh trello vector-search
#
# Skills are symlinked into ~/.claude/skills/ so edits to this repo
# are immediately available to Claude Code — no re-install needed.

set -e

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SKILLS_DIR="$HOME/.claude/skills"

# All available Claude Code skills (have SKILL.md)
AVAILABLE_SKILLS=(outlook trello vector-search pst-extract email-archive)

# Colours
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
BLUE='\033[0;34m'
BOLD='\033[1m'
NC='\033[0m'

info()  { echo -e "${BLUE}==>${NC} $*"; }
ok()    { echo -e "${GREEN}✓${NC} $*"; }
warn()  { echo -e "${YELLOW}!${NC} $*"; }
err()   { echo -e "${RED}✗${NC} $*"; }

# ─── Dependency checks ───────────────────────────────────────────────

check_deps_outlook() {
    local missing=""
    command -v az   &>/dev/null || missing="$missing azure-cli"
    command -v jq   &>/dev/null || missing="$missing jq"
    command -v curl &>/dev/null || missing="$missing curl"
    if [ -n "$missing" ]; then
        err "Missing dependencies for outlook:$missing"
        echo "    macOS: brew install$missing"
        echo "    Linux: sudo apt install$missing"
        return 1
    fi
    if ! command -v pandoc &>/dev/null; then
        warn "pandoc not found (optional — needed for markdown-formatted emails)"
    fi
    return 0
}

check_deps_trello() {
    local missing=""
    command -v jq   &>/dev/null || missing="$missing jq"
    command -v curl &>/dev/null || missing="$missing curl"
    if [ -n "$missing" ]; then
        err "Missing dependencies for trello:$missing"
        echo "    macOS: brew install$missing"
        echo "    Linux: sudo apt install$missing"
        return 1
    fi
    return 0
}

check_deps_vector_search() {
    if ! command -v python3 &>/dev/null; then
        err "Missing dependency for vector-search: python3"
        return 1
    fi
    return 0
}

check_deps_pst_extract() {
    if ! command -v python3 &>/dev/null; then
        err "Missing dependency for pst-extract: python3"
        return 1
    fi
    if ! command -v readpst &>/dev/null; then
        warn "readpst not found (optional — needed if libratom fails)"
        echo "    Ubuntu/Debian: sudo apt install pst-utils"
        echo "    macOS: brew install libpst"
    fi
    return 0
}

check_deps_email_archive() {
    if ! command -v python3 &>/dev/null; then
        err "Missing dependency for email-archive: python3"
        return 1
    fi
    return 0
}

check_deps() {
    local skill="$1"
    case "$skill" in
        outlook)        check_deps_outlook ;;
        trello)         check_deps_trello ;;
        vector-search)  check_deps_vector_search ;;
        pst-extract)    check_deps_pst_extract ;;
        email-archive)  check_deps_email_archive ;;
        *)              return 0 ;;
    esac
}

# ─── Install a single skill ──────────────────────────────────────────

install_skill() {
    local skill="$1"
    local source="$REPO_DIR/$skill"
    local target="$SKILLS_DIR/$skill"

    echo
    info "Installing ${BOLD}$skill${NC}"

    # Check source exists
    if [ ! -d "$source" ] || [ ! -f "$source/SKILL.md" ]; then
        err "$skill: not found or missing SKILL.md"
        return 1
    fi

    # Check dependencies
    if ! check_deps "$skill"; then
        warn "Skipping $skill (missing dependencies)"
        return 1
    fi

    # Handle existing installation
    if [ -L "$target" ]; then
        local current
        current="$(readlink "$target")"
        if [ "$current" = "$source" ]; then
            ok "$skill already linked → $source"
            # Still run post-install for things like .venv
            post_install "$skill"
            return 0
        fi
        info "Updating symlink (was → $current)"
        rm "$target"
    elif [ -d "$target" ]; then
        warn "Existing directory at $target (not a symlink)"
        if [ "$AUTO" = "1" ]; then
            info "Replacing with symlink (--all mode)"
        else
            read -p "    Replace with symlink? (y/N): " replace
            if [[ ! "$replace" =~ ^[Yy]$ ]]; then
                warn "Skipped $skill"
                return 0
            fi
        fi
        rm -rf "$target"
    fi

    # Create symlink
    mkdir -p "$SKILLS_DIR"
    ln -s "$source" "$target"
    ok "$skill → $source"

    # Post-install steps
    post_install "$skill"
}

post_install() {
    local skill="$1"

    case "$skill" in
        outlook)
            # Ensure scripts are executable
            chmod +x "$REPO_DIR/outlook/scripts/"*.sh 2>/dev/null || true
            chmod +x "$REPO_DIR/outlook/install.sh" 2>/dev/null || true

            if [ -f "$HOME/.outlook/credentials.json" ]; then
                ok "Outlook credentials found"
            else
                warn "No Outlook credentials — run: ~/.claude/skills/outlook/scripts/outlook-setup.sh"
            fi
            ;;

        trello)
            chmod +x "$REPO_DIR/trello/scripts/"*.sh 2>/dev/null || true

            if [ -f "$HOME/.trello/config.json" ]; then
                ok "Trello credentials found"
            else
                warn "No Trello credentials — run: ~/.claude/skills/trello/scripts/trello-setup.sh"
            fi
            ;;

        vector-search)
            chmod +x "$REPO_DIR/vector-search/setup.sh" 2>/dev/null || true
            chmod +x "$REPO_DIR/vector-search/ingest.py" 2>/dev/null || true
            chmod +x "$REPO_DIR/vector-search/query.py" 2>/dev/null || true

            # Set up Python venv if needed
            if [ ! -d "$REPO_DIR/vector-search/.venv" ]; then
                info "Setting up Python virtual environment..."
                "$REPO_DIR/vector-search/setup.sh"
            else
                ok "Python venv already exists"
                # Update deps quietly
                "$REPO_DIR/vector-search/.venv/bin/pip" install -r "$REPO_DIR/vector-search/requirements.txt" -q 2>/dev/null || true
            fi
            ;;

        pst-extract)
            chmod +x "$REPO_DIR/pst-extract/setup.sh" 2>/dev/null || true
            chmod +x "$REPO_DIR/pst-extract/scripts/extract_pst.py" 2>/dev/null || true

            # Set up Python venv if needed
            if [ ! -d "$REPO_DIR/pst-extract/.venv" ]; then
                info "Setting up Python virtual environment..."
                "$REPO_DIR/pst-extract/setup.sh"
            else
                ok "Python venv already exists"
                # Update deps quietly
                "$REPO_DIR/pst-extract/.venv/bin/pip" install -r "$REPO_DIR/pst-extract/requirements.txt" -q 2>/dev/null || true
            fi
            ;;

        email-archive)
            chmod +x "$REPO_DIR/email-archive/setup.sh" 2>/dev/null || true

            # Set up Python venv if needed
            if [ ! -d "$REPO_DIR/email-archive/.venv" ]; then
                info "Setting up Python virtual environment..."
                "$REPO_DIR/email-archive/setup.sh"
            else
                ok "Python venv already exists"
                # Update deps quietly
                "$REPO_DIR/email-archive/.venv/bin/pip" install -e "$REPO_DIR/email-archive" -q 2>/dev/null || true
            fi
            ;;
    esac
}

# ─── Main ─────────────────────────────────────────────────────────────

AUTO=0
REQUESTED_SKILLS=()

# Parse args
for arg in "$@"; do
    case "$arg" in
        --all|-a)
            AUTO=1
            ;;
        --help|-h)
            echo "Usage: $0 [--all] [skill ...]"
            echo
            echo "Available skills: ${AVAILABLE_SKILLS[*]}"
            echo
            echo "Options:"
            echo "  --all, -a    Install all skills without prompts"
            echo "  --help, -h   Show this help"
            echo
            echo "Examples:"
            echo "  $0                    # Interactive — choose which skills to install"
            echo "  $0 --all              # Install everything"
            echo "  $0 outlook trello     # Install specific skills"
            exit 0
            ;;
        *)
            REQUESTED_SKILLS+=("$arg")
            ;;
    esac
done

echo
echo -e "${BOLD}=== Claude Code Skills Installer ===${NC}"
echo -e "Repo:   $REPO_DIR"
echo -e "Target: $SKILLS_DIR"

# Determine which skills to install
if [ "$AUTO" = "1" ]; then
    REQUESTED_SKILLS=("${AVAILABLE_SKILLS[@]}")
elif [ ${#REQUESTED_SKILLS[@]} -eq 0 ]; then
    # Interactive mode
    echo
    echo "Available skills:"
    for i in "${!AVAILABLE_SKILLS[@]}"; do
        skill="${AVAILABLE_SKILLS[$i]}"
        status=""
        if [ -L "$SKILLS_DIR/$skill" ]; then
            status=" ${GREEN}(linked)${NC}"
        elif [ -d "$SKILLS_DIR/$skill" ]; then
            status=" ${YELLOW}(installed — not linked)${NC}"
        fi
        echo -e "  $((i+1)). $skill$status"
    done
    echo
    read -p "Install all? (Y/n): " install_all
    if [[ "$install_all" =~ ^[Nn]$ ]]; then
        read -p "Which skills? (space-separated, e.g. 'outlook trello'): " -a REQUESTED_SKILLS
        if [ ${#REQUESTED_SKILLS[@]} -eq 0 ]; then
            echo "Nothing selected. Exiting."
            exit 0
        fi
    else
        REQUESTED_SKILLS=("${AVAILABLE_SKILLS[@]}")
    fi
fi

# Validate requested skills
for skill in "${REQUESTED_SKILLS[@]}"; do
    found=0
    for available in "${AVAILABLE_SKILLS[@]}"; do
        if [ "$skill" = "$available" ]; then
            found=1
            break
        fi
    done
    if [ "$found" = "0" ]; then
        err "Unknown skill: $skill"
        echo "Available: ${AVAILABLE_SKILLS[*]}"
        exit 1
    fi
done

# Install each skill
INSTALLED=0
FAILED=0
for skill in "${REQUESTED_SKILLS[@]}"; do
    if install_skill "$skill"; then
        INSTALLED=$((INSTALLED + 1))
    else
        FAILED=$((FAILED + 1))
    fi
done

# Summary
echo
echo -e "${BOLD}=== Done ===${NC}"
echo -e "  Installed: ${GREEN}$INSTALLED${NC}"
[ "$FAILED" -gt 0 ] && echo -e "  Skipped:   ${YELLOW}$FAILED${NC}"
echo
echo "Skills are symlinked — edits to this repo are live in Claude Code."
echo "No need to re-install after pulling updates."
echo
