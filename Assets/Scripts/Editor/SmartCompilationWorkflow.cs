#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Smart compilation workflow that automatically detects and fixes errors.
/// This is what Claude uses to verify work autonomously.
/// </summary>
public class SmartCompilationWorkflow
{
    private const string STATUS_LOG_PATH = "Logs/CompilationStatus.txt";
    private const string ERROR_LOG_PATH = "Logs/CompilationErrors.txt";

    /// <summary>
    /// Check compilation status. Returns true if no errors, false if errors exist.
    /// </summary>
    [MenuItem("Tools/Claude/Check Compilation Status")]
    public static bool CheckStatus()
    {
        // Trigger export
        CompilationMonitor.ExportCompilationStatus();

        // Read status
        if (!File.Exists(STATUS_LOG_PATH))
        {
            Debug.LogWarning("[SmartWorkflow] No status file found. Compilation may not have run yet.");
            return false;
        }

        string status = File.ReadAllText(STATUS_LOG_PATH);

        if (status.Contains("COMPILATION_STATUS=SUCCESS"))
        {
            Debug.Log("[SmartWorkflow] ✅ Compilation successful - no errors!");
            return true;
        }
        else
        {
            // Extract error count
            var lines = status.Split('\n');
            string errorCount = "unknown";
            foreach (var line in lines)
            {
                if (line.StartsWith("ERROR_COUNT="))
                {
                    errorCount = line.Substring("ERROR_COUNT=".Length);
                    break;
                }
            }

            Debug.LogWarning($"[SmartWorkflow] ❌ Compilation failed with {errorCount} errors");
            Debug.LogWarning($"[SmartWorkflow] Error details in: {ERROR_LOG_PATH}");

            return false;
        }
    }

    /// <summary>
    /// Full auto-fix workflow: Check → Fix → Re-check → Report
    /// </summary>
    [MenuItem("Tools/Claude/Auto-Fix Workflow")]
    public static void RunAutoFixWorkflow()
    {
        Debug.Log("[SmartWorkflow] ========================================");
        Debug.Log("[SmartWorkflow] Starting Auto-Fix Workflow...");
        Debug.Log("[SmartWorkflow] ========================================");

        // Step 1: Check current status
        Debug.Log("[SmartWorkflow] Step 1: Checking compilation status...");
        bool isClean = CheckStatus();

        if (isClean)
        {
            EditorUtility.DisplayDialog(
                "All Clear!",
                "✅ No compilation errors found.\n\nEverything is working correctly!",
                "OK");
            return;
        }

        // Step 2: Attempt auto-fix
        Debug.Log("[SmartWorkflow] Step 2: Attempting auto-fix...");
        AutoErrorFixer.AutoFixErrors();

        // Step 3: Wait for recompilation
        Debug.Log("[SmartWorkflow] Step 3: Waiting for recompilation...");
        EditorApplication.delayCall += () =>
        {
            System.Threading.Thread.Sleep(2000); // Wait 2 seconds for compilation

            EditorApplication.delayCall += () =>
            {
                // Step 4: Re-check status
                Debug.Log("[SmartWorkflow] Step 4: Re-checking status...");
                bool isNowClean = CheckStatus();

                if (isNowClean)
                {
                    Debug.Log("[SmartWorkflow] ========================================");
                    Debug.Log("[SmartWorkflow] ✅ AUTO-FIX SUCCESSFUL!");
                    Debug.Log("[SmartWorkflow] All errors resolved. Ready to proceed.");
                    Debug.Log("[SmartWorkflow] ========================================");

                    EditorUtility.DisplayDialog(
                        "Auto-Fix Successful!",
                        "✅ All compilation errors have been fixed!\n\n" +
                        "The code is now ready to use.",
                        "OK");
                }
                else
                {
                    Debug.LogWarning("[SmartWorkflow] ========================================");
                    Debug.LogWarning("[SmartWorkflow] ⚠️ AUTO-FIX INCOMPLETE");
                    Debug.LogWarning("[SmartWorkflow] Some errors could not be fixed automatically.");
                    Debug.LogWarning($"[SmartWorkflow] See details: {ERROR_LOG_PATH}");
                    Debug.LogWarning("[SmartWorkflow] ========================================");

                    EditorUtility.DisplayDialog(
                        "Auto-Fix Incomplete",
                        "⚠️ Some errors require manual fixing.\n\n" +
                        "Check the Console for details.",
                        "OK");
                }
            };
        };
    }

    /// <summary>
    /// Quick helper: Get current error count without UI popups.
    /// Returns -1 if status unavailable, 0 if clean, >0 if errors exist.
    /// </summary>
    public static int GetErrorCount()
    {
        CompilationMonitor.ExportCompilationStatus();

        if (!File.Exists(STATUS_LOG_PATH))
        {
            return -1;
        }

        string status = File.ReadAllText(STATUS_LOG_PATH);
        var lines = status.Split('\n');

        foreach (var line in lines)
        {
            if (line.StartsWith("ERROR_COUNT="))
            {
                string countStr = line.Substring("ERROR_COUNT=".Length).Trim();
                if (int.TryParse(countStr, out int count))
                {
                    return count;
                }
            }
        }

        return -1;
    }
}
