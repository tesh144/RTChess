using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Singleton that manages drag state with arcing line and grid cell highlighting.
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

        // Arc line rendering
        private LineRenderer arcLine;
        private int arcSegments = 30; // Number of points in the arc
        private float arcHeight = 4f; // Height of the arc
        private Material arcMaterial;
        private float animationSpeed = 2f; // Speed of dot animation

        // Canvas info (cached on drag start for correct UI→world conversion)
        private Canvas iconCanvas;
        private Camera canvasCamera;

        // Grid cell highlight
        private GameObject cellHighlight;
        private MeshRenderer cellHighlightRenderer;

        // Colors
        private Color validColor = new Color(1f, 1f, 1f, 0.6f);
        private Color invalidColor = new Color(1f, 0.3f, 0.3f, 0.6f);

        // Camera tracking during drag
        private Vector3 preDragCameraTarget;
        private float preDragZoomDistance;
        private float dragZoomAmount = 5f; // How much to zoom in during drag
        private float dragCameraEaseSpeed = 3f; // How fast camera eases toward target

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Create line renderer for arcing trajectory
            arcLine = gameObject.AddComponent<LineRenderer>();
            arcLine.startWidth = 0.25f;
            arcLine.endWidth = 0.15f;

            // Create animated dotted line material
            arcMaterial = new Material(Shader.Find("Sprites/Default"));
            arcMaterial.color = new Color(1f, 1f, 1f, 0.8f);
            arcLine.material = arcMaterial;

            arcLine.startColor = new Color(1f, 1f, 1f, 0.8f);
            arcLine.endColor = new Color(1f, 1f, 1f, 0.8f);
            arcLine.positionCount = arcSegments;
            arcLine.enabled = false;
            arcLine.useWorldSpace = true;

            // Enable texture tiling for animated effect
            arcLine.textureMode = LineTextureMode.Tile;

            // Create cell highlight quad
            CreateCellHighlight();
        }

        private void Update()
        {
            // Animate arc line texture for moving dots effect
            if (isDragging && arcMaterial != null)
            {
                float offset = Time.time * animationSpeed;
                arcMaterial.mainTextureOffset = new Vector2(offset, 0);
            }
        }

        private void CreateCellHighlight()
        {
            cellHighlight = GameObject.CreatePrimitive(PrimitiveType.Quad);
            cellHighlight.name = "CellHighlight";
            cellHighlight.transform.rotation = Quaternion.Euler(90, 0, 0); // Face up
            cellHighlight.transform.localScale = Vector3.one; // Will be resized to cell size

            // Remove collider
            Collider collider = cellHighlight.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            // Setup material
            cellHighlightRenderer = cellHighlight.GetComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = validColor;
            cellHighlightRenderer.material = mat;

            cellHighlight.SetActive(false);
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
            isValidPlacement = false; // Reset - must hover a valid cell to place

            // Cache canvas info for correct UI-to-screen conversion
            iconCanvas = icon.GetComponentInParent<Canvas>();
            canvasCamera = (iconCanvas != null && iconCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? iconCanvas.worldCamera : null;

            // Save camera state before drag for tracking and zoom
            if (CameraController.Instance != null)
            {
                preDragCameraTarget = CameraController.Instance.CurrentTarget;
                preDragZoomDistance = CameraController.Instance.CurrentDistance;
                CameraController.Instance.ZoomTo(preDragZoomDistance - dragZoomAmount);
            }

            arcLine.enabled = true;
            cellHighlight.SetActive(true);

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
                // Validate placement and get grid coordinates
                bool valid = ValidatePlacement(worldPos, out targetGridX, out targetGridY);
                isValidPlacement = valid;

                // Snap to grid cell center
                Vector3 cellCenter = GridManager.Instance.GridToWorldPosition(targetGridX, targetGridY);

                // Update cell highlight
                UpdateCellHighlight(cellCenter, valid);

                // Ease camera halfway toward hovered cell
                if (CameraController.Instance != null)
                {
                    Vector3 halfTarget = Vector3.Lerp(preDragCameraTarget, cellCenter, 0.5f);
                    Vector3 current = CameraController.Instance.CurrentTarget;
                    Vector3 eased = Vector3.Lerp(current, halfTarget, Time.deltaTime * dragCameraEaseSpeed);
                    CameraController.Instance.SetTarget(eased);
                }

                // Update arc line
                UpdateArcLine(cellCenter);
            }
            else
            {
                // Mouse is off-screen or raycast failed - not a valid placement
                isValidPlacement = false;
            }
        }

        /// <summary>
        /// End drag - attempt placement or snap back
        /// </summary>
        public void EndDrag()
        {
            if (!isDragging) return;

            if (!isValidPlacement)
            {
                // Invalid placement - snap back to dock and restore camera
                currentDraggingIcon.SnapBackToOriginalPosition();
                if (CameraController.Instance != null)
                {
                    CameraController.Instance.SetTarget(preDragCameraTarget);
                    CameraController.Instance.ZoomTo(preDragZoomDistance);
                }
                CleanupDragVisuals();
                isDragging = false;
                return;
            }

            // Valid placement - spawn unit (FREE, no token cost!)
            Vector3 worldPos = GridManager.Instance.GridToWorldPosition(targetGridX, targetGridY);
            GameObject unitObj = Instantiate(currentUnitPrefab, worldPos, currentUnitPrefab.transform.rotation);
            unitObj.SetActive(true);

            // Initialize unit with UnitStats (Iteration 6)
            Unit unit = unitObj.GetComponent<Unit>();
            if (unit != null && currentDraggingIcon != null && currentDraggingIcon.UnitStats != null)
            {
                unit.Initialize(Team.Player, targetGridX, targetGridY, currentDraggingIcon.UnitStats);
            }

            // Add placement cooldown component
            PlacementCooldown cooldown = unitObj.AddComponent<PlacementCooldown>();
            cooldown.StartCooldown(2); // 2 intervals

            // Register with grid
            GridManager.Instance.PlaceUnit(targetGridX, targetGridY, unitObj, CellState.PlayerUnit);

            // Notify WaveManager that player placed a unit (for lose condition tracking)
            if (WaveManager.Instance != null)
            {
                WaveManager.Instance.OnPlayerUnitPlaced();
            }

            // Trigger wave start on first player unit placement
            if (WaveManager.Instance != null && !WaveManager.Instance.HasWaveStarted)
            {
                WaveManager.Instance.StartWave();
                Debug.Log("[DragDropHandler] First player unit placed - wave started!");
            }

            // Remove from dock
            DockBarManager.Instance.RemoveUnitIcon(currentDraggingIcon);

            // Quick-pan camera to the placed tile and restore zoom
            if (CameraController.Instance != null)
            {
                CameraController.Instance.SnapToCell(targetGridX, targetGridY);
                CameraController.Instance.ZoomTo(preDragZoomDistance);
            }

            // Cleanup
            CleanupDragVisuals();
            isDragging = false;
        }

        /// <summary>
        /// Cancel current drag
        /// </summary>
        public void CancelDrag()
        {
            if (!isDragging) return;

            currentDraggingIcon.SnapBackToOriginalPosition();
            if (CameraController.Instance != null)
            {
                CameraController.Instance.SetTarget(preDragCameraTarget);
                CameraController.Instance.ZoomTo(preDragZoomDistance);
            }
            CleanupDragVisuals();
            isDragging = false;
        }

        private void UpdateCellHighlight(Vector3 cellCenter, bool isValid)
        {
            if (cellHighlight == null || GridManager.Instance == null) return;

            // Position at cell center, slightly above ground
            Vector3 highlightPos = cellCenter;
            highlightPos.y = 0.02f; // Just above ground to avoid z-fighting

            cellHighlight.transform.position = highlightPos;

            // Scale to match cell size
            float cellSize = GridManager.Instance.CellSize;
            cellHighlight.transform.localScale = new Vector3(cellSize * 0.95f, cellSize * 0.95f, 1f);

            // Update color
            Color targetColor = isValid ? validColor : invalidColor;
            cellHighlightRenderer.material.color = targetColor;
        }

        private void UpdateArcLine(Vector3 targetPos)
        {
            if (arcLine == null || currentDraggingIcon == null) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            // Get icon's screen position correctly based on canvas render mode
            // For Overlay canvas: transform.position IS screen pixels
            // For Camera/World canvas: need WorldToScreenPoint with canvas camera
            Vector2 iconScreenPos;
            if (iconCanvas != null && iconCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                Vector3 pos = currentDraggingIcon.transform.position;
                iconScreenPos = new Vector2(pos.x, pos.y);
            }
            else
            {
                iconScreenPos = RectTransformUtility.WorldToScreenPoint(
                    canvasCamera ?? cam, currentDraggingIcon.transform.position);
            }

            // Project icon screen position onto the ground plane to get world-space start
            Ray iconRay = cam.ScreenPointToRay(iconScreenPos);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            Vector3 startPos;
            if (groundPlane.Raycast(iconRay, out float dist))
                startPos = iconRay.GetPoint(dist);
            else
                startPos = iconRay.GetPoint(10f); // fallback

            // End point: target cell center
            Vector3 endPos = targetPos;

            // Calculate arc points
            for (int i = 0; i < arcSegments; i++)
            {
                float t = i / (float)(arcSegments - 1);
                Vector3 point = CalculateArcPoint(startPos, endPos, t);
                arcLine.SetPosition(i, point);
            }
        }

        private Vector3 CalculateArcPoint(Vector3 start, Vector3 end, float t)
        {
            // Linear interpolation between start and end
            Vector3 linearPoint = Vector3.Lerp(start, end, t);

            // Add vertical arc (parabola shape)
            float height = arcHeight * Mathf.Sin(t * Mathf.PI);
            linearPoint.y += height;

            return linearPoint;
        }

        private void CleanupDragVisuals()
        {
            if (arcLine != null)
            {
                arcLine.enabled = false;
            }

            if (cellHighlight != null)
            {
                cellHighlight.SetActive(false);
            }
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
