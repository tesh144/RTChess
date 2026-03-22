#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System.Collections.Generic;

namespace LittleCafe
{
    /// <summary>
    /// Table furniture with grouping and interaction slot functionality.
    /// - Adjacent tables form groups (functional merging)
    /// - Grouped tables share interaction slots for NPCs
    /// </summary>
    public class TableObject : FurnitureObject
    {
        [Header("Table Grouping")]
        [SerializeField] private List<TableObject> groupedTables = new List<TableObject>();
        [SerializeField] private int interactionSlotsPerTable = 4; // 4 slots per 1x1 table

        public List<TableObject> GroupedTables => groupedTables;
        public int TotalInteractionSlots => GetTotalSlots();

        /// <summary>
        /// Called after table is placed. Forms groups with adjacent tables.
        /// </summary>
        public override void OnPlaced(int x, int y, Vector2Int size)
        {
            base.OnPlaced(x, y, size);
            FormTableGroup();
        }

        /// <summary>
        /// Form table group with adjacent tables (functional grouping).
        /// </summary>
        private void FormTableGroup()
        {
            groupedTables.Clear();
            groupedTables.Add(this); // Include self

            List<FurnitureObject> adjacentTables = GetAdjacentOfType(FurnitureType.Table);
            foreach (var furniture in adjacentTables)
            {
                TableObject table = furniture as TableObject;
                if (table != null && !groupedTables.Contains(table))
                {
                    groupedTables.Add(table);

                    // Recursively merge groups (flood fill)
                    foreach (var otherTable in table.GroupedTables)
                    {
                        if (!groupedTables.Contains(otherTable))
                        {
                            groupedTables.Add(otherTable);
                        }
                    }
                }
            }

            // Sync group across all tables in the group
            SyncGroupAcrossAllTables();

            Debug.Log($"[TableObject] {gameObject.name} formed group with {groupedTables.Count} tables, {TotalInteractionSlots} total slots");
        }

        /// <summary>
        /// Sync the grouped tables list across all tables in the group.
        /// </summary>
        private void SyncGroupAcrossAllTables()
        {
            foreach (var table in groupedTables)
            {
                table.groupedTables = new List<TableObject>(groupedTables);
            }
        }

        /// <summary>
        /// Calculate total interaction slots for the entire group.
        /// </summary>
        private int GetTotalSlots()
        {
            int total = 0;
            foreach (var table in groupedTables)
            {
                total += table.interactionSlotsPerTable;
            }
            return total;
        }

        /// <summary>
        /// Get all active chairs adjacent to this table group (for Phase 2 NPC seating).
        /// </summary>
        public List<ChairObject> GetAdjacentActiveChairs()
        {
            List<ChairObject> chairs = new List<ChairObject>();

            foreach (var table in groupedTables)
            {
                foreach (var furniture in table.AdjacentFurniture)
                {
                    if (furniture is ChairObject chair && chair.IsActive && !chairs.Contains(chair))
                    {
                        chairs.Add(chair);
                    }
                }
            }

            return chairs;
        }

        /// <summary>
        /// Refresh table group (call this when adjacent furniture changes).
        /// </summary>
        public void RefreshTableGroup()
        {
            DetectAdjacentFurniture();
            FormTableGroup();
        }
    }
}
