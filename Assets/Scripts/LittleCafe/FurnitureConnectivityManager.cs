#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System.Collections.Generic;
using ClockworkGrid;
using System.Linq;

namespace LittleCafe
{
    /// <summary>
    /// Singleton manager for furniture connectivity.
    /// Tracks all placed furniture and maintains connectivity groups.
    /// Detects when furniture is placed/removed and updates groups dynamically.
    /// </summary>
    public class FurnitureConnectivityManager : MonoBehaviour
    {
        public static FurnitureConnectivityManager Instance { get; private set; }

        // Track all furniture and their groups
        private Dictionary<FurnitureObject, FurnitureGroup> furnitureToGroup = new Dictionary<FurnitureObject, FurnitureGroup>();
        private HashSet<FurnitureGroup> allGroups = new HashSet<FurnitureGroup>();
        private List<FurnitureObject> allFurniture = new List<FurnitureObject>();

        // Public read-only access for debug visualization
        public IReadOnlyCollection<FurnitureGroup> AllGroups => allGroups;
        public IReadOnlyList<FurnitureObject> AllFurniture => allFurniture;

        // Color assignment for debug visualization
        private List<Color> assignedColors = new List<Color>();
        private int nextColorIndex = 0;

        // Callback system for when connectivity changes
        public delegate void OnConnectivityChanged();
        public event OnConnectivityChanged ConnectivityChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // Generate a palette of distinct colors for debug visualization
            GenerateColorPalette();
        }

        /// <summary>
        /// Register a newly placed furniture piece
        /// </summary>
        public void RegisterFurniture(FurnitureObject furniture)
        {
            if (furniture == null) return;
            if (allFurniture.Contains(furniture)) return;

            allFurniture.Add(furniture);

            Debug.Log($"[FurnitureConnectivityManager] Registered {furniture.Type} at ({furniture.GridX}, {furniture.GridY})");

            // Update connectivity based on new furniture
            UpdateConnectivity();
        }

        /// <summary>
        /// Unregister a furniture piece that was removed
        /// </summary>
        public void UnregisterFurniture(FurnitureObject furniture)
        {
            if (furniture == null) return;

            allFurniture.Remove(furniture);

            // Remove from its group
            if (furnitureToGroup.TryGetValue(furniture, out FurnitureGroup group))
            {
                group.RemoveMember(furniture);
                furnitureToGroup.Remove(furniture);

                // If group is now empty, remove it
                if (group.IsEmpty)
                {
                    allGroups.Remove(group);
                }
            }

            Debug.Log($"[FurnitureConnectivityManager] Unregistered {furniture.Type}");

            // Update connectivity
            UpdateConnectivity();
        }

        /// <summary>
        /// Get the connectivity group for a furniture piece
        /// </summary>
        public FurnitureGroup GetFurnitureGroup(FurnitureObject furniture)
        {
            if (furnitureToGroup.TryGetValue(furniture, out FurnitureGroup group))
            {
                return group;
            }
            return null;
        }

        /// <summary>
        /// Update connectivity - should be called whenever furniture is placed or removed
        /// </summary>
        public void UpdateConnectivity()
        {
            // Rebuild all groups from scratch
            RebuildAllGroups();

            // Notify listeners
            ConnectivityChanged?.Invoke();
        }

