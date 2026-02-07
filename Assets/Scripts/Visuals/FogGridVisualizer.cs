using UnityEngine;
using System.Collections.Generic;

namespace ClockworkGrid
{
    /// <summary>
    /// Creates and manages fog overlay visuals for the entire grid.
    /// Instantiates FogVisual components for each cell and reveals them when fog clears.
    /// Iteration 7: Fog of War & Scouting
    /// </summary>
    public class FogGridVisualizer : MonoBehaviour
    {
        // Singleton
        public static FogGridVisualizer Instance { get; private set; }

        [Header("Fog Visual Settings")]
        [SerializeField] private float fadeOutDuration = 0.3f;
        [SerializeField] private float fogOpacity = 0.85f;
        [SerializeField] private float fogHeight = 0.1f; // Height above grid to avoid z-fighting

        // Fog visuals per cell
        private Dictionary<Vector2Int, FogVisual> fogVisuals = new Dictionary<Vector2Int, FogVisual>();

        private GridManager gridManager;
        private FogManager fogManager;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Initialize(GridManager grid, FogManager fog)
        {
            gridManager = grid;
            fogManager = fog;

            // Create fog overlays for all cells
            CreateFogOverlays();

            // Subscribe to reveal events
            if (fogManager != null)
            {
                fogManager.OnCellRevealed += OnCellRevealed;
            }

            Debug.Log($"FogGridVisualizer initialized: {grid.Width}x{grid.Height} fog overlays created");
        }

        private void OnDestroy()
        {
            if (fogManager != null)
            {
                fogManager.OnCellRevealed -= OnCellRevealed;
            }
        }

        /// <summary>
        /// Create fog overlay quads for all grid cells
        /// </summary>
        private void CreateFogOverlays()
        {
            if (gridManager == null) return;

            for (int x = 0; x < gridManager.Width; x++)
            {
                for (int y = 0; y < gridManager.Height; y++)
                {
                    // Skip if cell is already revealed
                    if (fogManager != null && fogManager.IsCellRevealed(x, y))
                        continue;

                    CreateFogVisualForCell(x, y);
                }
            }
        }

        /// <summary>
        /// Create a single fog overlay quad for a cell
        /// </summary>
        private void CreateFogVisualForCell(int x, int y)
        {
            Vector3 worldPos = gridManager.GridToWorldPosition(x, y);
            worldPos.y = fogHeight; // Slightly above grid

            // Create fog quad
            GameObject fogObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fogObj.name = $"FogOverlay_{x}_{y}";
            fogObj.transform.SetParent(transform, false);
            fogObj.transform.position = worldPos;
            fogObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Face up for top-down camera

            // Scale to match grid cell size
            float cellSize = gridManager.CellSize;
            fogObj.transform.localScale = new Vector3(cellSize * 0.95f, cellSize * 0.95f, 1f); // Slightly smaller to show grid lines

            // Remove collider (don't want fog to block raycasts)
            Collider fogCollider = fogObj.GetComponent<Collider>();
            if (fogCollider != null)
            {
                Destroy(fogCollider);
            }

            // Set up material
            Renderer fogRenderer = fogObj.GetComponent<Renderer>();
            if (fogRenderer != null)
            {
                // Create unlit transparent material
                Material fogMat = new Material(Shader.Find("Unlit/Transparent"));
                fogMat.color = new Color(0.1f, 0.1f, 0.1f, fogOpacity);
                fogRenderer.material = fogMat;
            }

            // Add FogVisual component
            FogVisual fogVisual = fogObj.AddComponent<FogVisual>();
            fogVisual.SetFadeDuration(fadeOutDuration);
            fogVisual.SetFogOpacity(fogOpacity);

            // Store reference
            fogVisuals[new Vector2Int(x, y)] = fogVisual;
        }

        /// <summary>
        /// Called when a cell is revealed by FogManager
        /// </summary>
        private void OnCellRevealed(int x, int y)
        {
            Vector2Int key = new Vector2Int(x, y);
            if (fogVisuals.ContainsKey(key))
            {
                FogVisual fogVisual = fogVisuals[key];
                if (fogVisual != null)
                {
                    fogVisual.Reveal();
                }
                fogVisuals.Remove(key);
            }
        }

        /// <summary>
        /// Get fog visual for a cell (for debugging)
        /// </summary>
        public FogVisual GetFogVisual(int x, int y)
        {
            Vector2Int key = new Vector2Int(x, y);
            if (fogVisuals.ContainsKey(key))
            {
                return fogVisuals[key];
            }
            return null;
        }

        /// <summary>
        /// Check if fog visual exists for a cell
        /// </summary>
        public bool HasFogVisual(int x, int y)
        {
            return fogVisuals.ContainsKey(new Vector2Int(x, y));
        }

        /// <summary>
        /// Refresh fog overlays after grid expansion (Iteration 9).
        /// Destroys old fog visuals and creates new ones for the expanded grid.
        /// </summary>
        public void RefreshFogOverlays()
        {
            // Destroy all existing fog visuals
            foreach (var kvp in fogVisuals)
            {
                if (kvp.Value != null && kvp.Value.gameObject != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }
            fogVisuals.Clear();

            // Recreate fog overlays for all fogged cells
            CreateFogOverlays();

            Debug.Log($"FogGridVisualizer refreshed: {gridManager.Width}x{gridManager.Height} fog overlays recreated");
        }
    }
}
