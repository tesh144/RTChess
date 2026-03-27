#pragma warning disable CS0414, CS0219, CS0618
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
        [SerializeField] private GridShape shape;
        [SerializeField] private int currentRotation;

        [Header("Fog")]
        [SerializeField] private int fogRevealRadius = 1;

        // Adjacency tracking
        private List<FurnitureObject> adjacentFurniture = new List<FurnitureObject>();

        // Public accessors
        public FurnitureType Type => furnitureType;
        public void SetType(FurnitureType type) { furnitureType = type; }
        public bool IsFunctional => isFunctional;
        public virtual bool IsWalkable => isWalkableDefault; // Virtual - Chair overrides this
        public int GridX { get => gridX; set => gridX = value; }
        public int GridY { get => gridY; set => gridY = value; }
        public GridShape Shape { get => shape; set => shape = value; }
        public int CurrentRotation { get => currentRotation; set => currentRotation = value; }

        /// <summary>
        /// Legacy GridSize shim — returns bounding box of current shape at rotation 0.
        /// Use Shape + CurrentRotation directly where possible.
        /// </summary>
        public Vector2Int GridSize
        {
            get => (shape != null && !shape.IsEmpty) ? shape.GetBounds(0) : Vector2Int.one;
            set
            {
                // Preserve backward-compat: setting GridSize creates a rectangle shape
                shape = GridShape.Rectangle(value.x, value.y);
            }
        }

        public int FogRevealRadius { get => fogRevealRadius; set => fogRevealRadius = value; }
        public List<FurnitureObject> AdjacentFurniture => adjacentFurniture;

        /// <summary>
        /// Called after furniture is placed on the grid (legacy overload — uses rectangle shape).
        /// Prefer the GridShape overload for multi-cell / rotated objects.
        /// </summary>
        public virtual void OnPlaced(int x, int y, Vector2Int size)
        {
            OnPlaced(x, y, GridShape.Rectangle(size.x, size.y), 0);
        }

        /// <summary>
        /// Called after furniture is placed on the grid with a specific GridShape and rotation.
        /// </summary>
        public virtual void OnPlaced(int x, int y, GridShape placedShape, int rotation)
        {
            gridX = x;
            gridY = y;
            shape = placedShape ?? GridShape.Rectangle(1, 1);
            currentRotation = rotation;
            gameObject.name = $"{furnitureType}_{gridX}_{gridY}";

            DetectAdjacentFurniture();
            UpdateGridCellState();
            RevealSurroundingTiles();

            // Torches colorize environment objects in a 1-tile radius on placement,
            // and subscribe to fog reveal so objects that appear later also get colorized.
            if (fogRevealRadius >= 2)
            {
                ColorizeNearbyObjects(1);
                if (FogManager.Instance != null)
                    FogManager.Instance.OnCellRevealed += OnNearbyFogCellRevealed;
            }

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
        /// Uses PlaceWithOffsets so all shape cells are registered correctly.
        /// </summary>
        protected void UpdateGridCellState()
        {
            GridManager gm = GridManager.Instance;
            if (gm == null) return;

            CellState state = IsWalkable ? CellState.Empty : CellState.PlayerUnit;
            GridShape effectiveShape = (shape != null && !shape.IsEmpty)
                ? shape
                : GridShape.Rectangle(1, 1);
            gm.PlaceWithOffsets(gridX, gridY, effectiveShape, currentRotation, gameObject, state);
        }

        /// <summary>
        /// Called when any fog cell is revealed. If this furniture is a torch and the revealed
        /// cell is within 1 tile, colorize whatever just appeared there.
        /// Waits one frame so FogHideable has time to reactivate the GameObject before we colorize.
        /// </summary>
        private void OnNearbyFogCellRevealed(int x, int y)
        {
            int dx = Mathf.Abs(x - gridX);
            int dy = Mathf.Abs(y - gridY);
            if (dx > 1 || dy > 1) return;
            StartCoroutine(ColorizeRevealedCell(x, y));
        }

        private System.Collections.IEnumerator ColorizeRevealedCell(int x, int y)
        {
            yield return null; // Wait one frame for FogHideable to activate the object

            GridManager gm = GridManager.Instance;
            if (gm == null) yield break;

            GameObject occupant = gm.GetCellOccupant(x, y);
            if (occupant == null) yield break;

            var desat = occupant.GetComponentInChildren<ClockworkCraft.EnvironmentDesaturation>();
            if (desat != null && !desat.HasColorized)
                desat.Colorize();
        }

        protected virtual void OnDestroy()
        {
            if (fogRevealRadius >= 2 && FogManager.Instance != null)
                FogManager.Instance.OnCellRevealed -= OnNearbyFogCellRevealed;
        }

        /// <summary>
        /// Colorize (un-grey) environment objects within the given radius on the grid.
        /// Used by torches to reveal nearby objects on placement.
        /// </summary>
        protected void ColorizeNearbyObjects(int radius)
        {
            GridManager gm = GridManager.Instance;
            if (gm == null) return;

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    GameObject occupant = gm.GetCellOccupant(gridX + dx, gridY + dy);
                    if (occupant == null) continue;
                    var desat = occupant.GetComponentInChildren<ClockworkCraft.EnvironmentDesaturation>();
                    if (desat != null && !desat.HasColorized)
                        desat.Colorize();
                }
            }
        }

        /// <summary>
        /// Reveal fog of war around placed furniture.
        /// Iterates all occupied cells and reveals a radius around each one.
        /// </summary>
        protected void RevealSurroundingTiles()
        {
            // Route through FogManager so that BOTH tile visuals (TileFog)
            // and hidden objects (FogHideable) get notified via OnCellRevealed.
            // GridManager already subscribes to OnCellRevealed → RevealTile.
            if (FogManager.Instance == null) return;

            int revealRadius = fogRevealRadius;
            GridShape effectiveShape = (shape != null && !shape.IsEmpty)
                ? shape
                : GridShape.Rectangle(1, 1);

            var offsets = effectiveShape.GetOffsets(currentRotation);
            foreach (var offset in offsets)
            {
                int cellX = gridX + offset.x;
                int cellY = gridY + offset.y;
                for (int dx = -revealRadius; dx <= revealRadius; dx++)
                {
                    for (int dy = -revealRadius; dy <= revealRadius; dy++)
                    {
                        FogManager.Instance.RevealCell(cellX + dx, cellY + dy);
                    }
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
                GridShape effectiveShape = (shape != null && !shape.IsEmpty)
                    ? shape
                    : GridShape.Rectangle(1, 1);
                gm.RemoveWithOffsets(gridX, gridY, effectiveShape, currentRotation);
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
