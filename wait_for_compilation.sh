#!/bin/bash
# Auto-polling script to wait for Unity compilation to finish
# Usage: ./wait_for_compilation.sh [timeout_seconds]

STATUS_FILE="/sessions/gracious-gallant-mayer/mnt/RTChess/Logs/CompilationStatus.txt"
TIMEOUT=${1:-60}  # Default 60 seconds timeout
POLL_INTERVAL=2   # Check every 2 seconds

# Get current timestamp from status file
get_timestamp() {
    if [ -f "$STATUS_FILE" ]; then
        grep "TIMESTAMP=" "$STATUS_FILE" | cut -d'=' -f2
    else
        echo ""
    fi
}

# Get initial timestamp
INITIAL_TIMESTAMP=$(get_timestamp)
echo "⏳ Waiting for compilation..."
echo "   Initial timestamp: $INITIAL_TIMESTAMP"

# Poll until timestamp changes or timeout
ELAPSED=0
while [ $ELAPSED -lt $TIMEOUT ]; do
    sleep $POLL_INTERVAL
    ELAPSED=$((ELAPSED + POLL_INTERVAL))

    CURRENT_TIMESTAMP=$(get_timestamp)

    # Check if timestamp changed
    if [ "$CURRENT_TIMESTAMP" != "$INITIAL_TIMESTAMP" ] && [ -n "$CURRENT_TIMESTAMP" ]; then
        echo "✓ Compilation finished!"
        echo "   New timestamp: $CURRENT_TIMESTAMP"
        exit 0
    fi

    # Show progress every 10 seconds
    if [ $((ELAPSED % 10)) -eq 0 ]; then
        echo "   Still waiting... (${ELAPSED}s elapsed)"
    fi
done

echo "⚠️ Timeout reached after ${TIMEOUT}s - compilation may not have finished"
exit 1
