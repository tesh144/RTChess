using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Singleton that manages drag state and ghost preview for unit placement.
    /// Handles validation, visual feedback, and final placement from dock to grid.
    /// </summary>
    public class DragDropHandler : MonoBehaviour
    {
        // Singleton pattern
        public static DragDropHandler Instance { get; private set; }

        // Drag state
        private UnitIcon currentDraggingIcon;
        private GameObject currentUnitPrefab;
        private bool isDragging = false;
        private bool isValidPlacement = false;
        private int targetGridX, targetGridY;

        // Ghost preview
        private GameObject ghostPreview;
        private LineRenderer deploymentLine;

        // Colors
        private Color validColor = new Color(0.3f, 1f, 0.3f, 0.5f);
        private Color invalidColor = new Color(1f, 0.3f, 0.3f, 0.5f);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Create line renderer for deployment trajectory
            deploymentLine = gameObject.AddComponent<LineRenderer>();
            deploymentLine.startWidth = 0.05f;
            deploymentLine.endWidth = 0.05f;
            deploymentLine.material = new Material(Shader.Find("Sprites/Default"));
            deploymentLine.startColor = new Color(1f, 1f, 1f, 0.6f);
            deploymentLine.endColor = new Color(1f, 1f, 1f, 0.6f);
            deploymentLine.positionCount = 2;
            deploymentLine.enabled = false;
        }

        /// <summary>
        /// Start dragging a unit from the dock
        /// </summary>
        public bool StartDrag(UnitIcon icon, GameObject unitPrefab)
        {
            if (isDragging) return false;
            if (unitPrefab == null)
            {
                Debug.LogError("StartDrag called with null unitPrefab!");
                return false;
            }

            currentDraggingIcon = icon;
            currentUnitPrefab = unitPrefab;
            isDragging = true;

            CreateGhostPreview(unitPrefab);
            deploymentLine.enabled = true;

            return true;
        }

        /// <summary>
        /// Update drag state with current mouse position
        /// </summary>
        public void UpdateDrag(Vector2 screenPos)
        {
            if (!isDragging) return;

            // Raycast to ground plane
            if (RaycastToGroundPlane(screenPos, out Vector3 worldPos))
            {
                // Position ghost preview
                ghostPreview.transform.position = worldPos;

                // Validate placement
                bool valid = ValidatePlacement(worldPos, out targetGridX, out targetGridY);
                UpdateGhostColor(valid);
                isValidPlacement = valid;
            }

            // Update deployment line
            UpdateDeploymentLine(screenPos);
        }

        /// <summary>
        /// End drag - attempt placement or snap back
        /// </summary>
        public void EndDrag()
        {
            if (!isDragging) return;

            if (!isValidPlacement)
            {
                // Invalid placement - snap back to dock
                currentDraggingIcon.SnapBackToOriginalPosition();
                DestroyGhostPreview();
                isDragging = false;
                deploymentLine.enabled = false;
                return;
            }

            // Valid placement - spawn unit (FREE, no token cost!)
            Vector3 worldPos = GridManager.Instance.GridToWorldPosition(targetGridX, targetGridY);
            GameObject unitObj = Instantiate(currentUnitPrefab, worldPos, Quaternion.identity);
            unitObj.SetActive(true);

            // Initialize unit with UnitStats (Iteration 6)
            Unit unit = unitObj.GetComponent<Unit>();
            if (unit != null && currentDraggingIcon != null && currentDraggingIcon.UnitData != null)
            {
                unit.Initialize(Team.Player, targetGridX, targetGridY, currentDraggingIcon.UnitData.Stats);
            }

            // Add placement cooldown component
            PlacementCooldown cooldown = unitObj.AddComponent<PlacementCooldown>();
            cooldown.StartCooldown(2); // 2 intervals

            // Register with grid
            GridManager.Instance.PlaceUnit(targetGridX, targetGridY, unitObj, CellState.PlayerUnit);

            // Remove from dock
            DockBarManager.Instance.RemoveUnitIcon(currentDraggingIcon);

            // Cleanup
            DestroyGhostPreview();
            isDragging = false;
            deploymentLine.enabled = false;
        }

        /// <summary>
        /// Cancel current drag
        /// </summary>
        public void CancelDrag()
        {
            if (!isDragging) return;

            currentDraggingIcon.SnapBackToOriginalPosition();
            DestroyGhostPreview();
            isDragging = false;
            deploymentLine.enabled = false;
        }

        private void CreateGhostPreview(GameObject unitPrefab)
        {
            if (ghostPreview != null) DestroyGhostPreview();

            ghostPreview = Instantiate(unitPrefab);
            ghostPreview.SetActive(true);
            ghostPreview.name = "GhostPreview";

            // Disable all non-visual components
            Collider[] colliders = ghostPreview.GetComponentsInChildren<Collider>();
            foreach (Collider c in colliders) Destroy(c);

            MonoBehaviour[] scripts = ghostPreview.GetComponentsInChildren<MonoBehaviour>();
            foreach (MonoBehaviour s in scripts)
            {
                if (s != null) Destroy(s);
            }

            // Make semi-transparent
            Renderer[] renderers = ghostPreview.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;

                // Create new material instance
                Material mat = new Material(r.sharedMaterial);
                Color c = mat.color;
                c.a = 0.5f;
                mat.color = c;
                r.material = mat;
            }
        }

        private void DestroyGhostPreview()
        {
            if (ghostPreview != null)
            {
                Destroy(ghostPreview);
                ghostPreview = null;
            }
        }

        private void UpdateGhostColor(bool isValid)
        {
            if (ghostPreview == null) return;

            Color targetColor = isValid ? validColor : invalidColor;

            Renderer[] renderers = ghostPreview.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
            {
                if (r == null || r.material == null) continue;

                Color c = r.material.color;
                c.r = targetColor.r;
                c.g = targetColor.g;
                c.b = targetColor.b;
                c.a = targetColor.a;
                r.material.color = c;
            }
        }

        private void UpdateDeploymentLine(Vector2 screenPos)
        {
            if (currentDraggingIcon == null || ghostPreview == null) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            // Start point: dock icon position in world space
            Vector3 iconScreenPos = RectTransformUtility.WorldToScreenPoint(cam, currentDraggingIcon.transform.position);
            Ray iconRay = cam.ScreenPointToRay(iconScreenPos);
            Vector3 dockWorldPos = iconRay.GetPoint(10f);

            // End point: ghost preview position
            Vector3 cursorWorldPos = ghostPreview.transform.position;

            deploymentLine.SetPosition(0, dockWorldPos);
            deploymentLine.SetPosition(1, cursorWorldPos);
        }

        private bool ValidatePlacement(Vector3 worldPos, out int gridX, out int gridY)
        {
            gridX = 0;
            gridY = 0;

            if (GridManager.Instance == null) return false;

            // Convert world position to grid coordinates
            if (!GridManager.Instance.WorldToGridPosition(worldPos, out gridX, out gridY))
                return false;

            // Check if cell is revealed (Iteration 7: Fog of War)
            if (FogManager.Instance != null && !FogManager.Instance.IsCellRevealed(gridX, gridY))
                return false;

            // Check if cell is empty
            return GridManager.Instance.IsCellEmpty(gridX, gridY);
        }

        private bool RaycastToGroundPlane(Vector2 screenPos, out Vector3 worldPos)
        {
            worldPos = Vector3.zero;

            Camera cam = Camera.main;
            if (cam == null) return false;

            Ray ray = cam.ScreenPointToRay(screenPos);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

            if (groundPlane.Raycast(ray, out float distance))
            {
                worldPos = ray.GetPoint(distance);
                return true;
            }

            return false;
        }
    }
}
