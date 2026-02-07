using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Draws grid lines using GL.Lines for clear cell boundaries.
    /// Attach to the same GameObject as GridManager.
    /// </summary>
    [RequireComponent(typeof(GridManager))]
    public class GridVisualizer : MonoBehaviour
    {
        [Header("Grid Visuals")]
        [SerializeField] private Color gridLineColor = new Color(0.4f, 0.4f, 0.4f, 0.8f);
        [SerializeField] private Color gridFillColor = new Color(0.15f, 0.15f, 0.2f, 0.9f);
        [SerializeField] private float lineWidth = 0.03f;

        [Header("Cell Highlights - Phase 7")]
        [SerializeField] private Color occupiedCellColor = new Color(0.3f, 0.2f, 0.2f, 0.5f);
        [SerializeField] private bool showCellStates = true;

        private Material lineMaterial;
        private GridManager grid;

        private void Start()
        {
            grid = GetComponent<GridManager>();
            CreateLineMaterial();
        }

        private void CreateLineMaterial()
        {
            // Unity's built-in shader for drawing simple colored lines
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            lineMaterial = new Material(shader);
            lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            lineMaterial.SetInt("_ZWrite", 0);
        }

        private void OnRenderObject()
        {
            if (grid == null || lineMaterial == null) return;

            lineMaterial.SetPass(0);

            float halfCell = grid.CellSize * 0.5f;

            // Draw filled cell backgrounds (Phase 7: with state-based coloring)
            GL.PushMatrix();
            GL.Begin(GL.QUADS);

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    Vector3 center = grid.GridToWorldPosition(x, y);
                    float yPos = -0.01f; // Slightly below ground to avoid z-fighting

                    // Set color based on cell state (Phase 7)
                    Color cellColor = gridFillColor;
                    if (showCellStates)
                    {
                        CellState state = grid.GetCellState(x, y);
                        if (state != CellState.Empty)
                        {
                            // Occupied cell: slight reddish tint
                            cellColor = Color.Lerp(gridFillColor, occupiedCellColor, 0.5f);
                        }
                    }

                    GL.Color(cellColor);

                    GL.Vertex3(center.x - halfCell, yPos, center.z - halfCell);
                    GL.Vertex3(center.x + halfCell, yPos, center.z - halfCell);
                    GL.Vertex3(center.x + halfCell, yPos, center.z + halfCell);
                    GL.Vertex3(center.x - halfCell, yPos, center.z + halfCell);
                }
            }

            GL.End();
            GL.PopMatrix();

            // Draw grid lines
            GL.PushMatrix();
            GL.Begin(GL.LINES);
            GL.Color(gridLineColor);

            // Calculate grid bounds
            Vector3 bottomLeft = grid.GridToWorldPosition(0, 0);
            Vector3 topRight = grid.GridToWorldPosition(grid.Width - 1, grid.Height - 1);

            float minX = bottomLeft.x - halfCell;
            float maxX = topRight.x + halfCell;
            float minZ = bottomLeft.z - halfCell;
            float maxZ = topRight.z + halfCell;

            // Vertical lines
            for (int x = 0; x <= grid.Width; x++)
            {
                float xPos = minX + x * grid.CellSize;
                GL.Vertex3(xPos, 0f, minZ);
                GL.Vertex3(xPos, 0f, maxZ);
            }

            // Horizontal lines
            for (int y = 0; y <= grid.Height; y++)
            {
                float zPos = minZ + y * grid.CellSize;
                GL.Vertex3(minX, 0f, zPos);
                GL.Vertex3(maxX, 0f, zPos);
            }

            GL.End();
            GL.PopMatrix();
        }

        private void OnDestroy()
        {
            if (lineMaterial != null)
            {
                DestroyImmediate(lineMaterial);
            }
        }
    }
}
