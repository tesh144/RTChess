#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using ClockworkGrid;
using ClockworkCraft;

namespace LittleCafe.Editor
{
    /// <summary>
    /// Editor window that syncs data from Google Sheets (via cached JSON) to ScriptableObject databases.
    ///
    /// Workflow:
    ///   1. Claude reads Google Sheets via MCP and writes Assets/Scripts/Editor/SheetCache.json
    ///   2. Open this window: ClockworkCraft → Sheet Sync
    ///   3. Click "Sync Buildings", "Sync Workers", or "Sync Environment" to apply
    ///
    /// The JSON cache acts as an intermediary so Unity doesn't need Google API credentials.
    /// Claude updates the cache each session when asked to sync.
    ///
    /// Partial class split:
    ///   SheetSyncEditor.cs      — core: window, OnGUI, cache loading, DB finding, UI helpers
    ///   SheetSyncValidator.cs   — ValidateCacheColumns, ParseCacheJson, column constants, parse helpers
    ///   SheetSyncOperations.cs  — SyncBuildings, SyncWorkers, SyncUnits
    ///   SheetSyncEnvironment.cs — SyncEnvironment, SyncPOI, SyncDrawButton, SyncPlacementCosts
    /// </summary>
    public partial class SheetSyncEditor : EditorWindow
    {
        internal const string CACHE_PATH = "Assets/Scripts/Editor/SheetCache.json";

        private Vector2 scrollPos;
        private SheetCacheData cachedData;
        private string lastSyncTime = "Never";
        private string statusMessage = "";
        private MessageType statusType = MessageType.Info;

        // Database references (auto-found)
        private BuildingDatabase buildingDB;
        private WorkerDatabase workerDB;
        private UnitDatabase unitDB;
        private EnvironmentDatabase environmentDB;
        private PlacementCostsDatabase placementCostsDB;
        // POI data now lives on POIManager (scene object), not a ScriptableObject

        [MenuItem("ClockworkCraft/Sheet Sync")]
        public static void ShowWindow()
        {
            var window = GetWindow<SheetSyncEditor>("Sheet Sync");
            window.minSize = new Vector2(400, 500);
        }

        private void OnEnable()
        {
            FindDatabases();
            LoadCache();
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            // Header
            EditorGUILayout.LabelField("Google Sheets → ScriptableObject Sync", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // Cache status
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Cache File", CACHE_PATH);
            EditorGUILayout.LabelField("Last Synced", lastSyncTime);

            if (cachedData == null)
            {
                EditorGUILayout.HelpBox(
                    "No SheetCache.json found. Ask Claude to sync from Google Sheets first.\n" +
                    "Claude will read the sheets and write the cache file automatically.",
                    MessageType.Warning);
            }

            if (GUILayout.Button("Reload Cache"))
                LoadCache();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(8);

            // Database references
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Database References", EditorStyles.boldLabel);
            buildingDB = (BuildingDatabase)EditorGUILayout.ObjectField("Building DB", buildingDB, typeof(BuildingDatabase), false);
            workerDB = (WorkerDatabase)EditorGUILayout.ObjectField("Worker DB", workerDB, typeof(WorkerDatabase), false);
            unitDB = (UnitDatabase)EditorGUILayout.ObjectField("Unit DB", unitDB, typeof(UnitDatabase), false);
            environmentDB = (EnvironmentDatabase)EditorGUILayout.ObjectField("Environment DB", environmentDB, typeof(EnvironmentDatabase), false);
            placementCostsDB = (PlacementCostsDatabase)EditorGUILayout.ObjectField("Placement Costs DB", placementCostsDB, typeof(PlacementCostsDatabase), false);
            EditorGUILayout.LabelField("POI", "→ POIManager (scene)");
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(8);

            // Sync buttons
            GUI.enabled = cachedData != null;

            // Buildings
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Buildings & Production", EditorStyles.boldLabel);
            if (cachedData?.sheets != null && cachedData.sheets.ContainsKey(SheetKey.Buildings))
            {
                var sheet = cachedData.sheets[SheetKey.Buildings];
                EditorGUILayout.LabelField($"  {sheet.rows.Count} entries in cache");
                DrawSheetPreview(sheet);
            }

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = cachedData != null && buildingDB != null;
            if (GUILayout.Button("Sync Buildings", GUILayout.Height(28)))
                SyncBuildings();
            GUI.enabled = cachedData != null;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);

            // Workers
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Workers & Entities", EditorStyles.boldLabel);
            if (cachedData?.sheets != null && cachedData.sheets.ContainsKey(SheetKey.Workers))
            {
                var sheet = cachedData.sheets[SheetKey.Workers];
                EditorGUILayout.LabelField($"  {sheet.rows.Count} entries in cache");
                DrawSheetPreview(sheet);
            }

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = cachedData != null && workerDB != null;
            if (GUILayout.Button("Sync Workers", GUILayout.Height(28)))
                SyncWorkers();
            GUI.enabled = cachedData != null;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);

