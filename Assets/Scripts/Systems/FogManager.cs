using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Manages fog of war state for the grid.
    /// Tracks which cells are revealed and provides reveal functionality.
    /// Iteration 7: Fog of War & Scouting
    /// </summary>
    public class FogManager : MonoBehaviour
    {
        // Singleton
        public static FogManager Instance { get; private set; }

        [Header("Fog Configuration")]
        [SerializeField] private int initialRevealRadius = 2; // Starting revealed area (2 = 5x5 for 11x11 grid, reveals center area for placement)
        [SerializeField] private Vector2Int initialRevealCenter = new Vector2Int(5, 5); // Grid center (11x11: indices 0-10, center at 5,5)

        // Fog state grid (true = revealed, false = fogged)
        private bool[,] revealedCells;
        private int gridWidth;
        private int gridHeight;

        // Events
        public event Action<int, int> OnCellRevealed; // Fired when a cell is revealed (x, y)

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Initialize fog grid with all cells fogged
        /// </summary>
        public void Initialize(int width, int height)
        {
            gridWidth = width;
            gridHeight = height;

            // Create fog state grid (all fogged by default)
            revealedCells = new bool[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    revealedCells[x, y] = false;
                }
            }

            // Reveal starting area at grid center (dynamically computed)
            int centerX = width / 2;
            int centerY = height / 2;
            RevealRadius(centerX, centerY, initialRevealRadius);

            Debug.Log($"FogManager initialized: {width}x{height} grid, revealed {initialRevealRadius} radius at ({initialRevealCenter.x}, {initialRevealCenter.y})");
        }

        /// <summary>
        /// Check if a cell is revealed (visible)
        /// </summary>
        public bool IsCellRevealed(int x, int y)
        {
            if (!IsValidCell(x, y)) return false;
            return revealedCells[x, y];
        }

        /// <summary>
        /// Reveal a single cell
        /// </summary>
        public void RevealCell(int x, int y)
        {
            if (!IsValidCell(x, y)) return;

            if (!revealedCells[x, y])
            {
                revealedCells[x, y] = true;
                OnCellRevealed?.Invoke(x, y);
                Debug.Log($"Revealed cell ({x}, {y})");
            }
        }

        /// <summary>
        /// Reveal a circular/square area around a center point
        /// </summary>
        public void RevealRadius(int centerX, int centerY, int radius)
        {
            // Use square reveal pattern for simplicity
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int x = centerX + dx;
                    int y = centerY + dy;

                    // Optional: Use circular pattern instead of square
                    // float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    // if (distance <= radius)
                    //     RevealCell(x, y);

                    RevealCell(x, y);
                }
            }

            Debug.Log($"Revealed {radius} radius around ({centerX}, {centerY})");
        }

        /// <summary>
        /// Get all revealed cells (for debugging/UI)
        /// </summary>
        public List<Vector2Int> GetRevealedCells()
        {
            List<Vector2Int> revealed = new List<Vector2Int>();
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    if (revealedCells[x, y])
                    {
                        revealed.Add(new Vector2Int(x, y));
                    }
                }
            }
            return revealed;
        }

        /// <summary>
        /// Get all fogged cells (for enemy spawning)
        /// </summary>
        public List<Vector2Int> GetFoggedCells()
        {
            List<Vector2Int> fogged = new List<Vector2Int>();
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    if (!revealedCells[x, y])
                    {
                        fogged.Add(new Vector2Int(x, y));
                    }
                }
            }
            return fogged;
        }

        /// <summary>
        /// Check if cell coordinates are valid
        /// </summary>
        private bool IsValidCell(int x, int y)
        {
            return x >= 0 && x < gridWidth && y >= 0 && y < gridHeight;
        }

        /// <summary>
        /// Get reveal percentage (for debugging/UI)
        /// </summary>
        public float GetRevealPercentage()
        {
            int revealedCount = 0;
            int totalCells = gridWidth * gridHeight;

            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    if (revealedCells[x, y])
                        revealedCount++;
                }
            }

            return (float)revealedCount / totalCells * 100f;
        }

        /// <summary>
        /// Expand fog to match new grid size (Iteration 9: Grid Expansion).
        /// Preserves existing revealed state, new cells start fogged.
        /// </summary>
        public void ExpandFog(Vector2Int newSize)
        {
            if (newSize.x <= 0 || newSize.y <= 0)
            {
                Debug.LogWarning($"Invalid fog size: {newSize}");
                return;
            }

            Debug.Log($"Expanding fog from {gridWidth}×{gridHeight} to {newSize.x}×{newSize.y}");

            // Create new fog array
            bool[,] newRevealedCells = new bool[newSize.x, newSize.y];

            // Initialize all new cells as fogged
            for (int x = 0; x < newSize.x; x++)
            {
                for (int y = 0; y < newSize.y; y++)
                {
                    newRevealedCells[x, y] = false;
                }
            }

            // Copy existing revealed state (centered expansion)
            if (revealedCells != null)
            {
                int offsetX = (newSize.x - gridWidth) / 2;
                int offsetY = (newSize.y - gridHeight) / 2;

                for (int x = 0; x < gridWidth; x++)
                {
                    for (int y = 0; y < gridHeight; y++)
                    {
                        int newX = x + offsetX;
                        int newY = y + offsetY;

                        if (newX >= 0 && newX < newSize.x && newY >= 0 && newY < newSize.y)
                        {
                            newRevealedCells[newX, newY] = revealedCells[x, y];
                        }
                    }
                }
            }

            // Update fog dimensions
            gridWidth = newSize.x;
            gridHeight = newSize.y;
            revealedCells = newRevealedCells;

            // Notify FogGridVisualizer to create fog overlays for new cells
            if (FogGridVisualizer.Instance != null)
            {
                FogGridVisualizer.Instance.RefreshFogOverlays();
            }

            Debug.Log($"Fog expanded successfully to {gridWidth}×{gridHeight}");
        }
    }
}
