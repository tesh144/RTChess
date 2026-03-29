#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;

/// <summary>
/// Automatically detects and fixes common compilation errors.
/// Can be triggered manually or used programmatically.
/// </summary>
public class AutoErrorFixer
{
    [MenuItem("Tools/Debug/Auto-Fix Compilation Errors")]
    public static void AutoFixErrors()
    {
        string errorLogPath = "Logs/CompilationErrors.txt";

        if (!File.Exists(errorLogPath))
        {
            EditorUtility.DisplayDialog(
                "No Error Log Found",
                "Run compilation first or use:\nTools → Debug → Export Compilation Errors",
                "OK");
            return;
        }

        string errorLog = File.ReadAllText(errorLogPath);

        if (errorLog.Contains("✅ NO COMPILATION ERRORS"))
        {
            EditorUtility.DisplayDialog("Success", "No errors to fix!", "OK");
            return;
        }

        Debug.Log("[AutoErrorFixer] Starting automated error fixing...");

        int fixedCount = 0;
        var errors = ParseErrors(errorLog);

        foreach (var error in errors)
        {
            if (TryFixError(error))
            {
                fixedCount++;
            }
        }

        AssetDatabase.Refresh();

        if (fixedCount > 0)
        {
            Debug.Log($"[AutoErrorFixer] ✓ Fixed {fixedCount} errors. Recompiling...");
            EditorUtility.DisplayDialog(
                "Auto-Fix Complete",
                $"Fixed {fixedCount} errors.\n\nUnity is recompiling...\n\nCheck console for results.",
                "OK");
        }
        else
        {
            Debug.LogWarning("[AutoErrorFixer] Could not auto-fix errors. Manual intervention needed.");
            EditorUtility.DisplayDialog(
                "Could Not Auto-Fix",
                "These errors require manual fixing.\n\nCheck the Console for details.",
                "OK");
        }
    }

    private static List<CompilationError> ParseErrors(string errorLog)
    {
        var errors = new List<CompilationError>();
        var lines = errorLog.Split('\n');

        CompilationError currentError = null;

        foreach (string line in lines)
        {
            if (line.StartsWith("ERROR "))
            {
                if (currentError != null)
                {
                    errors.Add(currentError);
                }
                currentError = new CompilationError();
            }
            else if (currentError != null)
            {
                if (line.Contains("Message:"))
                    currentError.message = line.Substring(line.IndexOf(":") + 1).Trim();
                else if (line.Contains("File:"))
                    currentError.file = line.Substring(line.IndexOf(":") + 1).Trim();
                else if (line.Contains("Line:"))
                    int.TryParse(line.Substring(line.IndexOf(":") + 1).Trim(), out currentError.line);
            }
        }

        if (currentError != null)
        {
            errors.Add(currentError);
        }

        return errors;
    }

    private static bool TryFixError(CompilationError error)
    {
        // CS0246: The type or namespace name 'X' could not be found
        if (error.message.Contains("CS0246"))
        {
            return FixMissingType(error);
        }

        // CS0103: The name 'X' does not exist in the current context
        if (error.message.Contains("CS0103"))
        {
            return FixUndefinedName(error);
        }

        // CS1061: 'X' does not contain a definition for 'Y'
        if (error.message.Contains("CS1061"))
        {
            Debug.LogWarning($"[AutoErrorFixer] CS1061 errors require manual fixing: {error.message}");
            return false;
        }

        return false;
    }

    private static bool FixMissingType(CompilationError error)
    {
        // Extract type name from error message
        // Pattern: "The type or namespace name 'TypeName' could not be found"
        var match = Regex.Match(error.message, @"'([^']+)'");
        if (!match.Success) return false;

        string missingType = match.Groups[1].Value;

        Debug.Log($"[AutoErrorFixer] Attempting to fix missing type: {missingType} in {error.file}");

        // Common type → namespace mappings
        var namespaceMap = new Dictionary<string, string>
        {
            { "GridObject", "ClockworkGrid" },
            { "GridManager", "ClockworkGrid" },
            { "PlacedObject", "LittleCafe" },
            { "FurnitureObject", "LittleCafe" }, // shim — keep until prefabs migrate
            { "FurnitureDatabase", "LittleCafe" },
            { "FurnitureData", "LittleCafe" },
            { "FurnitureType", "LittleCafe" },
            { "ChairObject", "LittleCafe" },
            { "TableObject", "LittleCafe" },
            { "WallObject", "LittleCafe" },
            { "Vector2Int", "UnityEngine" },
            { "Vector3", "UnityEngine" },
            { "GameObject", "UnityEngine" },
            { "Transform", "UnityEngine" },
            { "MonoBehaviour", "UnityEngine" },
            { "ScriptableObject", "UnityEngine" },
            { "SerializedObject", "UnityEditor" },
            { "EditorWindow", "UnityEditor" },
            { "MenuItem", "UnityEditor" },
            { "AssetDatabase", "UnityEditor" }
        };

        if (namespaceMap.TryGetValue(missingType, out string requiredNamespace))
        {
            return AddUsingDirective(error.file, requiredNamespace);
        }

        Debug.LogWarning($"[AutoErrorFixer] Unknown type '{missingType}' - cannot auto-fix");
        return false;
    }

    private static bool FixUndefinedName(CompilationError error)
    {
        Debug.LogWarning($"[AutoErrorFixer] CS0103 errors require manual fixing: {error.message}");
        return false;
    }

    private static bool AddUsingDirective(string filePath, string namespaceName)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"[AutoErrorFixer] File not found: {filePath}");
            return false;
        }

        string content = File.ReadAllText(filePath);

        // Check if using directive already exists
        string usingStatement = $"using {namespaceName};";
        if (content.Contains(usingStatement))
        {
            Debug.Log($"[AutoErrorFixer] Using directive already exists: {usingStatement}");
            return false;
        }

        // Find last using statement and add after it
        var lines = content.Split('\n');
        int lastUsingIndex = -1;

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith("using "))
            {
                lastUsingIndex = i;
            }
        }

        if (lastUsingIndex >= 0)
        {
            // Insert after last using statement
            var newLines = new List<string>(lines);
            newLines.Insert(lastUsingIndex + 1, usingStatement);
            content = string.Join("\n", newLines);
        }
        else
        {
            // No using statements found - add at top
            content = usingStatement + "\n" + content;
        }

        File.WriteAllText(filePath, content);
        Debug.Log($"[AutoErrorFixer] ✓ Added: {usingStatement} to {Path.GetFileName(filePath)}");

        return true;
    }

    private class CompilationError
    {
        public string message;
        public string file;
        public int line;
    }
}
