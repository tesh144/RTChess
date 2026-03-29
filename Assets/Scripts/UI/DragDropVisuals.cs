#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System.Collections.Generic;

namespace ClockworkGrid
{
    /// <summary>
    /// DragDropHandler partial — visual feedback, validation, and utility methods.
    /// Cell highlights, arc line, feed-building highlights, placement validation,
    /// raycasting, and cleanup.
    /// </summary>
    public partial class DragDropHandler
    {
        // ---------------------------------------------------------------
        // Cell Highlights
        // ---------------------------------------------------------------

        private void UpdateCellHighlights(int anchorX, int anchorY)
        {
            if (GridManager.Instance == null) return;

            float cellSize = GridManager.Instance.CellSize;
            float highlightY = GetTileSurfaceY() + 0.02f;
            int idx = 0;

            var offsets = currentShape != null
                ? currentShape.GetOffsets(currentRotation)
                : new List<Vector2Int> { Vector2Int.zero };

            foreach (var offset in offsets)
            {
                if (idx >= MaxFootprintCells) break;

                int cx = anchorX + offset.x;
                int cy = anchorY + offset.y;

                Vector3 pos = GridManager.Instance.GridToWorldPosition(cx, cy);
                pos.y = highlightY;

                cellHighlights[idx].transform.position = pos;
                cellHighlights[idx].transform.localScale = new Vector3(cellSize * 0.95f, cellSize * 0.95f, 1f);
                cellHighlights[idx].SetActive(true);

                bool cellOk = GridManager.Instance.IsCellEmpty(cx, cy) && GridManager.Instance.IsTileRevealed(cx, cy);
                cellHighlightRenderers[idx].material.color = cellOk ? validColor : invalidColor;

                idx++;
            }

            for (int i = idx; i < MaxFootprintCells; i++)
                cellHighlights[i].SetActive(false);
        }

        // ---------------------------------------------------------------
        // Arc Line
        // ---------------------------------------------------------------

        private void UpdateArcLine(Vector3 targetPos)
        {
            if (arcLine == null || currentDraggingIcon == null) return;

            Camera cam = GetActiveCamera();
            if (cam == null) return;

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

            Ray iconRay = cam.ScreenPointToRay(iconScreenPos);
            float surfaceY = GetTileSurfaceY();
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, surfaceY, 0f));
            Vector3 startPos;
            if (groundPlane.Raycast(iconRay, out float dist))
                startPos = iconRay.GetPoint(dist);
            else
                startPos = iconRay.GetPoint(10f);

            Vector3 endPos = targetPos;

