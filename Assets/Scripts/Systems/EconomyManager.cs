#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System.Collections.Generic;
using ClockworkCraft;
using LittleCafe;

namespace ClockworkGrid
{
    /// <summary>
    /// Runtime economy manager that tracks placement counts and calculates
    /// escalating costs from EconomyBalanceConfig.
    ///
    /// Usage:
    ///   var costs = EconomyManager.Instance.GetPlacementCosts("TentSmall");
    ///   bool canAfford = EconomyManager.Instance.CanAfford("TentSmall");
    ///   EconomyManager.Instance.RecordPlacement("TentSmall");
    ///
    /// Singleton — auto-created by MapGeneratorV2.EnsureManagers() or found in scene.
    /// </summary>
    public class EconomyManager : MonoBehaviour
    {
        public static EconomyManager Instance { get; private set; }

        [Header("Config")]
        [Tooltip("The economy balance config asset. Assigned in inspector or by MapGeneratorV2.")]
        public EconomyBalanceConfig balanceConfig;

        // Tracks how many times each item has been placed this session
        private Dictionary<string, int> placementCounts = new Dictionary<string, int>();

        // ── Singleton ──────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // Auto-discover config if not assigned via Inspector or MapGeneratorV2
            if (balanceConfig == null)
            {
#if UNITY_EDITOR
                var guids = UnityEditor.AssetDatabase.FindAssets("t:EconomyBalanceConfig");
                if (guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    balanceConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<EconomyBalanceConfig>(path);
                    if (balanceConfig != null)
                        Debug.Log($"[EconomyManager] Auto-discovered config at {path} ({balanceConfig.Count} entries)");
                }
#endif
                if (balanceConfig == null)
                    Debug.LogWarning("[EconomyManager] No EconomyBalanceConfig found — all items will appear free");
            }
            else
            {
                Debug.Log($"[EconomyManager] Config assigned: {balanceConfig.Count} entries");
            }
        }

        // ── Public API ─────────────────────────────────────────────────

        /// <summary>
        /// Get the current placement costs for an item, factoring in escalation.
        /// Returns empty list if item is free or not found.
        /// </summary>
        public List<PlacementCost> GetPlacementCosts(string itemName)
        {
            if (balanceConfig == null) return new List<PlacementCost>();

            var entry = balanceConfig.GetEntry(itemName);
            if (entry == null) return new List<PlacementCost>();

            int count = GetActiveCountForEntry(entry);
            return entry.GetEffectivePlacementCosts(count);
        }

        /// <summary>
        /// Get the current placement costs for an item from a specific database.
        /// </summary>
        public List<PlacementCost> GetPlacementCosts(string itemName, ItemSourceDatabase source)
        {
            if (balanceConfig == null) return new List<PlacementCost>();

            var entry = balanceConfig.GetEntry(itemName, source);
            if (entry == null) return new List<PlacementCost>();

            int count = GetActiveCountForEntry(entry);
            return entry.GetEffectivePlacementCosts(count);
        }

        /// <summary>
        /// Returns the relevant active count for a config entry.
        /// Workers use the live active worker count from GridEntityManager so costs
        /// drop correctly when a worker dies. Everything else uses placement tracking.
        /// </summary>
        private int GetActiveCountForEntry(ItemEconomyEntry entry)
        {
            if (entry.sourceDatabase == ItemSourceDatabase.Worker)
            {
                if (LittleCafe.GridEntityManager.Instance != null)
                    return LittleCafe.GridEntityManager.Instance.GetActiveWorkerCount();
                return 0;
            }
            return GetPlacementCount(entry.itemName);
        }

        /// <summary>
        /// Check whether the player can afford to place this item right now.
        /// </summary>
        public bool CanAfford(string itemName)
        {
            var costs = GetPlacementCosts(itemName);
            if (costs.Count == 0) return true; // Free item

            if (ResourceManager.Instance == null) return false;

            foreach (var cost in costs)
            {
                if (ResourceManager.Instance.GetResource(cost.resourceType) < cost.amount)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Spend the resources for placing this item.
        /// Returns true if successful, false if can't afford.
        /// </summary>
        public bool SpendForPlacement(string itemName)
        {
            var costs = GetPlacementCosts(itemName);
            if (costs.Count == 0) return true; // Free

            if (!CanAfford(itemName)) return false;

            if (ResourceManager.Instance == null) return false;

            // Build cost dictionary for SpendResources
            var costDict = new Dictionary<ResourceType, int>();
            foreach (var cost in costs)
            {
                if (costDict.ContainsKey(cost.resourceType))
                    costDict[cost.resourceType] += cost.amount;
                else
                    costDict[cost.resourceType] = cost.amount;
            }

            ResourceManager.Instance.SpendResources(costDict);
            return true;
        }

        /// <summary>
        /// Record that an item was placed. Increments the placement count
        /// so future placements cost more (if costIncrement > 0).
        /// </summary>
        public void RecordPlacement(string itemName)
        {
            if (!placementCounts.ContainsKey(itemName))
                placementCounts[itemName] = 0;
            placementCounts[itemName]++;

            Debug.Log($"[EconomyManager] Recorded placement #{placementCounts[itemName]} for '{itemName}'");
        }

        /// <summary>How many times this item has been placed this session.</summary>
        public int GetPlacementCount(string itemName)
        {
            return placementCounts.TryGetValue(itemName, out int count) ? count : 0;
        }

        /// <summary>Reset all placement counts (e.g. on new game).</summary>
        public void ResetCounts()
        {
            placementCounts.Clear();
            Debug.Log("[EconomyManager] All placement counts reset");
        }

        /// <summary>
        /// Check if this item has any configured cost at all in the balance config.
        /// </summary>
        public bool HasConfiguredCost(string itemName)
        {
            if (balanceConfig == null) return false;
            var entry = balanceConfig.GetEntry(itemName);
            return entry != null && entry.HasAnyCost();
        }
    }
}