            // Units (enemies/monsters — shares Workers & Entities sheet)
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Units (Enemies/Monsters)", EditorStyles.boldLabel);
            if (cachedData?.sheets != null && cachedData.sheets.ContainsKey(SheetKey.Workers))
            {
                var unitSheet = cachedData.sheets[SheetKey.Workers];
                int enemyCount = unitSheet.rows.Count(r => GetValue(r, Col.AttackBehavior).Equals("Hostile", StringComparison.OrdinalIgnoreCase));
                EditorGUILayout.LabelField($"  {enemyCount} enemy entities in cache");
            }

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = cachedData != null && unitDB != null;
            if (GUILayout.Button("Sync Units", GUILayout.Height(28)))
                SyncUnits();
            GUI.enabled = cachedData != null;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);

            // Environment
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Environment & Loot", EditorStyles.boldLabel);
            if (cachedData?.sheets != null && cachedData.sheets.ContainsKey(SheetKey.Environment))
            {
                var sheet = cachedData.sheets[SheetKey.Environment];
                EditorGUILayout.LabelField($"  {sheet.rows.Count} entries in cache");
                DrawSheetPreview(sheet);
            }

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = cachedData != null && environmentDB != null;
            if (GUILayout.Button("Sync Environment", GUILayout.Height(28)))
                SyncEnvironment();
            GUI.enabled = cachedData != null;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Draw Button
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Draw Button", EditorStyles.boldLabel);
            if (cachedData?.sheets != null && cachedData.sheets.ContainsKey(SheetKey.DrawButton))
            {
                var sheet = cachedData.sheets[SheetKey.DrawButton];
                EditorGUILayout.LabelField($"  {sheet.rows.Count} levels in cache");
            }

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = cachedData != null;
            if (GUILayout.Button("Sync Draw Button", GUILayout.Height(28)))
                SyncDrawButton();
            GUI.enabled = cachedData != null;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Points of Interest
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Points of Interest", EditorStyles.boldLabel);
            if (cachedData?.sheets != null && cachedData.sheets.ContainsKey(SheetKey.POI))
            {
                var sheet = cachedData.sheets[SheetKey.POI];
                EditorGUILayout.LabelField($"  {sheet.rows.Count} entries in cache");
                DrawSheetPreview(sheet);
            }

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = cachedData != null;
            if (GUILayout.Button("Sync POI", GUILayout.Height(28)))
                SyncPOI();
            GUI.enabled = cachedData != null;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Placement Costs
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Placement Costs", EditorStyles.boldLabel);
            if (cachedData?.sheets != null && cachedData.sheets.ContainsKey(SheetKey.PlacementCosts))
            {
                var sheet = cachedData.sheets[SheetKey.PlacementCosts];
                var itemCount = new HashSet<string>();
                foreach (var row in sheet.rows) { var n = GetValue(row, Col.Item); if (!string.IsNullOrEmpty(n)) itemCount.Add(n); }
                EditorGUILayout.LabelField($"  {itemCount.Count} items ({string.Join(", ", itemCount)}), {sheet.rows.Count} cost entries in cache");
            }

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = cachedData != null && placementCostsDB != null;
            if (GUILayout.Button("Sync Placement Costs", GUILayout.Height(28)))
                SyncPlacementCosts();
            GUI.enabled = cachedData != null;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // Sync All
            GUI.enabled = cachedData != null;
            if (GUILayout.Button("Sync All Databases", GUILayout.Height(36)))
            {
                if (buildingDB != null) SyncBuildings();
                if (workerDB != null) SyncWorkers();
                if (unitDB != null) SyncUnits();
                if (environmentDB != null) SyncEnvironment();
                SyncPOI();
                SyncDrawButton();
                if (placementCostsDB != null) SyncPlacementCosts();
            }
            GUI.enabled = true;

