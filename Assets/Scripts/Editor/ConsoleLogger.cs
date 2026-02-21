using UnityEngine;
using UnityEditor;
using System.IO;
using System;

/// <summary>
/// Automatically captures Unity Console output to a log file for debugging
/// This allows external tools to read compilation errors and console messages
/// </summary>
[InitializeOnLoad]
public class ConsoleLogger
{
    private static string logFilePath;
    private static string latestLogPath;
    private static StreamWriter logWriter;
    private static StreamWriter latestLogWriter;
    private static readonly object lockObject = new object();
    private static bool initialized = false;

    static ConsoleLogger()
    {
        // Guard against double initialization
        if (initialized) return;
        initialized = true;

        // Create Logs folder in project root
        string logFolder = Path.Combine(Application.dataPath, "..", "Logs");
        Directory.CreateDirectory(logFolder);

        // Create timestamped log file
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        logFilePath = Path.Combine(logFolder, $"Unity_Console_{timestamp}.log");
        latestLogPath = Path.Combine(logFolder, "Unity_Console_Latest.log");

        try
        {
            // Initialize both log files — write to both simultaneously
            logWriter = new StreamWriter(logFilePath, append: true);
            logWriter.AutoFlush = true;

            // Overwrite "latest" each session (not a copy, a direct writer)
            latestLogWriter = new StreamWriter(latestLogPath, append: false);
            latestLogWriter.AutoFlush = true;

            // Write header
            WriteLog($"=== Unity Console Log Started: {DateTime.Now} ===");
            WriteLog($"Unity Version: {Application.unityVersion}");
            WriteLog($"Project: {Application.dataPath}");
            WriteLog("=" + new string('=', 60));
            WriteLog("");

            // Subscribe to log messages
            Application.logMessageReceived += OnLogMessageReceived;

            Debug.Log($"[ConsoleLogger] Logging to: {logFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ConsoleLogger] Failed to initialize: {e.Message}");
        }

        // Cleanup on editor quit
        EditorApplication.quitting += OnEditorQuitting;
    }

    private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        lock (lockObject)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                string logEntry = $"[{timestamp}] [{type}] {condition}";

                // Write to both files simultaneously
                WriteLog(logEntry);

                // Include stack trace for errors and exceptions
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                {
                    if (!string.IsNullOrEmpty(stackTrace))
                    {
                        WriteLog(stackTrace);
                    }
                    WriteLog(""); // Blank line after error
                }
            }
            catch (Exception)
            {
                // Silently fail to avoid infinite loop
            }
        }
    }

    private static void WriteLog(string message)
    {
        if (logWriter != null)
            logWriter.WriteLine(message);
        if (latestLogWriter != null)
            latestLogWriter.WriteLine(message);
    }

    private static void OnEditorQuitting()
    {
        lock (lockObject)
        {
            try
            {
                WriteLog("");
                WriteLog($"=== Unity Console Log Ended: {DateTime.Now} ===");

                if (logWriter != null)
                {
                    logWriter.Close();
                    logWriter = null;
                }
                if (latestLogWriter != null)
                {
                    latestLogWriter.Close();
                    latestLogWriter = null;
                }

                Application.logMessageReceived -= OnLogMessageReceived;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ConsoleLogger] Error during cleanup: {e.Message}");
            }
        }
    }
}
