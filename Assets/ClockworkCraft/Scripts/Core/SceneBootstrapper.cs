#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using ClockworkGrid;
using LittleCafe;

namespace ClockworkCraft
{
    /// <summary>
    /// Ensures all required singletons/managers exist in the scene.
    /// Extracted from MapGeneratorV2.EnsureManagers().
    /// </summary>
    public static class SceneBootstrapper
    {
        public static void EnsureAll(
            CurrencyDatabase currencyDB,
            EnvironmentDatabase environmentDB,
            UnitDatabase unitDB,
            WorkerDatabase workerDB,
            BuildingDatabase buildingDB,
            PlacementCostsDatabase economyConfig,
            GameObject buildingBubblePrefab)
        {
            if (FogManager.Instance == null)
                new GameObject("FogManager").AddComponent<FogManager>();
            if (NodeManager.Instance == null)
                new GameObject("NodeManager").AddComponent<NodeManager>();
            if (ResourceManager.Instance == null)
            {
                var rm = new GameObject("ResourceManager").AddComponent<ResourceManager>();
                rm.currencyDatabase = currencyDB;
            }
            else if (ResourceManager.Instance.currencyDatabase == null && currencyDB != null)
            {
                // Assign database to existing ResourceManager if it doesn't have one
                ResourceManager.Instance.currencyDatabase = currencyDB;
            }

            // Ensure furniture/entity systems exist (CafeSceneSetupV2 defers when MapGeneratorV2 is present)
            if (GridEntityManager.Instance == null)
            {
                if (Object.FindFirstObjectByType<GridEntityManager>() == null)
                    new GameObject("GridEntityManager").AddComponent<GridEntityManager>();
            }
            if (Object.FindFirstObjectByType<FurnitureConnectivityManager>() == null)
                new GameObject("FurnitureConnectivityManager").AddComponent<FurnitureConnectivityManager>();
            if (Object.FindFirstObjectByType<FurnitureRemovalHandler>() == null)
                new GameObject("FurnitureRemovalHandler").AddComponent<FurnitureRemovalHandler>();

            // Ensure economy
            if (ResourceTokenManager.Instance == null)
                new GameObject("ResourceTokenManager").AddComponent<ResourceTokenManager>();

            // Auto-discover PlacementCostsDatabase if not assigned in Inspector
            if (economyConfig == null)
            {
#if UNITY_EDITOR
                var guids = UnityEditor.AssetDatabase.FindAssets("t:PlacementCostsDatabase");
                if (guids.Length > 0)
                {
                    string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    economyConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<PlacementCostsDatabase>(assetPath);
                    Debug.Log($"[SceneBootstrapper] Auto-discovered PlacementCostsDatabase at {assetPath}");
                }
#endif
            }

            // Ensure economy manager (centralized placement cost balancing)
            if (EconomyManager.Instance == null)
            {
                var em = new GameObject("EconomyManager").AddComponent<EconomyManager>();
                if (economyConfig != null)
                    em.balanceConfig = economyConfig;
            }
            else if (EconomyManager.Instance.balanceConfig == null && economyConfig != null)
            {
                EconomyManager.Instance.balanceConfig = economyConfig;
            }

            // Ensure resource display UI
            if (Object.FindFirstObjectByType<ResourceDisplayUI>() == null)
                new GameObject("ResourceDisplayUI").AddComponent<ResourceDisplayUI>();

            // Ensure loot particle FX
            if (Object.FindFirstObjectByType<ResourceLootFX>() == null)
                new GameObject("ResourceLootFX").AddComponent<ResourceLootFX>();
            if (Object.FindObjectOfType<IconFlyFX>(true) == null)
                new GameObject("IconFlyFX").AddComponent<IconFlyFX>();

            // Ensure placement cost display (floating cost above drop location during drag)
            if (PlacementCostDisplay.Instance == null)
                new GameObject("PlacementCostDisplay").AddComponent<PlacementCostDisplay>();

            // Ensure gameplay recorder (analytics for balancing)
            if (GameplayRecorder.Instance == null)
                new GameObject("GameplayRecorder").AddComponent<GameplayRecorder>();

            // Ensure building production manager
            if (Object.FindFirstObjectByType<BuildingProductionManager>() == null)
            {
                var bpm = new GameObject("BuildingProductionManager").AddComponent<BuildingProductionManager>();
                bpm.workerDatabase = workerDB;
                bpm.SetBubblePrefabs(buildingBubblePrefab, buildingBubblePrefab);
            }
            else
            {
                var bpm = Object.FindFirstObjectByType<BuildingProductionManager>();
                if (bpm.workerDatabase == null && workerDB != null)
                    bpm.workerDatabase = workerDB;
                if (buildingBubblePrefab != null)
                    bpm.SetBubblePrefabs(buildingBubblePrefab, buildingBubblePrefab);
            }

            // Ensure hold-to-fill handler (input handler for HoldToFill buildings like Kitchen)
            if (HoldToFillHandler.Instance == null)
                new GameObject("HoldToFillHandler").AddComponent<HoldToFillHandler>();

            // Ensure corruption manager
            if (CorruptionManager.Instance == null)
                new GameObject("CorruptionManager").AddComponent<CorruptionManager>();

            // Ensure worker card fly FX
            if (Object.FindFirstObjectByType<WorkerCardFlyFX>() == null)
                new GameObject("WorkerCardFlyFX").AddComponent<WorkerCardFlyFX>();

            // Ensure GameSFXManager (unified sound effects)
            if (GameSFXManager.Instance == null && Object.FindFirstObjectByType<GameSFXManager>() == null)
                new GameObject("GameSFXManager").AddComponent<GameSFXManager>();

            // Interaction registry — owned by CafeSceneSetupV2 as a scene object.
            // Only populate if it exists but has no entries (e.g., databases weren't assigned on the component).
            if (InteractionRegistry.Instance != null && InteractionRegistry.Instance.AllEntries.Count == 0)
            {
                InteractionRegistry.Instance.PopulateFromDatabases(environmentDB, unitDB);
                Debug.Log("[SceneBootstrapper] Populated InteractionRegistry entries from databases (scene component had none).");
            }
            else if (InteractionRegistry.Instance == null)
            {
                Debug.LogWarning("[SceneBootstrapper] No InteractionRegistry found! Add one to the scene via CafeSceneSetupV2 or Tools > ClockworkCraft > Setup Interaction Registry.");
            }

            // Ensure UI infrastructure
            if (Object.FindFirstObjectByType<DragDropHandler>() == null)
                new GameObject("DragDropHandler").AddComponent<DragDropHandler>();
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem");
                esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }
    }
}