        /// <summary>
        /// Rebuild all connectivity groups.
        /// Rules:
        ///   - Tables connect to adjacent tables (BFS flood fill)
        ///   - Chairs attach to adjacent table groups (but NOT to other chairs)
        ///   - Other furniture types connect to same-type adjacents
        /// </summary>
        private void RebuildAllGroups()
        {
            // Clear existing groups
            furnitureToGroup.Clear();
            allGroups.Clear();

            // PASS 1: Build groups for functional furniture types only
            // Tables connect to tables, Walls connect to walls, etc. via same-type BFS
            // Decorations and Chairs are excluded from this pass
            foreach (var furniture in allFurniture)
            {
                if (furnitureToGroup.ContainsKey(furniture)) continue;
                if (furniture.Type == FurnitureType.Chair) continue;      // Chairs handled in pass 2
                if (furniture.Type == FurnitureType.Decoration) continue; // Decorations don't form groups

                FurnitureGroup group = FindOrCreateGroupForFurniture(furniture);
                group.AddMember(furniture);
                furnitureToGroup[furniture] = group;

                // BFS: connect same-type neighbors
                HashSet<FurnitureObject> processed = new HashSet<FurnitureObject> { furniture };
                Queue<FurnitureObject> toProcess = new Queue<FurnitureObject>();
                toProcess.Enqueue(furniture);

                while (toProcess.Count > 0)
                {
                    var current = toProcess.Dequeue();
                    var neighbors = FindAdjacentFurniture(current);

                    foreach (var neighbor in neighbors)
                    {
                        if (neighbor.Type != furniture.Type) continue;
                        if (processed.Contains(neighbor)) continue;

                        processed.Add(neighbor);

                        if (!furnitureToGroup.ContainsKey(neighbor))
                        {
                            group.AddMember(neighbor);
                            furnitureToGroup[neighbor] = group;
                            toProcess.Enqueue(neighbor);
                        }
                    }
                }
            }

            // PASS 2: Attach chairs to adjacent table groups
            foreach (var furniture in allFurniture)
            {
                if (furniture.Type != FurnitureType.Chair) continue;
                if (furnitureToGroup.ContainsKey(furniture)) continue;

                // Find adjacent tables and join their group
                var neighbors = FindAdjacentFurniture(furniture);
                FurnitureGroup tableGroup = null;

                foreach (var neighbor in neighbors)
                {
                    if (neighbor.Type == FurnitureType.Table && furnitureToGroup.TryGetValue(neighbor, out FurnitureGroup group))
                    {
                        tableGroup = group;
                        break;
                    }
                }

                if (tableGroup != null)
                {
                    // Chair joins the table group
                    tableGroup.AddMember(furniture);
                    furnitureToGroup[furniture] = tableGroup;
                }
                // Chairs NOT adjacent to tables get no group (isolated, no outline)
            }

            // Log summary
            Debug.Log($"[FurnitureConnectivityManager] Rebuilt connectivity: {allFurniture.Count} furniture in {allGroups.Count} groups");
            foreach (var group in allGroups)
            {
                Debug.Log($"  → {group}");
            }
        }

        /// <summary>
        /// Find all furniture adjacent to a given piece (orthogonal only: N, S, E, W)
        /// </summary>
        private List<FurnitureObject> FindAdjacentFurniture(FurnitureObject furniture)
        {
            List<FurnitureObject> adjacent = new List<FurnitureObject>();

            if (furniture.GridX == -1 || furniture.GridY == -1)
                return adjacent;

            Vector2Int gridSize = furniture.GridSize;
            int fx = furniture.GridX;
            int fy = furniture.GridY;

            // Strict orthogonal adjacency only — no diagonals.
            // For each other furniture, check if it shares a face edge (not just a corner).
            foreach (var other in allFurniture)
            {
                if (other == furniture) continue;
                if (other.GridX == -1 || other.GridY == -1) continue;

                Vector2Int otherSize = other.GridSize;
                int ox = other.GridX;
                int oy = other.GridY;

                bool isAdjacent = false;

                // North: other sits directly above, and their X ranges STRICTLY overlap (not just touch)
                if (oy == fy + gridSize.y && StrictOverlapX(fx, gridSize.x, ox, otherSize.x))
                    isAdjacent = true;

                // South: other sits directly below
                if (oy + otherSize.y == fy && StrictOverlapX(fx, gridSize.x, ox, otherSize.x))
                    isAdjacent = true;

                // East: other sits directly to the right, and Y ranges STRICTLY overlap
                if (ox == fx + gridSize.x && StrictOverlapY(fy, gridSize.y, oy, otherSize.y))
                    isAdjacent = true;

                // West: other sits directly to the left
                if (ox + otherSize.x == fx && StrictOverlapY(fy, gridSize.y, oy, otherSize.y))
                    isAdjacent = true;

                if (isAdjacent && !adjacent.Contains(other))
                {
                    adjacent.Add(other);
                    Debug.Log($"[Adjacency] {furniture.Type}({fx},{fy} {gridSize.x}x{gridSize.y}) ↔ {other.Type}({ox},{oy} {otherSize.x}x{otherSize.y})");
                }
            }

            return adjacent;
        }

