#!/bin/bash

# Unity Skills Pack Installer
# Installs Unity development skills to Claude's skills directory

set -e

echo "🎮 Unity Skills Pack Installer"
echo "================================"
echo ""

# Detect the script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

# Try to find the .claude/skills directory
SKILLS_DIR=""

# Option 1: Check current directory
if [ -d ".claude/skills" ]; then
    SKILLS_DIR=".claude/skills"
# Option 2: Check parent directory
elif [ -d "../.claude/skills" ]; then
    SKILLS_DIR="../.claude/skills"
# Option 3: Check home directory
elif [ -d "$HOME/.claude/skills" ]; then
    SKILLS_DIR="$HOME/.claude/skills"
# Option 4: Ask user
else
    echo "❓ Could not find .claude/skills directory automatically."
    echo ""
    read -p "Enter the path to your workspace folder: " WORKSPACE
    SKILLS_DIR="$WORKSPACE/.claude/skills"

    if [ ! -d "$SKILLS_DIR" ]; then
        echo "❌ Directory not found: $SKILLS_DIR"
        echo ""
        echo "Please ensure you're using Claude Cowork mode and have selected a workspace folder."
        exit 1
    fi
fi

echo "📁 Skills directory: $SKILLS_DIR"
echo ""

# Check if skills already exist
OVERWRITE=false
if [ -d "$SKILLS_DIR/unity-asset-management" ] || \
   [ -d "$SKILLS_DIR/unity-editor-scripting" ] || \
   [ -d "$SKILLS_DIR/unity-gameplay-dev" ]; then
    echo "⚠️  One or more Unity skills already exist."
    read -p "Overwrite existing skills? (y/N): " CONFIRM
    if [[ $CONFIRM =~ ^[Yy]$ ]]; then
        OVERWRITE=true
    else
        echo "❌ Installation cancelled."
        exit 0
    fi
fi

# Install skills
echo ""
echo "📦 Installing Unity skills..."
echo ""

install_skill() {
    local SKILL_NAME=$1
    if [ "$OVERWRITE" = true ] && [ -d "$SKILLS_DIR/$SKILL_NAME" ]; then
        echo "   🔄 Overwriting $SKILL_NAME..."
        rm -rf "$SKILLS_DIR/$SKILL_NAME"
    else
        echo "   ✅ Installing $SKILL_NAME..."
    fi
    cp -r "$SCRIPT_DIR/$SKILL_NAME" "$SKILLS_DIR/"
}

install_skill "unity-asset-management"
install_skill "unity-editor-scripting"
install_skill "unity-gameplay-dev"

echo ""
echo "✨ Installation complete!"
echo ""
echo "Installed skills:"
echo "  • unity-asset-management (Prefabs, Materials, FBX, Shaders)"
echo "  • unity-editor-scripting (Editor Automation, Batch Operations)"
echo "  • unity-gameplay-dev (MonoBehaviour, Game Logic, Runtime)"
echo ""
echo "🚀 The skills will automatically activate in new Claude conversations."
echo "   Just ask Unity-related questions and Claude will use them!"
echo ""
echo "📖 See README.md for usage examples and troubleshooting."
echo ""
