#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System.Collections.Generic;
using LittleCafe;

namespace ClockworkCraft
{
    /// <summary>
    /// Syncs spawn entry lists from databases. Keeps existing weights/settings
    /// for entries that still exist, adds defaults for new ones.
    /// Extracted from MapGeneratorV2.
    /// </summary>
    public static class SpawnEntrySyncer
    {
        public static List<EnvironmentSpawnEntry> SyncEnvironmentEntries(
            List<EnvironmentSpawnEntry> existing, EnvironmentDatabase envDB)
        {
            var lookup = new Dictionary<string, EnvironmentSpawnEntry>();
            foreach (var entry in existing)
            {
                if (!string.IsNullOrEmpty(entry.environmentName))
                    lookup[entry.environmentName] = entry;
            }

            var synced = new List<EnvironmentSpawnEntry>();
            foreach (var envData in envDB.AllEnvironment)
            {
                if (!envData.active) continue; // skip inactive entries
                if (!envData.isMapGenerated) continue; // only map-generated objects go into spawn entries
                if (lookup.TryGetValue(envData.assetName, out var kept))
                    synced.Add(kept);
                else
                    synced.Add(CreateDefaultEntry(envData.assetName));
            }
            return synced;
        }

        public static List<UnitSpawnEntry> SyncUnitEntries(
            List<UnitSpawnEntry> existing, UnitDatabase unitDB)
        {
            var lookup = new Dictionary<string, UnitSpawnEntry>();
            foreach (var entry in existing)
            {
                if (!string.IsNullOrEmpty(entry.unitName))
                    lookup[entry.unitName] = entry;
            }

            var synced = new List<UnitSpawnEntry>();
            foreach (var unitData in unitDB.AllUnits)
            {
                if (!unitData.active) continue; // skip inactive entries
                if (!unitData.isMapGenerated) continue; // only map-generated units go into spawn entries
                if (unitData.type == GameUnitType.Corruption) continue; // corruption entities are handled separately
                if (lookup.TryGetValue(unitData.assetName, out var kept))
                    synced.Add(kept);
                else
                    synced.Add(CreateDefaultUnitEntry(unitData.assetName, unitData.type));
            }
            return synced;
        }

        /// <summary>
        /// Syncs corruption spawn entries from UnitDatabase entries with type == Corruption
        /// AND isMapGenerated == true. Only map-generated corruption entities (hearts) are
        /// included. Spikes have isMapGenerated = false and must NOT be map-placed.
        /// </summary>
        public static List<CorruptionSpawnEntry> SyncCorruptionEntries(
            List<CorruptionSpawnEntry> entries, UnitDatabase unitDB)
        {
            // Build valid-name set: active, Corruption type, AND map-generated (hearts only)
            var validHeartNames = new HashSet<string>();
            foreach (var data in unitDB.GetByType(GameUnitType.Corruption))
            {
                if (data.active && data.isMapGenerated)
                    validHeartNames.Add(data.assetName);
            }

            // Remove stale entries (empty name, removed from database, inactive, or not map-generated)
            entries.RemoveAll(e =>
                string.IsNullOrEmpty(e.entityName) || !validHeartNames.Contains(e.entityName));

            // Remove duplicates — keep only the first entry per entityName
            var seenNames = new HashSet<string>();
            entries.RemoveAll(e => !seenNames.Add(e.entityName));

            // Add missing entries + refresh prefabs on existing ones
            foreach (var data in unitDB.GetByType(GameUnitType.Corruption))
            {
                if (!data.active || !data.isMapGenerated) continue;
                var existing = entries.Find(e => e.entityName == data.assetName);
                if (existing != null)
                {
                    existing.prefab = data.prefab;
                }
                else
                {
                    entries.Add(new CorruptionSpawnEntry
                    {
                        entityName = data.assetName,
                        prefab     = data.prefab
                    });
                }
            }

#if UNITY_EDITOR
            // Google Sheets sync never assigns prefab references — attempt to auto-resolve
            // any remaining null prefabs from AssetDatabase so the Inspector Sync button
            // works without requiring manual drag-and-drop for every heart entry.
            bool anyPrefabPatched = false;
            foreach (var entry in entries)
            {
                if (entry.prefab != null) continue;

                string[] guids = UnityEditor.AssetDatabase.FindAssets($"{entry.entityName} t:Prefab");
                foreach (string guid in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    // Exact name match to avoid false positives (e.g. "Spike1" != "CorruptionHeart")
                    if (!System.IO.Path.GetFileNameWithoutExtension(path)
                            .Equals(entry.entityName, System.StringComparison.OrdinalIgnoreCase)) continue;

                    var prefabAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefabAsset == null) continue;

                    entry.prefab = prefabAsset;

                    // Patch UnitDatabase so the reference survives future syncs
                    var dbEntry = unitDB.GetByName(entry.entityName);
                    if (dbEntry != null && dbEntry.prefab == null)
                    {
                        dbEntry.prefab = prefabAsset;
                        UnityEditor.EditorUtility.SetDirty(unitDB);
                        anyPrefabPatched = true;
                        Debug.Log($"[SpawnEntrySyncer] Auto-resolved prefab for '{entry.entityName}' → {path}");
                    }
                    break;
                }

                if (entry.prefab == null)
                    Debug.LogWarning($"[SpawnEntrySyncer] No prefab found for corruption entry '{entry.entityName}'. " +
                                     "Assign it in UnitDatabase or run Tools > Corruption > Create Corruption Prefabs first.");
            }
            if (anyPrefabPatched)
                UnityEditor.AssetDatabase.SaveAssets();
#endif

