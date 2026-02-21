#!/bin/bash
# Autonomous Play mode monitoring script
# Checks if Play mode has started and reports console logs automatically

PLAY_LOG="/sessions/gracious-gallant-mayer/mnt/RTChess/Logs/PlayModeEvents.txt"
CONSOLE_LOG="/sessions/gracious-gallant-mayer/mnt/RTChess/Logs/Unity_Console_Latest.log"
LAST_CHECK_FILE="/sessions/gracious-gallant-mayer/mnt/RTChess/.last_play_check"

# Check if Play mode log exists
if [ ! -f "$PLAY_LOG" ]; then
    echo "⚠️ No Play mode events detected yet"
    exit 1
fi

# Get last checked timestamp
LAST_CHECK=""
if [ -f "$LAST_CHECK_FILE" ]; then
    LAST_CHECK=$(cat "$LAST_CHECK_FILE")
fi

# Get most recent Play mode event
LATEST_EVENT=$(tail -1 "$PLAY_LOG")

# Check if it's a new Play mode start
if echo "$LATEST_EVENT" | grep -q "PLAY_MODE_STARTED"; then
    EVENT_TIME=$(echo "$LATEST_EVENT" | grep -oE "[0-9]{4}-[0-9]{2}-[0-9]{2} [0-9]{2}:[0-9]{2}:[0-9]{2}")

    # If this is a new event (different from last check)
    if [ "$EVENT_TIME" != "$LAST_CHECK" ]; then
        echo "========================================="
        echo "▶️ PLAY MODE DETECTED!"
        echo "Time: $EVENT_TIME"
        echo "========================================="
        echo ""

        # Report key console logs from this Play session
        echo "📋 CONSOLE LOG SUMMARY:"
        echo ""

        # CafeSceneSetupV2 initialization
        echo "🎮 Scene Setup:"
        grep "CafeSceneSetupV2" "$CONSOLE_LOG" | tail -15

        echo ""
        echo "⚠️ Warnings/Errors:"
        grep -E "\[Warning\]|\[Error\]" "$CONSOLE_LOG" | tail -10

        echo ""
        echo "========================================="

        # Save this timestamp
        echo "$EVENT_TIME" > "$LAST_CHECK_FILE"

        exit 0
    else
        echo "ℹ️ Already checked this Play session"
        exit 2
    fi
else
    echo "ℹ️ No recent Play mode start detected"
    exit 1
fi