            for (int i = 0; i < arcSegments; i++)
            {
                float t = i / (float)(arcSegments - 1);
                Vector3 point = CalculateArcPoint(startPos, endPos, t);
                arcLine.SetPosition(i, point);
            }
        }

        private Vector3 CalculateArcPoint(Vector3 start, Vector3 end, float t)
        {
            Vector3 linearPoint = Vector3.Lerp(start, end, t);
            float height = arcHeight * Mathf.Sin(t * Mathf.PI);
            linearPoint.y += height;
            return linearPoint;
        }

        // ---------------------------------------------------------------
        // Post-placement helpers
        // ---------------------------------------------------------------

        private static System.Collections.IEnumerator ColorizeAfterFrame(int x, int y)
        {
            yield return null;
            GameObject occupant = GridManager.Instance?.GetCellOccupant(x, y);
            if (occupant == null) yield break;
            var desat = occupant.GetComponentInChildren<ClockworkCraft.EnvironmentDesaturation>();
            if (desat != null && !desat.HasColorized)
                desat.Colorize();
        }

        // ---------------------------------------------------------------
        // Drag Visual Cleanup
        // ---------------------------------------------------------------

        private void CleanupDragVisuals()
        {
            if (arcLine != null)
                arcLine.enabled = false;

            for (int i = 0; i < MaxFootprintCells; i++)
            {
                if (cellHighlights[i] != null)
                    cellHighlights[i].SetActive(false);
            }

            allValidFeedBuildings.Clear();

            if (LittleCafe.PlacementCostDisplay.Instance != null)
                LittleCafe.PlacementCostDisplay.Instance.Hide();

            LittleCafe.BuildingProductionManager.Instance?.HideAllNeedBubbles();
        }

        // ---------------------------------------------------------------
        // Placement Validation
        // ---------------------------------------------------------------

        private bool ValidatePlacement(Vector3 worldPos, out int gridX, out int gridY)
        {
            gridX = 0;
            gridY = 0;

            if (!IsCellValid(worldPos, out gridX, out gridY))
                return false;

            if (currentDraggingIcon != null && currentDraggingIcon.UnitStats != null)
            {
                string itemName = currentDraggingIcon.UnitStats.unitName;
                if (EconomyManager.Instance != null &&
                    EconomyManager.Instance.HasConfiguredCost(itemName))
                {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    if (!LittleCafe.DevCheatMenu.FreeCosts &&
                        !EconomyManager.Instance.CanAfford(itemName))
                        return false;
#else
                    if (!EconomyManager.Instance.CanAfford(itemName))
                        return false;
#endif
                }
                else
                {
                    int cost = currentDraggingIcon.UnitStats.resourceCost;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    if (!LittleCafe.DevCheatMenu.FreeCosts &&
                        cost > 0 && (ResourceTokenManager.Instance == null || !ResourceTokenManager.Instance.HasEnoughTokens(cost)))
                        return false;
#else
                    if (cost > 0 && (ResourceTokenManager.Instance == null || !ResourceTokenManager.Instance.HasEnoughTokens(cost)))
                        return false;
#endif
                }
            }

            return true;
        }

        private bool IsCellValid(Vector3 worldPos, out int gridX, out int gridY)
        {
            gridX = 0;
            gridY = 0;

            if (GridManager.Instance == null) return false;

            if (!GridManager.Instance.WorldToGridPosition(worldPos, out gridX, out gridY))
                return false;

            if (!GridManager.Instance.AreOffsetCellsAvailable(gridX, gridY, currentShape, currentRotation))
                return false;

            return true;
        }

        // ---------------------------------------------------------------
        // Utility
        // ---------------------------------------------------------------

        private float GetTileSurfaceY()
        {
            return 0f;
        }

        private Camera GetActiveCamera()
        {
            Camera cam = Camera.main;
            if (cam != null) return cam;
            cam = FindFirstObjectByType<Camera>();
            return cam;
        }

        private bool RaycastToGroundPlane(Vector2 screenPos, out Vector3 worldPos)
        {
            worldPos = Vector3.zero;

            Camera cam = GetActiveCamera();
            if (cam == null) return false;

            Ray ray = cam.ScreenPointToRay(screenPos);
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, GetTileSurfaceY(), 0f));

            if (groundPlane.Raycast(ray, out float distance))
            {
                worldPos = ray.GetPoint(distance);
                return true;
            }

            return false;
        }

        private LittleCafe.ProductionInputType GetCardInputType()
        {
            if (currentDraggingIcon?.UnitStats == null) return LittleCafe.ProductionInputType.None;
            var stats = currentDraggingIcon.UnitStats;

            if (stats.isActive && stats.isAllied)
                return LittleCafe.ProductionInputType.Worker;

            return LittleCafe.ProductionInputType.Any;
        }

        // ---------------------------------------------------------------
        // Feed-Building Highlights
        // ---------------------------------------------------------------

        private void GetAllValidFeedBuildings(LittleCafe.ProductionInputType requiredInput, List<(int, int)> outPositions)
        {
            outPositions.Clear();
            if (LittleCafe.BuildingProductionManager.Instance == null) return;
            if (GridManager.Instance == null) return;

            var manager = LittleCafe.BuildingProductionManager.Instance;
            var gridManager = GridManager.Instance;

            int gridWidth = gridManager.Width;
            int gridHeight = gridManager.Height;

            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    if (manager.IsInputBuildingAt(x, y, requiredInput))
                        outPositions.Add((x, y));
                }
            }
        }

        private void UpdateAllFeedBuildingHighlights(int hoveredX, int hoveredY)
        {
            if (GridManager.Instance == null) return;
            if (allValidFeedBuildings.Count == 0)
            {
                for (int i = 0; i < MaxFootprintCells; i++)
                    cellHighlights[i].SetActive(false);
                return;
            }

            float cellSize = GridManager.Instance.CellSize;
            float highlightY = GetTileSurfaceY() + 0.02f;

            int highlightIndex = 0;
            foreach (var (gridX, gridY) in allValidFeedBuildings)
            {
                if (highlightIndex >= MaxFootprintCells)
                    break;

                Vector3 pos = GridManager.Instance.GridToWorldPosition(gridX, gridY);
                pos.y = highlightY;

                cellHighlights[highlightIndex].transform.position = pos;
                cellHighlights[highlightIndex].transform.localScale = new Vector3(cellSize * 0.95f, cellSize * 0.95f, 1f);
                cellHighlights[highlightIndex].SetActive(true);

                if (gridX == hoveredX && gridY == hoveredY)
                    cellHighlightRenderers[highlightIndex].material.color = feedBuildingColor;
                else
                    cellHighlightRenderers[highlightIndex].material.color = feedBuildingDimColor;

                highlightIndex++;
            }

            for (int i = highlightIndex; i < MaxFootprintCells; i++)
                cellHighlights[i].SetActive(false);
        }
    }
}
