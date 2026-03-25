#pragma warning disable CS0414, CS0219, CS0618
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClockworkCraft
{
    /// <summary>
    /// Tracks all resource currencies defined in the CurrencyDatabase.
    /// Starting amounts, unlock states, names, and emojis all come from the database.
    ///
    /// If no CurrencyDatabase is assigned, falls back to hardcoded defaults
    /// so the game still runs without the database asset.
    ///
    /// Buildings can call UnlockResource() at runtime to enable new types.
    /// Fires OnResourceChanged when any total changes so UI can react.
    /// Fires OnResourceUnlocked when a new type becomes available.
    /// </summary>
    public class ResourceManager : MonoBehaviour
    {
        public static ResourceManager Instance { get; private set; }

        [Header("Currency Source")]
        [Tooltip("Assign the CurrencyDatabase asset. Defines all currencies, starting amounts, and unlock states.")]
        public CurrencyDatabase currencyDatabase;

        /// <summary>Fired whenever a resource total changes. Args: type, new total.</summary>
        public event Action<ResourceType, int> OnResourceChanged;

        /// <summary>Fired when a resource type is unlocked. Arg: the newly unlocked type.</summary>
        public event Action<ResourceType> OnResourceUnlocked;

        // Runtime state — driven by CurrencyDatabase
        private Dictionary<ResourceType, int> resources = new Dictionary<ResourceType, int>();
        private Dictionary<ResourceType, bool> unlocked = new Dictionary<ResourceType, bool>();
        private Dictionary<ResourceType, int> startingAmounts = new Dictionary<ResourceType, int>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            InitializeFromDatabase();
        }

        // ─────────────────────────────────────────────────────────────────
        // Initialization
        // ─────────────────────────────────────────────────────────────────

        private void InitializeFromDatabase()
        {
            resources.Clear();
            unlocked.Clear();
            startingAmounts.Clear();

            if (currencyDatabase != null && currencyDatabase.AllCurrencies.Count > 0)
            {
                // Initialize from CurrencyDatabase — single source of truth
                // All resources start at 0; starting amounts are granted later via GrantStartingResources()
                foreach (var entry in currencyDatabase.AllCurrencies)
                {
                    if (entry.resourceType == ResourceType.None) continue;

                    resources[entry.resourceType] = 0;
                    startingAmounts[entry.resourceType] = entry.startingAmount;
                    unlocked[entry.resourceType] = entry.unlockedAtStart;
                }

                Debug.Log($"[ResourceManager] Initialized {resources.Count} currencies from CurrencyDatabase (all at 0, waiting for game start)");
            }
            else
            {
                // Fallback defaults when no database assigned
                Debug.LogWarning("[ResourceManager] No CurrencyDatabase assigned — using hardcoded defaults");
                resources[ResourceType.Gold]    = 0;
                resources[ResourceType.Wood]    = 0;
                resources[ResourceType.Food]    = 0;
                resources[ResourceType.Stone]   = 0;
                resources[ResourceType.Water]   = 0;
                resources[ResourceType.Clay]    = 0;
                resources[ResourceType.Flowers] = 0;

                startingAmounts[ResourceType.Gold] = 0;
                startingAmounts[ResourceType.Food] = 0;

                unlocked[ResourceType.Gold] = true;
                unlocked[ResourceType.Wood] = true;
                unlocked[ResourceType.Food] = false;
                unlocked[ResourceType.Stone] = false;
                unlocked[ResourceType.Water] = false;
                unlocked[ResourceType.Clay] = false;
                unlocked[ResourceType.Flowers] = false;
            }

            // Broadcast initial values (all 0) so UI initialises correctly
            foreach (var kvp in resources)
            {
                OnResourceChanged?.Invoke(kvp.Key, kvp.Value);
            }

            // Log unlock state
            var unlockedNames = new List<string>();
            foreach (var kvp in unlocked)
            {
                if (kvp.Value) unlockedNames.Add(kvp.Key.ToString());
            }
            Debug.Log($"[ResourceManager] Unlocked at start: {string.Join(", ", unlockedNames)}");
        }

        /// <summary>
        /// Grants the starting amounts defined in the CurrencyDatabase.
        /// Call this when the game actually starts (after GameStartGate fires).
        /// </summary>
        public void GrantStartingResources()
        {
            foreach (var kvp in startingAmounts)
            {
                if (kvp.Value > 0)
                {
                    AddResource(kvp.Key, kvp.Value);
                }
            }
            Debug.Log("[ResourceManager] Starting resources granted");
        }

        // ─────────────────────────────────────────────────────────────────
        // Public Accessors (backwards-compatible)
        // ─────────────────────────────────────────────────────────────────

        public int Gold    => GetResource(ResourceType.Gold);
        public int Wood    => GetResource(ResourceType.Wood);
        public int Food    => GetResource(ResourceType.Food);
        public int Stone   => GetResource(ResourceType.Stone);
        public int Water   => GetResource(ResourceType.Water);
        public int Clay    => GetResource(ResourceType.Clay);
        public int Flowers => GetResource(ResourceType.Flowers);

        // ─────────────────────────────────────────────────────────────────
        // Resource Unlock System
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Check whether workers are allowed to interact with this resource type.
        /// </summary>
        public bool IsResourceUnlocked(ResourceType type)
        {
            return unlocked.TryGetValue(type, out bool val) && val;
        }

        /// <summary>
        /// Unlock a resource type so workers can interact with it.
        /// Typically called by a building when placed (e.g. Lumber Mill unlocks Wood).
        /// Safe to call multiple times — no-ops if already unlocked.
        /// </summary>
        public void UnlockResource(ResourceType type)
        {
            bool wasLocked = !IsResourceUnlocked(type);
            unlocked[type] = true;

            // Ensure the resource has an entry
            if (!resources.ContainsKey(type))
                resources[type] = 0;

            if (wasLocked)
            {
                Debug.Log($"[ResourceManager] Unlocked {type}! Workers can now interact with {type} nodes.");
                OnResourceUnlocked?.Invoke(type);
            }
        }

        /// <summary>
        /// Lock a resource type (e.g. if a building is destroyed).
        /// </summary>
        public void LockResource(ResourceType type)
        {
            unlocked[type] = false;
            Debug.Log($"[ResourceManager] Locked {type} — workers can no longer interact with {type} nodes.");
        }

        // ─────────────────────────────────────────────────────────────────
        // Resource Tracking
        // ─────────────────────────────────────────────────────────────────

        public void AddResource(ResourceType type, int amount)
        {
            if (amount == 0 || type == ResourceType.None) return;

            if (!resources.ContainsKey(type))
                resources[type] = 0;

            resources[type] += amount;
            OnResourceChanged?.Invoke(type, resources[type]);

            // SFX removed — coin collect sound is triggered by ResourceLootFX on particle arrival.
            // Having it here too caused double-calls that broke the burst timing.

            Debug.Log($"[ResourceManager] +{amount} {type} → total {resources[type]}");
        }

        public int GetResource(ResourceType type)
        {
            return resources.TryGetValue(type, out int val) ? val : 0;
        }

        /// <summary>
        /// Check if the player can afford a set of costs. Pass a dictionary of type → amount.
        /// </summary>
        public bool CanAfford(Dictionary<ResourceType, int> costs)
        {
            foreach (var kvp in costs)
            {
                if (GetResource(kvp.Key) < kvp.Value) return false;
            }
            return true;
        }

        /// <summary>
        /// Legacy overload for simple gold/wood/food/stone costs.
        /// </summary>
        public bool CanAfford(int goldCost, int woodCost, int foodCost, int stoneCost)
            => GetResource(ResourceType.Gold)  >= goldCost
            && GetResource(ResourceType.Wood)  >= woodCost
            && GetResource(ResourceType.Food)  >= foodCost
            && GetResource(ResourceType.Stone) >= stoneCost;

        /// <summary>
        /// Deducts costs atomically. Returns false and makes no change if not affordable.
        /// </summary>
        public bool SpendResources(Dictionary<ResourceType, int> costs)
        {
            if (!CanAfford(costs)) return false;

            foreach (var kvp in costs)
            {
                resources[kvp.Key] -= kvp.Value;
                OnResourceChanged?.Invoke(kvp.Key, resources[kvp.Key]);
            }
            return true;
        }

        /// <summary>
        /// Legacy overload for simple gold/wood/food/stone costs.
        /// </summary>
        public bool SpendResources(int goldCost, int woodCost, int foodCost, int stoneCost)
        {
            if (!CanAfford(goldCost, woodCost, foodCost, stoneCost)) return false;

            AddResource(ResourceType.Gold,  -goldCost);
            AddResource(ResourceType.Wood,  -woodCost);
            AddResource(ResourceType.Food,  -foodCost);
            AddResource(ResourceType.Stone, -stoneCost);
            return true;
        }
    }
}
