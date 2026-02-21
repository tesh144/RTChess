#!/bin/bash
# Autonomous compilation checking and error fixing loop
# This is what Claude runs after making code changes

ERROR_LOG="/sessions/gracious-gallant-mayer/mnt/RTChess/Logs/CompilationErrors.txt"
STATUS_LOG="/sessions/gracious-gallant-mayer/mnt/RTChess/Logs/CompilationStatus.txt"
WAIT_SCRIPT="/sessions/gracious-gallant-mayer/mnt/RTChess/wait_for_compilation.sh"

MAX_ITERATIONS=5  # Prevent infinite loops
iteration=0

echo "========================================="
echo "AUTONOMOUS COMPILATION CHECK"
echo "========================================="

while [ $iteration -lt $MAX_ITERATIONS ]; do
    iteration=$((iteration + 1))
    echo ""
    echo "--- Iteration $iteration ---"

    # Wait for Unity to compile
    if ! $WAIT_SCRIPT 60; then
        echo "❌ Compilation timeout - Unity may not be running"
        exit 1
    fi

    # Check compilation status
    if [ ! -f "$STATUS_LOG" ]; then
        echo "❌ Status file not found - CompilationMonitor may not be running"
        exit 1
    fi

    STATUS=$(grep "COMPILATION_STATUS=" "$STATUS_LOG" | cut -d'=' -f2)
    ERROR_COUNT=$(grep "ERROR_COUNT=" "$STATUS_LOG" | cut -d'=' -f2)

    echo ""
    echo "📊 Status: $STATUS"
    echo "📊 Errors: $ERROR_COUNT"

    if [ "$STATUS" = "SUCCESS" ]; then
        echo ""
        echo "========================================="
        echo "✅ COMPILATION SUCCESSFUL!"
        echo "All errors fixed - ready to proceed"
        echo "========================================="
        exit 0
    fi

    # Errors exist - show them
    echo ""
    echo "❌ Found $ERROR_COUNT compilation error(s):"
    echo ""
    cat "$ERROR_LOG"

    echo ""
    echo "🔧 Attempting auto-fix..."

    # Note: Auto-fixing would happen here via Unity menu items
    # For now, just notify that manual intervention is needed
    echo ""
    echo "⚠️ Auto-fix requires Unity Editor to be open"
    echo "   Run: Tools → Claude → Auto-Fix Workflow"
    echo ""
    echo "Or I can fix errors manually and trigger another iteration..."

    # Exit and let Claude fix manually
    exit 1
done

echo ""
echo "⚠️ Maximum iterations reached - some errors may require manual intervention"
exit 1
