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
        private GameCardUI currentDraggingIcon;
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
        private Color feedBuildingColor = new Color(0.3f, 1f, 0.5f, 0.75f); // Bright green glow for valid input building

        // Feed-building state (dropping a card onto a production building)
        private bool isHoveringInputBuilding = false;
        private int feedTargetGridX, feedTargetGridY;

        // Camera tracking during drag
        private Vector3 preDragCameraTarget;
        private float preDragZoomDistance;
        private bool preDragAutoRotate;
        private float dragZoomRatio = 0.75f; // Zoom to 75% of current distance during drag (gentler)
        private float dragCameraEaseSpeed = 3f; // How fast camera eases toward target

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
        public bool StartDrag(GameCardUI icon, GameObject unitPrefab)
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

            // SFX: card picked up from dock
            if (GameSFXManager.Instance != null)
                GameSFXManager.Instance.PlayDragStart();

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

            // Pre-create placement cost display entries (hidden until hovering a valid cell)
            if (LittleCafe.PlacementCostDisplay.Instance != null && icon.UnitStats != null)
            {
                LittleCafe.PlacementCostDisplay.Instance.ResetOrbit();
                LittleCafe.PlacementCostDisplay.Instance.Show(icon.UnitStats);
                LittleCafe.PlacementCostDisplay.Instance.SetEntriesVisible(false);
            }

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
                // Check for feed-building interaction first (dropping a worker/fighter onto a production building)
                isHoveringInputBuilding = false;
                LittleCafe.ProductionInputType cardInputType = GetCardInputType();
                if (cardInputType != LittleCafe.ProductionInputType.None &&
                    GridManager.Instance != null &&
                    GridManager.Instance.WorldToGridPosition(worldPos, out int hoveredX, out int hoveredY))
                {
                    if (LittleCafe.BuildingProductionManager.Instance != null &&
                        LittleCafe.BuildingProductionManager.Instance.IsInputBuildingAt(hoveredX, hoveredY, cardInputType))
                    {
                        isHoveringInputBuilding = true;
                        feedTargetGridX = hoveredX;
                        feedTargetGridY = hoveredY;
                        isValidPlacement = true; // Allow drop here
                        targetGridX = hoveredX;
                        targetGridY = hoveredY;

                        // Show single bright green highlight on the building's cell
                        UpdateFeedBuildingHighlight(hoveredX, hoveredY);

                        // Camera + arc
                        Vector3 buildingCenter = GridManager.Instance.GridToWorldPosition(hoveredX, hoveredY);
                        buildingCenter.y = GetTileSurfaceY();

                        var feedCam = CameraSystemLocator.Current;
                        if (feedCam != null)
                        {
                            Vector3 blendTarget = Vector3.Lerp(preDragCameraTarget, buildingCenter, 0.5f);
                            Vector3 current = feedCam.CurrentTarget;
                            Vector3 eased = Vector3.Lerp(current, blendTarget, Time.deltaTime * dragCameraEaseSpeed);
                            feedCam.SetTarget(eased);
                        }

                        UpdateArcLine(buildingCenter);

                        // Hide placement cost display when hovering a feed building
                        if (LittleCafe.PlacementCostDisplay.Instance != null)
                            LittleCafe.PlacementCostDisplay.Instance.SetEntriesVisible(false);

                        return; // Skip normal placement validation
                    }
                }

                // Normal placement validation
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
                    Vector3 blendTarget = Vector3.Lerp(preDragCameraTarget, footprintCenter, 0.5f);
                    Vector3 current = dragCam.CurrentTarget;
                    Vector3 eased = Vector3.Lerp(current, blendTarget, Time.deltaTime * dragCameraEaseSpeed);
                    dragCam.SetTarget(eased);
                }

                // Update arc line (target = footprint center)
                UpdateArcLine(footprintCenter);

                // Show/hide placement cost display based on cell validity (empty + revealed)
                // Show even when player can't afford — they need to see what it costs
                if (LittleCafe.PlacementCostDisplay.Instance != null)
                {
                    bool cellValid = IsCellValid(worldPos, out _, out _);
                    LittleCafe.PlacementCostDisplay.Instance.SetWorldCenter(footprintCenter);
                    LittleCafe.PlacementCostDisplay.Instance.SetEntriesVisible(cellValid);
                }
            }
            else
            {
                // Mouse is off-screen or raycast failed - not a valid placement
                isValidPlacement = false;
                isHoveringInputBuilding = false;
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
                // SFX: drag cancelled / invalid drop
                if (GameSFXManager.Instance != null)
                    GameSFXManager.Instance.PlayDragCancel();

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
                isHoveringInputBuilding = false;
                return;
            }

            // ── Feed-building path: drop card onto a production building ──
            if (isHoveringInputBuilding)
            {
                LittleCafe.ProductionInputType cardInput = GetCardInputType();
                bool fed = LittleCafe.BuildingProductionManager.Instance != null &&
                           LittleCafe.BuildingProductionManager.Instance.FeedBuilding(feedTargetGridX, feedTargetGridY, cardInput);

                if (fed)
                {
                    // SFX: successful interaction
                    if (GameSFXManager.Instance != null)
                        GameSFXManager.Instance.PlayPlacement();

                    // Remove card from dock (consumed by the building)
                    DockBarManager.Instance.RemoveCard(currentDraggingIcon);
                    Debug.Log($"[DragDropHandler] Fed {cardInput} card into building at ({feedTargetGridX},{feedTargetGridY})");
                }
                else
                {
                    // Building didn't accept — snap back
                    if (GameSFXManager.Instance != null)
                        GameSFXManager.Instance.PlayDragCancel();
                    currentDraggingIcon.SnapBackToOriginalPosition();
                }

                // Restore camera
                var feedCam = CameraSystemLocator.Current;
                if (feedCam != null)
                {
                    feedCam.SetAutoRotate(preDragAutoRotate);
                    if (LittleCafe.GridCamera.Instance != null)
                        LittleCafe.GridCamera.Instance.ZoomToDefault();
                    else
                        feedCam.ZoomTo(preDragZoomDistance);
                }

                CleanupDragVisuals();
                isDragging = false;
                isHoveringInputBuilding = false;
                return;
            }

            // Check and spend placement cost
            // Priority 1: EconomyManager (multi-resource, escalating costs)
            // Priority 2: Legacy single-token cost (ResourceTokenManager)
            string placementItemName = currentDraggingIcon?.UnitStats?.unitName;
            bool usesEconomyManager = !string.IsNullOrEmpty(placementItemName) &&
                ClockworkGrid.EconomyManager.Instance != null &&
                ClockworkGrid.EconomyManager.Instance.HasConfiguredCost(placementItemName);

            if (usesEconomyManager)
            {
                if (!ClockworkGrid.EconomyManager.Instance.SpendForPlacement(placementItemName))
                {
                    // SFX: can't afford placement
                    if (GameSFXManager.Instance != null)
                        GameSFXManager.Instance.PlayPlacementError();

                    currentDraggingIcon.SnapBackToOriginalPosition();
                    CleanupDragVisuals();
                    isDragging = false;
                    return;
                }
            }
            else
            {
                int placementCost = currentDraggingIcon.UnitStats != null ? currentDraggingIcon.UnitStats.resourceCost : 0;
                if (placementCost > 0)
                {
                    if (ResourceTokenManager.Instance == null || !ResourceTokenManager.Instance.SpendTokens(placementCost))
                    {
                        // SFX: can't afford placement
                        if (GameSFXManager.Instance != null)
                            GameSFXManager.Instance.PlayPlacementError();

                        currentDraggingIcon.SnapBackToOriginalPosition();
                        CleanupDragVisuals();
                        isDragging = false;
                        return;
                    }
                }
            }

            // Place unit on grid — center on footprint
            Vector3 worldPos = GridManager.Instance.GetFootprintCenter(targetGridX, targetGridY, currentGridSize);
            worldPos.y = GetTileSurfaceY() + 0.05f; // Offset to prevent shadow clipping into grid
            // Randomize facing direction for non-active objects (buildings, furniture)
            // Active entities (workers, animals) get their facing from GridEntityActor.Initialize()
            Quaternion spawnRotation = currentUnitPrefab.transform.rotation;
            if (currentDraggingIcon?.UnitStats != null && !currentDraggingIcon.UnitStats.isActive)
            {
                float randomY = 90f * Random.Range(0, 4); // 0, 90, 180, or 270
                spawnRotation = Quaternion.Euler(0f, randomY, 0f);
            }
            GameObject unitObj = Instantiate(currentUnitPrefab, worldPos, spawnRotation);
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

                // Set fog reveal radius from database (e.g. Torch = 2)
                if (currentDraggingIcon?.UnitStats != null)
                {
                    furniture.FogRevealRadius = currentDraggingIcon.UnitStats.revealRadius;
                }

                furniture.OnPlaced(targetGridX, targetGridY, currentGridSize);
                placedWithFurniture = true;

                // Attach GridEntity components (health, actor, loot) based on database stats
                if (LittleCafe.GridEntityManager.Instance != null && currentDraggingIcon?.UnitStats != null)
                {
                    var stats = currentDraggingIcon.UnitStats;
                    int hp = stats.maxHP > 0 ? stats.maxHP : 0;
                    int attackPower = stats.attackDamage > 0 ? stats.attackDamage : 0;
                    bool isActive = stats.isActive;
                    LittleCafe.GridEntityManager.Instance.AttachComponents(unitObj, hp, attackPower, isActive,
                        stats.lootResourceType, stats.lootHpCost, stats.lootYield, stats.behaviorType,
                        registryName: stats.unitName, allied: stats.isAllied);
                }
            }

            // Non-furniture path (workers, units — no FurnitureObject component)
            if (!placedWithFurniture)
            {
                // Register with grid (multi-cell: all footprint cells point to same object)
                GridManager.Instance.PlaceMultiCell(targetGridX, targetGridY, currentGridSize, unitObj, CellState.PlayerUnit);

                // Reveal surrounding fog (same as FurnitureObject.OnPlaced)
                if (FogManager.Instance != null)
                {
                    int revealRadius = currentDraggingIcon?.UnitStats != null
                        ? currentDraggingIcon.UnitStats.revealRadius : 1;
                    for (int dx = -revealRadius; dx <= revealRadius + currentGridSize.x - 1; dx++)
                    for (int dy = -revealRadius; dy <= revealRadius + currentGridSize.y - 1; dy++)
                        FogManager.Instance.RevealCell(targetGridX + dx, targetGridY + dy);
                }

                // Attach GridEntity components (health, actor, loot) from UnitStats
                if (LittleCafe.GridEntityManager.Instance != null && currentDraggingIcon?.UnitStats != null)
                {
                    var stats = currentDraggingIcon.UnitStats;
                    int hp = stats.maxHP > 0 ? stats.maxHP : 0;
                    int attackPower = stats.attackDamage > 0 ? stats.attackDamage : 0;
                    bool isActive = stats.isActive;
                    LittleCafe.GridEntityManager.Instance.AttachComponents(unitObj, hp, attackPower, isActive,
                        stats.lootResourceType, stats.lootHpCost, stats.lootYield, stats.behaviorType,
                        registryName: stats.unitName, allied: stats.isAllied);
                }
            }

            // Attach MealBuffSource marker if flagged in database
            if (currentDraggingIcon?.UnitStats != null && currentDraggingIcon.UnitStats.isMealSource)
            {
                if (unitObj.GetComponent<LittleCafe.MealBuffSource>() == null)
                    unitObj.AddComponent<LittleCafe.MealBuffSource>();
            }

            // Update furniture connectivity after placement
            if (placedWithFurniture && LittleCafe.FurnitureConnectivityManager.Instance != null)
            {
                LittleCafe.FurnitureConnectivityManager.Instance.UpdateConnectivity();
            }

            // Register building with production manager if it has production data
            if (currentDraggingIcon?.UnitStats != null &&
                currentDraggingIcon.UnitStats.productionOutputType != LittleCafe.ProductionOutputType.None &&
                LittleCafe.BuildingProductionManager.Instance != null)
            {
                LittleCafe.BuildingProductionManager.Instance.RegisterBuilding(unitObj, currentDraggingIcon.UnitStats);
            }

            // Record placement for cost escalation
            if (usesEconomyManager)
                ClockworkGrid.EconomyManager.Instance.RecordPlacement(placementItemName);

            // Play placement SFX
            if (GameSFXManager.Instance != null)
                GameSFXManager.Instance.PlayPlacement();
            else if (SFXManager.Instance != null)
                SFXManager.Instance.PlayPlayerPlacement();

            // Remove from dock
            DockBarManager.Instance.RemoveCard(currentDraggingIcon);

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
            isHoveringInputBuilding = false;
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

            // Hide placement cost display
            if (LittleCafe.PlacementCostDisplay.Instance != null)
                LittleCafe.PlacementCostDisplay.Instance.Hide();
        }

        private bool ValidatePlacement(Vector3 worldPos, out int gridX, out int gridY)
        {
            gridX = 0;
            gridY = 0;

            if (!IsCellValid(worldPos, out gridX, out gridY))
                return false;

            // Check if player can afford placement cost
            // Priority 1: EconomyManager (multi-resource, escalating costs)
            // Priority 2: Legacy single-token cost (ResourceTokenManager)
            if (currentDraggingIcon != null && currentDraggingIcon.UnitStats != null)
            {
                string itemName = currentDraggingIcon.UnitStats.unitName;
                if (ClockworkGrid.EconomyManager.Instance != null &&
                    ClockworkGrid.EconomyManager.Instance.HasConfiguredCost(itemName))
                {
                    if (!ClockworkGrid.EconomyManager.Instance.CanAfford(itemName))
                        return false;
                }
                else
                {
                    int cost = currentDraggingIcon.UnitStats.resourceCost;
                    if (cost > 0 && (ResourceTokenManager.Instance == null || !ResourceTokenManager.Instance.HasEnoughTokens(cost)))
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Check if the hovered cell is a valid drop location (empty + revealed),
        /// WITHOUT checking affordability. Used to decide whether to show cost icons.
        /// </summary>
        private bool IsCellValid(Vector3 worldPos, out int gridX, out int gridY)
        {
            gridX = 0;
            gridY = 0;

            if (GridManager.Instance == null) return false;

            if (!GridManager.Instance.WorldToGridPosition(worldPos, out gridX, out gridY))
                return false;

            if (!GridManager.Instance.AreAllCellsAvailable(gridX, gridY, currentGridSize))
                return false;

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

        /// <summary>
        /// Determine what ProductionInputType this dragged card can provide to a building.
        /// Workers (isActive + isAllied) → Worker. Otherwise None.
        /// </summary>
        private LittleCafe.ProductionInputType GetCardInputType()
        {
            if (currentDraggingIcon?.UnitStats == null) return LittleCafe.ProductionInputType.None;
            var stats = currentDraggingIcon.UnitStats;

            // Workers are active, allied entities
            if (stats.isActive && stats.isAllied)
                return LittleCafe.ProductionInputType.Worker;

            // Future: fighters could map to ProductionInputType.Fighter
            return LittleCafe.ProductionInputType.None;
        }

        /// <summary>
        /// Show a single bright green highlight on a valid feed-building cell.
        /// Hides all other highlight quads.
        /// </summary>
        private void UpdateFeedBuildingHighlight(int gridX, int gridY)
        {
            if (GridManager.Instance == null) return;

            float cellSize = GridManager.Instance.CellSize;
            float highlightY = GetTileSurfaceY() + 0.02f;

            Vector3 pos = GridManager.Instance.GridToWorldPosition(gridX, gridY);
            pos.y = highlightY;

            cellHighlights[0].transform.position = pos;
            cellHighlights[0].transform.localScale = new Vector3(cellSize * 0.95f, cellSize * 0.95f, 1f);
            cellHighlights[0].SetActive(true);
            cellHighlightRenderers[0].material.color = feedBuildingColor;

            // Hide remaining quads
            for (int i = 1; i < MaxFootprintCells; i++)
                cellHighlights[i].SetActive(false);
        }
    }
}

