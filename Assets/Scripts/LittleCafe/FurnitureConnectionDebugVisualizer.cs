using UnityEngine;
using System.Collections.Generic;

namespace LittleCafe
{
    /// <summary>
    /// Debug visualizer for furniture connectivity system.
    /// Draws bold colored outlines around each connected furniture group,
    /// tracing the actual cell edges (like a Tetris piece border).
    /// Each group gets its own color. Seating markers shown as diamonds.
    ///
    /// Toggle on/off at runtime via Inspector or the public Toggle() method.
    /// </summary>
    public class FurnitureConnectionDebugVisualizer : MonoBehaviour
    {
        public static FurnitureConnectionDebugVisualizer Instance { get; private set; }

        [Header("Toggle")]
        [SerializeField] private bool showVisualization = true;

        [Header("Outline")]
        [SerializeField] private float outlineHeight = 0.08f;       // Y height of outline above ground
        [SerializeField] private float outlineThickness = 0.03f;    // How thick the outline band is
        [SerializeField] private float outlineAlpha = 0.65f;        // Opacity of the outline
        [SerializeField] private float outlinePadding = 0.02f;      // Small gap between outline and cell edge

        [Header("Fill")]
        [SerializeField] private bool showGroupFill = true;
        [SerializeField] private float fillAlpha = 0.12f;           // Very subtle tinted fill under group

        [Header("Seating Indicators")]
        [SerializeField] private bool showSeatingPositions = true;
        [SerializeField] private float seatMarkerSize = 0.15f;
        [SerializeField] private Color availableSeatColor = new Color(0f, 1f, 0.5f, 0.6f);
        [SerializeField] private Color occupiedSeatColor = new Color(1f, 0.2f, 0.2f, 0.6f);

        private Material lineMaterial;

        public bool IsVisible => showVisualization;

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
            CreateLineMaterial();
        }

        public void Toggle()
        {
            showVisualization = !showVisualization;
            Debug.Log($"[FurnitureDebugVis] Visualization {(showVisualization ? "ON" : "OFF")}");
        }

        private void CreateLineMaterial()
        {
            if (lineMaterial != null) return;

            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            lineMaterial = new Material(shader);
            lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            lineMaterial.SetInt("_ZWrite", 0);
            lineMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        }

        private void OnRenderObject()
        {
            if (!showVisualization) return;

            FurnitureConnectivityManager fcm = FurnitureConnectivityManager.Instance;
            if (fcm == null || fcm.AllGroups == null) return;

            if (lineMaterial == null) CreateLineMaterial();
            lineMaterial.SetPass(0);

            float cellSize = 1.5f;
            if (ClockworkGrid.GridManager.Instance != null)
                cellSize = ClockworkGrid.GridManager.Instance.CellSize;

            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);

            foreach (FurnitureGroup group in fcm.AllGroups)
            {
                if (group.Members.Count <= 0) continue;

                Color groupColor = group.DebugColor;

                // Draw filled tiles under group
                if (showGroupFill)
                {
                    DrawGroupFill(group, groupColor, cellSize);
                }

                // Draw bold perimeter outline
                DrawPerimeterOutline(group, groupColor, cellSize);
            }

            // Draw seating positions
            if (showSeatingPositions)
            {
                DrawSeatingPositions(fcm);
            }

