#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Monitors Play mode state changes and exports events for autonomous monitoring.
/// Automatically notifies Claude when you press Play/Stop.
/// </summary>
[InitializeOnLoad]
public static class PlayModeMonitor
{
    private const string PLAY_MODE_LOG = "Logs/PlayModeEvents.txt";

    static PlayModeMonitor()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        // Ensure Logs directory exists
        string logsDir = Path.GetDirectoryName(PLAY_MODE_LOG);
        if (!Directory.Exists(logsDir))
        {
            Directory.CreateDirectory(logsDir);
        }

        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string eventMessage = "";

        switch (state)
        {
            case PlayModeStateChange.EnteredEditMode:
                eventMessage = $"[{timestamp}] EXITED_PLAY_MODE";
                break;

            case PlayModeStateChange.ExitingEditMode:
                eventMessage = $"[{timestamp}] ENTERING_PLAY_MODE";
                break;

            case PlayModeStateChange.EnteredPlayMode:
                eventMessage = $"[{timestamp}] PLAY_MODE_STARTED";
                Debug.Log("[PlayModeMonitor] ▶️ Play mode started - Claude will check logs automatically");
                break;

            case PlayModeStateChange.ExitingPlayMode:
                eventMessage = $"[{timestamp}] EXITING_PLAY_MODE";
                break;
        }

        // Append to log file
        File.AppendAllText(PLAY_MODE_LOG, eventMessage + "\n");
    }
}
