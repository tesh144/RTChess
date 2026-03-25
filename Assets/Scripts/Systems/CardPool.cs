#pragma warning disable CS0414, CS0219, CS0618
using System.Collections.Generic;
using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Runtime pool of all drawable UnitStats cards.
    /// Populated by MapGeneratorV2.SetupDeck() from sheet-synced databases.
    /// Provides weighted random draws (by tier, draw weight) and name lookups.
    /// </summary>
    public class CardPool : MonoBehaviour
    {
        // Singleton
        public static CardPool Instance { get; private set; }

        [Header("Available Units")]
        [SerializeField] private List<UnitStats> allUnitStats = new List<UnitStats>();

        [Header("Rarity Weights (for testing/tweaking)")]
        [SerializeField] private float commonWeight = 60f;
        [SerializeField] private float rareWeight = 35f;
        [SerializeField] private float epicWeight = 5f;

        // Cached stats by type
        private Dictionary<UnitType, UnitStats> statsByType = new Dictionary<UnitType, UnitStats>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Register unit stats (called by GameSetup)
        /// </summary>
        public void RegisterUnitStats(List<UnitStats> stats)
        {
            allUnitStats = stats;
            statsByType.Clear();

            foreach (UnitStats stat in allUnitStats)
            {
                if (!statsByType.ContainsKey(stat.unitType))
                {
                    statsByType[stat.unitType] = stat;
                }
            }

            Debug.Log($"CardPool registered {allUnitStats.Count} unit types");
        }

        /// <summary>
        /// Draw a random unit based on rarity weights.
        /// Only considers cards with isRandomBuilding == true (excludes Feast, etc.).
        /// </summary>
        public UnitStats DrawRandomUnit()
        {
            if (allUnitStats.Count == 0)
            {
                Debug.LogError("No unit stats registered! Cannot draw unit.");
                return null;
            }

            // Calculate total weight, skipping cards excluded from the random pool
            float totalWeight = 0f;
            foreach (UnitStats stats in allUnitStats)
            {
                if (!stats.active || !stats.isRandomBuilding) continue;
                totalWeight += stats.GetEffectiveDrawWeight();
            }

            if (totalWeight <= 0f)
            {
                Debug.LogWarning("No drawable units in pool (all excluded or zero weight).");
                return null;
            }

            // Roll random value
            float roll = Random.Range(0f, totalWeight);

            // Select unit based on roll (only from eligible pool)
            float currentWeight = 0f;
            foreach (UnitStats stats in allUnitStats)
            {
                if (!stats.active || !stats.isRandomBuilding) continue;
                currentWeight += stats.GetEffectiveDrawWeight();
                if (roll <= currentWeight)
                {
                    Debug.Log($"Drew {stats.unitName} ({stats.rarity}, weight:{stats.GetEffectiveDrawWeight()})");
                    return stats;
                }
            }

            // Fallback — return first eligible
            foreach (UnitStats stats in allUnitStats)
            {
                if (stats.isRandomBuilding) return stats;
            }
            return allUnitStats[0];
        }

        /// <summary>
        /// Draw a random card filtered by tier AND card source type.
        /// Only considers cards with isRandomBuilding == true, tier == targetTier, and matching source.
        /// Falls back to unfiltered DrawRandomUnit() if no cards match.
        /// </summary>
        private UnitStats DrawByTierAndSource(int targetTier, System.Func<CardSourceType, bool> sourceFilter, string label)
        {
            if (allUnitStats.Count == 0)
            {
                Debug.LogError("No unit stats registered! Cannot draw.");
                return null;
            }

            float totalWeight = 0f;
            foreach (UnitStats stats in allUnitStats)
            {
                if (!stats.active || !stats.isRandomBuilding) continue;
                if (stats.tier != targetTier) continue;
                if (!sourceFilter(stats.cardSource)) continue;
                totalWeight += stats.GetEffectiveDrawWeight();
            }

            if (totalWeight <= 0f)
            {
                Debug.LogWarning($"No drawable {label} in tier {targetTier} pool — falling back to unfiltered draw.");
                return DrawRandomUnit();
            }

            float roll = Random.Range(0f, totalWeight);
            float currentWeight = 0f;
            foreach (UnitStats stats in allUnitStats)
            {
                if (!stats.active || !stats.isRandomBuilding) continue;
                if (stats.tier != targetTier) continue;
                if (!sourceFilter(stats.cardSource)) continue;
                currentWeight += stats.GetEffectiveDrawWeight();
                if (roll <= currentWeight)
                {
                    Debug.Log($"Drew {stats.unitName} ({label} tier:{targetTier}, weight:{stats.GetEffectiveDrawWeight()})");
                    return stats;
                }
            }

            // Fallback — first eligible match
            foreach (UnitStats stats in allUnitStats)
            {
                if (stats.isRandomBuilding && stats.tier == targetTier && sourceFilter(stats.cardSource))
                    return stats;
            }
            return DrawRandomUnit();
        }

        /// <summary>
        /// Draw a random building card filtered by tier (0-3).
        /// Only considers cards sourced from BuildingDatabase.
        /// </summary>
        public UnitStats DrawRandomBuildingByTier(int targetTier)
        {
            return DrawByTierAndSource(targetTier, s => s == CardSourceType.Building, "buildings");
        }

        /// <summary>
        /// Draw a random unit/worker card filtered by tier (0-3).
        /// Considers cards sourced from UnitDatabase or WorkerDatabase.
        /// </summary>
        public UnitStats DrawRandomUnitByTier(int targetTier)
        {
            return DrawByTierAndSource(targetTier,
                s => s == CardSourceType.Unit || s == CardSourceType.Worker, "units");
        }

        /// <summary>
        /// Draw a random unit from tier 0 up to maxTier (inclusive).
        /// RandomTier0 = tier 0 only. RandomTier1 = tier 0 + tier 1. etc.
        /// Falls back to unfiltered DrawRandomUnit() if no cards match.
        /// </summary>
        public UnitStats DrawRandomUnitUpToTier(int maxTier)
        {
            if (allUnitStats.Count == 0)
            {
                Debug.LogError("No unit stats registered! Cannot draw unit.");
                return null;
            }

            float totalWeight = 0f;
            foreach (UnitStats stats in allUnitStats)
            {
                if (!stats.active || !stats.isRandomBuilding) continue;
                if (stats.tier < 0 || stats.tier > maxTier) continue;
                totalWeight += stats.GetEffectiveDrawWeight();
            }

            if (totalWeight <= 0f)
            {
                Debug.LogWarning($"No drawable units in tier 0-{maxTier} pool — falling back to unfiltered draw.");
                return DrawRandomUnit();
            }

            float roll = Random.Range(0f, totalWeight);
            float currentWeight = 0f;
            foreach (UnitStats stats in allUnitStats)
            {
                if (!stats.active || !stats.isRandomBuilding) continue;
                if (stats.tier < 0 || stats.tier > maxTier) continue;
                currentWeight += stats.GetEffectiveDrawWeight();
                if (roll <= currentWeight)
                {
                    Debug.Log($"Drew {stats.unitName} (tier:{stats.tier}, maxTier:{maxTier}, weight:{stats.GetEffectiveDrawWeight()})");
                    return stats;
                }
            }

            foreach (UnitStats stats in allUnitStats)
            {
                if (stats.isRandomBuilding && stats.tier >= 0 && stats.tier <= maxTier) return stats;
            }
            return DrawRandomUnit();
        }

        /// <summary>
        /// Get unit stats by type
        /// </summary>
        public UnitStats GetUnitStats(UnitType type)
        {
            if (statsByType.ContainsKey(type))
            {
                return statsByType[type];
            }

            Debug.LogWarning($"No stats found for unit type: {type}");
            return null;
        }

        /// <summary>
        /// Find a registered UnitStats by unitName (case-insensitive).
        /// Returns null if not found.
        /// </summary>
        public UnitStats FindByName(string unitName)
        {
            if (string.IsNullOrEmpty(unitName)) return null;
            foreach (UnitStats stats in allUnitStats)
            {
                if (string.Equals(stats.unitName, unitName, System.StringComparison.OrdinalIgnoreCase))
                    return stats;
            }
            return null;
        }

        /// <summary>
        /// Get all registered unit stats
        /// </summary>
        public List<UnitStats> GetAllUnitStats()
        {
            return allUnitStats;
        }

        /// <summary>
        /// Get weight for a rarity tier (uses inspector values or defaults)
        /// </summary>
        private float GetWeightForRarity(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Common: return commonWeight;
                case Rarity.Rare: return rareWeight;
                case Rarity.Epic: return epicWeight;
                default: return 1f;
            }
        }

        /// <summary>
        /// Draw a random enemy unit type based on wave number
        /// </summary>
        public UnitStats DrawRandomEnemyUnit(int waveNumber)
        {
            // Early waves: Soldiers only
            if (waveNumber <= 5)
            {
                return GetUnitStats(UnitType.Soldier);
            }

            // Mid waves: Soldiers + Ninjas
            if (waveNumber <= 10)
            {
                float roll = Random.Range(0f, 100f);
                if (roll < 70f) // 70% Soldier
                    return GetUnitStats(UnitType.Soldier);
                else // 30% Ninja
                    return GetUnitStats(UnitType.Ninja);
            }

            // Late waves: All three types
            if (waveNumber <= 15)
            {
                float roll = Random.Range(0f, 100f);
                if (roll < 50f) // 50% Soldier
                    return GetUnitStats(UnitType.Soldier);
                else if (roll < 85f) // 35% Ninja
                    return GetUnitStats(UnitType.Ninja);
                else // 15% Ogre
                    return GetUnitStats(UnitType.Ogre);
            }

            // Very late waves: More diverse
            {
                float roll = Random.Range(0f, 100f);
                if (roll < 40f) // 40% Soldier
                    return GetUnitStats(UnitType.Soldier);
                else if (roll < 80f) // 40% Ninja
                    return GetUnitStats(UnitType.Ninja);
                else // 20% Ogre
                    return GetUnitStats(UnitType.Ogre);
            }
        }
    }
}
