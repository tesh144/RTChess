#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System;
using System.Collections.Generic;

namespace ClockworkGrid
{
    /// <summary>
    /// Describes the cell-offset footprint of a grid-aligned object.
    /// Each offset is a (dx, dy) from the anchor cell.
    /// Supports four 90° rotations (0 = 0°, 1 = 90° CW, 2 = 180°, 3 = 270° CW).
    ///
    /// Use the static factory <see cref="Rectangle"/> for common rectangular shapes.
    /// </summary>
    [Serializable]
    public class GridShape
    {
        [SerializeField] private List<Vector2Int> offsets = new List<Vector2Int>();

        // ── Constructors ─────────────────────────────────────────────────────

        public GridShape() { }

        public GridShape(IEnumerable<Vector2Int> cells)
        {
            offsets = new List<Vector2Int>(cells);
        }

        // ── Factory Methods ──────────────────────────────────────────────────

        /// <summary>
        /// Creates a rectangular shape with the given width (x) and height (y).
        /// The anchor is always (0,0) — offsets cover (0,0) to (w-1, h-1).
        /// </summary>
        public static GridShape Rectangle(int width, int height)
        {
            width  = Mathf.Max(1, width);
            height = Mathf.Max(1, height);

            var cells = new List<Vector2Int>(width * height);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    cells.Add(new Vector2Int(x, y));

            return new GridShape(cells);
        }

        // ── Properties ───────────────────────────────────────────────────────

        /// <summary>True when the shape has no cell offsets.</summary>
        public bool IsEmpty => offsets == null || offsets.Count == 0;

        /// <summary>Number of cells in this shape.</summary>
        public int Count => offsets?.Count ?? 0;

        // ── Query Methods ────────────────────────────────────────────────────

        /// <summary>
        /// Returns the cell offsets for a given 90° rotation step.
        /// 0 = 0°, 1 = 90° CW, 2 = 180°, 3 = 270° CW.
        /// </summary>
        public List<Vector2Int> GetOffsets(int rotation = 0)
        {
            if (offsets == null || offsets.Count == 0)
                return new List<Vector2Int> { Vector2Int.zero };

            rotation = ((rotation % 4) + 4) % 4; // normalise to 0-3
            if (rotation == 0)
                return new List<Vector2Int>(offsets);

            var rotated = new List<Vector2Int>(offsets.Count);
            foreach (var o in offsets)
            {
                rotated.Add(RotateOffset(o, rotation));
            }

            // Normalise so the min x and min y are 0 (anchor stays at bottom-left)
            int minX = int.MaxValue, minY = int.MaxValue;
            foreach (var r in rotated)
            {
                if (r.x < minX) minX = r.x;
                if (r.y < minY) minY = r.y;
            }
            if (minX != 0 || minY != 0)
            {
                for (int i = 0; i < rotated.Count; i++)
                    rotated[i] = new Vector2Int(rotated[i].x - minX, rotated[i].y - minY);
            }

            return rotated;
        }

        /// <summary>
        /// Returns the bounding-box size (width, height) of this shape at a given rotation.
        /// </summary>
        public Vector2Int GetBounds(int rotation = 0)
        {
            List<Vector2Int> cells = GetOffsets(rotation);
            if (cells.Count == 0) return Vector2Int.one;

            int maxX = 0, maxY = 0;
            foreach (var c in cells)
            {
                if (c.x > maxX) maxX = c.x;
                if (c.y > maxY) maxY = c.y;
            }
            return new Vector2Int(maxX + 1, maxY + 1);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>Rotate a single offset by 90° CW, <paramref name="steps"/> times.</summary>
        private static Vector2Int RotateOffset(Vector2Int o, int steps)
        {
            // 90° CW: (x,y) → (y, -x)
            for (int i = 0; i < steps; i++)
                o = new Vector2Int(o.y, -o.x);
            return o;
        }
    }
}