        /// <summary>
        /// Strict X overlap: ranges must share at least one full unit of overlap.
        /// This prevents corner-only touching from counting as adjacent.
        /// e.g. [2,3) and [3,4) do NOT overlap. [2,4) and [3,5) DO overlap.
        /// </summary>
        private bool StrictOverlapX(int x1, int size1, int x2, int size2)
        {
            int overlapStart = Mathf.Max(x1, x2);
            int overlapEnd = Mathf.Min(x1 + size1, x2 + size2);
            return overlapEnd > overlapStart; // Must have > 0 overlap width
        }

        /// <summary>
        /// Strict Y overlap: same logic for Y axis.
        /// </summary>
        private bool StrictOverlapY(int y1, int size1, int y2, int size2)
        {
            int overlapStart = Mathf.Max(y1, y2);
            int overlapEnd = Mathf.Min(y1 + size1, y2 + size2);
            return overlapEnd > overlapStart;
        }

        /// <summary>
        /// Check if two X ranges overlap
        /// </summary>
        private bool IsOverlappingX(int x1, int size1, int x2, int size2)
        {
            return !(x1 + size1 <= x2 || x2 + size2 <= x1);
        }

        /// <summary>
        /// Check if two Y ranges overlap
        /// </summary>
        private bool IsOverlappingY(int y1, int size1, int y2, int size2)
        {
            return !(y1 + size1 <= y2 || y2 + size2 <= y1);
        }

        /// <summary>
        /// Find or create a group for a furniture piece
        /// </summary>
        private FurnitureGroup FindOrCreateGroupForFurniture(FurnitureObject furniture)
        {
            // Create new group with assigned color
            Color groupColor = GetNextColor();
            FurnitureGroup group = new FurnitureGroup(furniture.Type, groupColor);
            allGroups.Add(group);
            return group;
        }

        /// <summary>
        /// Generate a palette of distinct colors for debug visualization
        /// </summary>
        private void GenerateColorPalette()
        {
            assignedColors.Clear();
            nextColorIndex = 0;

            // Generate colors using HSV space for good distribution
            for (int i = 0; i < 12; i++)
            {
                float hue = i / 12f;
                Color color = Color.HSVToRGB(hue, 0.8f, 0.9f);
                color.a = 0.7f; // Semi-transparent
                assignedColors.Add(color);
            }

            Debug.Log($"[FurnitureConnectivityManager] Generated {assignedColors.Count} debug colors");
        }

        /// <summary>
        /// Get the next color from the palette (cycles through)
        /// </summary>
        private Color GetNextColor()
        {
            if (assignedColors.Count == 0)
                GenerateColorPalette();

            Color color = assignedColors[nextColorIndex % assignedColors.Count];
            nextColorIndex++;
            return color;
        }

        /// <summary>
        /// Get all furniture groups
        /// </summary>
        public IEnumerable<FurnitureGroup> GetAllGroups()
        {
            return allGroups;
        }

        /// <summary>
        /// Get all furniture pieces
        /// </summary>
        public IEnumerable<FurnitureObject> GetAllFurniture()
        {
            return allFurniture;
        }

        /// <summary>
        /// Debug: Print all groups
        /// </summary>
        public void DebugPrintGroups()
        {
            Debug.Log("=== Furniture Connectivity Groups ===");
            foreach (var group in allGroups)
            {
                Debug.Log($"{group}");
                foreach (var member in group.Members)
                {
                    Debug.Log($"  - {member.Type} at ({member.GridX}, {member.GridY})");
                }
            }
        }
    }
}
