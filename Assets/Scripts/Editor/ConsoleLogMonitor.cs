#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Text;

/// <summary>
/// Automatically captures Unity console logs to a file when playing.
/// Allows Claude to monitor console output without manual screenshots.
///
/// How it works:
/// 1. When you press Play, this starts capturing all console output
/// 2. Logs are written to: Assets/../ConsoleOutput.txt
/// 3. Claude can read this file to see errors, warnings, and debug messages
/// 4. When you stop playing, capture stops and file is finalized
///
/// This eliminates the need to:
/// - Take screenshots of console errors
/// - Manually send error messages to Claude
/// - Describe what you're seeing in the console
/// </summary>
[InitializeOnLoad]
public class ConsoleLogMonitor
{
    private static string logFilePath;
    private static bool isCapturing = false;
    private static StringBuilder capturedLogs = new StringBuilder();

    static ConsoleLogMonitor()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;

        // Set up log file path (outside Assets folder for safety)
        logFilePath = Path.Combine(
            Path.GetDirectoryName(Application.dataPath),
            "ConsoleOutput.txt"
        );

        Debug.Log("[ConsoleLogMonitor] Initialized - will monitor console when you press Play");
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.EnteredPlayMode:
                StartCapturingLogs();
                break;

            case PlayModeStateChange.ExitingPlayMode:
                StopCapturingLogs();
                break;
        }
    }

    private static void StartCapturingLogs()
    {
        isCapturing = true;
        capturedLogs.Clear();

        // Add timestamp
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        capturedLogs.AppendLine($"=== Console Log Capture Started: {timestamp} ===\n");

        // Register callback to capture new logs
        Application.logMessageReceived += OnLogMessage;

        Debug.Log("[ConsoleLogMonitor] Started capturing console logs to: " + logFilePath);
    }

    private static void StopCapturingLogs()
    {
        if (!isCapturing) return;

        isCapturing = false;
        Application.logMessageReceived -= OnLogMessage;

        // Add end marker
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        capturedLogs.AppendLine($"\n=== Console Log Capture Ended: {timestamp} ===");

        // Write to file
        try
        {
            File.WriteAllText(logFilePath, capturedLogs.ToString());
            Debug.Log($"[ConsoleLogMonitor] Logs saved to: {logFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ConsoleLogMonitor] Failed to write log file: {e.Message}");
        }
    }

    private static void OnLogMessage(string logString, string stackTrace, LogType type)
    {
        if (!isCapturing) return;

        // Format: [LogType] message
        string prefix = type switch
        {
            LogType.Error => "[ERROR]",
            LogType.Assert => "[ASSERT]",
            LogType.Warning => "[WARNING]",
            LogType.Log => "[LOG]",
            LogType.Exception => "[EXCEPTION]",
            _ => "[UNKNOWN]"
        };

        capturedLogs.AppendLine($"{prefix} {logString}");

        // Include stack trace for errors
        if ((type == LogType.Error || type == LogType.Exception) && !string.IsNullOrEmpty(stackTrace))
        {
            capturedLogs.AppendLine($"  Stack: {stackTrace}");
        }

        capturedLogs.AppendLine();
    }

    /// <summary>
    /// Menu item to manually open the latest console log file
    /// </summary>
    [MenuItem("Tools/Open Console Log")]
    public static void OpenConsoleLog()
    {
        if (File.Exists(logFilePath))
        {
            EditorUtility.RevealInFinder(logFilePath);
            Debug.Log($"Console log file: {logFilePath}");
        }
        else
        {
            EditorUtility.DisplayDialog("Console Log",
                "No console log file found yet. Press Play to generate one.", "OK");
        }
    }

    /// <summary>
    /// Check if there's a recent log with errors (for debugging)
    /// </summary>
    [MenuItem("Tools/Check for Console Errors")]
    public static void CheckForErrors()
    {
        if (!File.Exists(logFilePath))
        {
            Debug.Log("[ConsoleLogMonitor] No log file found yet.");
            return;
        }

        string content = File.ReadAllText(logFilePath);
        int errorCount = content.Split(new[] { "[ERROR]" }, StringSplitOptions.None).Length - 1;
        int warningCount = content.Split(new[] { "[WARNING]" }, StringSplitOptions.None).Length - 1;

        Debug.Log($"[ConsoleLogMonitor] Errors: {errorCount}, Warnings: {warningCount}");

        if (errorCount > 0)
        {
            Debug.LogWarning("[ConsoleLogMonitor] Errors found in last session!");
        }
    }
}
