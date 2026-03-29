#pragma warning disable CS0414, CS0219, CS0618
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using ClockworkCraft;
using ClockworkGrid;

namespace LittleCafe.Editor
{
    /// <summary>
    /// Partial: SyncEnvironment, SyncPOI, SyncDrawButton, SyncPlacementCosts, and
    /// supporting helpers for SheetSyncEditor.
    /// See SheetSyncValidator.cs for column name constants (Col.*).
    /// </summary>
    public partial class SheetSyncEditor
    {
        // ─────────────────────────────────────────────────────────────────
        // Sync: Environment
        // ─────────────────────────────────────────────────────────────────

        private void SyncEnvironment()
        {
            if (environmentDB == null || cachedData?.sheets == null) return;
            if (!cachedData.sheets.ContainsKey(SheetKey.Environment)) return;

            var sheet   = cachedData.sheets[SheetKey.Environment];
            var envList = environmentDB.AllEnvironment;
            int updated = 0;

            foreach (var row in sheet.rows)
            {
                string objName = GetValue(row, Col.Object);
                if (string.IsNullOrEmpty(objName)) continue;

                var existing = envList.FirstOrDefault(e =>
                    e.assetName == objName ||
                    e.assetName.Equals(objName, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    UnityEngine.Debug.Log($"[SheetSync] Environment '{objName}' not found in database — skipping.");
                    continue;
                }

                bool changed = false;

                // Active flag
                string activeStr = GetValue(row, Col.Active);
                bool newActive = string.IsNullOrEmpty(activeStr) || activeStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || activeStr == "1";
                if (existing.active != newActive) { existing.active = newActive; changed = true; }

                // MapGenerated flag
                string mapGenStr = GetValue(row, Col.MapGenerated);
                if (!string.IsNullOrEmpty(mapGenStr))
                {
                    bool newMapGen = mapGenStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || mapGenStr == "1";
                    if (existing.isMapGenerated != newMapGen) { existing.isMapGenerated = newMapGen; changed = true; }
                }

                // HP
                changed |= TrySetInt(ref existing.hp, GetValue(row, Col.HP));

                // Layer type (Object vs Surface — from Type column)
                string layerTypeStr = GetValue(row, Col.Type);
                if (!string.IsNullOrEmpty(layerTypeStr) &&
                    System.Enum.TryParse<LittleCafe.EnvironmentLayerType>(layerTypeStr.Trim(), true, out var newLayerType))
                    if (existing.layerType != newLayerType) { existing.layerType = newLayerType; changed = true; }

                // Loot per hit
                changed |= TrySetInt(ref existing.lootYield, GetValue(row, Col.LootPerHit));

                // Loot resource type (strip emoji prefix e.g. "💰 Gold" → "Gold")
                string drops = StripEmoji(GetValue(row, Col.Drops));
                if (!string.IsNullOrEmpty(drops) &&
                    Enum.TryParse<ClockworkCraft.ResourceType>(drops.Replace(" ", ""), true, out var rt))
                    if (existing.lootResourceType != rt) { existing.lootResourceType = rt; changed = true; }

                // Killer's Behavior
                string killerStr = GetValue(row, Col.KillerBehavior);
                if (!string.IsNullOrEmpty(killerStr))
                {
                    bool newKillerAdvances = killerStr.Equals("Advance", StringComparison.OrdinalIgnoreCase);
                    if (existing.killerAdvances != newKillerAdvances) { existing.killerAdvances = newKillerAdvances; changed = true; }
                }

                // Drop on Death
                string dropOnDeathStr = StripEmoji(GetValue(row, Col.DropOnDeath));
                if (!string.IsNullOrEmpty(dropOnDeathStr))
                {
                    ClockworkCraft.ResourceType newDropOnDeath = ClockworkCraft.ResourceType.None;
                    if (!dropOnDeathStr.Equals("None", StringComparison.OrdinalIgnoreCase))
                        Enum.TryParse<ClockworkCraft.ResourceType>(dropOnDeathStr.Replace(" ", ""), true, out newDropOnDeath);
                    if (existing.dropOnDeath != newDropOnDeath) { existing.dropOnDeath = newDropOnDeath; changed = true; }
                }

                // Interaction categories
                string allyStr = GetValue(row, Col.AllyInteractible);
                if (!string.IsNullOrEmpty(allyStr))
                {
                    bool newAlly = allyStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || allyStr == "1";
                    if (existing.allyInteractible != newAlly) { existing.allyInteractible = newAlly; changed = true; }
                }
                string enemyStr = GetValue(row, Col.EnemyInteractible);
                if (!string.IsNullOrEmpty(enemyStr))
                {
                    bool newEnemy = enemyStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || enemyStr == "1";
                    if (existing.enemyInteractible != newEnemy) { existing.enemyInteractible = newEnemy; changed = true; }
                }
                string wildStr = GetValue(row, Col.WildInteractible);
                if (!string.IsNullOrEmpty(wildStr))
                {
                    bool newWild = wildStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || wildStr == "1";
                    if (existing.wildAnimalInteractible != newWild) { existing.wildAnimalInteractible = newWild; changed = true; }
                }

                if (changed)
                {
                    updated++;
                    UnityEngine.Debug.Log($"[SheetSync] Updated environment: {objName}");
                }
            }

            EditorUtility.SetDirty(environmentDB);
            AssetDatabase.SaveAssets();
            SetStatus($"Environment synced: {updated} updated", UnityEditor.MessageType.Info);
        }

        // ─────────────────────────────────────────────────────────────────
        // Sync: Points of Interest
        // ─────────────────────────────────────────────────────────────────

        private void SyncPOI()
        {
            if (cachedData?.sheets == null) return;
            if (!cachedData.sheets.ContainsKey(SheetKey.POI)) return;

            var poiManager = UnityEngine.GameObject.FindFirstObjectByType<POIManager>();
            if (poiManager == null)
            {
                SetStatus("POIManager not found in scene", UnityEditor.MessageType.Warning);
                return;
            }

            var sheet   = cachedData.sheets[SheetKey.POI];
            var entries = poiManager.Entries;
            entries.Clear();

            foreach (var row in sheet.rows)
            {
                string activeStr = GetValue(row, Col.Active);
                if (activeStr == "FALSE") continue;

                string objectName = StripEmoji(GetValue(row, Col.POIObject));
                if (string.IsNullOrEmpty(objectName)) continue;

                string resolvedName = ResolveAssetName(objectName, out POISourceType sourceType);
                if (resolvedName == null)
                {
                    UnityEngine.Debug.LogWarning($"[SheetSync] POI object '{objectName}' not found in any database — skipping.");
                    continue;
                }

                string labelText    = GetValue(row, Col.Name);
                string groupingStr  = GetValue(row, Col.Grouping);
                string quantityStr  = GetValue(row, Col.QuantityMinimum);
                string colorStr     = GetValue(row, Col.Color);
                string rewardTypeStr = StripEmoji(GetValue(row, Col.RewardType));
                string rewardQtyStr = GetValue(row, Col.RewardQuantity);

                POIGrouping grouping = POIGrouping.Singular;
                if (!string.IsNullOrEmpty(groupingStr))
                    System.Enum.TryParse(groupingStr, true, out grouping);

                POITier tier = POITier.Grey;
                if (!string.IsNullOrEmpty(colorStr))
                    System.Enum.TryParse(colorStr, true, out tier);

                int rewardQty = 0;
                int.TryParse(rewardQtyStr, out rewardQty);

                ResourceType rewardType = ResourceType.None;
                if (!string.IsNullOrEmpty(rewardTypeStr))
                    System.Enum.TryParse(rewardTypeStr.Replace(" ", ""), true, out rewardType);

                entries.Add(new POITypeData
                {
                    active          = true,
                    typeName        = resolvedName,
                    label           = string.IsNullOrEmpty(labelText) ? objectName : labelText,
                    sourceType      = sourceType,
                    groupingType    = grouping,
                    quantityMinimum = int.TryParse(quantityStr, out int qMin) ? qMin : 1,
                    tier            = tier,
                    rewardType      = rewardType,
                    rewardQuantity  = rewardQty
                });
            }

            EditorUtility.SetDirty(poiManager);
            SetStatus($"POI synced: {entries.Count} entries → POIManager", UnityEditor.MessageType.Info);
            UnityEngine.Debug.Log($"[SheetSync] POI synced: {entries.Count} entries to POIManager.");
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

            if (environmentDB != null)
                foreach (var entry in environmentDB.AllEnvironment)
                    if (entry.assetName.Replace(" ", "").ToLowerInvariant() == normalized)
                    { sourceType = POISourceType.Environment; return entry.assetName; }

            if (unitDB != null)
                foreach (var entry in unitDB.AllUnits)
                    if (entry.assetName.Replace(" ", "").ToLowerInvariant() == normalized)
                    { sourceType = POISourceType.Unit; return entry.assetName; }

            if (buildingDB != null)
                foreach (var entry in buildingDB.AllBuildings)
                    if (entry.assetName.Replace(" ", "").ToLowerInvariant() == normalized)
                    { sourceType = POISourceType.Building; return entry.assetName; }

            return null;
        }

        // ─────────────────────────────────────────────────────────────────
        // Sync: Draw Button
        // ─────────────────────────────────────────────────────────────────

        private void SyncDrawButton()
        {
            if (cachedData?.sheets == null) return;
            if (!cachedData.sheets.ContainsKey(SheetKey.DrawButton)) return;

            var controller = UnityEngine.GameObject.FindFirstObjectByType<DrawButtonController>();
            if (controller == null)
            {
                SetStatus("DrawButtonController not found in scene", UnityEditor.MessageType.Warning);
                return;
            }

            var sheet   = cachedData.sheets[SheetKey.DrawButton];
            var entries = new List<DrawButtonEntry>();

            foreach (var row in sheet.rows)
            {
                string orderStr = GetValue(row, Col.DrawButtonOrder);
                if (string.IsNullOrEmpty(orderStr) || !int.TryParse(orderStr, out int order)) continue;

                var entry = new DrawButtonEntry { order = order };

                string output = StripEmoji(GetValue(row, Col.DrawButtonOutput));
                entry.outputName = string.IsNullOrEmpty(output) ? "None" : output;

                string costCurrencyStr = StripEmoji(GetValue(row, Col.CostType));
                if (!string.IsNullOrEmpty(costCurrencyStr) &&
                    Enum.TryParse<ClockworkCraft.ResourceType>(costCurrencyStr.Replace(" ", ""), true, out var rt))
                    entry.costCurrency = rt;

                string valueStr = GetValue(row, Col.CostAmount);
                if (!string.IsNullOrEmpty(valueStr)) int.TryParse(valueStr, out entry.costValue);

                string cooldownStr = GetValue(row, Col.Cooldown);
                if (!string.IsNullOrEmpty(cooldownStr))
                    float.TryParse(cooldownStr, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out entry.cooldown);

                entries.Add(entry);
            }

            entries.Sort((a, b) => a.order.CompareTo(b.order));

            var so   = new SerializedObject(controller);
            var prop = so.FindProperty("drawLevels");
            prop.ClearArray();
            for (int i = 0; i < entries.Count; i++)
            {
                prop.InsertArrayElementAtIndex(i);
                var elem = prop.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("order").intValue           = entries[i].order;
                elem.FindPropertyRelative("outputName").stringValue   = entries[i].outputName;
                elem.FindPropertyRelative("costCurrency").intValue    = (int)entries[i].costCurrency;
                elem.FindPropertyRelative("costValue").intValue       = entries[i].costValue;
                elem.FindPropertyRelative("cooldown").floatValue      = entries[i].cooldown;
            }
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(controller);
            SetStatus($"Draw Button synced: {entries.Count} levels", UnityEditor.MessageType.Info);
        }

        // ─────────────────────────────────────────────────────────────────
        // Sync: Placement Costs
        // ─────────────────────────────────────────────────────────────────

        private void SyncPlacementCosts()
        {
            if (placementCostsDB == null || cachedData?.sheets == null) return;
            if (!cachedData.sheets.ContainsKey(SheetKey.PlacementCosts)) return;

            var sheet = cachedData.sheets[SheetKey.PlacementCosts];

            // Group rows by Item name
            var byItem = new Dictionary<string, List<Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in sheet.rows)
            {
                string item = GetValue(row, Col.Item);
                if (string.IsNullOrEmpty(item)) continue;
                if (!byItem.ContainsKey(item)) byItem[item] = new List<Dictionary<string, string>>();
                byItem[item].Add(row);
            }

            int updated = 0;
            int noMatch = 0;

            string[] currencyKeys = { Col.Currency1, Col.Currency2, Col.Currency3 };
            string[] costKeys     = { Col.Cost1,     Col.Cost2,     Col.Cost3     };

            foreach (var kvp in byItem)
            {
                string sheetItemName = kvp.Key;
                var itemRows = kvp.Value;

                var entry = FindPlacementEntry(sheetItemName);
                if (entry == null)
                {
                    UnityEngine.Debug.LogWarning($"[SheetSync] Placement Costs — no matching entry for '{sheetItemName}'. " +
                                     "Run 'Sync from Databases' in the PlacementCostsDatabase inspector first.");
                    noMatch++;
                    continue;
                }

                // Sort rows by # (count) ascending
                itemRows.Sort((a, b) =>
                {
                    int.TryParse(GetValue(a, Col.PlacementCount), out int na);
                    int.TryParse(GetValue(b, Col.PlacementCount), out int nb);
                    return na.CompareTo(nb);
                });

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

                    ClockworkCraft.ResourceType resType = ClockworkCraft.ResourceType.None;
                    if (!string.IsNullOrEmpty(currencyName))
                        Enum.TryParse<ClockworkCraft.ResourceType>(StripEmoji(currencyName).Replace(" ", "").Trim(), true, out resType);

                    var costTable = new List<int>();
                    foreach (var r in itemRows)
                    {
                        string costStr = GetValue(r, costKeys[slot]);
                        int.TryParse(costStr, out int cost);
                        costTable.Add(cost);
                    }

                    var costEntry = entry.costs[slot];
                    bool typeChanged  = costEntry.resourceType != resType;
                    bool tableChanged = !CostTablesEqual(costEntry.costTable, costTable);

                    if (typeChanged || tableChanged)
                    {
                        costEntry.resourceType = resType;
                        costEntry.costTable    = costTable;
                        costEntry.baseCost     = 0;
                        costEntry.costIncrement = 0;
                        changed = true;
                    }
                }

                if (changed)
                {
                    updated++;
                    UnityEngine.Debug.Log($"[SheetSync] Updated placement costs for '{sheetItemName}' → entry '{entry.itemName}'");
                }
            }

            EditorUtility.SetDirty(placementCostsDB);
            AssetDatabase.SaveAssets();
            string msg = $"Placement Costs synced: {updated} updated";
            if (noMatch > 0) msg += $", {noMatch} unmatched (run 'Sync from Databases' in PlacementCostsDatabase inspector)";
            SetStatus(msg, noMatch > 0 ? UnityEditor.MessageType.Warning : UnityEditor.MessageType.Info);
        }

        /// <summary>
        /// Flexible name match: tries exact, then case-insensitive, then normalised (lowercase no-spaces),
        /// then singular/plural tolerance.
        /// </summary>
        private ClockworkGrid.ItemEconomyEntry FindPlacementEntry(string sheetName)
        {
            if (placementCostsDB == null) return null;

            string norm = sheetName.ToLowerInvariant().Replace(" ", "");

            foreach (var e in placementCostsDB.entries)
                if (e.itemName == sheetName) return e;

            foreach (var e in placementCostsDB.entries)
                if (string.Equals(e.itemName, sheetName, StringComparison.OrdinalIgnoreCase)) return e;

            foreach (var e in placementCostsDB.entries)
                if (e.itemName.ToLowerInvariant().Replace(" ", "") == norm) return e;

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
    }
}
