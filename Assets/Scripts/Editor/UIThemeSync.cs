#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using ClockworkGrid;

/// <summary>
/// Syncs layout, hierarchy, and structural changes from LightMode panels
/// to their DarkMode counterparts — while preserving dark mode's visual
/// identity (colors, sprites, materials).
///
/// Workflow:
///   1. Make your design changes on LightMode panels only
///   2. Run Tools > ClockworkCraft > Sync Light → Dark Layout
///   3. Dark mode mirrors all positional/structural changes, keeps its own colors
///
/// What gets synced:
///   - RectTransform: anchoredPosition, sizeDelta, anchors, pivot, rotation, scale
///   - GameObject active state
///   - Text content (string only, not color/font)
///   - Slider values
///   - Hierarchy active/inactive state of children
///
/// What is preserved (NOT synced):
///   - Image sprites and colors (these define the dark theme)
///   - Text colors and fonts
///   - Material references
///   - Image types and fill settings
///
/// Menu: Tools > ClockworkCraft > Sync Light → Dark Layout
/// </summary>
public class UIThemeSync
{
    [MenuItem("Tools/ClockworkCraft/Sync Light → Dark Layout")]
    public static void SyncLightToDark()
    {
        // Find panels
        UIPanel[] allPanels = Object.FindObjectsOfType<UIPanel>(true);

        var lightByPageId = new Dictionary<string, UIPanel>();
        var darkByPageId = new Dictionary<string, UIPanel>();

        foreach (var panel in allPanels)
        {
            if (panel.isDarkMode)
                darkByPageId[panel.pageId] = panel;
            else
                lightByPageId[panel.pageId] = panel;
        }

        int syncedPanels = 0;
        int syncedElements = 0;

        foreach (var kvp in lightByPageId)
        {
            string pageId = kvp.Key;
            UIPanel lightPanel = kvp.Value;

            if (!darkByPageId.TryGetValue(pageId, out UIPanel darkPanel))
            {
                Debug.LogWarning($"[UIThemeSync] No dark counterpart for '{pageId}' — skipping.");
                continue;
            }

            int elements = SyncPanel(lightPanel.transform, darkPanel.transform);
            syncedElements += elements;
            syncedPanels++;

            EditorUtility.SetDirty(darkPanel);
        }

        Debug.Log($"[UIThemeSync] Synced {syncedPanels} panels, {syncedElements} elements updated.");
    }

