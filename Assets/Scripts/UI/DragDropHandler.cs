#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System.Collections.Generic;

namespace ClockworkGrid
{
    /// <summary>
    /// Singleton that manages drag state with arcing line and grid cell highlighting.
    /// Handles validation, visual feedback, and final placement from dock to grid.
    /// </summary>
    public partial class DragDropHandler : MonoBehaviour
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
        private GridShape currentShape;
        private int currentRotation = 0;

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
        private const int MaxFootprintCells = 16; // supports up to 4x4
        private GameObject[] cellHighlights = new GameObject[MaxFootprintCells];
        private MeshRenderer[] cellHighlightRenderers = new MeshRenderer[MaxFootprintCells];

        // Colors
        private Color validColor = new Color(1f, 1f, 1f, 0.6f);
        private Color invalidColor = new Color(1f, 0.3f, 0.3f, 0.6f);
        private Color feedBuildingColor = new Color(0.3f, 1f, 0.5f, 0.75f); // Bright green glow for valid input building (hovered)
        private Color feedBuildingDimColor = new Color(0.1f, 0.8f, 0.3f, 0.4f); // Dimmer green for valid input buildings (not hovered)

        // Feed-building state (dropping a card onto a production building)
        private bool isHoveringInputBuilding = false;
        private int feedTargetGridX, feedTargetGridY;
        private List<(int gridX, int gridY)> allValidFeedBuildings = new List<(int, int)>(); // All valid drop locations
        private int feedBuildingHighlightStartIndex = 1; // Cell highlights start from index 1 (index 0 is for hovered)

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

            // ── Rotation input (only for multi-cell shapes during drag) ────
            if (isDragging && currentShape != null && currentShape.Count > 1)
            {
                bool rotated = false;

                // Right-click = rotate CW one step
                if (Input.GetMouseButtonDown(1))
                {
                    currentRotation = (currentRotation + 1) % 4;
                    rotated = true;
                }

                // Scroll wheel: up = CW, down = CCW
                float scroll = Input.mouseScrollDelta.y;
                if (scroll > 0f)
                {
                    currentRotation = (currentRotation + 1) % 4;
                    rotated = true;
                }
                else if (scroll < 0f)
                {
                    currentRotation = ((currentRotation - 1) % 4 + 4) % 4;
                    rotated = true;
                }

                // Refresh highlights immediately if rotation changed
                if (rotated)
                    UpdateCellHighlights(targetGridX, targetGridY);
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
            currentRotation = 0;      // Reset rotation for every new drag

            // SFX: card picked up from dock
            if (GameSFXManager.Instance != null)
                GameSFXManager.Instance.PlayDragStart();

            // Show need bubbles on buildings that want this card
            var bpm = LittleCafe.BuildingProductionManager.Instance;
            var cardType = GetCardInputType();
            Debug.Log($"[DragDrop] Drag start — BPM={bpm != null}, cardInputType={cardType}");
            bpm?.ShowNeedBubbles(cardType);

            // Get GridShape: prefer GridObject on the prefab, fall back to UnitStats.shape,
            // final fallback to a 1×1 rectangle.
            currentShape = null;
            GridObject gridObj = currentUnitPrefab.GetComponent<GridObject>();
            if (gridObj != null && gridObj.Shape != null && !gridObj.Shape.IsEmpty)
            {
                currentShape = gridObj.Shape;
            }
            else
            {
                UnitStats stats = currentDraggingIcon?.UnitStats;
                if (stats != null && stats.shape != null && !stats.shape.IsEmpty)
                    currentShape = stats.shape;
            }
            if (currentShape == null || currentShape.IsEmpty)
                currentShape = GridShape.Rectangle(1, 1);

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
                Debug.Log($"[DragDrop] Captured preDragZoomDistance={preDragZoomDistance}");
                preDragAutoRotate = cam.IsAutoRotating;
                cam.SetAutoRotate(false);
                // Zoom in slightly from player's current level — bypass minDistance clamp
                if (LittleCafe.GridCamera.Instance != null)
                    LittleCafe.GridCamera.Instance.ZoomToUnclamped(preDragZoomDistance * dragZoomRatio);
                else
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
                if (cardInputType != LittleCafe.ProductionInputType.None)
                {
                    // Get all valid feed-buildings and highlight them
                    GetAllValidFeedBuildings(cardInputType, allValidFeedBuildings);

                    if (GridManager.Instance != null &&
                        GridManager.Instance.WorldToGridPosition(worldPos, out int hoveredX, out int hoveredY))
                    {
                        // Check if hovered position is a valid feed-building
                        if (LittleCafe.BuildingProductionManager.Instance != null &&
                            LittleCafe.BuildingProductionManager.Instance.IsInputBuildingAt(hoveredX, hoveredY, cardInputType))
                        {
                            isHoveringInputBuilding = true;
                            feedTargetGridX = hoveredX;
                            feedTargetGridY = hoveredY;
                            isValidPlacement = true; // Allow drop here
                            targetGridX = hoveredX;
                            targetGridY = hoveredY;

                            // Show all valid feed-buildings with the hovered one bright, others dimmer
                            UpdateAllFeedBuildingHighlights(hoveredX, hoveredY);

                            // Camera + arc to hovered building
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
                        else if (allValidFeedBuildings.Count > 0)
                        {
                            // Not hovering a valid building, but valid buildings exist — show them all dimmer
                            UpdateAllFeedBuildingHighlights(-1, -1);

                            // Hide placement cost display
                            if (LittleCafe.PlacementCostDisplay.Instance != null)
                                LittleCafe.PlacementCostDisplay.Instance.SetEntriesVisible(false);
                        }
                    }
                    else if (allValidFeedBuildings.Count > 0)
                    {
                        // Raycast failed but valid buildings exist — show them all
                        UpdateAllFeedBuildingHighlights(-1, -1);
                        if (LittleCafe.PlacementCostDisplay.Instance != null)
                            LittleCafe.PlacementCostDisplay.Instance.SetEntriesVisible(false);
                    }
                }

                // Normal placement validation
                bool valid = ValidatePlacement(worldPos, out targetGridX, out targetGridY);
                isValidPlacement = valid;

                // Footprint center for camera tracking and arc endpoint
                Vector3 footprintCenter = GridManager.Instance.GetOffsetFootprintCenter(targetGridX, targetGridY, currentShape, currentRotation);
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

            // Re-validate at drop time — resources may have changed since last UpdateDrag frame
            if (!isHoveringInputBuilding)
            {
                Vector3 dropWorldPos = GridManager.Instance.GridToWorldPosition(targetGridX, targetGridY);
                isValidPlacement = ValidatePlacement(dropWorldPos, out targetGridX, out targetGridY);
            }

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
                    feedCam.ZoomTo(preDragZoomDistance);
                }

                CleanupDragVisuals();
                isDragging = false;
                isHoveringInputBuilding = false;
                return;
            }

            // Spend resources and place the unit on the grid
            ExecutePlacement();
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

    }
}