            // Status
            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(statusMessage, statusType);
            }

            EditorGUILayout.EndScrollView();
        }

        // ─────────────────────────────────────────────────────────────────
        // Cache Loading
        // ─────────────────────────────────────────────────────────────────

        private void LoadCache()
        {
            string fullPath = Path.Combine(Application.dataPath, "..", CACHE_PATH);
            if (!File.Exists(fullPath))
            {
                cachedData = null;
                lastSyncTime = "No cache file found";
                return;
            }

            try
            {
                string json = File.ReadAllText(fullPath);
                cachedData = ParseCacheJson(json);
                lastSyncTime = cachedData?.lastSynced ?? "Unknown";

                // Validate that every column this editor reads is present in the cache.
                // A mismatch here means the sheet was updated and the cache regenerated
                // without also updating the column references in this file.
                var columnErrors = ValidateCacheColumns(cachedData);
                if (columnErrors.Count > 0)
                {
                    string msg = "Cache column mismatch — sync is BLOCKED until resolved:\n" +
                                 string.Join("\n", columnErrors);
                    SetStatus(msg, MessageType.Error);
                    cachedData = null;   // prevent any sync buttons from firing
                }
                else
                {
                    SetStatus($"Cache loaded — {cachedData?.sheets?.Count ?? 0} sheets", MessageType.Info);
                }
            }
            catch (Exception e)
            {
                cachedData = null;
                SetStatus($"Failed to load cache: {e.Message}", MessageType.Error);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Database Finding
        // ─────────────────────────────────────────────────────────────────

        private void FindDatabases()
        {
            if (buildingDB == null)     buildingDB = FindAsset<BuildingDatabase>();
            if (workerDB == null)       workerDB = FindAsset<WorkerDatabase>();
            if (unitDB == null)         unitDB = FindAsset<UnitDatabase>();
            if (environmentDB == null)  environmentDB = FindAsset<EnvironmentDatabase>();
            if (placementCostsDB == null) placementCostsDB = FindAsset<PlacementCostsDatabase>();
            // POI data syncs to POIManager (scene object) — no asset to find
        }

        private T FindAsset<T>() where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<T>(path);
            }
            return null;
        }

        // ─────────────────────────────────────────────────────────────────
        // UI Helpers
        // ─────────────────────────────────────────────────────────────────

        private void DrawSheetPreview(SheetData sheet)
        {
            if (sheet.rows.Count == 0) return;

            EditorGUI.indentLevel++;
            foreach (var row in sheet.rows)
            {
                string label = row.Values.FirstOrDefault() ?? "(empty)";
                EditorGUILayout.LabelField($"  • {label}", EditorStyles.miniLabel);
            }
            EditorGUI.indentLevel--;
        }

        private void SetStatus(string msg, MessageType type)
        {
            statusMessage = msg;
            statusType = type;
            Debug.Log($"[SheetSync] {msg}");
            Repaint();
        }

        // ─────────────────────────────────────────────────────────────────
        // Data Classes
        // ─────────────────────────────────────────────────────────────────

        [Serializable]
        private class SheetCacheData
        {
            public string lastSynced;
            public string spreadsheetId;
            public Dictionary<string, SheetData> sheets;
        }

        [Serializable]
        private class SheetData
        {
            public List<string> headers;
            public List<Dictionary<string, string>> rows;
        }
    }
}
