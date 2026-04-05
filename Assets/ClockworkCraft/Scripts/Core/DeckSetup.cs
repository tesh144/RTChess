#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System.Collections.Generic;
using ClockworkGrid;
using LittleCafe;

namespace ClockworkCraft
{
    /// <summary>
    /// Populates the CardPool draw pool and initializes the DockBarManager.
    /// Extracted from MapGeneratorV2.SetupDeck().
    /// </summary>
    public static class DeckSetup
    {
        public static void Initialize(
            BuildingDatabase buildingDB,
            WorkerDatabase workerDB,
            EnvironmentDatabase environmentDB)
        {
            Debug.Log("[DeckSetup] Initialize() starting...");

            // ── CardPool ─────────────────────────────────────────────
            if (CardPool.Instance != null)
            {
                Object.DestroyImmediate(CardPool.Instance.gameObject);
            }
            new GameObject("CardPool").AddComponent<CardPool>();

            var deckStats = new List<UnitStats>();

            {
                // ── Normal deck (Buildings only — workers come from building production) ──
                bool hasBuildings = buildingDB != null && buildingDB.AllBuildings.Count > 0;

                if (!hasBuildings)
                {
                    Debug.LogWarning("[DeckSetup] No BuildingDatabase assigned — dock will be empty.");
                    return;
                }

                int buildingCount = 0;
                foreach (BuildingData data in buildingDB.AllBuildings)
                {
                    if (data.prefab == null) continue;
                    if (!data.active) continue; // Skip inactive entries

                    UnitStats stats       = ScriptableObject.CreateInstance<UnitStats>();
                    stats.active          = data.active;
                    stats.unitType        = UnitType.Soldier;
                    stats.unitName        = data.GetCleanName();
                    stats.rarity          = Rarity.Common;
                    stats.tier            = data.tier;
                    stats.drawWeight      = data.drawWeight;
                    stats.iconSprite      = data.icon;
                    stats.unitColor       = Color.white;
                    stats.unitPrefab      = data.prefab;
                    stats.resourceCost    = data.placementCost;
                    stats.gridSize        = data.gridSize;
                    stats.modelScale      = data.visualScale;
                    stats.enemyPrefab     = null;
                    stats.isActive        = data.isActive;
                    stats.maxHP           = data.hp;
                    stats.attackDamage    = data.attackPower;
                    stats.furnitureTypeOverride = -1;
                    // Meals must NOT be allied so workers can interact with them
                    stats.isAllied        = !data.isMealSource;

                    // Fog reveal
                    stats.revealRadius            = data.fogRevealRadius;
                    stats.isMealSource            = data.isMealSource;

                    // Production fields
                    stats.productionInputType    = data.productionInputType;
                    stats.productionOutputType   = data.productionOutputType;
                    stats.productionInterval      = data.productionInterval;
                    stats.productionIntervalBonus = data.productionIntervalBonus;
                    stats.producedResourceType    = data.producedResourceType;
                    stats.producedCardName        = data.producedCardName;
                    stats.productionAmount        = data.productionAmount;
                    stats.killerAdvances          = data.killerAdvances;
                    stats.productionCostResourceType = data.productionCostResourceType;
                    stats.productionCostAmount       = data.productionCostAmount;
                    stats.productionCostIncrement = data.productionCostIncrement;

                    // Card source type (for tier-based draw filtering)
                    stats.cardSource              = CardSourceType.Building;

                    // Random pool & interaction categories (from sheet)
                    stats.isRandomBuilding        = data.isRandomBuilding;
                    stats.allyInteractible        = data.allyInteractible;
                    stats.enemyInteractible       = data.enemyInteractible;
                    stats.wildAnimalInteractible  = data.wildAnimalInteractible;

                    deckStats.Add(stats);
                    buildingCount++;
                }
                Debug.Log($"[DeckSetup] Added {buildingCount} buildings to deck (workers excluded — produced by buildings)");
            }

            CardPool.Instance.RegisterUnitStats(deckStats);
            Debug.Log($"[DeckSetup] Registered {deckStats.Count} total items with CardPool");

            // ── Create and register special production cards ──
            // NOTE: Feast is already in deckStats from BuildingDatabase (with proper icon, prefab, and stats).
            // FindMealCard() uses FindByName("Feast") which finds the BuildingDatabase version.

            // Fighter card: produced by Barracks building — pull from WorkerDatabase for proper icon/prefab
            WorkerData fighterData = workerDB != null ? workerDB.GetByName("Fighter") : null;
            UnitStats fighterCard = ScriptableObject.CreateInstance<UnitStats>();
            fighterCard.unitName = "Fighter";
            fighterCard.unitType = UnitType.Soldier;
            fighterCard.rarity = Rarity.Common;
            fighterCard.drawWeight = 0f; // Not drawable — produced by Barracks only
            fighterCard.isRandomBuilding = false;
            fighterCard.isMealSource = false;
            fighterCard.cardSource = CardSourceType.Worker;
            if (fighterData != null)
            {
                fighterCard.iconSprite      = fighterData.icon;
                fighterCard.unitPrefab      = fighterData.prefab;
                fighterCard.isActive        = fighterData.isActive;
                fighterCard.isAllied        = true; // Player-owned unit
                fighterCard.maxHP           = fighterData.hp;
                fighterCard.attackDamage    = fighterData.attackPower;
                fighterCard.behaviorType    = fighterData.behaviorType;
                fighterCard.killerAdvances  = fighterData.killerAdvances;
                fighterCard.gridSize        = fighterData.gridSize;
                fighterCard.modelScale      = fighterData.visualScale;
            }
            else
            {
                Debug.LogWarning("[DeckSetup] Fighter not found in WorkerDatabase — card will have no prefab/icon");
            }
            deckStats.Add(fighterCard);

            // ── Environment production cards (Lizard, Scrap) ──
            // These are outputs from Hutch and Scrapper buildings.
            // They live in EnvironmentDatabase but need UnitStats in CardPool for FindByName().
            string[] envProductionNames = { "Lizard", "Scrap", "Tree" };
            if (environmentDB != null)
            {
                foreach (string envName in envProductionNames)
                {
                    EnvironmentData envData = environmentDB.GetByName(envName);
                    if (envData == null) { Debug.LogWarning($"[DeckSetup] '{envName}' not found in EnvironmentDatabase — skipping"); continue; }

                    UnitStats envCard = ScriptableObject.CreateInstance<UnitStats>();
                    envCard.unitName        = envName;
                    envCard.unitType        = UnitType.Soldier;
                    envCard.rarity          = Rarity.Common;
                    envCard.drawWeight      = 0f; // Not drawable — only produced by buildings
                    envCard.isRandomBuilding = false;
                    envCard.isMealSource    = false;
                    envCard.cardSource      = CardSourceType.Unit;
                    envCard.iconSprite      = envData.icon;
                    envCard.unitPrefab      = envData.prefab;
                    envCard.isActive        = envData.isActive;
                    envCard.isAllied        = false; // Environment entities must be non-allied so workers target them
                    envCard.maxHP           = envData.hp;
                    envCard.attackDamage    = envData.attackPower;
                    envCard.killerAdvances  = envData.killerAdvances;
                    envCard.lootResourceType = envData.lootResourceType;
                    envCard.lootHpCost      = envData.lootHpCost;
                    envCard.lootYield       = envData.lootYield;
                    envCard.gridSize        = envData.gridSize;
                    envCard.modelScale      = envData.visualScale;
                    envCard.active          = envData.active;
                    deckStats.Add(envCard);
                    Debug.Log($"[DeckSetup] Registered '{envName}' card for building production");
                }
            }

            CardPool.Instance.RegisterUnitStats(deckStats);
            Debug.Log("[DeckSetup] Registered Fighter + environment production cards with CardPool");

            // ── DockBarManager ───────────────────────────────────────────
            DockBarManager dockManager = Object.FindFirstObjectByType<DockBarManager>(FindObjectsInactive.Include);
            if (dockManager == null)
            {
                Debug.LogWarning("[DeckSetup] DockBarManager not found in scene — no hand UI.");
                return;
            }

            dockManager.gameObject.SetActive(true);
            dockManager.enabled = true;

            // Free draws in ClockworkCraft mode
            dockManager.SetDrawCost(0, 0);

            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                dockManager.Initialize(canvas);
                Debug.Log("[DeckSetup] DockBarManager initialized (free draws)");
            }

            // ── Starting hand: Worker only ─────────────────────────────────
            // Player starts with a single Worker card. After placing it,
            // the draw button reveals and subsequent cards come from draws.

            if (workerDB != null && workerDB.Count > 0)
            {
                dockManager.AddStartingWorker(workerDB);
                Debug.Log("[DeckSetup] Added starting Worker card to hand (draw button hidden until first placement)");
            }
        }
    }
}
