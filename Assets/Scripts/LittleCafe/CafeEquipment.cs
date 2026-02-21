using UnityEngine;
using ClockworkGrid;

namespace LittleCafe
{
    public class CafeEquipment : MonoBehaviour
    {
        public EquipmentType Type { get; private set; }
        public int GridX { get; private set; }
        public int GridY { get; private set; }
        public Vector2Int GridSize { get; private set; } = Vector2Int.one;

        private bool initialized;

        /// <summary>
        /// Called by DragDropHandler after placement.
        /// </summary>
        public void Initialize(int gridX, int gridY, Vector2Int gridSize)
        {
            GridX = gridX;
            GridY = gridY;
            GridSize = gridSize;
            initialized = true;
            gameObject.name = $"Equipment_{gridX}_{gridY}_{gridSize.x}x{gridSize.y}";

            GridManager gm = GridManager.Instance;
            if (gm != null)
                RevealAdjacentTiles(gm);
        }

        private void Start()
        {
            if (initialized) return;

            // Fallback: discover grid position from world position
            GridManager gm = GridManager.Instance;
            if (gm == null) return;

            if (gm.WorldToGridPosition(transform.position, out int gx, out int gy))
            {
                GridX = gx;
                GridY = gy;
                RevealAdjacentTiles(gm);
            }
        }

        /// <summary>
        /// Reveal tiles adjacent to ALL cells in the footprint.
        /// </summary>
        private void RevealAdjacentTiles(GridManager gm)
        {
            // Reveal a 1-cell border around the full footprint
            for (int x = -1; x <= GridSize.x; x++)
            {
                for (int y = -1; y <= GridSize.y; y++)
                {
                    // Skip interior cells (only reveal the border)
                    if (x >= 0 && x < GridSize.x && y >= 0 && y < GridSize.y)
                        continue;

                    gm.RevealTile(GridX + x, GridY + y, immediate: false);
                }
            }
        }

        /// <summary>
        /// Remove this equipment from all occupied grid cells.
        /// </summary>
        public void RemoveFromGrid()
        {
            GridManager gm = GridManager.Instance;
            if (gm != null)
                gm.RemoveMultiCell(GridX, GridY, GridSize);
        }
    }
}
