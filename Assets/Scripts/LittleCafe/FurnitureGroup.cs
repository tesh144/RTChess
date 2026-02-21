using UnityEngine;
using System.Collections.Generic;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// Represents a connected group of furniture pieces (tables, counters, or walls).
    /// Calculates seating capacity based on the perimeter of the connected group.
    /// Assigns a unique color for debug visualization.
    /// </summary>
    public class FurnitureGroup
    {
        public HashSet<FurnitureObject> Members { get; private set; } = new HashSet<FurnitureObject>();
        public FurnitureType GroupType { get; private set; }
        public Color DebugColor { get; private set; }
        public int TotalSeatingCapacity { get; private set; }
        public Vector3 GroupCenter { get; private set; }

        // Track which cells are occupied by this group (for perimeter calculation)
        private HashSet<(int x, int y)> occupiedCells = new HashSet<(int x, int y)>();

        /// <summary>
        /// Read-only access to occupied cell coordinates (for debug visualization).
        /// </summary>
        public IReadOnlyCollection<(int x, int y)> OccupiedCells => occupiedCells;

        public FurnitureGroup(FurnitureType groupType, Color debugColor)
        {
            GroupType = groupType;
            DebugColor = debugColor;
            TotalSeatingCapacity = 0;
        }

        /// <summary>
        /// Add a furniture piece to this group
        /// </summary>
        public void AddMember(FurnitureObject furniture)
        {
            if (furniture == null) return;

            Members.Add(furniture);

            // Track occupied cells from this furniture
            AddFurnitureCells(furniture);

            // Recalculate capacity
            CalculateSeatingCapacity();

            // Update group center
            UpdateGroupCenter();
        }

        /// <summary>
        /// Remove a furniture piece from this group
        /// </summary>
        public void RemoveMember(FurnitureObject furniture)
        {
            if (furniture == null) return;

            Members.Remove(furniture);

            // Remove occupied cells
            RemoveFurnitureCells(furniture);

            // Recalculate capacity
            CalculateSeatingCapacity();

            // Update group center
            UpdateGroupCenter();
        }

        /// <summary>
        /// Calculate total seating positions based on perimeter of connected group
        /// </summary>
        private void CalculateSeatingCapacity()
        {
            TotalSeatingCapacity = 0;

            // For each occupied cell, count exposed edges
            foreach (var cell in occupiedCells)
            {
                int edgeCount = 4; // All 4 edges initially

                // Check each direction: N, S, E, W
                if (occupiedCells.Contains((cell.x, cell.y + 1))) edgeCount--; // North
                if (occupiedCells.Contains((cell.x, cell.y - 1))) edgeCount--; // South
                if (occupiedCells.Contains((cell.x + 1, cell.y))) edgeCount--; // East
                if (occupiedCells.Contains((cell.x - 1, cell.y))) edgeCount--; // West

                TotalSeatingCapacity += edgeCount;
            }

            Debug.Log($"[FurnitureGroup] {GroupType} group: {Members.Count} furniture pieces, {occupiedCells.Count} cells, {TotalSeatingCapacity} seating positions");
        }

        /// <summary>
        /// Get all available perimeter positions for chairs
        /// </summary>
        public List<Vector3> GetAllPerimeterPositions()
        {
            List<Vector3> positions = new List<Vector3>();

            GridManager gm = GridManager.Instance;
            if (gm == null) return positions;

            // For each occupied cell, find exposed edges and calculate world positions
            foreach (var cell in occupiedCells)
            {
                int x = cell.x;
                int y = cell.y;

                // Check each direction for available positions
                // North
                if (!occupiedCells.Contains((x, y + 1)))
                {
                    Vector3 pos = gm.GridToWorldPosition(x, y + 1);
                    positions.Add(pos);
                }

                // South
                if (!occupiedCells.Contains((x, y - 1)))
                {
                    Vector3 pos = gm.GridToWorldPosition(x, y - 1);
                    positions.Add(pos);
                }

                // East
                if (!occupiedCells.Contains((x + 1, y)))
                {
                    Vector3 pos = gm.GridToWorldPosition(x + 1, y);
                    positions.Add(pos);
                }

                // West
                if (!occupiedCells.Contains((x - 1, y)))
                {
                    Vector3 pos = gm.GridToWorldPosition(x - 1, y);
                    positions.Add(pos);
                }
            }

            return positions;
        }

        /// <summary>
        /// Add all cells occupied by a furniture piece
        /// </summary>
        private void AddFurnitureCells(FurnitureObject furniture)
        {
            if (furniture.GridX == -1 || furniture.GridY == -1) return;

            Vector2Int gridSize = furniture.GridSize;

            for (int dx = 0; dx < gridSize.x; dx++)
            {
                for (int dy = 0; dy < gridSize.y; dy++)
                {
                    occupiedCells.Add((furniture.GridX + dx, furniture.GridY + dy));
                }
            }
        }

        /// <summary>
        /// Remove all cells occupied by a furniture piece
        /// </summary>
        private void RemoveFurnitureCells(FurnitureObject furniture)
        {
            if (furniture.GridX == -1 || furniture.GridY == -1) return;

            Vector2Int gridSize = furniture.GridSize;

            for (int dx = 0; dx < gridSize.x; dx++)
            {
                for (int dy = 0; dy < gridSize.y; dy++)
                {
                    occupiedCells.Remove((furniture.GridX + dx, furniture.GridY + dy));
                }
            }
        }

        /// <summary>
        /// Update the group center based on all member positions
        /// </summary>
        private void UpdateGroupCenter()
        {
            if (Members.Count == 0)
            {
                GroupCenter = Vector3.zero;
                return;
            }

            Vector3 sum = Vector3.zero;
            foreach (var member in Members)
            {
                sum += member.transform.position;
            }

            GroupCenter = sum / Members.Count;
        }

        /// <summary>
        /// Check if this group is empty (no members)
        /// </summary>
        public bool IsEmpty => Members.Count == 0;

        /// <summary>
        /// Get debug info string
        /// </summary>
        public override string ToString()
        {
            return $"FurnitureGroup({GroupType}, {Members.Count} members, {TotalSeatingCapacity} seats, Color:{DebugColor.ToString()})";
        }
    }
}
