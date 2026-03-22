#pragma warning disable CS0414, CS0219, CS0618
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using ClockworkGrid;
using ClockworkCraft;
using LittleCafe;

/// <summary>
/// Custom inspector for EconomyBalanceConfig.
/// Provides a clean balancing view with:
///   - "Sync from Databases" button to pull all placeable items
///   - Grouped by source database (Buildings, Workers, Furniture)
///   - Per-item: icon preview, up to 3 resource cost rows
///   - Per-cost: ResourceType dropdown + base cost + increment
///   - Compact layout designed for rapid balancing iteration
/// </summary>
[CustomEditor(typeof(EconomyBalanceConfig))]
public class EconomyBalanceConfigEditor : Editor
{
    private EconomyBalanceConfig config;
    private Dictionary<ItemSourceDatabase, bool> foldouts = new Dictionary<ItemSourceDatabase, bool>();
    private Dictionary<string, bool> itemFoldouts = new Dictionary<string, bool>();
    private Vector2 scrollPos;

    // ResourceType names cache for dropdown
    private string[] resourceTypeNames;
    private ResourceType[] resourceTypeValues;

    private void OnEnable()
    {
        config = (EconomyBalanceConfig)target;

        foldouts[ItemSourceDatabase.Building] = true;
        foldouts[ItemSourceDatabase.Worker] = true;
        foldouts[ItemSourceDatabase.Furniture] = true;

        CacheResourceTypes();
    }

    private void CacheResourceTypes()
    {
        var types = System.Enum.GetValues(typeof(ResourceType)).Cast<ResourceType>().ToList();
        resourceTypeNames = types.Select(t => t.ToString()).ToArray();
        resourceTypeValues = types.ToArray();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // ── Header ─────────────────────────────────────────────────────
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Economy Balance Config", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Central placement costs for all items. Each item can have up to 3 resource costs.\n" +
            "Increment = how much the cost rises per successful placement.",
            MessageType.Info);

        EditorGUILayout.Space(4);

        // ── Sync Button ────────────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Sync from Databases", GUILayout.Height(28)))
        {
            SyncFromDatabases();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        // ── Entry Count ────────────────────────────────────────────────
        int buildingCount = config.entries.Count(e => e.sourceDatabase == ItemSourceDatabase.Building);
        int workerCount = config.entries.Count(e => e.sourceDatabase == ItemSourceDatabase.Worker);
        int furnitureCount = config.entries.Count(e => e.sourceDatabase == ItemSourceDatabase.Furniture);
        EditorGUILayout.LabelField($"Entries: {config.entries.Count} total  |  Buildings: {buildingCount}  |  Workers: {workerCount}  |  Furniture: {furnitureCount}",
            EditorStyles.miniLabel);

        EditorGUILayout.Space(4);

        // ── Grouped Entries ────────────────────────────────────────────
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        DrawGroup(ItemSourceDatabase.Building, "Buildings", new Color(0.9f, 0.75f, 0.5f));
        DrawGroup(ItemSourceDatabase.Worker, "Workers", new Color(0.5f, 0.8f, 0.9f));
        DrawGroup(ItemSourceDatabase.Furniture, "Furniture", new Color(0.7f, 0.9f, 0.6f));

        EditorGUILayout.EndScrollView();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(config);
        }

