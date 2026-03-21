using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using ClockworkGrid;

/// <summary>
/// Editor tool that auto-attaches UIPanel components to every panel
/// in the GUI Pro Kit LightMode/DarkMode hierarchy, scans their child
/// elements into serialized lists (visible in Inspector), pairs light/dark
/// counterparts, and sets up the UIThemeManager.
///
/// Menu: Tools > ClockworkCraft > Setup UI Panels
///
/// After running:
/// - Every panel under LightMode and DarkMode gets a UIPanel component
/// - Each UIPanel's element list shows every child with its path, name,
///   Transform reference, and component tags — all visible in Inspector
/// - Light and dark panels with matching pageIds are cross-referenced
/// - UIThemeManager is configured on the Canvas
/// </summary>
public class SetupUIPanels
{
    [MenuItem("Tools/ClockworkCraft/Setup UI Panels")]
    public static void Run()
    {
        // Find the Canvas that has LightMode and DarkMode children
        Canvas guiProCanvas = FindGUIProCanvas();
        if (guiProCanvas == null)
        {
            Debug.LogError("[SetupUIPanels] Could not find a Canvas with LightMode and DarkMode children. " +
                           "Make sure the GUI Pro Kit demo panels have been imported into the scene.");
            return;
        }

        Transform lightModeRoot = guiProCanvas.transform.Find("LightMode")
                                  ?? FindDeep(guiProCanvas.transform, "LightMode");
        Transform darkModeRoot = guiProCanvas.transform.Find("DarkMode")
                                 ?? FindDeep(guiProCanvas.transform, "DarkMode");

        if (lightModeRoot == null)
        {
            Debug.LogError("[SetupUIPanels] LightMode transform not found.");
            return;
        }

        // ── Phase 1: Attach UIPanel + scan elements ──────────────────

        var lightPanels = new Dictionary<string, UIPanel>();
        var darkPanels = new Dictionary<string, UIPanel>();
        int totalElements = 0;

        // Light panels
        for (int i = 0; i < lightModeRoot.childCount; i++)
        {
            Transform child = lightModeRoot.GetChild(i);
            string pageId = CleanPageId(child.name);

            var panel = child.GetComponent<UIPanel>();
            if (panel == null)
                panel = Undo.AddComponent<UIPanel>(child.gameObject);

            panel.pageId = pageId;
            panel.isDarkMode = false;

            // Scan hierarchy — populates serialized element list
            panel.ScanHierarchy();
            totalElements += panel.ElementCount;

            EditorUtility.SetDirty(panel);
            lightPanels[pageId] = panel;
        }

        // Dark panels
        if (darkModeRoot != null)
        {
            for (int i = 0; i < darkModeRoot.childCount; i++)
            {
                Transform child = darkModeRoot.GetChild(i);
                string pageId = CleanPageId(child.name);

                var panel = child.GetComponent<UIPanel>();
                if (panel == null)
                    panel = Undo.AddComponent<UIPanel>(child.gameObject);

                panel.pageId = pageId;
                panel.isDarkMode = true;

                // Scan hierarchy — populates serialized element list
                panel.ScanHierarchy();
                totalElements += panel.ElementCount;

                EditorUtility.SetDirty(panel);
                darkPanels[pageId] = panel;
            }
        }

        // ── Phase 2: Pair light <-> dark by pageId ────────────────────

        int paired = 0;
        foreach (var kvp in lightPanels)
        {
            string pageId = kvp.Key;
            UIPanel lightPanel = kvp.Value;

            if (darkPanels.TryGetValue(pageId, out UIPanel darkPanel))
            {
                lightPanel.pairedPanel = darkPanel;
                darkPanel.pairedPanel = lightPanel;
                EditorUtility.SetDirty(lightPanel);
                EditorUtility.SetDirty(darkPanel);
                paired++;
            }
            else
            {
                lightPanel.pairedPanel = null;
            }
        }

        // Dark panels without a light counterpart
        foreach (var kvp in darkPanels)
        {
            if (!lightPanels.ContainsKey(kvp.Key))
                kvp.Value.pairedPanel = null;
        }

        // ── Phase 3: Setup UIThemeManager ─────────────────────────────

        var themeManager = guiProCanvas.GetComponent<UIThemeManager>();
        if (themeManager == null)
            themeManager = Undo.AddComponent<UIThemeManager>(guiProCanvas.gameObject);

        themeManager.lightModeRoot = lightModeRoot;
        themeManager.darkModeRoot = darkModeRoot;
        EditorUtility.SetDirty(themeManager);

        // ── Done ──────────────────────────────────────────────────────

        Debug.Log($"[SetupUIPanels] Done!\n" +
                  $"  Light panels: {lightPanels.Count}\n" +
                  $"  Dark panels:  {darkPanels.Count}\n" +
                  $"  Paired:       {paired}\n" +
                  $"  Total elements indexed: {totalElements}\n" +
                  $"  UIThemeManager on: '{guiProCanvas.name}'");
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static Canvas FindGUIProCanvas()
    {
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>(true);

        foreach (var canvas in canvases)
        {
            if (canvas.transform.Find("LightMode") != null &&
                canvas.transform.Find("DarkMode") != null)
                return canvas;
        }

        // Try deeper search
        foreach (var canvas in canvases)
        {
            foreach (Transform child in canvas.transform)
            {
                if (child.Find("LightMode") != null &&
                    child.Find("DarkMode") != null)
                    return canvas;
            }
        }

        return null;
    }

    /// <summary>
    /// Cleans a panel name into a consistent pageId.
    /// Removes "(1)" suffix (Unity duplicate naming for dark mode copies),
    /// trims whitespace, and strips trailing underscores.
    /// </summary>
    private static string CleanPageId(string name)
    {
        string cleaned = System.Text.RegularExpressions.Regex.Replace(name, @"\s*\(\d+\)$", "");
        cleaned = cleaned.Trim().TrimEnd('_');
        return cleaned;
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    // ── Debug Tools ──────────────────────────────────────────────

    [MenuItem("Tools/ClockworkCraft/Debug UI Panel Hierarchy")]
    public static void DebugHierarchy()
    {
        UIPanel[] allPanels = Object.FindObjectsOfType<UIPanel>(true);

        if (allPanels.Length == 0)
        {
            Debug.LogWarning("[Debug UI] No UIPanel components found. Run 'Setup UI Panels' first.");
            return;
        }

        Debug.Log($"[Debug UI] Found {allPanels.Length} UIPanel components:");
        foreach (var panel in allPanels)
        {
            string mode = panel.isDarkMode ? "Dark" : "Light";
            string pair = panel.pairedPanel != null ? $"paired with {panel.pairedPanel.name}" : "no pair";
            Debug.Log($"  [{mode}] {panel.pageId} — {panel.ElementCount} elements — {pair} — " +
                      $"Active: {panel.gameObject.activeInHierarchy}");
        }
    }

    [MenuItem("Tools/ClockworkCraft/Log Panel Elements (Selected)")]
    public static void LogSelectedPanelElements()
    {
        if (Selection.activeGameObject == null)
        {
            Debug.LogWarning("[Debug UI] Select a GameObject with a UIPanel component first.");
            return;
        }

        var panel = Selection.activeGameObject.GetComponent<UIPanel>();
        if (panel == null)
        {
            Debug.LogWarning($"[Debug UI] '{Selection.activeGameObject.name}' has no UIPanel component.");
            return;
        }

        panel.DebugLogHierarchy();
    }

    /// <summary>
    /// Re-scans a single panel's hierarchy without touching others.
    /// </summary>
    [MenuItem("Tools/ClockworkCraft/Rescan Selected Panel")]
    public static void RescanSelectedPanel()
    {
        if (Selection.activeGameObject == null)
        {
            Debug.LogWarning("[Rescan] Select a GameObject with a UIPanel component first.");
            return;
        }

        var panel = Selection.activeGameObject.GetComponent<UIPanel>();
        if (panel == null)
        {
            Debug.LogWarning($"[Rescan] '{Selection.activeGameObject.name}' has no UIPanel component.");
            return;
        }

        panel.ScanHierarchy();
        EditorUtility.SetDirty(panel);
        Debug.Log($"[Rescan] '{panel.pageId}' rescanned — {panel.ElementCount} elements.");
    }
}
