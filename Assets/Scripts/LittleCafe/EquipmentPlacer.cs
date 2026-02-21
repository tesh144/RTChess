using UnityEngine;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// Handles equipment placement via click-to-place and click-to-remove.
    /// Selected equipment type is set by the EquipmentPalette UI.
    /// </summary>
    public class EquipmentPlacer : MonoBehaviour
    {
        public static EquipmentPlacer Instance { get; private set; }

        [Header("Visual Feedback")]
        [SerializeField] private Color validPlacementColor = new Color(0.3f, 1f, 0.3f, 0.5f);
        [SerializeField] private Color invalidPlacementColor = new Color(1f, 0.3f, 0.3f, 0.5f);

        private EquipmentType? selectedType;
        private GameObject ghostPreview;
        private GameObject cellHighlight;
        private MeshRenderer cellHighlightRenderer;
        private bool isValidPlacement;
        private int targetGridX, targetGridY;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            CreateCellHighlight();
        }

        private void Update()
        {
            if (GameModeManager.Instance == null) return;
            if (GameModeManager.Instance.CurrentMode != GameMode.Build) return;

            if (selectedType.HasValue)
            {
                UpdateGhostPreview();

                if (Input.GetMouseButtonDown(0) && isValidPlacement)
                {
                    PlaceAtTarget();
                }
            }
            else
            {
                // No equipment selected — click to remove
                if (Input.GetMouseButtonDown(0))
                {
                    HandleClickToRemove();
                }
            }

            // Right-click or Escape to deselect
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                DeselectEquipment();
            }
        }

        public void SelectEquipment(EquipmentType type)
        {
            selectedType = type;
            CreateGhostPreview(type);
        }

        public void DeselectEquipment()
        {
            selectedType = null;
            DestroyGhost();
            cellHighlight.SetActive(false);
        }

        private void PlaceAtTarget()
        {
            if (!selectedType.HasValue) return;

            bool placed = LayoutManager.Instance.PlaceEquipment(targetGridX, targetGridY, selectedType.Value);
            if (placed)
            {
                // Keep the same type selected for rapid placement
                // Ghost stays active for next click
            }
        }

        private void HandleClickToRemove()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                CafeEquipment equip = hit.collider.GetComponent<CafeEquipment>();
                if (equip != null)
                {
                    LayoutManager.Instance.RemoveEquipment(equip.GridX, equip.GridY);
                }
            }
        }

        // --- Ghost Preview ---

        private void CreateGhostPreview(EquipmentType type)
        {
            DestroyGhost();

            GameObject prefab = LayoutManager.Instance.GetPrefab(type);
            if (prefab == null) return;

            ghostPreview = Instantiate(prefab);
            ghostPreview.name = "GhostPreview";

            // Make semi-transparent
            MeshRenderer rend = ghostPreview.GetComponentInChildren<MeshRenderer>();
            if (rend != null)
            {
                Material mat = new Material(rend.material);
                Color c = mat.color;
                c.a = 0.5f;
                mat.color = c;
                mat.SetFloat("_Mode", 3); // Transparent
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
                rend.material = mat;
            }

            // Remove collider so it doesn't block raycasts
            Collider col = ghostPreview.GetComponent<Collider>();
            if (col != null) Destroy(col);

            ghostPreview.SetActive(false);
            cellHighlight.SetActive(true);
        }

        private void UpdateGhostPreview()
        {
            if (ghostPreview == null) return;

            if (RaycastToGround(out Vector3 worldPos))
            {
                if (GridManager.Instance.WorldToGridPosition(worldPos, out int gx, out int gy))
                {
                    targetGridX = gx;
                    targetGridY = gy;
                    isValidPlacement = GridManager.Instance.IsCellEmpty(gx, gy);

                    Vector3 cellCenter = GridManager.Instance.GridToWorldPosition(gx, gy);
                    float cellSize = GridManager.Instance.CellSize;
                    float height = cellSize * 0.6f;
                    ghostPreview.transform.position = new Vector3(cellCenter.x, height * 0.5f, cellCenter.z);
                    ghostPreview.SetActive(true);

                    UpdateCellHighlight(cellCenter, isValidPlacement);
                    return;
                }
            }

            isValidPlacement = false;
            ghostPreview.SetActive(false);
            cellHighlight.SetActive(false);
        }

        private void DestroyGhost()
        {
            if (ghostPreview != null)
            {
                Destroy(ghostPreview);
                ghostPreview = null;
            }
            cellHighlight.SetActive(false);
        }

        // --- Cell Highlight ---

        private void CreateCellHighlight()
        {
            cellHighlight = GameObject.CreatePrimitive(PrimitiveType.Quad);
            cellHighlight.name = "CafeCellHighlight";
            cellHighlight.transform.rotation = Quaternion.Euler(90, 0, 0);

            Collider col = cellHighlight.GetComponent<Collider>();
            if (col != null) Destroy(col);

            cellHighlightRenderer = cellHighlight.GetComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = validPlacementColor;
            cellHighlightRenderer.material = mat;

            cellHighlight.SetActive(false);
        }

        private void UpdateCellHighlight(Vector3 cellCenter, bool isValid)
        {
            float cellSize = GridManager.Instance.CellSize;
            cellHighlight.transform.position = new Vector3(cellCenter.x, 0.02f, cellCenter.z);
            cellHighlight.transform.localScale = new Vector3(cellSize * 0.95f, cellSize * 0.95f, 1f);
            cellHighlightRenderer.material.color = isValid ? validPlacementColor : invalidPlacementColor;
            cellHighlight.SetActive(true);
        }

        // --- Raycasting ---

        private bool RaycastToGround(out Vector3 worldPos)
        {
            worldPos = Vector3.zero;
            Camera cam = Camera.main;
            if (cam == null) return false;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(ray, out float distance))
            {
                worldPos = ray.GetPoint(distance);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Called by CafeEquipmentIcon on drag release to place at current target.
        /// </summary>
        public void TryPlaceAtCurrentTarget()
        {
            if (isValidPlacement && selectedType.HasValue)
            {
                PlaceAtTarget();
            }
            // Deselect after drag-release placement
            DeselectEquipment();
        }

        public bool HasSelection => selectedType.HasValue;
    }
}
