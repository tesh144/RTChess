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
        public bool IsDragging => isDragging;
        private bool isValidPlacement = false;
        private int targetGridX, targetGridY;
        private Vector2Int currentGridSize = new Vector2Int(1, 1);

        // Arc line rendering
        private LineRenderer arcLine;
        private int arcSegments = 30; // Number of points in the arc
        private float arcHeight = 4f; // Height of the arc
        private Material arcMaterial;
        private float animationSpeed = 2f; // Speed of dot animation

        // Canvas info (cached on drag start for correct UI→world conversion)
        private Canvas iconCanvas;
        private Camera canvasCamera;

        // Grid cell highlights (pooled for multi-cell footprints)
        private const int MaxFootprintCells = 9; // supports up to 3x3
        private GameObject[] cellHighlights = new GameObject[MaxFootprintCells];
        private MeshRenderer[] cellHighlightRenderers = new MeshRenderer[MaxFootprintCells];

        // Colors
        private Color validColor = new Color(1f, 1f, 1f, 0.6f);
        private Color invalidColor = new Color(1f, 0.3f, 0.3f, 0.6f);

        // Camera tracking during drag
        private Vector3 preDragCameraTarget;
        private float preDragZoomDistance;
        private bool preDragAutoRotate;
        private float dragZoomRatio = 0.5f; // Zoom to 50% of current distance during drag
        private float dragCameraEaseSpeed = 5f; // How fast camera eases toward target

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Debug.Log($"[DragDropHandler] Awake — Instance set on '{gameObject.name}'");

            // Create line renderer for arcing trajectory
            arcLine = gameObject.AddComponent<LineRenderer>();
            arcLine.startWidth = 0.25f;
            arcLine.endWidth = 0.15f;

            // Create animated dotted line material — renders on top of all geometry
            arcMaterial = new Material(Shader.Find("Sprites/Default"));
            arcMaterial.color = new Color(1f, 1f, 1f, 0.8f);
            arcMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            arcLine.material = arcMaterial;
            arcLine.sortingOrder = 100;

            arcLine.startColor = new Color(1f, 1f, 1f, 0.8f);
            arcLine.endColor = new Color(1f, 1f, 1f, 0.8f);
            arcLine.positionCount = arcSegments;
            arcLine.alignment = LineAlignment.TransformZ; // Prevents 180° twist at arc apex
            arcLine.enabled = false;
            arcLine.useWorldSpace = true;

            // Enable texture tiling for animated effect
            arcLine.textureMode = LineTextureMode.Tile;

            // Create cell highlight quads (pooled for multi-cell footprints)
            CreateCellHighlights();
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

        private void CreateCellHighlights()
        {
            Material sharedMat = new Material(Shader.Find("Sprites/Default"));
            for (int i = 0; i < MaxFootprintCells; i++)
            {
                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = $"CellHighlight_{i}";
                quad.transform.rotation = Quaternion.Euler(90, 0, 0);
                quad.transform.localScale = Vector3.one;

                Collider col = quad.GetComponent<Collider>();
                if (col != null) Destroy(col);

                cellHighlightRenderers[i] = quad.GetComponent<MeshRenderer>();
                cellHighlightRenderers[i].material = new Material(sharedMat);

                quad.SetActive(false);
                cellHighlights[i] = quad;
            }
        }

        /// <summary>
        /// Start dragging a unit from the dock
        /// </summary>
        public bool StartDrag(UnitIcon icon, GameObject unitPrefab)
        {
            Debug.Log($"[DragDropHandler] StartDrag called — isDragging={isDragging}, unitPrefab={unitPrefab?.name ?? "NULL"}");
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

            // Get grid size from GridObject component on the prefab (not UnitStats)
            GridObject gridObj = currentUnitPrefab.GetComponent<GridObject>();
            currentGridSize = (gridObj != null) ? gridObj.GridSize : new Vector2Int(1, 1);

            // Cache canvas info for correct UI-to-screen conversion
            iconCanvas = icon.GetComponentInParent<Canvas>();
            canvasCamera = (iconCanvas != null && iconCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? iconCanvas.worldCamera : null;

            // Save camera state before drag for tracking and zoom
            var cam = CameraSystemLocator.Current;
            if (cam != null)
            {
                preDragCameraTarget = cam.CurrentTarget;
                preDragZoomDistance = cam.CurrentDistance;
                preDragAutoRotate = cam.IsAutoRotating;
                cam.SetAutoRotate(false);
                cam.ZoomTo(preDragZoomDistance * dragZoomRatio);
            }

            arcLine.enabled = true;
            // Highlights are activated per-cell in UpdateCellHighlights

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

                // Footprint center for camera tracking and arc endpoint
                Vector3 footprintCenter = GridManager.Instance.GetFootprintCenter(targetGridX, targetGridY, currentGridSize);
                footprintCenter.y = GetTileSurfaceY();

                // Update per-cell highlights
                UpdateCellHighlights(targetGridX, targetGridY);

                // Ease camera toward hovered cell
                var dragCam = CameraSystemLocator.Current;
                if (dragCam != null)
                {
                    Vector3 blendTarget = Vector3.Lerp(preDragCameraTarget, footprintCenter, 0.8f);
                    Vector3 current = dragCam.CurrentTarget;
                    Vector3 eased = Vector3.Lerp(current, blendTarget, Time.deltaTime * dragCameraEaseSpeed);
                    dragCam.SetTarget(eased);
                }

                // Update arc line (target = footprint center)
                UpdateArcLine(footprintCenter);
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
                var snapCam = CameraSystemLocator.Current;
                if (snapCam != null)
                {
                    snapCam.SetTarget(preDragCameraTarget);
                    snapCam.ZoomTo(preDragZoomDistance);
                    snapCam.SetAutoRotate(preDragAutoRotate);
                }
                CleanupDragVisuals();
                isDragging = false;
                return;
            }

            // Check and spend placement cost
            int placementCost = currentDraggingIcon.UnitStats != null ? currentDraggingIcon.UnitStats.resourceCost : 0;
            if (placementCost > 0)
            {
                if (ResourceTokenManager.Instance == null || !ResourceTokenManager.Instance.SpendTokens(placementCost))
                {
                    // Can't afford - snap back to dock
                    currentDraggingIcon.SnapBackToOriginalPosition();
                    CleanupDragVisuals();
                    isDragging = false;
                    return;
                }
            }

            // Place unit on grid — center on footprint
            Vector3 worldPos = GridManager.Instance.GetFootprintCenter(targetGridX, targetGridY, currentGridSize);
            worldPos.y = GetTileSurfaceY() + 0.05f; // Offset to prevent shadow clipping into grid
            GameObject unitObj = Instantiate(currentUnitPrefab, worldPos, currentUnitPrefab.transform.rotation);
            unitObj.SetActive(true);

            // Placement animation handled by Animator component (Unit_Appear animation)
            // Objects with PlaceableObject controller will automatically play spawn animation

            // Initialize based on component type
            bool placedWithFurniture = false;

            // NEW: Initialize FurnitureObject (LittleCafe furniture system)
            LittleCafe.FurnitureObject furniture = unitObj.GetComponent<LittleCafe.FurnitureObject>();
            if (furniture != null)
            {
                // Apply correct FurnitureType from database (variant prefabs may have wrong serialized type)
                if (currentDraggingIcon?.UnitStats != null && currentDraggingIcon.UnitStats.furnitureTypeOverride >= 0)
                {
                    furniture.SetType((LittleCafe.FurnitureType)currentDraggingIcon.UnitStats.furnitureTypeOverride);
                }

                furniture.OnPlaced(targetGridX, targetGridY, currentGridSize);
                placedWithFurniture = true;
            }

            // LEGACY: Initialize Unit with UnitStats (RTChess combat system)
            Unit unit = unitObj.GetComponent<Unit>();
            if (unit != null && currentDraggingIcon != null && currentDraggingIcon.UnitStats != null)
            {
                unit.Initialize(Team.Player, targetGridX, targetGridY, currentDraggingIcon.UnitStats);
            }

            // LEGACY: Initialize CafeEquipment (old system, kept for backwards compatibility)
            if (!placedWithFurniture)
            {
                LittleCafe.CafeEquipment equip = unitObj.GetComponent<LittleCafe.CafeEquipment>();
                if (equip == null)
                    equip = unitObj.AddComponent<LittleCafe.CafeEquipment>();
                equip.Initialize(targetGridX, targetGridY, currentGridSize);

                // Register with grid (multi-cell: all footprint cells point to same object)
                GridManager.Instance.PlaceMultiCell(targetGridX, targetGridY, currentGridSize, unitObj, CellState.PlayerUnit);
            }
            // Note: FurnitureObject.OnPlaced() already handles grid registration

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

            // Update furniture connectivity after placement
            if (placedWithFurniture && LittleCafe.FurnitureConnectivityManager.Instance != null)
            {
                LittleCafe.FurnitureConnectivityManager.Instance.UpdateConnectivity();
            }

            // Play placement SFX
            if (SFXManager.Instance != null)
                SFXManager.Instance.PlayPlayerPlacement();

            // Remove from dock
            DockBarManager.Instance.RemoveUnitIcon(currentDraggingIcon);

            // Restore camera settings and re-center on the newly placed object
            var placeCam = CameraSystemLocator.Current;
            if (placeCam != null)
            {
                placeCam.SetAutoRotate(preDragAutoRotate);

                // Update orbit pivot to the placed object's position
                placeCam.FocusOnPosition(worldPos);

                // Zoom out to adaptive default (grows with revealed tiles)
                if (LittleCafe.GridCamera.Instance != null)
                    LittleCafe.GridCamera.Instance.ZoomToDefault();
                else
                    placeCam.ZoomTo(preDragZoomDistance); // Fallback for RTChess scene
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
            var cancelCam = CameraSystemLocator.Current;
            if (cancelCam != null)
            {
                cancelCam.SetTarget(preDragCameraTarget);
                cancelCam.ZoomTo(preDragZoomDistance);
                cancelCam.SetAutoRotate(preDragAutoRotate);
            }
            CleanupDragVisuals();
            isDragging = false;
        }

        private void UpdateCellHighlights(int anchorX, int anchorY)
        {
            if (GridManager.Instance == null) return;

            float cellSize = GridManager.Instance.CellSize;
            float highlightY = GetTileSurfaceY() + 0.02f;
            int idx = 0;

            for (int dx = 0; dx < currentGridSize.x; dx++)
            {
                for (int dy = 0; dy < currentGridSize.y; dy++)
                {
                    if (idx >= MaxFootprintCells) break;

                    int cx = anchorX + dx;
                    int cy = anchorY + dy;

                    Vector3 pos = GridManager.Instance.GridToWorldPosition(cx, cy);
                    pos.y = highlightY;

                    cellHighlights[idx].transform.position = pos;
                    cellHighlights[idx].transform.localScale = new Vector3(cellSize * 0.95f, cellSize * 0.95f, 1f);
                    cellHighlights[idx].SetActive(true);

                    // Per-cell color: green if available, red if blocked
                    bool cellOk = GridManager.Instance.IsCellEmpty(cx, cy) && GridManager.Instance.IsTileRevealed(cx, cy);
                    cellHighlightRenderers[idx].material.color = cellOk ? validColor : invalidColor;

                    idx++;
                }
            }

            // Hide unused highlight quads
            for (int i = idx; i < MaxFootprintCells; i++)
                cellHighlights[i].SetActive(false);
        }

        private void UpdateArcLine(Vector3 targetPos)
        {
            if (arcLine == null || currentDraggingIcon == null) return;

            Camera cam = GetActiveCamera();
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
            float surfaceY = GetTileSurfaceY();
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, surfaceY, 0f));
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
                arcLine.enabled = false;

            for (int i = 0; i < MaxFootprintCells; i++)
            {
                if (cellHighlights[i] != null)
                    cellHighlights[i].SetActive(false);
            }
        }

        private bool ValidatePlacement(Vector3 worldPos, out int gridX, out int gridY)
        {
            gridX = 0;
            gridY = 0;

            if (GridManager.Instance == null) return false;

            // Convert world position to anchor grid coordinates
            if (!GridManager.Instance.WorldToGridPosition(worldPos, out gridX, out gridY))
                return false;

            // Check ALL cells in the footprint are available (empty + revealed)
            if (!GridManager.Instance.AreAllCellsAvailable(gridX, gridY, currentGridSize))
                return false;

            // Check if player can afford placement cost
            if (currentDraggingIcon != null && currentDraggingIcon.UnitStats != null)
            {
                int cost = currentDraggingIcon.UnitStats.resourceCost;
                if (cost > 0 && (ResourceTokenManager.Instance == null || !ResourceTokenManager.Instance.HasEnoughTokens(cost)))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Returns the Y coordinate of the tile surface (top of tiles).
        /// GridManager places tile centers at Y = -cellSize/2 so the top surface is always Y=0.
        /// We use this constant rather than reading tile.transform.position because
        /// TileFog animates tiles upward from a lowered position.
        /// </summary>
        private float GetTileSurfaceY()
        {
            return 0f;
        }

        private Camera GetActiveCamera()
        {
            Camera cam = Camera.main;
            if (cam != null) return cam;
            // Fallback when camera isn't tagged "MainCamera" (e.g. GridCamera in cafe scene)
            cam = FindObjectOfType<Camera>();
            Debug.Log($"[DragDropHandler] Camera.main was null, fallback found: {cam?.name ?? "NULL"}");
            return cam;
        }

        private bool RaycastToGroundPlane(Vector2 screenPos, out Vector3 worldPos)
        {
            worldPos = Vector3.zero;

            Camera cam = GetActiveCamera();
            if (cam == null) return false;

            Ray ray = cam.ScreenPointToRay(screenPos);

            // Ground plane at tile surface Y=0 (constant, not read from tile transform
            // which may be mid-animation due to TileFog).
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, GetTileSurfaceY(), 0f));

            if (groundPlane.Raycast(ray, out float distance))
            {
                worldPos = ray.GetPoint(distance);
                return true;
            }

            return false;
        }
    }
}

