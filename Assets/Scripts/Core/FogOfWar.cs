using UnityEngine;
using System.Collections.Generic;

namespace ClockworkGrid
{
    /// <summary>
    /// Fog of War system that reveals grid cells based on player unit vision.
    /// Phase 7: Creates strategic depth by hiding unknown areas.
    /// </summary>
    public class FogOfWar : MonoBehaviour
    {
        // Singleton
        public static FogOfWar Instance { get; private set; }

        [Header("Fog Settings")]
        [SerializeField] private int visionRadius = 2; // Cells around player units
        [SerializeField] private Color fogColor = new Color(0.05f, 0.05f, 0.1f, 0.95f); // Very dark
        [SerializeField] private Color revealedColor = new Color(0.15f, 0.15f, 0.2f, 0.3f); // Light overlay

        // Fog state (per cell)
        private bool[,] isRevealed; // Has this cell ever been seen?
        private bool[,] isVisible;  // Is this cell currently visible?

        // Fog visuals
        private Material fogMaterial;
        private GridManager grid;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Initialize(GridManager gridManager)
        {
            grid = gridManager;

            // Initialize fog arrays
            isRevealed = new bool[grid.Width, grid.Height];
            isVisible = new bool[grid.Width, grid.Height];

            // All cells start unrevealed
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    isRevealed[x, y] = false;
                    isVisible[x, y] = false;
                }
            }

            CreateFogMaterial();

            // Update fog when units are placed or moved
            UpdateVision();
        }

        /// <summary>
        /// Check if a cell is currently visible
        /// </summary>
        public bool IsCellVisible(int x, int y)
        {
            if (!IsValidCell(x, y)) return false;
            return isVisible[x, y];
        }

        /// <summary>
        /// Check if a cell has been revealed (seen at least once)
        /// </summary>
        public bool IsCellRevealed(int x, int y)
        {
            if (!IsValidCell(x, y)) return false;
            return isRevealed[x, y];
        }

        /// <summary>
        /// Reveal a cell (can now see it)
        /// </summary>
        public void RevealCell(int x, int y)
        {
            if (!IsValidCell(x, y)) return;
            isRevealed[x, y] = true;
            isVisible[x, y] = true;
        }

        /// <summary>
        /// Update vision for all player units
        /// </summary>
        public void UpdateVision()
        {
            if (grid == null) return;

            // Reset visibility (keep revealed state)
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    isVisible[x, y] = false;
                }
            }

            // Find all player units and reveal around them
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    CellState state = grid.GetCellState(x, y);
                    if (state == CellState.PlayerUnit)
                    {
                        RevealAreaAroundCell(x, y, visionRadius);
                    }
                }
            }

            // Update enemy unit visibility
            UpdateEnemyVisibility();
        }

        /// <summary>
        /// Reveal circular area around a cell
        /// </summary>
        private void RevealAreaAroundCell(int centerX, int centerY, int radius)
        {
            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                for (int y = centerY - radius; y <= centerY + radius; y++)
                {
                    if (!IsValidCell(x, y)) continue;

                    // Check if within circular radius
                    int dx = x - centerX;
                    int dy = y - centerY;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    if (distance <= radius)
                    {
                        RevealCell(x, y);
                    }
                }
            }
        }

        /// <summary>
        /// Hide/show enemy units based on visibility
        /// </summary>
        private void UpdateEnemyVisibility()
        {
            // Find all enemy units and hide those not visible
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            foreach (GameObject obj in allObjects)
            {
                Unit unit = obj.GetComponent<Unit>();
                if (unit != null && unit.Team == Team.Enemy)
                {
                    bool shouldShow = IsCellVisible(unit.GridX, unit.GridY);

                    // Show/hide renderers
                    Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
                    foreach (Renderer r in renderers)
                    {
                        if (r != null) r.enabled = shouldShow;
                    }
                }
            }
        }

        /// <summary>
        /// Render fog overlay
        /// </summary>
        private void OnRenderObject()
        {
            if (grid == null || fogMaterial == null) return;

            fogMaterial.SetPass(0);

            float halfCell = grid.CellSize * 0.5f;
            float yPos = 0.01f; // Slightly above grid floor

            // Draw fog for unrevealed/invisible cells
            GL.PushMatrix();
            GL.Begin(GL.QUADS);

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    Color cellColor;

                    if (!isRevealed[x, y])
                    {
                        // Never seen: Full fog
                        cellColor = fogColor;
                    }
                    else if (!isVisible[x, y])
                    {
                        // Seen before but not visible now: Light fog
                        cellColor = revealedColor;
                    }
                    else
                    {
                        // Currently visible: No fog
                        continue;
                    }

                    GL.Color(cellColor);

                    Vector3 center = grid.GridToWorldPosition(x, y);

                    GL.Vertex3(center.x - halfCell, yPos, center.z - halfCell);
                    GL.Vertex3(center.x + halfCell, yPos, center.z - halfCell);
                    GL.Vertex3(center.x + halfCell, yPos, center.z + halfCell);
                    GL.Vertex3(center.x - halfCell, yPos, center.z + halfCell);
                }
            }

            GL.End();
            GL.PopMatrix();
        }

        private void CreateFogMaterial()
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            fogMaterial = new Material(shader);
            fogMaterial.hideFlags = HideFlags.HideAndDontSave;
            fogMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            fogMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            fogMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            fogMaterial.SetInt("_ZWrite", 0);
        }

        private bool IsValidCell(int x, int y)
        {
            return x >= 0 && x < grid.Width && y >= 0 && y < grid.Height;
        }

        private void OnDestroy()
        {
            if (fogMaterial != null)
            {
                DestroyImmediate(fogMaterial);
            }
        }
    }
}
