#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using ClockworkGrid;

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

            EditorGUILayout.Space(8);

            // Sync All
            GUI.enabled = cachedData != null;
            if (GUILayout.Button("Sync All Databases", GUILayout.Height(36)))
            {
                if (buildingDB != null) SyncBuildings();
                if (workerDB != null) SyncWorkers();
                if (unitDB != null) SyncUnits();
                if (environmentDB != null) SyncEnvironment();
                SyncDrawButton();
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
                SetStatus($"Cache loaded — {cachedData?.sheets?.Count ?? 0} sheets", MessageType.Info);
            }
            catch (Exception e)
            {
                cachedData = null;
                SetStatus($"Failed to load cache: {e.Message}", MessageType.Error);
            }
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

                // Production
                changed |= TrySetFloat(ref existing.productionInterval, GetValue(row, "Prod. Interval (s)"));
                changed |= TrySetFloat(ref existing.productionIntervalBonus, GetValue(row, "Interval Bonus (s)"));
                changed |= TrySetInt(ref existing.productionAmount, GetValue(row, "Output Amt"));

                // Input type
                string inputStr = GetValue(row, "Input");
                ProductionInputType newInput = ParseInputType(inputStr);
                if (existing.productionInputType != newInput) { existing.productionInputType = newInput; changed = true; }

                // Output type
                string outputStr = GetValue(row, "Output");
                ProductionOutputType newOutput = ParseOutputType(outputStr);
                if (existing.productionOutputType != newOutput) { existing.productionOutputType = newOutput; changed = true; }

                // Reveal radius
                changed |= TrySetInt(ref existing.fogRevealRadius, GetValue(row, "Reveal Radius"));

                // HP / Attack
                changed |= TrySetInt(ref existing.hp, GetValue(row, "HP"));
                changed |= TrySetInt(ref existing.attackPower, GetValue(row, "Attack"));

                // isMealSource: derived from "Ally Interactible" column
                string interactible = GetValue(row, "Ally Interactible");
                bool newMealSource = !string.IsNullOrEmpty(interactible) &&
                    (interactible.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || interactible == "1");
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

                // Production resource cost
                string costResStr = StripEmoji(GetValue(row, "Cost Resource")).Replace(" ", "");
                if (!string.IsNullOrEmpty(costResStr) && !costResStr.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    if (Enum.TryParse<ClockworkCraft.ResourceType>(costResStr, true, out var costRes))
                        if (existing.productionCostResourceType != costRes) { existing.productionCostResourceType = costRes; changed = true; }
                }
                changed |= TrySetInt(ref existing.productionCostAmount, GetValue(row, "Cost Amount"));

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

                // Slot Takeable
                string slotStr = GetValue(row, "Slot Takeable");
                if (!string.IsNullOrEmpty(slotStr))
                {
                    bool newSlotTakeable = slotStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || slotStr == "1";
                    if (existing.isSlotTakeable != newSlotTakeable) { existing.isSlotTakeable = newSlotTakeable; changed = true; }
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

                // Derive isEnemy from Attack Behavior column (Hostile = enemy, Peaceful = ally)
                string attackBehavior = GetValue(row, "Attack Behavior");
                bool isEnemy = attackBehavior.Equals("Hostile", StringComparison.OrdinalIgnoreCase);
                // Only sync enemy/monster entries to UnitDatabase
                if (!isEnemy) continue;

                string cleanName = entity.Split('(')[0].Trim();
                var existing = unitList.FirstOrDefault(u =>
                    u.assetName == cleanName ||
                    u.assetName == entity ||
                    u.assetName.Equals(cleanName, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    Debug.Log($"[SheetSync] Unit '{entity}' not found in UnitDatabase — skipping.");
                    continue;
                }

                bool changed = false;

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

                // Slot Takeable
                string slotStr = GetValue(row, "Slot Takeable");
                bool slotTakeable = slotStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase);
                if (existing.isSlotTakeable != slotTakeable)
                {
                    existing.isSlotTakeable = slotTakeable;
                    changed = true;
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

                // HP
                changed |= TrySetInt(ref existing.hp, GetValue(row, "HP"));

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
            if (Enum.TryParse<ProductionInputType>(clean, true, out var result)) return result;
            return ProductionInputType.None;
        }

        private ProductionOutputType ParseOutputType(string s)
        {
            string clean = StripEmoji(s);
            if (string.IsNullOrEmpty(clean) || clean == "None") return ProductionOutputType.None;
            // Sheet uses "Feast" but enum uses "Meal" — alias for compatibility
            if (clean.Equals("Feast", StringComparison.OrdinalIgnoreCase)) return ProductionOutputType.Meal;
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

    // ─────────────────────────────────────────────────────────────────
    // MiniJson — lightweight JSON parser (public domain)
    // Handles Dictionary<string, object> and List<object> nesting
    // ─────────────────────────────────────────────────────────────────
    public static class MiniJson
    {
        public static object Deserialize(string json)
        {
            if (json == null) return null;
            return Parser.Parse(json);
        }

        private sealed class Parser : IDisposable
        {
            StringReader reader;

            public static object Parse(string jsonString)
            {
                using (var instance = new Parser(jsonString))
                    return instance.ParseValue();
            }

            Parser(string jsonString) { reader = new StringReader(jsonString); }

            public void Dispose() { reader.Dispose(); }

            Dictionary<string, object> ParseObject()
            {
                var table = new Dictionary<string, object>();
                reader.Read(); // skip {
                while (true)
                {
                    var token = NextToken();
                    if (token == TOKEN.CURLY_CLOSE) break;
                    if (token != TOKEN.STRING) break;
                    string key = ParseString();
                    if (NextToken() != TOKEN.COLON) break;
                    table[key] = ParseValue();
                    token = NextToken();
                    if (token == TOKEN.CURLY_CLOSE) break;
                }
                return table;
            }

            List<object> ParseArray()
            {
                var array = new List<object>();
                reader.Read(); // skip [
                while (true)
                {
                    var token = PeekToken();
                    if (token == TOKEN.SQUARE_CLOSE) { reader.Read(); break; }
                    array.Add(ParseValue());
                    token = NextToken();
                    if (token == TOKEN.SQUARE_CLOSE) break;
                }
                return array;
            }

            object ParseValue()
            {
                var token = PeekToken();
                switch (token)
                {
                    case TOKEN.STRING: return ParseString();
                    case TOKEN.NUMBER: return ParseNumber();
                    case TOKEN.CURLY_OPEN: return ParseObject();
                    case TOKEN.SQUARE_OPEN: return ParseArray();
                    case TOKEN.TRUE: reader.Read(); reader.Read(); reader.Read(); reader.Read(); return true;
                    case TOKEN.FALSE: reader.Read(); reader.Read(); reader.Read(); reader.Read(); reader.Read(); return false;
                    case TOKEN.NULL: reader.Read(); reader.Read(); reader.Read(); reader.Read(); return null;
                    default: return null;
                }
            }

            string ParseString()
            {
                var s = new System.Text.StringBuilder();
                reader.Read(); // skip opening quote
                while (true)
                {
                    int c = reader.Read();
                    if (c == -1 || c == '"') break;
                    if (c == '\\')
                    {
                        c = reader.Read();
                        switch (c)
                        {
                            case '"': case '\\': case '/': s.Append((char)c); break;
                            case 'b': s.Append('\b'); break;
                            case 'f': s.Append('\f'); break;
                            case 'n': s.Append('\n'); break;
                            case 'r': s.Append('\r'); break;
                            case 't': s.Append('\t'); break;
                            case 'u':
                                var hex = new char[4];
                                for (int i = 0; i < 4; i++) hex[i] = (char)reader.Read();
                                s.Append((char)Convert.ToInt32(new string(hex), 16));
                                break;
                        }
                    }
                    else
                    {
                        s.Append((char)c);
                    }
                }
                return s.ToString();
            }

            object ParseNumber()
            {
                var s = new System.Text.StringBuilder();
                while ("0123456789+-.eE".IndexOf((char)reader.Peek()) != -1)
                {
                    s.Append((char)reader.Read());
                    if (reader.Peek() == -1) break;
                }
                string numStr = s.ToString();
                if (numStr.Contains(".") || numStr.Contains("e") || numStr.Contains("E"))
                {
                    double.TryParse(numStr, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double d);
                    return d;
                }
                long.TryParse(numStr, out long l);
                return l;
            }

            void SkipWhitespace()
            {
                while (" \t\n\r".IndexOf((char)reader.Peek()) != -1) reader.Read();
            }

            enum TOKEN { NONE, CURLY_OPEN, CURLY_CLOSE, SQUARE_OPEN, SQUARE_CLOSE, COLON, COMMA, STRING, NUMBER, TRUE, FALSE, NULL }

            TOKEN PeekToken()
            {
                SkipWhitespace();
                int c = reader.Peek();
                switch (c)
                {
                    case '{': return TOKEN.CURLY_OPEN;
                    case '}': return TOKEN.CURLY_CLOSE;
                    case '[': return TOKEN.SQUARE_OPEN;
                    case ']': return TOKEN.SQUARE_CLOSE;
                    case ':': return TOKEN.COLON;
                    case ',': return TOKEN.COMMA;
                    case '"': return TOKEN.STRING;
                    case 't': return TOKEN.TRUE;
                    case 'f': return TOKEN.FALSE;
                    case 'n': return TOKEN.NULL;
                    default: return "0123456789-".IndexOf((char)c) != -1 ? TOKEN.NUMBER : TOKEN.NONE;
                }
            }

            TOKEN NextToken()
            {
                SkipWhitespace();
                int c = reader.Peek();
                switch (c)
                {
                    case '{': reader.Read(); return TOKEN.CURLY_OPEN;
                    case '}': reader.Read(); return TOKEN.CURLY_CLOSE;
                    case '[': reader.Read(); return TOKEN.SQUARE_OPEN;
                    case ']': reader.Read(); return TOKEN.SQUARE_CLOSE;
                    case ':': reader.Read(); return TOKEN.COLON;
                    case ',': reader.Read(); return TOKEN.COMMA;
                    case '"': return TOKEN.STRING;
                    default: return TOKEN.NONE;
                }
            }
        }
    }
}
