using UnityEngine;
using System.Collections.Generic;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// Base component for all placeable furniture objects.
    /// Handles grid positioning, adjacency detection, and walkability state.
    /// </summary>
    public class FurnitureObject : MonoBehaviour
    {
        [Header("Furniture Properties")]
        [SerializeField] private FurnitureType furnitureType = FurnitureType.Decoration;
        [SerializeField] private bool isFunctional = false;
        [SerializeField] private bool isWalkableDefault = false; // Default: false (blocks movement)

        [Header("Grid State")]
        [SerializeField] private int gridX;
        [SerializeField] private int gridY;
        [SerializeField] private Vector2Int gridSize = Vector2Int.one;

        // Adjacency tracking
        private List<FurnitureObject> adjacentFurniture = new List<FurnitureObject>();

        // Public accessors
        public FurnitureType Type => furnitureType;
        public void SetType(FurnitureType type) { furnitureType = type; }
        public bool IsFunctional => isFunctional;
        public virtual bool IsWalkable => isWalkableDefault; // Virtual - Chair overrides this
        public int GridX { get => gridX; set => gridX = value; }
        public int GridY { get => gridY; set => gridY = value; }
        public Vector2Int GridSize { get => gridSize; set => gridSize = value; }
        public List<FurnitureObject> AdjacentFurniture => adjacentFurniture;

        /// <summary>
        /// Called after furniture is placed on the grid.
        /// Detects adjacent furniture and performs type-specific setup.
        /// </summary>
        public virtual void OnPlaced(int x, int y, Vector2Int size)
        {
            gridX = x;
            gridY = y;
            gridSize = size;
            gameObject.name = $"{furnitureType}_{gridX}_{gridY}";

            DetectAdjacentFurniture();
            UpdateGridCellState();
            RevealSurroundingTiles();

            // Register with connectivity manager
            FurnitureConnectivityManager.Instance?.RegisterFurniture(this);

            // Trigger placement animation
            TriggerPlacementAnimation();
        }

        /// <summary>
        /// Detect furniture in adjacent cells (N, S, E, W).
        /// </summary>
        protected void DetectAdjacentFurniture()
        {
            adjacentFurniture.Clear();

            GridManager gm = GridManager.Instance;
            if (gm == null) return;

            // Check 4 adjacent cells (North, South, East, West)
            Vector2Int[] directions = new Vector2Int[]
            {
                new Vector2Int(0, 1),  // North
                new Vector2Int(0, -1), // South
                new Vector2Int(1, 0),  // East
                new Vector2Int(-1, 0)  // West
            };

            foreach (Vector2Int dir in directions)
            {
                int checkX = gridX + dir.x;
                int checkY = gridY + dir.y;

                GameObject occupant = gm.GetCellOccupant(checkX, checkY);
                if (occupant != null)
                {
                    FurnitureObject furniture = occupant.GetComponent<FurnitureObject>();
                    if (furniture != null)
                    {
                        adjacentFurniture.Add(furniture);
                    }
                }
            }

            Debug.Log($"[FurnitureObject] {gameObject.name} detected {adjacentFurniture.Count} adjacent furniture");
        }

        /// <summary>
        /// Update the GridManager cell state based on walkability.
        /// </summary>
        protected void UpdateGridCellState()
        {
            GridManager gm = GridManager.Instance;
            if (gm == null) return;

            CellState state = IsWalkable ? CellState.Empty : CellState.PlayerUnit; // TODO: Add Furniture_Walkable state
            gm.PlaceMultiCell(gridX, gridY, gridSize, gameObject, state);
        }

        /// <summary>
        /// Reveal fog of war around placed furniture.
        /// </summary>
        protected void RevealSurroundingTiles()
        {
            // Route through FogManager so that BOTH tile visuals (TileFog)
            // and hidden objects (FogHideable) get notified via OnCellRevealed.
            // GridManager already subscribes to OnCellRevealed → RevealTile.
            if (FogManager.Instance == null) return;

            int revealRadius = 1;

            for (int dx = -revealRadius; dx <= revealRadius + gridSize.x - 1; dx++)
            {
                for (int dy = -revealRadius; dy <= revealRadius + gridSize.y - 1; dy++)
                {
                    int checkX = gridX + dx;
                    int checkY = gridY + dy;
                    FogManager.Instance.RevealCell(checkX, checkY);
                }
            }
        }

        /// <summary>
        /// Check if a specific furniture type is adjacent.
        /// </summary>
        public bool HasAdjacentType(FurnitureType type)
        {
            foreach (var furniture in adjacentFurniture)
            {
                if (furniture.Type == type)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Get adjacent furniture of a specific type.
        /// </summary>
        public List<FurnitureObject> GetAdjacentOfType(FurnitureType type)
        {
            List<FurnitureObject> result = new List<FurnitureObject>();
            foreach (var furniture in adjacentFurniture)
            {
                if (furniture.Type == type)
                    result.Add(furniture);
            }
            return result;
        }

        /// <summary>
        /// Called when furniture is removed from the grid.
        /// </summary>
        public virtual void OnRemoved()
        {
            // Unregister from connectivity manager
            FurnitureConnectivityManager.Instance?.UnregisterFurniture(this);

            GridManager gm = GridManager.Instance;
            if (gm != null)
            {
                gm.RemoveMultiCell(gridX, gridY, gridSize);
            }
        }

        /// <summary>
        /// Trigger the placement animation via Animator.
        /// </summary>
        private void TriggerPlacementAnimation()
        {
            // Try using AnimatorLifecycleManager if available
            AnimatorLifecycleManager lifecycleManager = GetComponent<AnimatorLifecycleManager>();
            if (lifecycleManager != null)
            {
                lifecycleManager.PlayPlacementAnimation();
                return;
            }

            // Fallback: Direct animator trigger
            // Find the AnimatorHolder child (created by prefab generator)
            Transform animatorHolder = transform.Find("AnimatorHolder");
            if (animatorHolder == null)
            {
                Debug.LogWarning($"[FurnitureObject] No AnimatorHolder found on {gameObject.name}");
                return;
            }

            Animator animator = animatorHolder.GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogWarning($"[FurnitureObject] No Animator found on {gameObject.name}/AnimatorHolder");
                return;
            }

            // Trigger the "appear" parameter (trigger type in Animator)
            animator.SetTrigger("appear");
            Debug.Log($"[FurnitureObject] Triggered 'appear' animation on {gameObject.name}");
        }
    }

    /// <summary>
    /// Furniture type enumeration.
    /// </summary>
    public enum FurnitureType
    {
        Decoration, // Default - non-functional, visual only
        Table,      // Functional - has interaction slots, can group
        Chair,      // Functional - auto-rotates, dynamic walkability
        Wall,       // Functional - blocks movement
        Countertop, // Functional - counter/counter-like surface
        Sink,       // Functional - sink/basin
        Cooker      // Functional - stove/cooking appliance
    }
}
