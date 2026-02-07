using System.Collections.Generic;
using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Manages rarity-based weighted random unit selection.
    /// Singleton pattern for global access.
    /// </summary>
    public class RaritySystem : MonoBehaviour
    {
        // Singleton
        public static RaritySystem Instance { get; private set; }

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

            Debug.Log($"RaritySystem registered {allUnitStats.Count} unit types");
        }

        /// <summary>
        /// Draw a random unit based on rarity weights
        /// </summary>
        public UnitStats DrawRandomUnit()
        {
            if (allUnitStats.Count == 0)
            {
                Debug.LogError("No unit stats registered! Cannot draw unit.");
                return null;
            }

            // Calculate total weight
            float totalWeight = 0f;
            foreach (UnitStats stats in allUnitStats)
            {
                totalWeight += GetWeightForRarity(stats.rarity);
            }

            // Roll random value
            float roll = Random.Range(0f, totalWeight);

            // Select unit based on roll
            float currentWeight = 0f;
            foreach (UnitStats stats in allUnitStats)
            {
                currentWeight += GetWeightForRarity(stats.rarity);
                if (roll <= currentWeight)
                {
                    Debug.Log($"Drew {stats.unitName} ({stats.rarity})");
                    return stats;
                }
            }

            // Fallback (should never happen)
            return allUnitStats[0];
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
