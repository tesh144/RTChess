using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Monitors Unity compilation and exports errors to a file for automated detection.
/// Runs automatically after every compilation.
/// </summary>
[InitializeOnLoad]
public class CompilationMonitor
{
    private const string ERROR_LOG_PATH = "Logs/CompilationErrors.txt";
    private const string STATUS_LOG_PATH = "Logs/CompilationStatus.txt";

    static CompilationMonitor()
    {
        // Subscribe to compilation events
        CompilationPipeline.compilationFinished += OnCompilationFinished;
    }

    private static void OnCompilationFinished(object obj)
    {
        // Wait a frame for console to update
        EditorApplication.delayCall += () =>
        {
            ExportCompilationStatus();
        };
    }

    [MenuItem("Tools/Debug/Export Compilation Errors")]
    public static void ExportCompilationStatus()
    {
        // Ensure Logs directory exists
        string logsDir = Path.GetDirectoryName(ERROR_LOG_PATH);
        if (!Directory.Exists(logsDir))
        {
            Directory.CreateDirectory(logsDir);
        }

        // Get console errors using reflection
        var logEntries = GetConsoleErrors();

        // Write errors to file
        using (StreamWriter writer = new StreamWriter(ERROR_LOG_PATH, false))
        {
            writer.WriteLine("=== Unity Compilation Status ===");
            writer.WriteLine($"Time: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"Total Errors: {logEntries.Count}");
            writer.WriteLine("================================\n");

            if (logEntries.Count == 0)
            {
                writer.WriteLine("✅ NO COMPILATION ERRORS");
                writer.WriteLine("All scripts compiled successfully!");
            }
            else
            {
                writer.WriteLine("❌ COMPILATION ERRORS FOUND:\n");

                int errorNum = 1;
                foreach (var entry in logEntries)
                {
                    writer.WriteLine($"ERROR {errorNum}:");
                    writer.WriteLine($"  Message: {entry.message}");
                    writer.WriteLine($"  File: {entry.file}");
                    writer.WriteLine($"  Line: {entry.line}");
                    writer.WriteLine($"  Type: {entry.mode}");
                    writer.WriteLine();
                    errorNum++;
                }
            }
        }

        // Write simple status file
        using (StreamWriter writer = new StreamWriter(STATUS_LOG_PATH, false))
        {
            writer.WriteLine($"COMPILATION_STATUS={(logEntries.Count == 0 ? "SUCCESS" : "FAILED")}");
            writer.WriteLine($"ERROR_COUNT={logEntries.Count}");
            writer.WriteLine($"TIMESTAMP={System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }

        // Only log to console if there are errors (don't spam on success)
        if (logEntries.Count > 0)
        {
            Debug.Log($"[CompilationMonitor] Exported {logEntries.Count} errors to {ERROR_LOG_PATH}");
        }
    }

    private static List<LogEntry> GetConsoleErrors()
    {
        var logEntries = new List<LogEntry>();

        try
        {
            // Use reflection to access Unity's internal console
            var consoleWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ConsoleWindow");
            var logEntriesType = typeof(EditorWindow).Assembly.GetType("UnityEditor.LogEntries");

            if (logEntriesType != null)
            {
                // Get entry count
                var getCountMethod = logEntriesType.GetMethod("GetCount", BindingFlags.Static | BindingFlags.Public);
                int count = (int)getCountMethod.Invoke(null, null);

                // Get each entry
                var getEntryMethod = logEntriesType.GetMethod("GetEntryInternal", BindingFlags.Static | BindingFlags.Public);
                var logEntryType = typeof(EditorWindow).Assembly.GetType("UnityEditor.LogEntry");

                for (int i = 0; i < count; i++)
                {
                    object logEntry = System.Activator.CreateInstance(logEntryType);
                    getEntryMethod.Invoke(null, new object[] { i, logEntry });

                    // Get fields
                    var messageField = logEntryType.GetField("message", BindingFlags.Instance | BindingFlags.Public);
                    var fileField = logEntryType.GetField("file", BindingFlags.Instance | BindingFlags.Public);
                    var lineField = logEntryType.GetField("line", BindingFlags.Instance | BindingFlags.Public);
                    var modeField = logEntryType.GetField("mode", BindingFlags.Instance | BindingFlags.Public);

                    string message = messageField?.GetValue(logEntry) as string ?? "";
                    string file = fileField?.GetValue(logEntry) as string ?? "";
                    int line = (int)(lineField?.GetValue(logEntry) ?? 0);
                    int mode = (int)(modeField?.GetValue(logEntry) ?? 0);

                    // Only capture errors (mode 2 = error, mode 4 = script compile error)
                    if (mode == 2 || mode == 4)
                    {
                        logEntries.Add(new LogEntry
                        {
                            message = message,
                            file = file,
                            line = line,
                            mode = mode == 2 ? "Error" : "ScriptError"
                        });
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[CompilationMonitor] Could not read console: {e.Message}");
        }

        return logEntries;
    }

    private class LogEntry
    {
        public string message;
        public string file;
        public int line;
        public string mode;
    }
}
