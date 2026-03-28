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
    /// </summary>
    public class SheetSyncEditor : EditorWindow
    {
        private const string CACHE_PATH = "Assets/Scripts/Editor/SheetCache.json";

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
            if (cachedData?.sheets != null && cachedData.sheets.ContainsKey("Buildings & Production"))
            {
                var sheet = cachedData.sheets["Buildings & Production"];
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
            if (cachedData?.sheets != null && cachedData.sheets.ContainsKey("Workers & Entities"))
            {
                var sheet = cachedData.sheets["Workers & Entities"];
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
            if (cachedData?.sheets != null && cachedData.sheets.ContainsKey("Workers & Entities"))
            {
                var unitSheet = cachedData.sheets["Workers & Entities"];
                int enemyCount = unitSheet.rows.Count(r => GetValue(r, "Attack Behavior").Equals("Hostile", StringComparison.OrdinalIgnoreCase));
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
            if (cachedData?.sheets != null && cachedData.sheets.ContainsKey("Environment & Loot"))
            {
                var sheet = cachedData.sheets["Environment & Loot"];
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
            if (cachedData?.sheets != null && cachedData.sheets.ContainsKey("DrawButton"))
            {
                var sheet = cachedData.sheets["DrawButton"];
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
            if (cachedData?.sheets != null && cachedData.sheets.ContainsKey("PointsOfInterest"))
            {
                var sheet = cachedData.sheets["PointsOfInterest"];
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
            if (cachedData?.sheets != null && cachedData.sheets.ContainsKey("Placement Costs"))
            {
                var sheet = cachedData.sheets["Placement Costs"];
                var itemCount = new HashSet<string>();
                foreach (var row in sheet.rows) { var n = GetValue(row, "Item"); if (!string.IsNullOrEmpty(n)) itemCount.Add(n); }
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
                cachedData = JsonUtility.FromJson<SheetCacheData>(json);

                // JsonUtility doesn't handle Dictionary — parse manually
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

        /// <summary>
        /// Checks that every column key this editor reads by name is present in the
        /// corresponding cached sheet. Returns one error string per missing column.
        /// If the cache or a sheet is absent the check is skipped for that sheet
        /// (the missing-sheet warning is handled elsewhere).
        /// </summary>
        private List<string> ValidateCacheColumns(SheetCacheData data)
        {
            var errors = new List<string>();
            if (data?.sheets == null) return errors;

            void Check(string sheetKey, params string[] required)
            {
                if (!data.sheets.TryGetValue(sheetKey, out var sheet)) return;
                var headers = new HashSet<string>(sheet.headers);
                foreach (var col in required)
                    if (!headers.Contains(col))
                        errors.Add($"  [{sheetKey}] missing column: \"{col}\"");
            }

            Check("Buildings & Production",
                "Building", "Active", "Prod. Interval (s)", "Interval Bonus (s)",
                "Input Card", "Output Card", "Reveal Radius", "HP", "Attack",
                "Ally Interactible", "Enemy Interactible", "Wild Animal Interactible",
                "Killer's Behavior", "Tier (button)", "DrawWeight", "isRandomBuilding",
                "Resource Use", "Resource Amount", "Resource Increment", "BuildOn");

            Check("Workers & Entities",
                "Entity", "Type", "Active", "HP", "Attack Power",
                "Movement Behavior", "Draw Weight", "Killer's Behavior", "Tier (button)", "Walkable");

            Check("Environment & Loot",
                "Object", "Active", "MapGenerated", "Type", "Drops", "Loot per Hit",
                "HP", "Total Yield", "Ally Interactible", "Enemy Interactible",
                "Wild Animal Interactible", "Killer's Behavior", "Drop on Death");

            Check("DrawButton",
                "Draw Button Order", "Output", "Cost Type", "Cost Amount", "Cooldown (s)");

            Check("PointsOfInterest",
                "Active", "Object", "Grouping", "Quantity Minimum",
                "Name", "Color", "Reward Type", "Reward Quantity");

            Check("Placement Costs",
                "Item", "Tier", "#",
                "Currency 1", "Cost 1",
                "Currency 2", "Cost 2",
                "Currency 3", "Cost 3");

            return errors;
        }

        /// <summary>
        /// Manual JSON parsing because JsonUtility can't handle Dictionary or heterogeneous structures.
        /// Uses a lightweight approach — parse the known structure.
        /// </summary>
        private SheetCacheData ParseCacheJson(string json)
        {
            var data = new SheetCacheData();

            // Use Unity's built-in JSON parser for the simple parts
            // For the complex nested structure, we'll use a simple line-by-line approach
            // Actually, let's use MiniJSON or manual parsing

            // Simple approach: use System.Text.Json is not available in Unity,
            // so we'll parse with a lightweight helper
            var parsed = MiniJson.Deserialize(json) as Dictionary<string, object>;
            if (parsed == null) return null;

            data.lastSynced = parsed.ContainsKey("lastSynced") ? parsed["lastSynced"] as string : "";
            data.spreadsheetId = parsed.ContainsKey("spreadsheetId") ? parsed["spreadsheetId"] as string : "";
            data.sheets = new Dictionary<string, SheetData>();

            if (parsed.ContainsKey("sheets") && parsed["sheets"] is Dictionary<string, object> sheets)
            {
                foreach (var kvp in sheets)
                {
                    if (kvp.Value is Dictionary<string, object> sheetDict)
                    {
                        var sheetData = new SheetData();
                        sheetData.headers = new List<string>();
                        sheetData.rows = new List<Dictionary<string, string>>();

                        if (sheetDict.ContainsKey("headers") && sheetDict["headers"] is List<object> headers)
                        {
                            foreach (var h in headers)
                                sheetData.headers.Add(h?.ToString() ?? "");
                        }

                        if (sheetDict.ContainsKey("rows") && sheetDict["rows"] is List<object> rows)
                        {
                            foreach (var rowObj in rows)
                            {
                                if (rowObj is Dictionary<string, object> rowDict)
                                {
                                    var row = new Dictionary<string, string>();
                                    foreach (var cell in rowDict)
                                        row[cell.Key] = cell.Value?.ToString() ?? "";
                                    sheetData.rows.Add(row);
                                }
                            }
                        }

                        data.sheets[kvp.Key] = sheetData;
                    }
                }
            }

            return data;
        }

        // ─────────────────────────────────────────────────────────────────
        // Database Finding
        // ─────────────────────────────────────────────────────────────────

        private void FindDatabases()
        {
            if (buildingDB == null)
                buildingDB = FindAsset<BuildingDatabase>();
            if (workerDB == null)
                workerDB = FindAsset<WorkerDatabase>();
            if (unitDB == null)
                unitDB = FindAsset<UnitDatabase>();
            if (environmentDB == null)
                environmentDB = FindAsset<EnvironmentDatabase>();
            if (placementCostsDB == null)
                placementCostsDB = FindAsset<PlacementCostsDatabase>();
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
        // Sync: Buildings
        // ─────────────────────────────────────────────────────────────────

        private void SyncBuildings()
        {
            if (buildingDB == null || cachedData?.sheets == null) return;
            if (!cachedData.sheets.ContainsKey("Buildings & Production")) return;

            var sheet = cachedData.sheets["Buildings & Production"];
            var buildingList = buildingDB.AllBuildings;
            int updated = 0;
            int added = 0;

            foreach (var row in sheet.rows)
            {
                string name = GetValue(row, "Building");
                if (string.IsNullOrEmpty(name)) continue;

                var existing = buildingList.FirstOrDefault(b => b.assetName == name);
                if (existing == null)
                {
                    Debug.Log($"[SheetSync] New building '{name}' in sheet — add it manually in Inspector first, then re-sync.");
                    continue;
                }

                bool changed = false;

                // Active flag
                string activeStr = GetValue(row, "Active");
                bool newActive = string.IsNullOrEmpty(activeStr) || activeStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || activeStr == "1";
                if (existing.active != newActive) { existing.active = newActive; changed = true; }

                // Production
                changed |= TrySetFloat(ref existing.productionInterval, GetValue(row, "Prod. Interval (s)"));
                changed |= TrySetFloat(ref existing.productionIntervalBonus, GetValue(row, "Interval Bonus (s)"));

                // Input type (sheet column is "Input Card")
                string inputStr = GetValue(row, "Input Card");
                ProductionInputType newInput = ParseInputType(inputStr);
                if (existing.productionInputType != newInput) { existing.productionInputType = newInput; changed = true; }

                // Output type (sheet column is "Output Card")
                string outputStr = GetValue(row, "Output Card");
                ProductionOutputType newOutput = ParseOutputType(outputStr);
                if (existing.productionOutputType != newOutput) { existing.productionOutputType = newOutput; changed = true; }

                // BuildOn (placement surface requirement)
                string buildOnVal = GetValue(row, "BuildOn");
                if (!string.IsNullOrEmpty(buildOnVal) && existing.buildOn != buildOnVal)
                { existing.buildOn = buildOnVal; changed = true; }

                // Reveal radius
                changed |= TrySetInt(ref existing.fogRevealRadius, GetValue(row, "Reveal Radius"));

                // HP / Attack
                changed |= TrySetInt(ref existing.hp, GetValue(row, "HP"));
                changed |= TrySetInt(ref existing.attackPower, GetValue(row, "Attack"));

                // isMealSource: true only for buildings that are ally-interactible AND have no production
                // output (i.e. Feast). Buildings like Scrapper/Hutch/Garden are ally-interactible
                // for other reasons and must not get FeastVisualDegradation attached.
                string interactible = GetValue(row, "Ally Interactible");
                bool allyInteractible = !string.IsNullOrEmpty(interactible) &&
                    (interactible.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || interactible == "1");
                bool newMealSource = allyInteractible && newOutput == ProductionOutputType.None;
                if (existing.isMealSource != newMealSource) { existing.isMealSource = newMealSource; changed = true; }

                // Killer's Behavior: Advance = true, Stay = false
                string killerStr = GetValue(row, "Killer's Behavior");
                bool newKillerAdvances = killerStr.Equals("Advance", StringComparison.OrdinalIgnoreCase);
                if (existing.killerAdvances != newKillerAdvances) { existing.killerAdvances = newKillerAdvances; changed = true; }

                // Tier
                string tierStr = GetValue(row, "Tier (button)");
                if (!string.IsNullOrEmpty(tierStr))
                {
                    int newTier = -1;
                    if (tierStr.StartsWith("Tier ", StringComparison.OrdinalIgnoreCase))
                        int.TryParse(tierStr.Substring(5).Trim(), out newTier);
                    else if (tierStr == "-")
                        newTier = -1;
                    else
                        int.TryParse(tierStr, out newTier);
                    if (existing.tier != newTier) { existing.tier = newTier; changed = true; }
                }

                // DrawWeight
                changed |= TrySetFloat(ref existing.drawWeight, GetValue(row, "DrawWeight"));

                // isRandomBuilding
                string randomStr = GetValue(row, "isRandomBuilding");
                if (!string.IsNullOrEmpty(randomStr))
                {
                    bool newIsRandom = randomStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || randomStr == "1";
                    if (existing.isRandomBuilding != newIsRandom) { existing.isRandomBuilding = newIsRandom; changed = true; }
                }

                // Interaction categories
                string allyStr = GetValue(row, "Ally Interactible");
                if (!string.IsNullOrEmpty(allyStr))
                {
                    bool newAlly = allyStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || allyStr == "1";
                    if (existing.allyInteractible != newAlly) { existing.allyInteractible = newAlly; changed = true; }
                }
                string enemyStr = GetValue(row, "Enemy Interactible");
                if (!string.IsNullOrEmpty(enemyStr))
                {
                    bool newEnemy = enemyStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || enemyStr == "1";
                    if (existing.enemyInteractible != newEnemy) { existing.enemyInteractible = newEnemy; changed = true; }
                }
                string wildStr = GetValue(row, "Wild Animal Interactible");
                if (!string.IsNullOrEmpty(wildStr))
                {
                    bool newWild = wildStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || wildStr == "1";
                    if (existing.wildAnimalInteractible != newWild) { existing.wildAnimalInteractible = newWild; changed = true; }
                }

                // Production resource cost (sheet columns: "Resource Use" and "Resource Amount")
                string costResStr = StripEmoji(GetValue(row, "Resource Use")).Replace(" ", "");
                if (!string.IsNullOrEmpty(costResStr) && costResStr != "-" && !costResStr.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    if (Enum.TryParse<ClockworkCraft.ResourceType>(costResStr, true, out var costRes))
                        if (existing.productionCostResourceType != costRes) { existing.productionCostResourceType = costRes; changed = true; }
                }
                else if (costResStr == "-" || costResStr.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    if (existing.productionCostResourceType != ClockworkCraft.ResourceType.None) { existing.productionCostResourceType = ClockworkCraft.ResourceType.None; changed = true; }
                }
                string costAmtStr = GetValue(row, "Resource Amount");
                if (costAmtStr != "-")
                    changed |= TrySetInt(ref existing.productionCostAmount, costAmtStr);
                string costIncStr = GetValue(row, "Resource Increment");
                if (costIncStr != "-")
                    changed |= TrySetInt(ref existing.productionCostIncrement, costIncStr);

                if (changed)
                {
                    updated++;
                    Debug.Log($"[SheetSync] Updated building: {name}");
                }
            }

            EditorUtility.SetDirty(buildingDB);
            AssetDatabase.SaveAssets();
            SetStatus($"Buildings synced: {updated} updated, {added} added", MessageType.Info);
        }

        // ─────────────────────────────────────────────────────────────────
        // Sync: Workers
        // ─────────────────────────────────────────────────────────────────

        private void SyncWorkers()
        {
            if (workerDB == null || cachedData?.sheets == null) return;
            if (!cachedData.sheets.ContainsKey("Workers & Entities")) return;

            var sheet = cachedData.sheets["Workers & Entities"];
            var workerList = workerDB.AllWorkers;
            int updated = 0;

            foreach (var row in sheet.rows)
            {
                string entity = GetValue(row, "Entity");
                string type = GetValue(row, "Type");
                if (string.IsNullOrEmpty(entity) || string.IsNullOrEmpty(type)) continue;

                // Try to find by name (clean version without parenthetical)
                string cleanName = entity.Split('(')[0].Trim();
                var existing = workerList.FirstOrDefault(w =>
                    w.assetName == cleanName ||
                    w.assetName == entity ||
                    w.GetCleanName() == cleanName);

                if (existing == null)
                {
                    Debug.Log($"[SheetSync] Worker '{entity}' not found in database — skipping.");
                    continue;
                }

                bool changed = false;

                // Active flag
                string activeStr = GetValue(row, "Active");
                bool newActive = string.IsNullOrEmpty(activeStr) || activeStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || activeStr == "1";
                if (existing.active != newActive) { existing.active = newActive; changed = true; }

                // HP
                changed |= TrySetInt(ref existing.hp, GetValue(row, "HP"));

                // Attack
                changed |= TrySetInt(ref existing.attackPower, GetValue(row, "Attack Power"));

                // Behavior (column: "Movement Behavior")
                string behaviorStr = GetValue(row, "Movement Behavior");
                if (!string.IsNullOrEmpty(behaviorStr))
                {
                    if (Enum.TryParse<BehaviorType>(behaviorStr, true, out var newBehavior))
                    {
                        if (existing.behaviorType != newBehavior)
                        {
                            existing.behaviorType = newBehavior;
                            changed = true;
                        }
                    }
                }

                // Draw weight
                float dw = 0;
                if (float.TryParse(GetValue(row, "Draw Weight"),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out dw))
                {
                    if (Math.Abs(existing.drawWeight - dw) > 0.001f) { existing.drawWeight = dw; changed = true; }
                }

                // Killer's Behavior: Advance = true, Stay = false
                string killerStr = GetValue(row, "Killer's Behavior");
                if (!string.IsNullOrEmpty(killerStr))
                {
                    bool newKillerAdvances = killerStr.Equals("Advance", StringComparison.OrdinalIgnoreCase);
                    if (existing.killerAdvances != newKillerAdvances) { existing.killerAdvances = newKillerAdvances; changed = true; }
                }

                // Tier
                string tierStr = GetValue(row, "Tier (button)");
                if (!string.IsNullOrEmpty(tierStr))
                {
                    int newTier = -1;
                    if (tierStr.StartsWith("Tier ", StringComparison.OrdinalIgnoreCase))
                        int.TryParse(tierStr.Substring(5).Trim(), out newTier);
                    else if (tierStr == "-")
                        newTier = -1;
                    else
                        int.TryParse(tierStr, out newTier);
                    if (existing.tier != newTier) { existing.tier = newTier; changed = true; }
                }

                if (changed)
                {
                    updated++;
                    Debug.Log($"[SheetSync] Updated worker: {entity}");
                }
            }

            EditorUtility.SetDirty(workerDB);
            AssetDatabase.SaveAssets();
            SetStatus($"Workers synced: {updated} updated", MessageType.Info);
        }

        // ─────────────────────────────────────────────────────────────────
        // Sync: Units (enemies/monsters from Workers & Entities sheet)
        // ─────────────────────────────────────────────────────────────────

        private void SyncUnits()
        {
            if (unitDB == null || cachedData?.sheets == null) return;
            if (!cachedData.sheets.ContainsKey("Workers & Entities")) return;

            var sheet = cachedData.sheets["Workers & Entities"];
            var unitList = unitDB.AllUnits;
            int updated = 0;

            foreach (var row in sheet.rows)
            {
                string entity = GetValue(row, "Entity");
                string type = GetValue(row, "Type");
                if (string.IsNullOrEmpty(entity) || string.IsNullOrEmpty(type)) continue;

                // Read isEnemy from the explicit "Enemy" column; fall back to Attack Behavior if absent
                string enemyCol = GetValue(row, "Enemy");
                bool isEnemy;
                if (!string.IsNullOrEmpty(enemyCol))
                    isEnemy = enemyCol.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || enemyCol == "1";
                else
                {
                    string attackBehavior = GetValue(row, "Attack Behavior");
                    isEnemy = attackBehavior.Equals("Hostile", StringComparison.OrdinalIgnoreCase);
                }

                // Only sync Hostile units OR Corruption-type entities to UnitDatabase
                // (Corruption hearts are Peaceful but still live in UnitDatabase)
                bool isCorruption = type.Equals("Corruption", StringComparison.OrdinalIgnoreCase);
                if (!isEnemy && !isCorruption) continue;

                string cleanName = entity.Split('(')[0].Trim();
                // Normalise spaces for matching — sheet entries may use "Corrupted Heart"
                // while UnitDatabase assetNames use "CorruptedHeart" (no spaces).
                string cleanNameNoSpaces = cleanName.Replace(" ", "");
                var existing = unitList.FirstOrDefault(u =>
                    u.assetName == cleanName ||
                    u.assetName == entity ||
                    u.assetName.Equals(cleanName, StringComparison.OrdinalIgnoreCase) ||
                    u.assetName.Replace(" ", "").Equals(cleanNameNoSpaces, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    Debug.Log($"[SheetSync] Unit '{entity}' not found in UnitDatabase — skipping.");
                    continue;
                }

                bool changed = false;

                // Active flag
                string activeStr = GetValue(row, "Active");
                bool newActive = string.IsNullOrEmpty(activeStr) || activeStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || activeStr == "1";
                if (existing.active != newActive) { existing.active = newActive; changed = true; }

                // HP
                changed |= TrySetInt(ref existing.hp, GetValue(row, "HP"));

                // Attack
                changed |= TrySetInt(ref existing.attackPower, GetValue(row, "Attack Power"));

                // Behavior (Movement Behavior column)
                string behaviorStr = GetValue(row, "Movement Behavior");
                if (!string.IsNullOrEmpty(behaviorStr))
                {
                    if (Enum.TryParse<BehaviorType>(behaviorStr, true, out var newBehavior))
                    {
                        if (existing.behaviorType != newBehavior)
                        {
                            existing.behaviorType = newBehavior;
                            changed = true;
                        }
                    }
                }

                // isEnemy
                if (existing.isEnemy != isEnemy)
                {
                    existing.isEnemy = isEnemy;
                    changed = true;
                }

                // MapGenerated flag
                string mapGenStr = GetValue(row, "MapGenerated");
                if (!string.IsNullOrEmpty(mapGenStr))
                {
                    bool newMapGen = mapGenStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || mapGenStr == "1";
                    if (existing.isMapGenerated != newMapGen) { existing.isMapGenerated = newMapGen; changed = true; }
                }

                // GameUnitType — sync from Type column
                string typeStr = GetValue(row, "Type");
                if (!string.IsNullOrEmpty(typeStr))
                {
                    if (Enum.TryParse<LittleCafe.GameUnitType>(typeStr, true, out var newUnitType))
                    {
                        if (existing.type != newUnitType) { existing.type = newUnitType; changed = true; }
                    }
                }

                // Draw weight
                float dw = 0;
                if (float.TryParse(GetValue(row, "Draw Weight"),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out dw))
                {
                    if (Math.Abs(existing.drawWeight - dw) > 0.001f) { existing.drawWeight = dw; changed = true; }
                }

                // Killer's Behavior: Advance = true, Stay = false
                string killerStr = GetValue(row, "Killer's Behavior");
                if (!string.IsNullOrEmpty(killerStr))
                {
                    bool newKillerAdvances = killerStr.Equals("Advance", StringComparison.OrdinalIgnoreCase);
                    if (existing.killerAdvances != newKillerAdvances) { existing.killerAdvances = newKillerAdvances; changed = true; }
                }

                // Drop on Death — resource type dropped when this unit is destroyed
                string dropOnDeathStr = StripEmoji(GetValue(row, "Drop on Death"));
                if (!string.IsNullOrEmpty(dropOnDeathStr))
                {
                    ClockworkCraft.ResourceType newDropOnDeath = ClockworkCraft.ResourceType.None;
                    if (!dropOnDeathStr.Equals("None", StringComparison.OrdinalIgnoreCase))
                        Enum.TryParse<ClockworkCraft.ResourceType>(dropOnDeathStr.Replace(" ", ""), true, out newDropOnDeath);
                    if (existing.dropOnDeath != newDropOnDeath) { existing.dropOnDeath = newDropOnDeath; changed = true; }
                }

                // Loot resource type (from Drops column)
                string unitDrops = StripEmoji(GetValue(row, "Drops"));
                if (!string.IsNullOrEmpty(unitDrops) && !unitDrops.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    if (Enum.TryParse<ClockworkCraft.ResourceType>(unitDrops.Replace(" ", ""), true, out var lootRt))
                    {
                        if (existing.lootResourceType != lootRt) { existing.lootResourceType = lootRt; changed = true; }
                    }
                }

                // Loot per hit
                changed |= TrySetInt(ref existing.lootHpCost, GetValue(row, "Loot per Hit"));

                if (changed)
                {
                    updated++;
                    Debug.Log($"[SheetSync] Updated unit: {entity}");
                }
            }

            EditorUtility.SetDirty(unitDB);
            AssetDatabase.SaveAssets();
            SetStatus($"Units synced: {updated} updated", MessageType.Info);
        }

        // ─────────────────────────────────────────────────────────────────
        // Sync: Environment
        // ─────────────────────────────────────────────────────────────────

        private void SyncEnvironment()
        {
            if (environmentDB == null || cachedData?.sheets == null) return;
            if (!cachedData.sheets.ContainsKey("Environment & Loot")) return;

            var sheet = cachedData.sheets["Environment & Loot"];
            var envList = environmentDB.AllEnvironment;
            int updated = 0;

            foreach (var row in sheet.rows)
            {
                string objName = GetValue(row, "Object");
                if (string.IsNullOrEmpty(objName)) continue;

                var existing = envList.FirstOrDefault(e =>
                    e.assetName == objName ||
                    e.assetName.Equals(objName, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    Debug.Log($"[SheetSync] Environment '{objName}' not found in database — skipping.");
                    continue;
                }

                bool changed = false;

                // Active flag
                string activeStr = GetValue(row, "Active");
                bool newActive = string.IsNullOrEmpty(activeStr) || activeStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || activeStr == "1";
                if (existing.active != newActive) { existing.active = newActive; changed = true; }

                // MapGenerated flag
                string mapGenStr = GetValue(row, "MapGenerated");
                if (!string.IsNullOrEmpty(mapGenStr))
                {
                    bool newMapGen = mapGenStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || mapGenStr == "1";
                    if (existing.isMapGenerated != newMapGen) { existing.isMapGenerated = newMapGen; changed = true; }
                }

                // HP
                changed |= TrySetInt(ref existing.hp, GetValue(row, "HP"));

                // Layer type (Object vs Surface — from "Type" column)
                string layerTypeStr = GetValue(row, "Type");
                if (!string.IsNullOrEmpty(layerTypeStr))
                {
                    if (System.Enum.TryParse<LittleCafe.EnvironmentLayerType>(layerTypeStr.Trim(), true, out var newLayerType))
                    {
                        if (existing.layerType != newLayerType) { existing.layerType = newLayerType; changed = true; }
                    }
                }

                // Loot per hit
                changed |= TrySetInt(ref existing.lootYield, GetValue(row, "Loot per Hit"));

                // Loot resource type (from Drops column — strip emoji prefix like "💰 Gold" → "Gold")
                string drops = StripEmoji(GetValue(row, "Drops"));
                if (!string.IsNullOrEmpty(drops))
                {
                    if (Enum.TryParse<ClockworkCraft.ResourceType>(drops.Replace(" ", ""), true, out var rt))
                    {
                        if (existing.lootResourceType != rt)
                        {
                            existing.lootResourceType = rt;
                            changed = true;
                        }
                    }
                }

                // Killer's Behavior: Advance = true, Stay = false
                string killerStr = GetValue(row, "Killer's Behavior");
                if (!string.IsNullOrEmpty(killerStr))
                {
                    bool newKillerAdvances = killerStr.Equals("Advance", StringComparison.OrdinalIgnoreCase);
                    if (existing.killerAdvances != newKillerAdvances) { existing.killerAdvances = newKillerAdvances; changed = true; }
                }

                // Drop on Death — resource type dropped when this object is destroyed
                string dropOnDeathStr = StripEmoji(GetValue(row, "Drop on Death"));
                if (!string.IsNullOrEmpty(dropOnDeathStr))
                {
                    ClockworkCraft.ResourceType newDropOnDeath = ClockworkCraft.ResourceType.None;
                    if (!dropOnDeathStr.Equals("None", StringComparison.OrdinalIgnoreCase))
                        Enum.TryParse<ClockworkCraft.ResourceType>(dropOnDeathStr.Replace(" ", ""), true, out newDropOnDeath);
                    if (existing.dropOnDeath != newDropOnDeath) { existing.dropOnDeath = newDropOnDeath; changed = true; }
                }

                // Interaction categories
                string allyStr = GetValue(row, "Ally Interactible");
                if (!string.IsNullOrEmpty(allyStr))
                {
                    bool newAlly = allyStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || allyStr == "1";
                    if (existing.allyInteractible != newAlly) { existing.allyInteractible = newAlly; changed = true; }
                }
                string enemyStr = GetValue(row, "Enemy Interactible");
                if (!string.IsNullOrEmpty(enemyStr))
                {
                    bool newEnemy = enemyStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || enemyStr == "1";
                    if (existing.enemyInteractible != newEnemy) { existing.enemyInteractible = newEnemy; changed = true; }
                }
                string wildStr = GetValue(row, "Wild Animal Interactible");
                if (!string.IsNullOrEmpty(wildStr))
                {
                    bool newWild = wildStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || wildStr == "1";
                    if (existing.wildAnimalInteractible != newWild) { existing.wildAnimalInteractible = newWild; changed = true; }
                }

                if (changed)
                {
                    updated++;
                    Debug.Log($"[SheetSync] Updated environment: {objName}");
                }
            }

            EditorUtility.SetDirty(environmentDB);
            AssetDatabase.SaveAssets();
            SetStatus($"Environment synced: {updated} updated", MessageType.Info);
        }

        // ─────────────────────────────────────────────────────────────────
        // Sync: Points of Interest
        // ─────────────────────────────────────────────────────────────────

        private void SyncPOI()
        {
            if (cachedData?.sheets == null) return;
            if (!cachedData.sheets.ContainsKey("PointsOfInterest")) return;

            // POI data lives on POIManager (scene object), same pattern as DrawButtonController
            var poiManager = GameObject.FindFirstObjectByType<POIManager>();
            if (poiManager == null)
            {
                SetStatus("POIManager not found in scene", MessageType.Warning);
                return;
            }

            var sheet = cachedData.sheets["PointsOfInterest"];
            var entries = poiManager.Entries;
            entries.Clear();

            foreach (var row in sheet.rows)
            {
                // Sheet columns: Active, Object, Grouping, Quantity Minimum, Name, Color, Reward Type, Reward Quantity
                string activeStr = GetValue(row, "Active");
                if (activeStr == "FALSE") continue;

                string objectName = StripEmoji(GetValue(row, "Object"));
                if (string.IsNullOrEmpty(objectName)) continue;

                // Resolve sheet name to actual database assetName + source type
                string resolvedName = ResolveAssetName(objectName, out POISourceType sourceType);
                if (resolvedName == null)
                {
                    Debug.LogWarning($"[SheetSync] POI object '{objectName}' not found in any database — skipping.");
                    continue;
                }

                string labelText = GetValue(row, "Name");
                string groupingStr = GetValue(row, "Grouping");
                string quantityStr = GetValue(row, "Quantity Minimum");
                string colorStr = GetValue(row, "Color");
                string rewardTypeStr = StripEmoji(GetValue(row, "Reward Type"));
                string rewardQtyStr = GetValue(row, "Reward Quantity");

                // Parse grouping — sheet values: Singular, Cluster, Area
                POIGrouping grouping = POIGrouping.Singular;
                if (!string.IsNullOrEmpty(groupingStr))
                    System.Enum.TryParse(groupingStr, true, out grouping);

                // Parse tier from Color column
                POITier tier = POITier.Grey;
                if (!string.IsNullOrEmpty(colorStr))
                    System.Enum.TryParse(colorStr, true, out tier);

                // Parse reward
                int rewardQty = 0;
                int.TryParse(rewardQtyStr, out rewardQty);

                ResourceType rewardType = ResourceType.None;
                if (!string.IsNullOrEmpty(rewardTypeStr))
                    System.Enum.TryParse(rewardTypeStr.Replace(" ", ""), true, out rewardType);

                entries.Add(new POITypeData
                {
                    active = true,
                    typeName = resolvedName,
                    label = string.IsNullOrEmpty(labelText) ? objectName : labelText,
                    sourceType = sourceType,
                    groupingType = grouping,
                    quantityMinimum = int.TryParse(quantityStr, out int qMin) ? qMin : 1,
                    tier = tier,
                    rewardType = rewardType,
                    rewardQuantity = rewardQty
                });
            }

            EditorUtility.SetDirty(poiManager);
            SetStatus($"POI synced: {entries.Count} entries → POIManager", MessageType.Info);
            Debug.Log($"[SheetSync] POI synced: {entries.Count} entries to POIManager.");
        }

        /// <summary>
        /// Resolves a sheet object name (e.g. "Corrupted Heart") to the real database assetName
        /// (e.g. "CorruptedHeart") by searching EnvironmentDB, UnitDB, and BuildingDB.
        /// Matches by stripping spaces and comparing case-insensitively.
        /// Returns null if not found in any database.
        /// </summary>
        private string ResolveAssetName(string sheetName, out POISourceType sourceType)
        {
            sourceType = POISourceType.Environment;
            if (string.IsNullOrEmpty(sheetName)) return null;

            string normalized = sheetName.Replace(" ", "").ToLowerInvariant();

            // Search EnvironmentDatabase
            if (environmentDB != null)
            {
                foreach (var entry in environmentDB.AllEnvironment)
                {
                    if (entry.assetName.Replace(" ", "").ToLowerInvariant() == normalized)
                    {
                        sourceType = POISourceType.Environment;
                        return entry.assetName;
                    }
                }
            }

            // Search UnitDatabase
            if (unitDB != null)
            {
                foreach (var entry in unitDB.AllUnits)
                {
                    if (entry.assetName.Replace(" ", "").ToLowerInvariant() == normalized)
                    {
                        sourceType = POISourceType.Unit;
                        return entry.assetName;
                    }
                }
            }

            // Search BuildingDatabase
            if (buildingDB != null)
            {
                foreach (var entry in buildingDB.AllBuildings)
                {
                    if (entry.assetName.Replace(" ", "").ToLowerInvariant() == normalized)
                    {
                        sourceType = POISourceType.Building;
                        return entry.assetName;
                    }
                }
            }

            return null;
        }

        // ─────────────────────────────────────────────────────────────────
        // Sync: Draw Button
        // ─────────────────────────────────────────────────────────────────

        private void SyncDrawButton()
        {
            if (cachedData?.sheets == null) return;
            if (!cachedData.sheets.ContainsKey("DrawButton")) return;

            // Find DrawButtonController in the scene
            var controller = GameObject.FindFirstObjectByType<DrawButtonController>();
            if (controller == null)
            {
                SetStatus("DrawButtonController not found in scene", MessageType.Warning);
                return;
            }

            var sheet = cachedData.sheets["DrawButton"];
            var entries = new List<DrawButtonEntry>();

            foreach (var row in sheet.rows)
            {
                string orderStr = GetValue(row, "Draw Button Order");
                if (string.IsNullOrEmpty(orderStr)) continue;
                if (!int.TryParse(orderStr, out int order)) continue;

                var entry = new DrawButtonEntry();
                entry.order = order;

                // Output — strip emoji, keep name as-is (None, Worker, Fighter, Home, RandomTier0, etc.)
                string output = StripEmoji(GetValue(row, "Output"));
                entry.outputName = string.IsNullOrEmpty(output) ? "None" : output;

                // Cost currency — strip emoji, parse ResourceType
                // Sheet header is "Cost Type" (e.g. "💰 Gold")
                string costCurrencyStr = StripEmoji(GetValue(row, "Cost Type"));
                if (!string.IsNullOrEmpty(costCurrencyStr))
                {
                    if (Enum.TryParse<ClockworkCraft.ResourceType>(costCurrencyStr.Replace(" ", ""), true, out var rt))
                        entry.costCurrency = rt;
                }

                // Cost value — sheet header is "Cost Amount"
                string valueStr = GetValue(row, "Cost Amount");
                if (!string.IsNullOrEmpty(valueStr))
                    int.TryParse(valueStr, out entry.costValue);

                // Cooldown
                string cooldownStr = GetValue(row, "Cooldown (s)");
                if (!string.IsNullOrEmpty(cooldownStr))
                    float.TryParse(cooldownStr, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out entry.cooldown);

                entries.Add(entry);
            }

            // Sort by order
            entries.Sort((a, b) => a.order.CompareTo(b.order));

            // Write to the serialized field via SerializedObject
            var so = new SerializedObject(controller);
            var prop = so.FindProperty("drawLevels");
            prop.ClearArray();
            for (int i = 0; i < entries.Count; i++)
            {
                prop.InsertArrayElementAtIndex(i);
                var elem = prop.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("order").intValue = entries[i].order;
                elem.FindPropertyRelative("outputName").stringValue = entries[i].outputName;
                elem.FindPropertyRelative("costCurrency").intValue = (int)entries[i].costCurrency;
                elem.FindPropertyRelative("costValue").intValue = entries[i].costValue;
                elem.FindPropertyRelative("cooldown").floatValue = entries[i].cooldown;
            }
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(controller);
            SetStatus($"Draw Button synced: {entries.Count} levels", MessageType.Info);
        }

        // ─────────────────────────────────────────────────────────────────
        // Sync: Placement Costs
        // ─────────────────────────────────────────────────────────────────

        private void SyncPlacementCosts()
        {
            if (placementCostsDB == null || cachedData?.sheets == null) return;
            if (!cachedData.sheets.ContainsKey("Placement Costs")) return;

            var sheet = cachedData.sheets["Placement Costs"];

            // Group rows by Item name
            var byItem = new Dictionary<string, List<Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in sheet.rows)
            {
                string item = GetValue(row, "Item");
                if (string.IsNullOrEmpty(item)) continue;
                if (!byItem.ContainsKey(item)) byItem[item] = new List<Dictionary<string, string>>();
                byItem[item].Add(row);
            }

            int updated = 0;
            int noMatch = 0;

            foreach (var kvp in byItem)
            {
                string sheetItemName = kvp.Key;
                var itemRows = kvp.Value;

                // Find matching entry in the database (flexible name match)
                var entry = FindPlacementEntry(sheetItemName);
                if (entry == null)
                {
                    Debug.LogWarning($"[SheetSync] Placement Costs — no matching entry for '{sheetItemName}'. " +
                                     "Run 'Sync from Databases' in the PlacementCostsDatabase inspector first.");
                    noMatch++;
                    continue;
                }

                // Sort rows by # (count) ascending
                itemRows.Sort((a, b) =>
                {
                    int.TryParse(GetValue(a, "#"), out int na);
                    int.TryParse(GetValue(b, "#"), out int nb);
                    return na.CompareTo(nb);
                });

                // Build cost tables for up to 3 slots
                // Determine which slots are non-empty by checking any row for a non-empty currency
                string[] currencyKeys = { "Currency 1", "Currency 2", "Currency 3" };
                string[] costKeys     = { "Cost 1",     "Cost 2",     "Cost 3"     };

                // Ensure 3 cost slots exist
                while (entry.costs.Count < 3)
                    entry.costs.Add(new ClockworkGrid.ResourceCostEntry());

                bool changed = false;
                for (int slot = 0; slot < 3; slot++)
                {
                    // Determine resource type from first non-empty value across all rows
                    string currencyName = "";
                    foreach (var r in itemRows)
                    {
                        string c = GetValue(r, currencyKeys[slot]);
                        if (!string.IsNullOrEmpty(c)) { currencyName = c; break; }
                    }

                    // Strip emoji, whitespace, and spaces then parse ResourceType
                    ClockworkCraft.ResourceType resType = ClockworkCraft.ResourceType.None;
                    if (!string.IsNullOrEmpty(currencyName))
                        Enum.TryParse<ClockworkCraft.ResourceType>(StripEmoji(currencyName).Replace(" ", "").Trim(), true, out resType);

                    // Build cost table (one entry per row, in order)
                    var costTable = new List<int>();
                    foreach (var r in itemRows)
                    {
                        string costStr = GetValue(r, costKeys[slot]);
                        int.TryParse(costStr, out int cost);
                        costTable.Add(cost);
                    }

                    var costEntry = entry.costs[slot];

                    // Only update if something changed
                    bool typeChanged = costEntry.resourceType != resType;
                    bool tableChanged = !CostTablesEqual(costEntry.costTable, costTable);

                    if (typeChanged || tableChanged)
                    {
                        costEntry.resourceType = resType;
                        costEntry.costTable = costTable;
                        // When using a cost table, base cost and increment are ignored at runtime
                        costEntry.baseCost = 0;
                        costEntry.costIncrement = 0;
                        changed = true;
                    }
                }

                if (changed)
                {
                    updated++;
                    Debug.Log($"[SheetSync] Updated placement costs for '{sheetItemName}' → entry '{entry.itemName}'");
                }
            }

            EditorUtility.SetDirty(placementCostsDB);
            AssetDatabase.SaveAssets();
            string msg = $"Placement Costs synced: {updated} updated";
            if (noMatch > 0) msg += $", {noMatch} unmatched (run 'Sync from Databases' in PlacementCostsDatabase inspector)";
            SetStatus(msg, noMatch > 0 ? MessageType.Warning : MessageType.Info);
        }

        /// <summary>
        /// Flexible name match: tries exact, then case-insensitive, then normalised (lowercase no-spaces),
        /// then singular/plural tolerance. Searches all entries regardless of source database.
        /// </summary>
        private ClockworkGrid.ItemEconomyEntry FindPlacementEntry(string sheetName)
        {
            if (placementCostsDB == null) return null;

            string norm = sheetName.ToLowerInvariant().Replace(" ", "");

            // 1. Exact
            foreach (var e in placementCostsDB.entries)
                if (e.itemName == sheetName) return e;

            // 2. Case-insensitive exact
            foreach (var e in placementCostsDB.entries)
                if (string.Equals(e.itemName, sheetName, StringComparison.OrdinalIgnoreCase)) return e;

            // 3. Normalised (strip spaces, lowercase)
            foreach (var e in placementCostsDB.entries)
                if (e.itemName.ToLowerInvariant().Replace(" ", "") == norm) return e;

            // 4. Singular/plural tolerance (sheet uses "Workers", db may use "Worker")
            string singular = norm.TrimEnd('s');
            foreach (var e in placementCostsDB.entries)
            {
                string entryNorm = e.itemName.ToLowerInvariant().Replace(" ", "");
                if (entryNorm == singular || entryNorm.TrimEnd('s') == singular) return e;
            }

            return null;
        }

        private static bool CostTablesEqual(List<int> a, List<int> b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i] != b[i]) return false;
            return true;
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
                // Show first identifying column
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
        // Parse Helpers
        // ─────────────────────────────────────────────────────────────────

        private string GetValue(Dictionary<string, string> row, string key)
        {
            return row.ContainsKey(key) ? row[key] : "";
        }

        /// <summary>
        /// Strips emoji prefix from values like "💰 Gold" → "Gold", "👷 Worker" → "Worker".
        /// Handles surrogate pairs and multi-byte emoji. Returns original string if no emoji found.
        /// </summary>
        private string StripEmoji(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            // Find first ASCII letter — everything before it is emoji/whitespace
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
                    return s.Substring(i);
            }
            return s;
        }

        private bool TrySetInt(ref int field, string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            if (int.TryParse(value, out int v) && field != v)
            {
                field = v;
                return true;
            }
            return false;
        }

        private bool TrySetFloat(ref float field, string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            if (float.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float v) && Math.Abs(field - v) > 0.001f)
            {
                field = v;
                return true;
            }
            return false;
        }

        private ProductionInputType ParseInputType(string s)
        {
            string clean = StripEmoji(s);
            if (string.IsNullOrEmpty(clean) || clean == "None") return ProductionInputType.None;
            // Sheet verbose values → enum names
            if (clean.StartsWith("Worker", StringComparison.OrdinalIgnoreCase)) return ProductionInputType.Worker;
            if (clean.Equals("Hold to Fill", StringComparison.OrdinalIgnoreCase) ||
                clean.Equals("HoldToFill", StringComparison.OrdinalIgnoreCase)) return ProductionInputType.HoldToFill;
            if (clean.StartsWith("Any", StringComparison.OrdinalIgnoreCase)) return ProductionInputType.Any;
            if (Enum.TryParse<ProductionInputType>(clean, true, out var result)) return result;
            return ProductionInputType.None;
        }

        private ProductionOutputType ParseOutputType(string s)
        {
            string clean = StripEmoji(s);
            if (string.IsNullOrEmpty(clean) || clean == "None") return ProductionOutputType.None;
            // Sheet verbose values → enum names
            if (clean.StartsWith("Worker", StringComparison.OrdinalIgnoreCase)) return ProductionOutputType.Worker;
            if (clean.Equals("Tree", StringComparison.OrdinalIgnoreCase)) return ProductionOutputType.TreeSeed;
            if (clean.Equals("Feast", StringComparison.OrdinalIgnoreCase)) return ProductionOutputType.Meal;
            if (clean.Equals("Lizard", StringComparison.OrdinalIgnoreCase)) return ProductionOutputType.Lizard;
            if (clean.Equals("Tree Seed", StringComparison.OrdinalIgnoreCase)) return ProductionOutputType.TreeSeed;
            if (Enum.TryParse<ProductionOutputType>(clean, true, out var result)) return result;
            return ProductionOutputType.None;
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

    // ─────────────────────────────────────────────────────────────────────────
    // MiniJSON — lightweight JSON deserializer (Darrell Bethea, MIT licence)
    // Parses JSON into Dictionary<string,object> / List<object> / primitives.
    // ─────────────────────────────────────────────────────────────────────────
    internal static class MiniJson
    {
        public static object Deserialize(string json)
        {
            if (json == null) return null;
            return Parser.Parse(json);
        }

        sealed class Parser : IDisposable
        {
            const string WORD_BREAK = "{}[],:\"";

            StringReader json;

            Parser(string jsonString) { json = new StringReader(jsonString); }

            public static object Parse(string jsonString)
            {
                using (var instance = new Parser(jsonString))
                    return instance.ParseValue();
            }

            public void Dispose() { json.Dispose(); }

            Dictionary<string, object> ParseObject()
            {
                var table = new Dictionary<string, object>();
                json.Read(); // {
                while (true)
                {
                    switch (NextToken)
                    {
                        case TOKEN.NONE: return null;
                        case TOKEN.COMMA: continue;
                        case TOKEN.CURLY_CLOSE: return table;
                        default:
                            string name = ParseString();
                            if (name == null) return null;
                            if (NextToken != TOKEN.COLON) return null;
                            json.Read();
                            table[name] = ParseValue();
                            break;
                    }
                }
            }

            List<object> ParseArray()
            {
                var array = new List<object>();
                json.Read(); // [
                bool parsing = true;
                while (parsing)
                {
                    TOKEN nextToken = NextToken;
                    switch (nextToken)
                    {
                        case TOKEN.NONE: return null;
                        case TOKEN.COMMA: continue;
                        case TOKEN.SQUARED_CLOSE: parsing = false; break;
                        default:
                            array.Add(ParseByToken(nextToken));
                            break;
                    }
                }
                return array;
            }

            object ParseValue()
            {
                TOKEN nextToken = NextToken;
                return ParseByToken(nextToken);
            }

            object ParseByToken(TOKEN token)
            {
                switch (token)
                {
                    case TOKEN.STRING: return ParseString();
                    case TOKEN.CURLY_OPEN: return ParseObject();
                    case TOKEN.SQUARED_OPEN: return ParseArray();
                    case TOKEN.TRUE: return true;
                    case TOKEN.FALSE: return false;
                    case TOKEN.NULL: return null;
                    default: return ParseNumber();
                }
            }

            string ParseString()
            {
                var s = new System.Text.StringBuilder();
                json.Read(); // "
                bool parsing = true;
                while (parsing)
                {
                    if (json.Peek() == -1) break;
                    char c = NextChar;
                    switch (c)
                    {
                        case '"': parsing = false; break;
                        case '\\':
                            if (json.Peek() == -1) { parsing = false; break; }
                            char escaped = NextChar;
                            switch (escaped)
                            {
                                case '"': s.Append('"'); break;
                                case '\\': s.Append('\\'); break;
                                case '/': s.Append('/'); break;
                                case 'b': s.Append('\b'); break;
                                case 'f': s.Append('\f'); break;
                                case 'n': s.Append('\n'); break;
                                case 'r': s.Append('\r'); break;
                                case 't': s.Append('\t'); break;
                                case 'u':
                                    var hex = new char[4];
                                    for (int i = 0; i < 4; i++) hex[i] = NextChar;
                                    s.Append((char)Convert.ToInt32(new string(hex), 16));
                                    break;
                            }
                            break;
                        default: s.Append(c); break;
                    }
                }
                return s.ToString();
            }

            object ParseNumber()
            {
                string number = NextWord;
                if (number.IndexOf('.') == -1)
                {
                    long.TryParse(number, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out long parsedLong);
                    return parsedLong;
                }
                double.TryParse(number, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double parsedDouble);
                return parsedDouble;
            }

            void EatWhitespace()
            {
                while (char.IsWhiteSpace((char)json.Peek()))
                {
                    json.Read();
                    if (json.Peek() == -1) break;
                }
            }

            char NextChar => (char)json.Read();

            TOKEN NextToken
            {
                get
                {
                    EatWhitespace();
                    if (json.Peek() == -1) return TOKEN.NONE;
                    switch ((char)json.Peek())
                    {
                        case '{': return TOKEN.CURLY_OPEN;
                        case '}': json.Read(); return TOKEN.CURLY_CLOSE;
                        case '[': return TOKEN.SQUARED_OPEN;
                        case ']': json.Read(); return TOKEN.SQUARED_CLOSE;
                        case ',': json.Read(); return TOKEN.COMMA;
                        case '"': return TOKEN.STRING;
                        case ':': return TOKEN.COLON;
                        case '0': case '1': case '2': case '3': case '4':
                        case '5': case '6': case '7': case '8': case '9':
                        case '-': return TOKEN.NUMBER;
                    }
                    string word = NextWord;
                    switch (word)
                    {
                        case "false": return TOKEN.FALSE;
                        case "true": return TOKEN.TRUE;
                        case "null": return TOKEN.NULL;
                    }
                    return TOKEN.NONE;
                }
            }

            string NextWord
            {
                get
                {
                    var word = new System.Text.StringBuilder();
                    while (!IsWordBreak((char)json.Peek()))
                    {
                        word.Append(NextChar);
                        if (json.Peek() == -1) break;
                    }
                    return word.ToString();
                }
            }

            bool IsWordBreak(char c) => char.IsWhiteSpace(c) || WORD_BREAK.IndexOf(c) != -1;

            enum TOKEN
            {
                NONE, CURLY_OPEN, CURLY_CLOSE, SQUARED_OPEN, SQUARED_CLOSE,
                COLON, COMMA, STRING, NUMBER, TRUE, FALSE, NULL
            }
        }
    }
}
