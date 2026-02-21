#!/bin/bash
# Unity Log Copier - Makes Unity Editor logs accessible
# Run this script whenever you want Claude to see Unity console errors

echo "Copying Unity Editor logs..."

# Find the most recent Unity Editor log
UNITY_LOG_DIR="$HOME/Library/Logs/Unity"
PROJECT_LOG_DIR="$(dirname "$0")/Logs"

# Create Logs directory if it doesn't exist
mkdir -p "$PROJECT_LOG_DIR"

if [ -f "$UNITY_LOG_DIR/Editor.log" ]; then
    cp "$UNITY_LOG_DIR/Editor.log" "$PROJECT_LOG_DIR/Unity_Editor.log"
    echo "✓ Copied Editor.log to $PROJECT_LOG_DIR/Unity_Editor.log"

    # Show last 50 lines with errors
    echo ""
    echo "=== Recent Errors/Warnings ==="
    grep -i "error\|warning\|exception" "$PROJECT_LOG_DIR/Unity_Editor.log" | tail -50

else
    echo "✗ Unity Editor.log not found at $UNITY_LOG_DIR/Editor.log"
    echo "  Make sure Unity is running"
fi

echo ""
echo "Done! Claude can now read: Logs/Unity_Editor.log"
