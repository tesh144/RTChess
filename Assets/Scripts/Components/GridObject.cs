#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Marks a GameObject as a grid-aligned object with defined slot size.
    /// Used for scaling, placement validation, and multi-cell resource management.
    /// </summary>
    public class GridObject : MonoBehaviour
    {
        [Header("Grid Shape")]
        [Tooltip("Cell footprint this object occupies. Replaces the legacy gridSize field.")]
        [SerializeField] private GridShape shape;

        // ── Legacy field (hidden but kept serialised for migration) ──────────
        [HideInInspector]
        [SerializeField] private Vector2Int gridSize = Vector2Int.one;

        [Header("Visual Alignment")]
        [Tooltip("Center the object within its grid footprint")]
        [SerializeField] private bool centerInSlot = true;

        [Header("Debug")]
        [SerializeField] private bool showGizmos = true;

        // ── Properties ────────────────────────────────────────────────────────

        /// <summary>Shape of this object's grid footprint (no rotation applied here).</summary>
        public GridShape Shape
        {
            get => shape;
            set => shape = value;
        }

        /// <summary>
        /// Legacy GridSize shim — returns the bounding box of the shape at rotation 0.
        /// Setting this replaces the shape with a new rectangle.
        /// </summary>
        public Vector2Int GridSize
        {
            get
            {
                if (shape != null && !shape.IsEmpty) return shape.GetBounds(0);
                return new Vector2Int(Mathf.Max(1, gridSize.x), Mathf.Max(1, gridSize.y));
            }
            set
            {
                gridSize = new Vector2Int(Mathf.Max(1, value.x), Mathf.Max(1, value.y));
                shape = GridShape.Rectangle(gridSize.x, gridSize.y);
            }
        }

        /// <summary>Whether this object should be centered within its grid footprint.</summary>
        public bool CenterInSlot => centerInSlot;

        /// <summary>Is this a 1×1 single-cell object?</summary>
        public bool IsSingleCell => (shape == null || shape.Count == 1);

        /// <summary>Total number of cells this object occupies.</summary>
        public int CellCount => shape?.Count ?? 1;

        /// <summary>
        /// Get the world-space size this object should occupy based on its shape bounds.
        /// </summary>
        public Vector3 GetWorldSize()
        {
            Vector2Int bounds = (shape != null && !shape.IsEmpty)
                ? shape.GetBounds(0)
                : new Vector2Int(Mathf.Max(1, gridSize.x), Mathf.Max(1, gridSize.y));

            if (GridManager.Instance != null)
            {
                float cellSize = GridManager.Instance.CellSize;
                return new Vector3(bounds.x * cellSize, 0, bounds.y * cellSize);
            }

            const float fallbackCellSize = 1.5f;
            return new Vector3(bounds.x * fallbackCellSize, 0, bounds.y * fallbackCellSize);
        }

        /// <summary>
        /// Check if this object's current scale matches its intended grid size.
        /// </summary>
        public bool IsProperlyScaled()
        {
            Bounds bounds = CalculateBounds();
            Vector3 targetSize = GetWorldSize();

            float tolerance = 0.1f; // 10% tolerance
            bool widthMatch = Mathf.Abs(bounds.size.x - targetSize.x) < tolerance;
            bool depthMatch = Mathf.Abs(bounds.size.z - targetSize.z) < tolerance;

            return widthMatch && depthMatch;
        }

        /// <summary>
        /// Calculate visual bounds of this object and all children.
        /// </summary>
        public Bounds CalculateBounds()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(transform.position, Vector3.one);
            }

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            return bounds;
        }

        void OnDrawGizmos()
        {
            if (!showGizmos) return;

            Vector3 worldSize = GetWorldSize();
            Vector3 center = centerInSlot ? transform.position : transform.position + new Vector3(worldSize.x * 0.5f, 0, worldSize.z * 0.5f);

            // Draw green wireframe box showing intended grid footprint
            Gizmos.color = IsProperlyScaled() ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(center, worldSize + Vector3.up * 0.1f);

            // Draw corner markers
            Gizmos.color = Color.cyan;
            float markerSize = 0.2f;
            Vector3 halfSize = worldSize * 0.5f;

            Vector3[] corners = new Vector3[]
            {
                center + new Vector3(-halfSize.x, 0, -halfSize.z),
                center + new Vector3(halfSize.x, 0, -halfSize.z),
                center + new Vector3(halfSize.x, 0, halfSize.z),
                center + new Vector3(-halfSize.x, 0, halfSize.z)
            };

            foreach (Vector3 corner in corners)
            {
                Gizmos.DrawWireSphere(corner, markerSize);
            }
        }

        void OnDrawGizmosSelected()
        {
            if (!showGizmos) return;

            // Draw current bounds in red
            Bounds currentBounds = CalculateBounds();
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireCube(currentBounds.center, currentBounds.size);

#if UNITY_EDITOR
            // Draw label showing shape bounds
            Vector2Int labelBounds = (shape != null && !shape.IsEmpty)
                ? shape.GetBounds(0)
                : new Vector2Int(Mathf.Max(1, gridSize.x), Mathf.Max(1, gridSize.y));
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2f,
                $"{labelBounds.x}×{labelBounds.y} cells ({CellCount} total)\n{(IsProperlyScaled() ? "✓ Scaled" : "⚠ Needs scaling")}"
            );
#endif
        }
    }
}