            GL.PopMatrix();
        }

        /// <summary>
        /// Draw a subtle tinted quad under each cell in the group.
        /// </summary>
        private void DrawGroupFill(FurnitureGroup group, Color color, float cellSize)
        {
            Color fillColor = color;
            fillColor.a = fillAlpha;

            float y = outlineHeight - 0.01f;
            float half = cellSize / 2f;

            ClockworkGrid.GridManager gm = ClockworkGrid.GridManager.Instance;
            if (gm == null) return;

            GL.Begin(GL.QUADS);
            GL.Color(fillColor);

            foreach (var cell in group.OccupiedCells)
            {
                Vector3 center = gm.GridToWorldPosition(cell.x, cell.y);

                GL.Vertex3(center.x - half, y, center.z - half);
                GL.Vertex3(center.x + half, y, center.z - half);
                GL.Vertex3(center.x + half, y, center.z + half);
                GL.Vertex3(center.x - half, y, center.z + half);
            }

            GL.End();
        }

        /// <summary>
        /// Draw a bold outline around the perimeter of a group by finding exposed cell edges
        /// and rendering them as thick quads.
        /// </summary>
        private void DrawPerimeterOutline(FurnitureGroup group, Color color, float cellSize)
        {
            Color outlineColor = color;
            outlineColor.a = outlineAlpha;

            float y = outlineHeight;
            float half = cellSize / 2f;
            float pad = outlinePadding;
            float thick = outlineThickness;

            ClockworkGrid.GridManager gm = ClockworkGrid.GridManager.Instance;
            if (gm == null) return;

            var cells = group.OccupiedCells;

            GL.Begin(GL.QUADS);
            GL.Color(outlineColor);

            foreach (var cell in cells)
            {
                Vector3 center = gm.GridToWorldPosition(cell.x, cell.y);

                // North edge exposed (no cell above)
                if (!ContainsCell(cells, cell.x, cell.y + 1))
                {
                    float z = center.z + half + pad;
                    GL.Vertex3(center.x - half - pad, y, z);
                    GL.Vertex3(center.x + half + pad, y, z);
                    GL.Vertex3(center.x + half + pad, y, z + thick);
                    GL.Vertex3(center.x - half - pad, y, z + thick);
                }

                // South edge exposed
                if (!ContainsCell(cells, cell.x, cell.y - 1))
                {
                    float z = center.z - half - pad;
                    GL.Vertex3(center.x - half - pad, y, z - thick);
                    GL.Vertex3(center.x + half + pad, y, z - thick);
                    GL.Vertex3(center.x + half + pad, y, z);
                    GL.Vertex3(center.x - half - pad, y, z);
                }

                // East edge exposed
                if (!ContainsCell(cells, cell.x + 1, cell.y))
                {
                    float x = center.x + half + pad;
                    GL.Vertex3(x, y, center.z - half - pad);
                    GL.Vertex3(x + thick, y, center.z - half - pad);
                    GL.Vertex3(x + thick, y, center.z + half + pad);
                    GL.Vertex3(x, y, center.z + half + pad);
                }

                // West edge exposed
                if (!ContainsCell(cells, cell.x - 1, cell.y))
                {
                    float x = center.x - half - pad;
                    GL.Vertex3(x - thick, y, center.z - half - pad);
                    GL.Vertex3(x, y, center.z - half - pad);
                    GL.Vertex3(x, y, center.z + half + pad);
                    GL.Vertex3(x - thick, y, center.z + half + pad);
                }
            }

            GL.End();
        }

        /// <summary>
        /// Helper to check if a cell coordinate exists in the collection.
        /// </summary>
        private bool ContainsCell(IReadOnlyCollection<(int x, int y)> cells, int x, int y)
        {
            // IReadOnlyCollection doesn't have Contains for tuples efficiently,
            // so we cast back to HashSet if possible
            if (cells is HashSet<(int x, int y)> hashSet)
                return hashSet.Contains((x, y));

            // Fallback linear scan (shouldn't happen)
            foreach (var c in cells)
            {
                if (c.x == x && c.y == y) return true;
            }
            return false;
        }

        /// <summary>
        /// Draw markers at available and occupied seating positions.
        /// </summary>
        private void DrawSeatingPositions(FurnitureConnectivityManager fcm)
        {
            TableSeatingManager tsm = TableSeatingManager.Instance;
            if (tsm == null) return;

            foreach (FurnitureGroup group in fcm.AllGroups)
            {
                if (group.GroupType != FurnitureType.Table) continue;

                List<Vector3> allPositions = group.GetAllPerimeterPositions();
                List<ChairObject> attachedChairs = tsm.GetAttachedChairsForGroup(group);

                foreach (Vector3 pos in allPositions)
                {
                    bool hasSeat = false;
                    bool isOccupied = false;

                    foreach (ChairObject chair in attachedChairs)
                    {
                        if (chair != null && Vector3.Distance(chair.transform.position, pos) < 0.5f)
                        {
                            hasSeat = true;
                            isOccupied = chair.IsOccupied;
                            break;
                        }
                    }

                    Color markerColor;
                    if (hasSeat)
                        markerColor = isOccupied ? occupiedSeatColor : availableSeatColor;
                    else
                        markerColor = new Color(1f, 1f, 1f, 0.25f);

                    DrawDiamond(pos + Vector3.up * 0.15f, seatMarkerSize, markerColor);
                }
            }
        }

        private void DrawDiamond(Vector3 center, float size, Color color)
        {
            GL.Begin(GL.LINE_STRIP);
            GL.Color(color);
            GL.Vertex3(center.x, center.y + size, center.z);
            GL.Vertex3(center.x + size, center.y, center.z);
            GL.Vertex3(center.x, center.y - size, center.z);
            GL.Vertex3(center.x - size, center.y, center.z);
            GL.Vertex3(center.x, center.y + size, center.z);
            GL.End();
        }
    }
}