    /// <summary>
    /// Sync a single panel from a selected LightMode panel to its dark counterpart.
    /// </summary>
    [MenuItem("Tools/ClockworkCraft/Sync Selected Panel → Dark")]
    public static void SyncSelectedToDark()
    {
        if (Selection.activeGameObject == null)
        {
            Debug.LogWarning("[UIThemeSync] Select a LightMode panel first.");
            return;
        }

        var lightPanel = Selection.activeGameObject.GetComponent<UIPanel>();
        if (lightPanel == null || lightPanel.isDarkMode)
        {
            Debug.LogWarning("[UIThemeSync] Select a LightMode panel (not dark mode).");
            return;
        }

        if (lightPanel.pairedPanel == null)
        {
            Debug.LogWarning($"[UIThemeSync] '{lightPanel.pageId}' has no paired dark panel. Run Setup UI Panels first.");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(lightPanel.pairedPanel.gameObject, "Sync Light to Dark");

        int elements = SyncPanel(lightPanel.transform, lightPanel.pairedPanel.transform);
        EditorUtility.SetDirty(lightPanel.pairedPanel);

        Debug.Log($"[UIThemeSync] Synced '{lightPanel.pageId}': {elements} elements updated.");
    }

    // ── Core Sync Logic ──────────────────────────────────────────

    /// <summary>
    /// Recursively syncs layout properties from source to target,
    /// matching children by name. Returns count of synced elements.
    /// </summary>
    private static int SyncPanel(Transform source, Transform target)
    {
        int count = 0;

        // Sync the root RectTransform
        SyncRectTransform(source, target);
        SyncActiveState(source, target);
        SyncTextContent(source, target);
        SyncSliderValue(source, target);
        count++;

        // Build name→child map for the target to match by name
        var targetChildren = new Dictionary<string, Transform>();
        for (int i = 0; i < target.childCount; i++)
        {
            Transform child = target.GetChild(i);
            // Use name without " (1)" suffix for matching
            string cleanName = CleanName(child.name);
            if (!targetChildren.ContainsKey(cleanName))
                targetChildren[cleanName] = child;
        }

        // Recurse into matching children
        for (int i = 0; i < source.childCount; i++)
        {
            Transform sourceChild = source.GetChild(i);
            string cleanName = CleanName(sourceChild.name);

            if (targetChildren.TryGetValue(cleanName, out Transform targetChild))
            {
                count += SyncPanel(sourceChild, targetChild);
            }
        }

        // Sync sibling order to match source
        SyncSiblingOrder(source, target);

        return count;
    }

    /// <summary>
    /// Syncs RectTransform layout properties — position, size, anchors, pivot, scale.
    /// Does NOT touch visual properties.
    /// </summary>
    private static void SyncRectTransform(Transform source, Transform target)
    {
        RectTransform src = source as RectTransform;
        RectTransform dst = target as RectTransform;
        if (src == null || dst == null) return;

        dst.anchoredPosition = src.anchoredPosition;
        dst.sizeDelta = src.sizeDelta;
        dst.anchorMin = src.anchorMin;
        dst.anchorMax = src.anchorMax;
        dst.pivot = src.pivot;
        dst.localRotation = src.localRotation;
        dst.localScale = src.localScale;
    }

    /// <summary>
    /// Syncs active state of the GameObject.
    /// </summary>
    private static void SyncActiveState(Transform source, Transform target)
    {
        if (target.gameObject.activeSelf != source.gameObject.activeSelf)
            target.gameObject.SetActive(source.gameObject.activeSelf);
    }

    /// <summary>
    /// Syncs text STRING content only — preserves dark mode's text color, font, size.
    /// </summary>
    private static void SyncTextContent(Transform source, Transform target)
    {
        var srcTMP = source.GetComponent<TextMeshProUGUI>();
        var dstTMP = target.GetComponent<TextMeshProUGUI>();
        if (srcTMP != null && dstTMP != null)
        {
            if (dstTMP.text != srcTMP.text)
                dstTMP.text = srcTMP.text;

            // Sync font size (layout property) but NOT color or font asset
            if (!Mathf.Approximately(dstTMP.fontSize, srcTMP.fontSize))
                dstTMP.fontSize = srcTMP.fontSize;

            // Sync alignment (layout property)
            if (dstTMP.alignment != srcTMP.alignment)
                dstTMP.alignment = srcTMP.alignment;
        }
    }

    /// <summary>
    /// Syncs slider value and range — preserves visual appearance.
    /// </summary>
    private static void SyncSliderValue(Transform source, Transform target)
    {
        var srcSlider = source.GetComponent<Slider>();
        var dstSlider = target.GetComponent<Slider>();
        if (srcSlider != null && dstSlider != null)
        {
            dstSlider.minValue = srcSlider.minValue;
            dstSlider.maxValue = srcSlider.maxValue;
            dstSlider.value = srcSlider.value;
            dstSlider.wholeNumbers = srcSlider.wholeNumbers;
        }
    }

    /// <summary>
    /// Reorders target's children to match source's sibling order (by name).
    /// </summary>
    private static void SyncSiblingOrder(Transform source, Transform target)
    {
        // Build name→index from source
        var sourceOrder = new Dictionary<string, int>();
        for (int i = 0; i < source.childCount; i++)
            sourceOrder[CleanName(source.GetChild(i).name)] = i;

        // Sort target children to match
        var targetChildren = new List<Transform>();
        for (int i = 0; i < target.childCount; i++)
            targetChildren.Add(target.GetChild(i));

        targetChildren.Sort((a, b) =>
        {
            string nameA = CleanName(a.name);
            string nameB = CleanName(b.name);
            int orderA = sourceOrder.ContainsKey(nameA) ? sourceOrder[nameA] : 999;
            int orderB = sourceOrder.ContainsKey(nameB) ? sourceOrder[nameB] : 999;
            return orderA.CompareTo(orderB);
        });

        for (int i = 0; i < targetChildren.Count; i++)
            targetChildren[i].SetSiblingIndex(i);
    }

    /// <summary>
    /// Strips Unity's " (1)", " (2)" duplicate suffixes for name matching.
    /// </summary>
    private static string CleanName(string name)
    {
        return System.Text.RegularExpressions.Regex.Replace(name, @"\s*\(\d+\)$", "").Trim();
    }

    // ── Validation ───────────────────────────────────────────────

    [MenuItem("Tools/ClockworkCraft/Sync Selected Panel → Dark", true)]
    public static bool ValidateSyncSelected()
    {
        if (Selection.activeGameObject == null) return false;
        var panel = Selection.activeGameObject.GetComponent<UIPanel>();
        return panel != null && !panel.isDarkMode && panel.pairedPanel != null;
    }
}
#endif