            return entries;
        }

        // ─────────────────────────────────────────────────────────────────
        // Default entry factories
        // ─────────────────────────────────────────────────────────────────

        static EnvironmentSpawnEntry CreateDefaultEntry(string name)
        {
            string lower = name.ToLowerInvariant();
            var entry = new EnvironmentSpawnEntry { environmentName = name };

            if (lower.Contains("tree") || lower.Contains("pine") || lower.Contains("forest"))
            {
                // Trees: dominant clustered presence with natural break-offs
                entry.spawnMode     = SpawnMode.Clustered;
                entry.spawnWeight   = 12f;
                entry.fragmentation = 0.35f;   // mostly cohesive blobs with some loose trees
                entry.clusterSpread = 0.55f;   // slightly organic shape
            }
            else if (lower.Contains("water") || lower.Contains("lake") || lower.Contains("river"))
            {
                // Water: clustered pools near trees, cohesive not scattered
                entry.spawnMode     = SpawnMode.Clustered;
                entry.spawnWeight   = 6f;
                entry.fragmentation = 0.15f;   // mostly cohesive pools, very few loose tiles
                entry.clusterSpread = 0.7f;    // rounder blobs (ponds/lakes)
            }
            else if (lower.Contains("rock") || lower.Contains("stone") || lower.Contains("boulder"))
            {
                // Rocks: clustered outcrops with high fragmentation for natural rocky patches
                entry.spawnMode     = SpawnMode.Clustered;
                entry.spawnWeight   = 4f;
                entry.fragmentation = 0.55f;   // many small outcrops + loose stones
                entry.clusterSpread = 0.45f;   // organic/irregular shape
            }
            else if (lower.Contains("gold") || lower.Contains("mine") || lower.Contains("ore"))
            {
                // Goldmines: rare, well-spaced
                entry.spawnMode   = SpawnMode.Scattered;
                entry.spawnWeight = 2f;
                entry.minSpacing  = 8;
            }
            else if (lower.Contains("coral"))
            {
                // Coral: rare scattered landmarks
                entry.spawnMode   = SpawnMode.Scattered;
                entry.spawnWeight = 3f;
                entry.minSpacing  = 6;
            }
            else if (lower.Contains("flower") || lower.Contains("flora") || lower.Contains("bush"))
            {
                // Flowers: border of tree clusters for a natural meadow feel
                entry.spawnMode    = SpawnMode.Edge;
                entry.spawnWeight  = 3f;
                entry.edgeBorderOf = "Tree";
            }
            else if (lower.Contains("lily") || lower.Contains("pad") || lower.Contains("lotus"))
            {
                // Water plants: spawn on top of water tiles
                entry.spawnMode       = SpawnMode.OnTop;
                entry.spawnWeight     = 0f; // OnTop doesn't use weight-based budgets
                entry.requiredSurface = ClockworkGrid.SurfaceType.Water;
                entry.coveragePercent = 0.25f;
                entry.minSpacing      = 2;
            }
            else if (lower.Contains("light") || lower.Contains("lamp") || lower.Contains("decor"))
            {
                // Decorative: light scattered presence
                entry.spawnMode   = SpawnMode.Scattered;
                entry.spawnWeight = 4f;
                entry.minSpacing  = 4;
            }
            else
            {
                // Generic default: scattered, moderate weight
                entry.spawnMode   = SpawnMode.Scattered;
                entry.spawnWeight = 5f;
                entry.minSpacing  = 0;
            }

            return entry;
        }

        static UnitSpawnEntry CreateDefaultUnitEntry(string name, GameUnitType type)
        {
            string lower = name.ToLowerInvariant();
            var entry = new UnitSpawnEntry { unitName = name };

            // Beasts and bosses: scattered, rare, well-spaced
            if (type == GameUnitType.Beast || lower.Contains("beast") || lower.Contains("wolf") ||
                lower.Contains("bear") || lower.Contains("boar") || lower.Contains("spider"))
            {
                entry.spawnMode   = SpawnMode.Scattered;
                entry.spawnWeight = 3f;
                entry.minSpacing  = 5;
            }
            else if (type == GameUnitType.Boss || lower.Contains("boss") || lower.Contains("dragon"))
            {
                entry.spawnMode   = SpawnMode.Scattered;
                entry.spawnWeight = 1f;
                entry.minSpacing  = 12;
            }
            else if (type == GameUnitType.Soldier || type == GameUnitType.Archer ||
                     lower.Contains("guard") || lower.Contains("skeleton") || lower.Contains("goblin"))
            {
                // Enemy troops: small clustered packs
                entry.spawnMode     = SpawnMode.Clustered;
                entry.spawnWeight   = 4f;
                entry.fragmentation = 0.6f;
                entry.clusterSpread = 0.3f;
            }
            else
            {
                // Generic default: scattered, moderate
                entry.spawnMode   = SpawnMode.Scattered;
                entry.spawnWeight = 2f;
                entry.minSpacing  = 4;
            }

            return entry;
        }
    }
}
