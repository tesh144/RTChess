#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using UnityEditor;
using ClockworkCraft;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Custom PropertyDrawer for ResourceType fields.
/// When a CurrencyDatabase asset exists in the project, shows currency names
/// from that database as the dropdown options instead of raw enum names.
/// Falls back to the default enum popup if no CurrencyDatabase is found.
///
/// This makes the CurrencyDatabase the single source of truth for currency
/// names — EnvironmentData, ResourceNode, and any other script that uses
/// ResourceType will show friendly names like "Gold", "Wood", "Clay" etc.
/// </summary>
[CustomPropertyDrawer(typeof(ResourceType))]
public class ResourceTypeDrawer : PropertyDrawer
{
    // Cache the database reference so we don't scan every frame
    private static CurrencyDatabase cachedDB;
    private static double lastScanTime;
    private const double SCAN_INTERVAL = 5.0; // Re-scan every 5 seconds

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        CurrencyDatabase db = FindCurrencyDatabase();

        if (db == null || db.AllCurrencies.Count == 0)
        {
            // Fallback: standard enum popup
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        // Build display names and ResourceType values from database
        var entries = db.AllCurrencies;

        // Always include "None" as the first option
        List<string> displayNames = new List<string> { "None (No Loot)" };
        List<int> enumValues = new List<int> { (int)ResourceType.None };

        foreach (var entry in entries)
        {
            if (entry.resourceType == ResourceType.None) continue;

            string displayName = !string.IsNullOrEmpty(entry.currencyName)
                ? $"{entry.currencyName}"
                : entry.resourceType.ToString();

            // Add emoji hint if available
            if (!string.IsNullOrEmpty(entry.fallbackEmoji))
                displayName = $"{entry.fallbackEmoji}  {displayName}";

            displayNames.Add(displayName);
            enumValues.Add((int)entry.resourceType);
        }

        // Find current selection index
        int currentValue = property.intValue;
        int selectedIndex = enumValues.IndexOf(currentValue);
        if (selectedIndex < 0) selectedIndex = 0; // Default to None

        // Draw popup
        EditorGUI.BeginProperty(position, label, property);
        int newIndex = EditorGUI.Popup(position, label.text, selectedIndex, displayNames.ToArray());
        if (newIndex != selectedIndex && newIndex >= 0 && newIndex < enumValues.Count)
        {
            property.intValue = enumValues[newIndex];
        }
        EditorGUI.EndProperty();
    }

    private static CurrencyDatabase FindCurrencyDatabase()
    {
        // Use cached version if recent
        if (cachedDB != null && EditorApplication.timeSinceStartup - lastScanTime < SCAN_INTERVAL)
            return cachedDB;

        lastScanTime = EditorApplication.timeSinceStartup;

        // Search for CurrencyDatabase assets in the project
        string[] guids = AssetDatabase.FindAssets("t:CurrencyDatabase");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            cachedDB = AssetDatabase.LoadAssetAtPath<CurrencyDatabase>(path);
            return cachedDB;
        }

        cachedDB = null;
        return null;
    }
}
