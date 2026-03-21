using UnityEngine;
using System.Collections.Generic;
using ClockworkCraft;
using LittleCafe;

namespace ClockworkGrid
{
    /// <summary>
    /// A single resource cost entry in the economy balance config.
    /// Each placeable item can have up to 3 of these.
    /// </summary>
    [System.Serializable]
    public class ResourceCostEntry
    {
        [Tooltip("Which resource this cost requires.")]
        public ResourceType resourceType = ResourceType.None;

        [Tooltip("Base cost for the first placement.")]
        public int baseCost = 0;

        [Tooltip("How much the cost increases per successful placement. 0 = flat cost.")]
        public int costIncrement = 0;
    }

    /// <summary>
    /// Economy configuration for a single placeable item.
    /// Pulled from and identified by its database entry name.
    /// </summary>
    [System.Serializable]
    public class ItemEconomyEntry
    {
        [Tooltip("The asset name from the source database (read-only identifier).")]
        public string itemName;

        [Tooltip("Which database this item comes from.")]
        public ItemSourceDatabase sourceDatabase;

        [Tooltip("Icon sprite for quick visual reference in the inspector.")]
        public Sprite icon;

        [Tooltip("Up to 3 resource costs for placing this item.")]
        public List<ResourceCostEntry> costs = new List<ResourceCostEntry>();

        /// <summary>True if this entry has at least one non-zero cost.</summary>
        public bool HasAnyCost()
        {
            foreach (var c in costs)
                if (c.resourceType != ResourceType.None && c.baseCost > 0)
                    return true;
            return false;
        }

        /// <summary>
        /// Get the effective cost for a resource slot after N placements.
        /// effectiveCost = baseCost + (placementCount * costIncrement)
        /// </summary>
        public int GetEffectiveCost(int slotIndex, int placementCount)
        {
            if (slotIndex < 0 || slotIndex >= costs.Count) return 0;
            var entry = costs[slotIndex];
            if (entry.resourceType == ResourceType.None) return 0;
            return entry.baseCost + (placementCount * entry.costIncrement);
        }

        /// <summary>
        /// Get all effective costs as PlacementCost structs (for PlacementCostDisplay).
        /// </summary>
        public List<PlacementCost> GetEffectivePlacementCosts(int placementCount)
        {
            var result = new List<PlacementCost>();
            foreach (var c in costs)
            {
                if (c.resourceType == ResourceType.None) continue;
                int effective = c.baseCost + (placementCount * c.costIncrement);
                if (effective <= 0) continue;

                result.Add(new PlacementCost
                {
                    resourceType = c.resourceType,
                    amount = effective,
                    icon = null // Resolved at runtime from CurrencyDatabase
                });
            }
            return result;
        }
    }

    /// <summary>
    /// Which database an item originates from.
    /// </summary>
    public enum ItemSourceDatabase
    {
        Building,
        Worker,
        Furniture
    }

    /// <summary>
    /// Central game economy and balancing configuration.
    /// One ScriptableObject that controls placement costs and cost escalation
    /// for ALL placeable items across all databases.
    ///
    /// The custom inspector provides:
    ///   - "Sync from Databases" button to pull all entries
    ///   - Per-item: up to 3 resource costs with dropdown from ResourceType
    ///   - Per-cost: base amount + additive increment per placement
    ///   - Visual icon for quick identification
    ///
    /// At runtime, EconomyManager reads this config and tracks placement counts
    /// to calculate escalating costs.
    ///
    /// Create via: Assets → Create → ClockworkCraft → Economy Balance Config
    /// </summary>
    [CreateAssetMenu(fileName = "EconomyBalanceConfig", menuName = "ClockworkCraft/Economy Balance Config")]
    public class EconomyBalanceConfig : ScriptableObject
    {
        [Header("Item Costs")]
        [Tooltip("Economy entries for all placeable items. Use 'Sync from Databases' in inspector to populate.")]
        public List<ItemEconomyEntry> entries = new List<ItemEconomyEntry>();

        // ── Query Methods ──────────────────────────────────────────────

        /// <summary>Find the economy entry for an item by name.</summary>
        public ItemEconomyEntry GetEntry(string itemName)
        {
            foreach (var e in entries)
                if (e.itemName == itemName)
                    return e;
            return null;
        }

        /// <summary>Find the economy entry for an item by name and source database.</summary>
        public ItemEconomyEntry GetEntry(string itemName, ItemSourceDatabase source)
        {
            foreach (var e in entries)
                if (e.itemName == itemName && e.sourceDatabase == source)
                    return e;
            return null;
        }

        /// <summary>Get all entries from a specific database.</summary>
        public List<ItemEconomyEntry> GetEntriesFromDatabase(ItemSourceDatabase source)
        {
            var result = new List<ItemEconomyEntry>();
            foreach (var e in entries)
                if (e.sourceDatabase == source)
                    result.Add(e);
            return result;
        }

        /// <summary>Total number of entries.</summary>
        public int Count => entries.Count;
    }
}
