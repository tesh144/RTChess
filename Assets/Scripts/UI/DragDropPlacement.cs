#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// DragDropHandler partial — placement execution.
    /// Handles cost spending, unit instantiation, component attachment,
    /// fog reveal, animation triggers, and post-placement registration.
    /// </summary>
    public partial class DragDropHandler
    {
        /// <summary>
        /// Spend resources and place the dragged unit on the grid.
        /// Called from EndDrag after validation passes and feed-building path is ruled out.
        /// </summary>
        private void ExecutePlacement()
        {
            // Check and spend placement cost
            string placementItemName = currentDraggingIcon?.UnitStats?.unitName;
            bool usesEconomyManager = !string.IsNullOrEmpty(placementItemName) &&
                EconomyManager.Instance != null &&
                EconomyManager.Instance.HasConfiguredCost(placementItemName);

            if (usesEconomyManager)
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                bool economySpendOk = LittleCafe.DevCheatMenu.FreeCosts ||
                    EconomyManager.Instance.SpendForPlacement(placementItemName);
#else
                bool economySpendOk = EconomyManager.Instance.SpendForPlacement(placementItemName);
#endif
                if (!economySpendOk)
                {
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
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                if (LittleCafe.DevCheatMenu.FreeCosts) placementCost = 0;
#endif
                if (placementCost > 0)
                {
                    if (ResourceTokenManager.Instance == null || !ResourceTokenManager.Instance.SpendTokens(placementCost))
                    {
                        if (GameSFXManager.Instance != null)
                            GameSFXManager.Instance.PlayPlacementError();
                        currentDraggingIcon.SnapBackToOriginalPosition();
                        CleanupDragVisuals();
                        isDragging = false;
                        return;
                    }
                }
            }

            // Place unit on grid
            Vector3 worldPos = GridManager.Instance.GetOffsetFootprintCenter(targetGridX, targetGridY, currentShape, currentRotation);
            worldPos.y = GetTileSurfaceY() + 0.05f;

            Quaternion spawnRotation = currentUnitPrefab.transform.rotation;
            if (currentDraggingIcon?.UnitStats != null && !currentDraggingIcon.UnitStats.isActive)
            {
                bool isMultiCell = currentShape != null && currentShape.Count > 1;
                float yDeg = isMultiCell
                    ? currentRotation * 90f
                    : 90f * Random.Range(0, 4);
                spawnRotation = Quaternion.Euler(0f, yDeg, 0f);
            }
            GameObject unitObj = Instantiate(currentUnitPrefab, worldPos, spawnRotation);
            unitObj.SetActive(true);

            if (currentDraggingIcon?.UnitStats != null && !string.IsNullOrEmpty(currentDraggingIcon.UnitStats.unitName))
                unitObj.name = currentDraggingIcon.UnitStats.unitName;

            // Initialize based on component type
            bool placedWithFurniture = false;
            bool isHoldToFill = (currentDraggingIcon?.UnitStats?.productionInputType == LittleCafe.ProductionInputType.HoldToFill);
            LittleCafe.FurnitureObject furniture = isHoldToFill ? null : unitObj.GetComponent<LittleCafe.FurnitureObject>();

            if (furniture != null)
            {
                if (currentDraggingIcon?.UnitStats != null && currentDraggingIcon.UnitStats.furnitureTypeOverride >= 0)
                    furniture.SetType((LittleCafe.FurnitureType)currentDraggingIcon.UnitStats.furnitureTypeOverride);

                if (currentDraggingIcon?.UnitStats != null)
                    furniture.FogRevealRadius = currentDraggingIcon.UnitStats.revealRadius;

                furniture.OnPlaced(targetGridX, targetGridY, currentShape, currentRotation);
                placedWithFurniture = true;

                AttachGridEntityFromStats(unitObj);
            }

            if (!placedWithFurniture)
            {
                GridManager.Instance.PlaceWithOffsets(targetGridX, targetGridY, currentShape, currentRotation,
                    unitObj, CellState.PlayerUnit);

                if (FogManager.Instance != null)
                {
                    int revealRadius = currentDraggingIcon?.UnitStats != null
                        ? currentDraggingIcon.UnitStats.revealRadius : 1;
                    var fogOffsets = currentShape.GetOffsets(currentRotation);
                    foreach (var offset in fogOffsets)
                    {
                        int cellX = targetGridX + offset.x;
                        int cellY = targetGridY + offset.y;
                        for (int dx = -revealRadius; dx <= revealRadius; dx++)
                        for (int dy = -revealRadius; dy <= revealRadius; dy++)
                            FogManager.Instance.RevealCell(cellX + dx, cellY + dy);
                    }
                }

                AttachGridEntityFromStats(unitObj);
            }

            // Meal buff source
            if (currentDraggingIcon?.UnitStats != null && currentDraggingIcon.UnitStats.isMealSource)
            {
                if (unitObj.GetComponent<LittleCafe.MealBuffSource>() == null)
                {
                    var mbs = unitObj.AddComponent<LittleCafe.MealBuffSource>();
                    mbs.icon = currentDraggingIcon.UnitStats.iconSprite;
                }
                if (unitObj.GetComponent<LittleCafe.FeastVisualDegradation>() == null)
                    unitObj.AddComponent<LittleCafe.FeastVisualDegradation>();
            }

            // Placement animation
            Transform animHolder = unitObj.transform.Find("AnimatorHolder");
            if (animHolder != null)
            {
                Animator anim = animHolder.GetComponent<Animator>();
                if (anim != null) anim.SetTrigger("appear");
            }

            // Torch colorization
            if (currentDraggingIcon?.UnitStats != null && currentDraggingIcon.UnitStats.revealRadius >= 2)
            {
                var colorizeOffsets = currentShape.GetOffsets(currentRotation);
                foreach (var offset in colorizeOffsets)
                {
                    int cx = targetGridX + offset.x;
                    int cy = targetGridY + offset.y;
                    for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        GameObject occupant = GridManager.Instance.GetCellOccupant(cx + dx, cy + dy);
                        if (occupant == null) continue;
                        var desat = occupant.GetComponentInChildren<ClockworkCraft.EnvironmentDesaturation>();
                        if (desat != null && !desat.HasColorized) desat.Colorize();
                    }
                }

                if (FogManager.Instance != null)
                {
                    int torchX = targetGridX;
                    int torchY = targetGridY;
                    FogManager.Instance.OnCellRevealed += (x, y) =>
                    {
                        if (Mathf.Abs(x - torchX) > 1 || Mathf.Abs(y - torchY) > 1) return;
                        if (Instance != null) Instance.StartCoroutine(ColorizeAfterFrame(x, y));
                    };
                }
            }

            // Connectivity
            if (placedWithFurniture && LittleCafe.FurnitureConnectivityManager.Instance != null)
                LittleCafe.FurnitureConnectivityManager.Instance.UpdateConnectivity();

            // Production registration
            if (currentDraggingIcon?.UnitStats != null &&
                currentDraggingIcon.UnitStats.productionOutputType != LittleCafe.ProductionOutputType.None &&
                LittleCafe.BuildingProductionManager.Instance != null)
            {
                LittleCafe.BuildingProductionManager.Instance.RegisterBuilding(unitObj, currentDraggingIcon.UnitStats);
            }

            // Cost escalation
            if (usesEconomyManager)
                EconomyManager.Instance.RecordPlacement(placementItemName);

            // SFX
            if (GameSFXManager.Instance != null)
                GameSFXManager.Instance.PlayPlacement();
            else if (SFXManager.Instance != null)
                SFXManager.Instance.PlayPlayerPlacement();

            // Remove from dock
            DockBarManager.Instance.RemoveCard(currentDraggingIcon);

            // Restore camera
            var placeCam = CameraSystemLocator.Current;
            if (placeCam != null)
            {
                placeCam.SetAutoRotate(preDragAutoRotate);
                placeCam.FocusOnPosition(worldPos);
                placeCam.ZoomTo(preDragZoomDistance);
            }

            CleanupDragVisuals();
            isDragging = false;
        }

        /// <summary>Attach health/actor/loot from current UnitStats to the placed object.</summary>
        private void AttachGridEntityFromStats(GameObject unitObj)
        {
            if (LittleCafe.GridEntityManager.Instance == null) return;
            if (currentDraggingIcon?.UnitStats == null) return;

            var stats = currentDraggingIcon.UnitStats;
            int hp = stats.maxHP > 0 ? stats.maxHP : 0;
            int attackPower = stats.attackDamage > 0 ? stats.attackDamage : 0;

            // Resource nodes (trees, rocks) must be non-allied so workers target them,
            // matching AttachFromEnvironmentData behavior. Only truly player units stay allied.
            bool allied = stats.isAllied && stats.lootResourceType == ClockworkCraft.ResourceType.None;

            LittleCafe.GridEntityManager.Instance.AttachComponents(unitObj, hp, attackPower, stats.isActive,
                stats.lootResourceType, stats.lootHpCost, stats.lootYield, stats.behaviorType,
                registryName: stats.unitName, allied: allied, killerAdvances: stats.killerAdvances);
        }
    }
}
