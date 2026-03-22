#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Helper script for Claude to read and analyze console logs.
/// This works in conjunction with ConsoleLogMonitor to provide automatic error detection.
///
/// Claude uses this to:
/// 1. Check for compilation errors in the console
/// 2. Detect runtime errors during gameplay
/// 3. Monitor console warnings and debug messages
/// 4. Provide real-time feedback without manual intervention
/// </summary>
public class ConsoleLogReader
{
    private static string logFilePath => Path.Combine(
        Path.GetDirectoryName(Application.dataPath),
        "ConsoleOutput.txt"
    );

    /// <summary>
    /// Check if there are any errors in the latest console log
    /// Returns: (hasErrors, errorCount, warningCount, errorMessages)
    /// </summary>
    public static (bool hasErrors, int errorCount, int warningCount, List<string> errorMessages) CheckForErrors()
    {
        if (!File.Exists(logFilePath))
        {
            return (false, 0, 0, new List<string> { "No console log file found" });
        }

        try
        {
            string[] lines = File.ReadAllLines(logFilePath);
            int errors = 0;
            int warnings = 0;
            var errorMessages = new List<string>();

            foreach (string line in lines)
            {
                if (line.StartsWith("[ERROR]"))
                {
                    errors++;
                    errorMessages.Add(line.Replace("[ERROR]", "").Trim());
                }
                else if (line.StartsWith("[WARNING]"))
                {
                    warnings++;
                }
                else if (line.StartsWith("[EXCEPTION]"))
                {
                    errors++;
                    errorMessages.Add(line.Replace("[EXCEPTION]", "").Trim());
                }
            }

            return (errors > 0, errors, warnings, errorMessages);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ConsoleLogReader] Error reading log file: {e.Message}");
            return (false, 0, 0, new List<string> { e.Message });
        }
    }

    /// <summary>
    /// Get the full console log as a string
    /// </summary>
    public static string GetFullLog()
    {
        if (!File.Exists(logFilePath))
        {
            return "No console log file found yet. Press Play in the editor to generate one.";
        }

        try
        {
            return File.ReadAllText(logFilePath);
        }
        catch (Exception e)
        {
            return $"Error reading log file: {e.Message}";
        }
    }

    /// <summary>
    /// Get only the error and exception messages
    /// </summary>
    public static List<string> GetErrorsOnly()
    {
        var errors = new List<string>();

        if (!File.Exists(logFilePath))
            return errors;

        try
        {
            string[] lines = File.ReadAllLines(logFilePath);
            foreach (string line in lines)
            {
                if (line.StartsWith("[ERROR]") || line.StartsWith("[EXCEPTION]"))
                {
                    errors.Add(line);
                }
            }
        }
        catch (Exception e)
        {
            errors.Add($"Error reading log: {e.Message}");
        }

        return errors;
    }

    /// <summary>
    /// Get the last N lines of the console log
    /// </summary>
    public static List<string> GetLastNLines(int n)
    {
        if (!File.Exists(logFilePath))
            return new List<string> { "No log file found" };

        try
        {
            string[] allLines = File.ReadAllLines(logFilePath);
            return allLines.Skip(Math.Max(0, allLines.Length - n)).ToList();
        }
        catch (Exception e)
        {
            return new List<string> { $"Error: {e.Message}" };
        }
    }

    /// <summary>
    /// Check if log contains specific text
    /// </summary>
    public static bool LogContains(string searchText)
    {
        if (!File.Exists(logFilePath))
            return false;

        try
        {
            string content = File.ReadAllText(logFilePath);
            return content.Contains(searchText, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
