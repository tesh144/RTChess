#pragma warning disable CS0414, CS0219, CS0618
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

        [Tooltip("Base cost for the first placement. Ignored if costTable is populated.")]
        public int baseCost = 0;

        [Tooltip("How much the cost increases per successful placement. 0 = flat cost. Ignored if costTable is populated.")]
        public int costIncrement = 0;

        [Tooltip("Optional stepped cost table. If populated, overrides baseCost + increment. " +
                 "Index = number of currently active units of this type. " +
                 "Last entry is used for any count beyond the table length.")]
        public List<int> costTable = new List<int>();
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
            {
                if (c.resourceType == ResourceType.None) continue;
                if (c.baseCost > 0) return true;
                if (c.costTable != null)
                    foreach (var v in c.costTable)
                        if (v > 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Get the effective cost for a resource slot given the current active unit count.
        /// If a costTable is provided, it is used as a stepped lookup (index = activeCount).
        /// Otherwise falls back to: baseCost + (activeCount * costIncrement).
        /// </summary>
        public int GetEffectiveCost(int slotIndex, int activeCount)
        {
            if (slotIndex < 0 || slotIndex >= costs.Count) return 0;
            var entry = costs[slotIndex];
            if (entry.resourceType == ResourceType.None) return 0;

            if (entry.costTable != null && entry.costTable.Count > 0)
            {
                int tableIndex = Mathf.Clamp(activeCount, 0, entry.costTable.Count - 1);
                return entry.costTable[tableIndex];
            }

            return entry.baseCost + (activeCount * entry.costIncrement);
        }

        /// <summary>
        /// Get all effective costs as PlacementCost structs (for PlacementCostDisplay).
        /// activeCount is the number of currently active units of this type.
        /// </summary>
        public List<PlacementCost> GetEffectivePlacementCosts(int activeCount)
        {
            var result = new List<PlacementCost>();
            foreach (var c in costs)
            {
                if (c.resourceType == ResourceType.None) continue;

                int effective;
                if (c.costTable != null && c.costTable.Count > 0)
                {
                    int tableIndex = Mathf.Clamp(activeCount, 0, c.costTable.Count - 1);
                    effective = c.costTable[tableIndex];
                }
                else
                {
                    effective = c.baseCost + (activeCount * c.costIncrement);
                }

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
    /// Central placement costs database for all placeable items.
    /// One ScriptableObject that controls placement costs and cost escalation
    /// for ALL placeable items across all databases.
    ///
    /// The custom inspector provides:
    ///   - "Sync from Databases" button to pull all item entries
    ///   - Per-item: up to 3 resource cost slots with ResourceType dropdown
    ///   - Per-cost: stepped cost table (from Sheets sync) or base amount + increment
    ///   - Visual icon for quick identification
    ///
    /// At runtime, EconomyManager reads this config and tracks placement counts
    /// to calculate escalating costs.
    ///
    /// Cost tables are populated by SheetSyncEditor → "Sync Placement Costs".
    ///
    /// Create via: Assets → Create → ClockworkCraft → Placement Costs Database
    /// </summary>
    [CreateAssetMenu(fileName = "PlacementCostsDatabase", menuName = "ClockworkCraft/Placement Costs Database")]
    public class PlacementCostsDatabase : ScriptableObject
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