        serializedObject.ApplyModifiedProperties();
    }

    // ── Draw a Database Group ──────────────────────────────────────────

    private void DrawGroup(ItemSourceDatabase source, string label, Color headerColor)
    {
        var groupEntries = config.entries.Where(e => e.sourceDatabase == source).ToList();
        if (groupEntries.Count == 0) return;

        EditorGUILayout.Space(4);

        // Group header with colored background
        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = headerColor;
        foldouts[source] = EditorGUILayout.BeginFoldoutHeaderGroup(foldouts[source],
            $"  {label} ({groupEntries.Count})");
        GUI.backgroundColor = prevBg;

        if (foldouts[source])
        {
            EditorGUI.indentLevel++;

            foreach (var entry in groupEntries)
            {
                DrawItemEntry(entry);
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    // ── Draw a Single Item Entry ───────────────────────────────────────

    private void DrawItemEntry(ItemEconomyEntry entry)
    {
        string key = $"{entry.sourceDatabase}_{entry.itemName}";
        if (!itemFoldouts.ContainsKey(key))
            itemFoldouts[key] = false;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Item header row: icon + name + cost summary
        EditorGUILayout.BeginHorizontal();

        // Icon thumbnail
        if (entry.icon != null)
        {
            GUILayout.Label(new GUIContent(entry.icon.texture), GUILayout.Width(32), GUILayout.Height(32));
        }
        else
        {
            GUILayout.Label("?", EditorStyles.boldLabel, GUILayout.Width(32), GUILayout.Height(32));
        }

        // Foldout with name
        string costSummary = GetCostSummary(entry);
        string displayLabel = string.IsNullOrEmpty(costSummary)
            ? $"{entry.itemName}  (free)"
            : $"{entry.itemName}  [{costSummary}]";

        itemFoldouts[key] = EditorGUILayout.Foldout(itemFoldouts[key], displayLabel, true, EditorStyles.foldout);

        EditorGUILayout.EndHorizontal();

        // Expanded: show cost slots
        if (itemFoldouts[key])
        {
            EditorGUI.indentLevel++;

            // Ensure we have exactly 3 cost slots
            while (entry.costs.Count < 3)
                entry.costs.Add(new ResourceCostEntry());

            // Column headers
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16);
            EditorGUILayout.LabelField("Resource", EditorStyles.miniLabel, GUILayout.Width(120));
            EditorGUILayout.LabelField("Base Cost", EditorStyles.miniLabel, GUILayout.Width(70));
            EditorGUILayout.LabelField("+ Per Place", EditorStyles.miniLabel, GUILayout.Width(70));
            EditorGUILayout.LabelField("After 5x", EditorStyles.miniLabel, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < 3; i++)
            {
                DrawCostRow(entry.costs[i], i);
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    // ── Draw a Single Cost Row ─────────────────────────────────────────

    private void DrawCostRow(ResourceCostEntry cost, int index)
    {
        EditorGUILayout.BeginHorizontal();

        // Resource type dropdown
        int currentIndex = System.Array.IndexOf(resourceTypeValues, cost.resourceType);
        if (currentIndex < 0) currentIndex = 0;
        int newIndex = EditorGUILayout.Popup(currentIndex, resourceTypeNames, GUILayout.Width(120));
        cost.resourceType = resourceTypeValues[newIndex];

        // Base cost
        cost.baseCost = EditorGUILayout.IntField(cost.baseCost, GUILayout.Width(70));

        // Increment
        cost.costIncrement = EditorGUILayout.IntField(cost.costIncrement, GUILayout.Width(70));

        // Preview: cost after 5 placements
        if (cost.resourceType != ResourceType.None && cost.baseCost > 0)
        {
            int after5 = cost.baseCost + (5 * cost.costIncrement);
            EditorGUILayout.LabelField($"→ {after5}", EditorStyles.miniLabel, GUILayout.Width(60));
        }
        else
        {
            EditorGUILayout.LabelField("—", EditorStyles.miniLabel, GUILayout.Width(60));
        }

        EditorGUILayout.EndHorizontal();
    }

    // ── Cost Summary String ────────────────────────────────────────────

    private string GetCostSummary(ItemEconomyEntry entry)
    {
        var parts = new List<string>();
        foreach (var c in entry.costs)
        {
            if (c.resourceType == ResourceType.None || c.baseCost <= 0) continue;
            string inc = c.costIncrement > 0 ? $"+{c.costIncrement}" : "";
            parts.Add($"{c.baseCost}{inc} {c.resourceType}");
        }
        return string.Join(", ", parts);
    }

    // ── Sync from Databases ────────────────────────────────────────────

    private void SyncFromDatabases()
    {
        Undo.RecordObject(config, "Sync Economy from Databases");

        // Build lookup of existing entries so we don't lose configured costs
        var existing = new Dictionary<string, ItemEconomyEntry>();
        foreach (var e in config.entries)
            existing[$"{e.sourceDatabase}_{e.itemName}"] = e;

        var newEntries = new List<ItemEconomyEntry>();

        // Buildings
        var buildingDBs = FindAllAssets<BuildingDatabase>();
        foreach (var db in buildingDBs)
        {
            foreach (var building in db.AllBuildings)
            {
                string key = $"{ItemSourceDatabase.Building}_{building.GetCleanName()}";
                if (existing.TryGetValue(key, out var ex))
                {
                    ex.icon = building.icon;
                    newEntries.Add(ex);
                }
                else
                {
                    var entry = new ItemEconomyEntry
                    {
                        itemName = building.GetCleanName(),
                        sourceDatabase = ItemSourceDatabase.Building,
                        icon = building.icon,
                        costs = new List<ResourceCostEntry>
                        {
                            new ResourceCostEntry(),
                            new ResourceCostEntry(),
                            new ResourceCostEntry()
                        }
                    };
                    newEntries.Add(entry);
                }
            }
        }

        // Workers
        var workerDBs = FindAllAssets<WorkerDatabase>();
        foreach (var db in workerDBs)
        {
            foreach (var worker in db.AllWorkers)
            {
                string key = $"{ItemSourceDatabase.Worker}_{worker.GetCleanName()}";
                if (existing.TryGetValue(key, out var ex))
                {
                    ex.icon = worker.icon;
                    newEntries.Add(ex);
                }
                else
                {
                    var entry = new ItemEconomyEntry
                    {
                        itemName = worker.GetCleanName(),
                        sourceDatabase = ItemSourceDatabase.Worker,
                        icon = worker.icon,
                        costs = new List<ResourceCostEntry>
                        {
                            new ResourceCostEntry(),
                            new ResourceCostEntry(),
                            new ResourceCostEntry()
                        }
                    };
                    newEntries.Add(entry);
                }
            }
        }

        // Furniture
        var furnitureDBs = FindAllAssets<FurnitureDatabase>();
        foreach (var db in furnitureDBs)
        {
            foreach (var furn in db.AllFurniture)
            {
                string key = $"{ItemSourceDatabase.Furniture}_{furn.GetCleanName()}";
                if (existing.TryGetValue(key, out var ex))
                {
                    ex.icon = furn.icon;
                    newEntries.Add(ex);
                }
                else
                {
                    var entry = new ItemEconomyEntry
                    {
                        itemName = furn.GetCleanName(),
                        sourceDatabase = ItemSourceDatabase.Furniture,
                        icon = furn.icon,
                        costs = new List<ResourceCostEntry>
                        {
                            new ResourceCostEntry(),
                            new ResourceCostEntry(),
                            new ResourceCostEntry()
                        }
                    };
                    newEntries.Add(entry);
                }
            }
        }

        config.entries = newEntries;
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();

        Debug.Log($"[EconomyBalanceConfig] Synced {newEntries.Count} entries " +
                  $"(Buildings: {newEntries.Count(e => e.sourceDatabase == ItemSourceDatabase.Building)}, " +
                  $"Workers: {newEntries.Count(e => e.sourceDatabase == ItemSourceDatabase.Worker)}, " +
                  $"Furniture: {newEntries.Count(e => e.sourceDatabase == ItemSourceDatabase.Furniture)})");
    }

    // ── Utility ────────────────────────────────────────────────────────

    private static List<T> FindAllAssets<T>() where T : ScriptableObject
    {
        var result = new List<T>();
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) result.Add(asset);
        }
        return result;
    }
}
#endif
