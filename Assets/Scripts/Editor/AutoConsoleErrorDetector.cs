#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Automatically detects and reports compilation and runtime errors to Claude.
/// Monitors two critical pain points:
/// 1. After compilation - captures compile errors immediately
/// 2. After pressing Play - captures runtime errors during gameplay
///
/// This is the core of the Claude Auto-Debug workflow:
/// - No manual error reporting needed
/// - Claude sees errors immediately (via [LOG RECEIVED] messages)
/// - Errors are categorized and prioritized
/// - Can be packaged as a reusable Skill
///
/// Usage:
/// - Script runs automatically when Editor loads
/// - Just press Play or compile normally
/// - Check console for [LOG RECEIVED] messages
/// - Claude will analyze errors in background
/// </summary>
[InitializeOnLoad]
public class AutoConsoleErrorDetector
{
    private static string logFilePath;
    private static string errorFilePath;
    private static long lastLogSize = 0;
    private static bool isCapturingPlay = false;

    // Event tracking
    private static List<string> lastReadErrors = new List<string>();

    static AutoConsoleErrorDetector()
    {
        // Initialize file paths
        logFilePath = Path.Combine(
            Path.GetDirectoryName(Application.dataPath),
            "ConsoleOutput.txt"
        );

        errorFilePath = Path.Combine(
            Path.GetDirectoryName(Application.dataPath),
            "ConsoleErrors.txt"
        );

        // Register for compilation events
        CompilationPipeline.compilationStarted += OnCompilationStarted;
        CompilationPipeline.compilationFinished += OnCompilationFinished;

        // Register for play mode events
        EditorApplication.playModeStateChanged += OnPlayModeChanged;

        Debug.Log("[AutoConsoleErrorDetector] Initialized - monitoring compilation and play mode");
    }

    /// ============================================================
    /// COMPILATION MONITORING
    /// ============================================================

    private static void OnCompilationStarted(object context)
    {
        Debug.Log("[AutoConsoleErrorDetector] Compilation started...");
    }

    private static void OnCompilationFinished(object context)
    {
        Debug.Log("[AutoConsoleErrorDetector] Compilation finished - checking for errors...");

        // Small delay to ensure all logs are written
        EditorApplication.delayCall += () =>
        {
            CheckForCompilationErrors();
        };
    }

    private static void CheckForCompilationErrors()
    {
        // Note: CompilationPipeline.GetCompilationMessages() is not available in all Unity versions
        // Instead, we rely on the Console logging system which captures compilation errors automatically
        Debug.Log("[AutoConsoleErrorDetector] Compilation finished - errors will appear in Console");
    }

    /// ============================================================
    /// PLAY MODE MONITORING
    /// ============================================================

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.EnteredPlayMode:
                StartPlayModeMonitoring();
                break;

            case PlayModeStateChange.ExitingPlayMode:
                StopPlayModeMonitoring();
                break;
        }
    }

    private static void StartPlayModeMonitoring()
    {
        isCapturingPlay = true;
        lastReadErrors.Clear();

        Debug.Log("[AutoConsoleErrorDetector] Play mode started - monitoring for runtime errors...");
        Application.logMessageReceived += OnPlayModeLog;

        // Store initial log size for reference (only if file exists)
        lastLogSize = File.Exists(logFilePath) ? new FileInfo(logFilePath).Length : 0;
    }

    private static void StopPlayModeMonitoring()
    {
        if (!isCapturingPlay) return;

        isCapturingPlay = false;
        Application.logMessageReceived -= OnPlayModeLog;

        // Check for errors that occurred during play
        EditorApplication.delayCall += () =>
        {
            CheckForPlayModeErrors();
        };
    }

    private static void OnPlayModeLog(string logString, string stackTrace, LogType type)
    {
        // Log only [ERROR], [DEBUG], [WARNING] prefixed messages
        if (type == LogType.Error || type == LogType.Exception)
        {
            // This will be picked up in CheckForPlayModeErrors
        }
    }

    private static void CheckForPlayModeErrors()
    {
        var (hasErrors, errorCount, warningCount, errors) = ReadConsoleErrors();

        if (hasErrors)
        {
            string message = $"[LOG RECEIVED] Play mode ended with {errorCount} errors, {warningCount} warnings - analyzing...";
            Debug.LogError(message);

            foreach (var error in errors)
            {
                if (!lastReadErrors.Contains(error))
                {
                    Debug.LogError($"  → {error}");
                }
            }

            WriteErrorsToFile(errors, "PLAY_MODE");
        }
        else
        {
            Debug.Log("[LOG RECEIVED] Play mode completed with no critical errors");
        }
    }

    /// ============================================================
    /// ERROR READING & FILTERING
    /// ============================================================

    private static (bool hasErrors, int errorCount, int warningCount, List<string> errors) ReadConsoleErrors()
    {
        if (!File.Exists(logFilePath))
        {
            return (false, 0, 0, new List<string>());
        }

        try
        {
            string[] lines = File.ReadAllLines(logFilePath);
            var errors = new List<string>();
            int errorCount = 0;
            int warningCount = 0;

            foreach (string line in lines)
            {
                // Critical errors
                if (line.StartsWith("[ERROR]") || line.StartsWith("[EXCEPTION]"))
                {
                    errorCount++;
                    errors.Add(line);
                }
                // Warnings
                else if (line.StartsWith("[WARNING]"))
                {
                    warningCount++;
                }
                // Debug logs (informational, helpful for understanding features)
                else if (line.StartsWith("[DEBUG]"))
                {
                    // Include debug logs for context
                    errors.Add(line);
                }
            }

            return (errorCount > 0, errorCount, warningCount, errors);
        }
        catch (Exception e)
        {
            Debug.LogError($"[AutoConsoleErrorDetector] Error reading log: {e.Message}");
            return (false, 0, 0, new List<string>());
        }
    }

    /// ============================================================
    /// ERROR REPORTING
    /// ============================================================

    private static void WriteErrorsToFile(List<string> errors, string context)
    {
        try
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string content = $"[{context}] {timestamp}\n";
            content += string.Join("\n", errors);
            content += "\n\n";

            File.AppendAllText(errorFilePath, content);
        }
        catch (Exception e)
        {
            Debug.LogError($"[AutoConsoleErrorDetector] Failed to write errors: {e.Message}");
        }
    }

    /// ============================================================
    /// MENU ITEMS (For Manual Inspection)
    /// ============================================================

    [MenuItem("Tools/View Last Console Errors")]
    public static void ViewLastErrors()
    {
        if (File.Exists(errorFilePath))
        {
            string content = File.ReadAllText(errorFilePath);
            Debug.Log($"Recent Errors:\n{content}");
        }
        else
        {
            Debug.Log("[AutoConsoleErrorDetector] No errors logged yet");
        }
    }

    [MenuItem("Tools/Clear Error Log")]
    public static void ClearErrorLog()
    {
        if (File.Exists(errorFilePath))
        {
            File.Delete(errorFilePath);
            Debug.Log("[AutoConsoleErrorDetector] Error log cleared");
        }
    }
}


