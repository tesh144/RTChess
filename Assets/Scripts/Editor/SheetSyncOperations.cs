#pragma warning disable CS0414, CS0219, CS0618
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using ClockworkCraft;

namespace LittleCafe.Editor
{
    /// <summary>
    /// Partial: SyncBuildings, SyncWorkers, SyncUnits for SheetSyncEditor.
    /// Each method reads rows from the cached sheet and applies changes to the
    /// corresponding ScriptableObject database. Only fields that actually changed
    /// are written (to avoid dirtying assets unnecessarily).
    /// See SheetSyncValidator.cs for column name constants (Col.*).
    /// </summary>
    public partial class SheetSyncEditor
    {
        // ─────────────────────────────────────────────────────────────────
        // Sync: Buildings
        // ─────────────────────────────────────────────────────────────────

        private void SyncBuildings()
        {
            if (buildingDB == null || cachedData?.sheets == null) return;
            if (!cachedData.sheets.ContainsKey(SheetKey.Buildings)) return;

            var sheet = cachedData.sheets[SheetKey.Buildings];
            var buildingList = buildingDB.AllBuildings;
            int updated = 0;
            int added = 0;

            foreach (var row in sheet.rows)
            {
                string name = GetValue(row, Col.Building);
                if (string.IsNullOrEmpty(name)) continue;

                var existing = buildingList.FirstOrDefault(b => b.assetName == name);
                if (existing == null)
                {
                    UnityEngine.Debug.Log($"[SheetSync] New building '{name}' in sheet — add it manually in Inspector first, then re-sync.");
                    continue;
                }

                bool changed = false;

                // Active flag
                string activeStr = GetValue(row, Col.Active);
                bool newActive = string.IsNullOrEmpty(activeStr) || activeStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || activeStr == "1";
                if (existing.active != newActive) { existing.active = newActive; changed = true; }

                // Production
                changed |= TrySetFloat(ref existing.productionInterval,      GetValue(row, Col.ProdInterval));
                changed |= TrySetFloat(ref existing.productionIntervalBonus, GetValue(row, Col.IntervalBonus));

                // Input / output type
                ProductionInputType newInput = ParseInputType(GetValue(row, Col.InputCard));
                if (existing.productionInputType != newInput) { existing.productionInputType = newInput; changed = true; }

                ProductionOutputType newOutput = ParseOutputType(GetValue(row, Col.OutputCard));
                if (existing.productionOutputType != newOutput) { existing.productionOutputType = newOutput; changed = true; }

                // BuildOn
                string buildOnVal = GetValue(row, Col.BuildOn);
                if (!string.IsNullOrEmpty(buildOnVal) && existing.buildOn != buildOnVal)
                { existing.buildOn = buildOnVal; changed = true; }

                // Reveal radius
                changed |= TrySetInt(ref existing.fogRevealRadius, GetValue(row, Col.RevealRadius));

                // HP / Attack
                changed |= TrySetInt(ref existing.hp,          GetValue(row, Col.HP));
                changed |= TrySetInt(ref existing.attackPower, GetValue(row, Col.Attack));

                // isMealSource: ally-interactible buildings with no production output (e.g. Feast table)
                string interactible = GetValue(row, Col.AllyInteractible);
                bool allyInteractible = !string.IsNullOrEmpty(interactible) &&
                    (interactible.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || interactible == "1");
                bool newMealSource = allyInteractible && newOutput == ProductionOutputType.None;
                if (existing.isMealSource != newMealSource) { existing.isMealSource = newMealSource; changed = true; }

                // Killer's Behavior
                string killerStr = GetValue(row, Col.KillerBehavior);
                bool newKillerAdvances = killerStr.Equals("Advance", StringComparison.OrdinalIgnoreCase);
                if (existing.killerAdvances != newKillerAdvances) { existing.killerAdvances = newKillerAdvances; changed = true; }

                // Tier
                string tierStr = GetValue(row, Col.TierButton);
                if (!string.IsNullOrEmpty(tierStr))
                {
                    int newTier = -1;
                    if (tierStr.StartsWith("Tier ", StringComparison.OrdinalIgnoreCase))
                        int.TryParse(tierStr.Substring(5).Trim(), out newTier);
                    else if (tierStr != "-")
                        int.TryParse(tierStr, out newTier);
                    if (existing.tier != newTier) { existing.tier = newTier; changed = true; }
                }

                // DrawWeight (Buildings sheet uses "DrawWeight" — no space)
                changed |= TrySetFloat(ref existing.drawWeight, GetValue(row, Col.DrawWeightBuilding));

                // isRandomBuilding
                string randomStr = GetValue(row, Col.IsRandomBuilding);
                if (!string.IsNullOrEmpty(randomStr))
                {
                    bool newIsRandom = randomStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || randomStr == "1";
                    if (existing.isRandomBuilding != newIsRandom) { existing.isRandomBuilding = newIsRandom; changed = true; }
                }

                // Interaction categories
                if (!string.IsNullOrEmpty(interactible))
                {
                    bool newAlly = interactible.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || interactible == "1";
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

                // Production resource cost
                string costResStr = StripEmoji(GetValue(row, Col.ResourceUse)).Replace(" ", "");
                if (!string.IsNullOrEmpty(costResStr) && costResStr != "-" && !costResStr.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    if (Enum.TryParse<ClockworkCraft.ResourceType>(costResStr, true, out var costRes))
                        if (existing.productionCostResourceType != costRes) { existing.productionCostResourceType = costRes; changed = true; }
                }
                else if (costResStr == "-" || costResStr.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    if (existing.productionCostResourceType != ClockworkCraft.ResourceType.None)
                    { existing.productionCostResourceType = ClockworkCraft.ResourceType.None; changed = true; }
                }

                string costAmtStr = GetValue(row, Col.ResourceAmount);
                if (costAmtStr != "-") changed |= TrySetInt(ref existing.productionCostAmount, costAmtStr);
                string costIncStr = GetValue(row, Col.ResourceIncrement);
                if (costIncStr != "-") changed |= TrySetInt(ref existing.productionCostIncrement, costIncStr);

                if (changed)
                {
                    updated++;
                    UnityEngine.Debug.Log($"[SheetSync] Updated building: {name}");
                }
            }

            EditorUtility.SetDirty(buildingDB);
            AssetDatabase.SaveAssets();
            SetStatus($"Buildings synced: {updated} updated, {added} added", UnityEditor.MessageType.Info);
        }

        // ─────────────────────────────────────────────────────────────────
        // Sync: Workers
        // ─────────────────────────────────────────────────────────────────

        private void SyncWorkers()
        {
            if (workerDB == null || cachedData?.sheets == null) return;
            if (!cachedData.sheets.ContainsKey(SheetKey.Workers)) return;

            var sheet = cachedData.sheets[SheetKey.Workers];
            var workerList = workerDB.AllWorkers;
            int updated = 0;

            foreach (var row in sheet.rows)
            {
                string entity = GetValue(row, Col.Entity);
                string type   = GetValue(row, Col.Type);
                if (string.IsNullOrEmpty(entity) || string.IsNullOrEmpty(type)) continue;

                string cleanName = entity.Split('(')[0].Trim();
                var existing = workerList.FirstOrDefault(w =>
                    w.assetName == cleanName ||
                    w.assetName == entity ||
                    w.GetCleanName() == cleanName);

                if (existing == null)
                {
                    UnityEngine.Debug.Log($"[SheetSync] Worker '{entity}' not found in database — skipping.");
                    continue;
                }

                bool changed = false;

                // Active flag
                string activeStr = GetValue(row, Col.Active);
                bool newActive = string.IsNullOrEmpty(activeStr) || activeStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || activeStr == "1";
                if (existing.active != newActive) { existing.active = newActive; changed = true; }

                // HP / Attack
                changed |= TrySetInt(ref existing.hp,          GetValue(row, Col.HP));
                changed |= TrySetInt(ref existing.attackPower, GetValue(row, Col.AttackPower));

                // Behavior (Workers sheet uses "Movement Behavior")
                string behaviorStr = GetValue(row, Col.MovementBehavior);
                if (!string.IsNullOrEmpty(behaviorStr) && Enum.TryParse<BehaviorType>(behaviorStr, true, out var newBehavior))
                    if (existing.behaviorType != newBehavior) { existing.behaviorType = newBehavior; changed = true; }

                // Draw weight (Workers sheet uses "Draw Weight" — with space)
                float dw = 0;
                if (float.TryParse(GetValue(row, Col.DrawWeightWorker),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out dw))
                    if (Math.Abs(existing.drawWeight - dw) > 0.001f) { existing.drawWeight = dw; changed = true; }

                // Killer's Behavior
                string killerStr = GetValue(row, Col.KillerBehavior);
                if (!string.IsNullOrEmpty(killerStr))
                {
                    bool newKillerAdvances = killerStr.Equals("Advance", StringComparison.OrdinalIgnoreCase);
                    if (existing.killerAdvances != newKillerAdvances) { existing.killerAdvances = newKillerAdvances; changed = true; }
                }

                // Tier
                string tierStr = GetValue(row, Col.TierButton);
                if (!string.IsNullOrEmpty(tierStr))
                {
                    int newTier = -1;
                    if (tierStr.StartsWith("Tier ", StringComparison.OrdinalIgnoreCase))
                        int.TryParse(tierStr.Substring(5).Trim(), out newTier);
                    else if (tierStr != "-")
                        int.TryParse(tierStr, out newTier);
                    if (existing.tier != newTier) { existing.tier = newTier; changed = true; }
                }

                if (changed)
                {
                    updated++;
                    UnityEngine.Debug.Log($"[SheetSync] Updated worker: {entity}");
                }
            }

            EditorUtility.SetDirty(workerDB);
            AssetDatabase.SaveAssets();
            SetStatus($"Workers synced: {updated} updated", UnityEditor.MessageType.Info);
        }

        // ─────────────────────────────────────────────────────────────────
        // Sync: Units (enemies/monsters from Workers & Entities sheet)
        // ─────────────────────────────────────────────────────────────────

        private void SyncUnits()
        {
            if (unitDB == null || cachedData?.sheets == null) return;
            if (!cachedData.sheets.ContainsKey(SheetKey.Workers)) return;

            var sheet    = cachedData.sheets[SheetKey.Workers];
            var unitList = unitDB.AllUnits;
            int updated  = 0;

            foreach (var row in sheet.rows)
            {
                string entity = GetValue(row, Col.Entity);
                string type   = GetValue(row, Col.Type);
                if (string.IsNullOrEmpty(entity) || string.IsNullOrEmpty(type)) continue;

                // Read isEnemy from the explicit "Enemy" column; fall back to Attack Behavior if absent
                string enemyCol = GetValue(row, "Enemy");
                bool isEnemy;
                if (!string.IsNullOrEmpty(enemyCol))
                    isEnemy = enemyCol.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || enemyCol == "1";
                else
                    isEnemy = GetValue(row, Col.AttackBehavior).Equals("Hostile", StringComparison.OrdinalIgnoreCase);

                // Only sync Hostile units OR Corruption-type entities to UnitDatabase
                bool isCorruption = type.Equals("Corruption", StringComparison.OrdinalIgnoreCase);
                if (!isEnemy && !isCorruption) continue;

                string cleanName = entity.Split('(')[0].Trim();
                string cleanNameNoSpaces = cleanName.Replace(" ", "");
                var existing = unitList.FirstOrDefault(u =>
                    u.assetName == cleanName ||
                    u.assetName == entity ||
                    u.assetName.Equals(cleanName, StringComparison.OrdinalIgnoreCase) ||
                    u.assetName.Replace(" ", "").Equals(cleanNameNoSpaces, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    UnityEngine.Debug.Log($"[SheetSync] Unit '{entity}' not found in UnitDatabase — skipping.");
                    continue;
                }

                bool changed = false;

                // Active flag
                string activeStr = GetValue(row, Col.Active);
                bool newActive = string.IsNullOrEmpty(activeStr) || activeStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || activeStr == "1";
                if (existing.active != newActive) { existing.active = newActive; changed = true; }

                // HP / Attack
                changed |= TrySetInt(ref existing.hp,          GetValue(row, Col.HP));
                changed |= TrySetInt(ref existing.attackPower, GetValue(row, Col.AttackPower));

                // Behavior
                string behaviorStr = GetValue(row, Col.MovementBehavior);
                if (!string.IsNullOrEmpty(behaviorStr) && Enum.TryParse<BehaviorType>(behaviorStr, true, out var newBehavior))
                    if (existing.behaviorType != newBehavior) { existing.behaviorType = newBehavior; changed = true; }

                // isEnemy
                if (existing.isEnemy != isEnemy) { existing.isEnemy = isEnemy; changed = true; }

                // MapGenerated flag
                string mapGenStr = GetValue(row, Col.MapGenerated);
                if (!string.IsNullOrEmpty(mapGenStr))
                {
                    bool newMapGen = mapGenStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || mapGenStr == "1";
                    if (existing.isMapGenerated != newMapGen) { existing.isMapGenerated = newMapGen; changed = true; }
                }

                // GameUnitType
                string typeStr = GetValue(row, Col.Type);
                if (!string.IsNullOrEmpty(typeStr) && Enum.TryParse<LittleCafe.GameUnitType>(typeStr, true, out var newUnitType))
                    if (existing.type != newUnitType) { existing.type = newUnitType; changed = true; }

                // Draw weight (Workers sheet uses "Draw Weight" — with space)
                float dw = 0;
                if (float.TryParse(GetValue(row, Col.DrawWeightWorker),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out dw))
                    if (Math.Abs(existing.drawWeight - dw) > 0.001f) { existing.drawWeight = dw; changed = true; }

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

                // Loot resource type (Drops column)
                string unitDrops = StripEmoji(GetValue(row, Col.Drops));
                if (!string.IsNullOrEmpty(unitDrops) && !unitDrops.Equals("None", StringComparison.OrdinalIgnoreCase))
                    if (Enum.TryParse<ClockworkCraft.ResourceType>(unitDrops.Replace(" ", ""), true, out var lootRt))
                        if (existing.lootResourceType != lootRt) { existing.lootResourceType = lootRt; changed = true; }

                // Loot per hit
                changed |= TrySetInt(ref existing.lootHpCost, GetValue(row, Col.LootPerHit));

                if (changed)
                {
                    updated++;
                    UnityEngine.Debug.Log($"[SheetSync] Updated unit: {entity}");
                }
            }

            EditorUtility.SetDirty(unitDB);
            AssetDatabase.SaveAssets();
            SetStatus($"Units synced: {updated} updated", UnityEditor.MessageType.Info);
        }
    }
}
