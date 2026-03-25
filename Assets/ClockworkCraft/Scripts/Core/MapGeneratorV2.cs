#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using ClockworkGrid;   // GridManager, FogManager, CellState
using LittleCafe;      // EnvironmentDatabase, EnvironmentData, GridEntityManager

namespace ClockworkCraft
{
    /// <summary>
    /// How an environment entry is distributed across the map.
    /// </summary>
    public enum SpawnMode
    {
        Scattered,  // Random per-tile placement with optional min spacing
        Clustered,  // BFS blobs from random seed points
        Edge        // Spawns along the border of existing clusters of a specific type
    }

    /// <summary>
    /// Spawn config for a single EnvironmentDatabase entry.
    /// Lives on MapGeneratorV2 so all distribution tuning is in one place.
    ///
    /// spawnWeight (1-20) is a RELATIVE proportion shared by ALL entries regardless
    /// of spawn mode. The master mapDensity slider controls total tile coverage,
    /// and each entry's weight determines its share of that budget.
    /// </summary>
    [System.Serializable]
    public class EnvironmentSpawnEntry
    {
        [HideInInspector] public string environmentName;

        [Tooltip("How this type is placed on the map.")]
        public SpawnMode spawnMode = SpawnMode.Scattered;

        [Tooltip("Relative weight (0-20). Determines this entry's share of the total tile budget. 0 = disabled. Higher = more tiles of this type.")]
        [Range(0f, 20f)] public float spawnWeight = 5f;

        [Tooltip("Minimum cell distance between two instances (Scattered mode only).")]
        [Min(0)] public int minSpacing = 0;

        // ── Cluster settings (only used when spawnMode == Clustered) ──
        [Tooltip("0 = few large cohesive blobs. 1 = many small clusters with loose break-off pieces.")]
        [Range(0f, 1f)] public float fragmentation = 0.3f;
        [Tooltip("Blob shape: 0 = stringy/organic, 1 = round/blobby.")]
        [Range(0f, 1f)] public float clusterSpread = 0.6f;

        // ── Edge settings (only used when spawnMode == Edge) ──
        [Tooltip("Name of the environment type whose clusters this should border.")]
        public string edgeBorderOf = "";
    }

    /// <summary>
    /// Spawn config for a single UnitDatabase entry (beasts, monsters, enemies).
    /// Same spawn modes as environment but spawns units with CellState.EnemyUnit.
    /// </summary>
    [System.Serializable]
    public class UnitSpawnEntry
    {
        [HideInInspector] public string unitName;

        [Tooltip("How this unit type is placed on the map.")]
        public SpawnMode spawnMode = SpawnMode.Scattered;

        [Tooltip("Relative weight (0-20). Determines this entry's share of the unit tile budget. 0 = disabled.")]
        [Range(0f, 20f)] public float spawnWeight = 2f;

        [Tooltip("Minimum cell distance between two instances (Scattered mode only).")]
        [Min(0)] public int minSpacing = 4;

        // ── Cluster settings ──
        [Tooltip("0 = few large packs. 1 = many small scattered groups.")]
        [Range(0f, 1f)] public float fragmentation = 0.5f;
        [Tooltip("Pack shape: 0 = stringy/spread out, 1 = tight/round.")]
        [Range(0f, 1f)] public float clusterSpread = 0.4f;

        // ── Edge settings ──
        [Tooltip("Name of the environment or unit type whose clusters this should spawn near.")]
        public string edgeBorderOf = "";
    }

    /// <summary>
    /// Spawn config for a single corruption heart prefab.
    /// Uses an explicit spawnCount instead of a proportional tile budget, because
    /// corruption entities are sparse and their exact count matters for game balance.
    /// Populated by SyncCorruptionSpawnEntries() from UnitDatabase entries of type Corruption.
    /// </summary>
    [System.Serializable]
    public class CorruptionSpawnEntry
    {
        /// <summary>Asset name from UnitData — used as the planGrid key. Populated during sync.</summary>
        [HideInInspector] public string entityName;

        [Tooltip("CorruptionHeart prefab to instantiate. Populated automatically by Sync from Database.")]
        public GameObject prefab;

        [Tooltip("How this corruption entity is distributed on the map.")]
        public SpawnMode spawnMode = SpawnMode.Scattered;

        [Tooltip("Exact number of this entity to place on the map. Set to 0 to disable.")]
        [Min(0)] public int spawnCount = 5;

        [Tooltip("Minimum Chebyshev distance from the player start before an entity may spawn.")]
        [Min(0)] public int minDistFromCenter = 10;

        [Tooltip("Minimum Chebyshev distance between two instances of this entity (Scattered mode).")]
        [Min(0)] public int minSpacing = 8;

        // ── Cluster settings (Clustered mode) ──
        [Tooltip("0 = few large clusters. 1 = many small scattered groups.")]
        [Range(0f, 1f)] public float fragmentation = 0.4f;
        [Tooltip("Cluster shape: 0 = stringy/organic, 1 = round/blobby.")]
        [Range(0f, 1f)] public float clusterSpread = 0.5f;

        // ── Edge settings (Edge mode) ──
        [Tooltip("Name of the environment or unit type whose clusters this should spawn near.")]
        public string edgeBorderOf = "";

        // ── Initial corruption footprint ──
        [Tooltip("Moore neighbourhood radius of pre-corrupted tiles around each heart on spawn. 1 = heart cell + 8 surrounding tiles.")]
        [Min(0)] public int initialCorruptionRadius = 1;
    }

    /// <summary>
    /// Single authority for map creation in the ClockworkCraft scene.
    ///
    /// Owns the full pipeline:
    ///   1. Ensure singletons (GridManager, FogManager, NodeManager, etc.)
    ///   2. Initialize GridManager with grid size
    ///   3. Initialize FogManager and reveal starting area
    ///   4. Plan placement using mapDensity + per-entry relative weights
    ///   5. Spawn all objects (center + environment)
    ///
    /// [DefaultExecutionOrder(-10)] runs before GridManager.Start (default 0)
    /// so we can call InitializeGrid with our size before GridManager auto-inits.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class MapGeneratorV2 : MonoBehaviour
    {
        public static MapGeneratorV2 Instance { get; private set; }

        // ─────────────────────────────────────────────────────────────────
        // Inspector
        // ─────────────────────────────────────────────────────────────────

        [Header("Grid Settings")]
        public int mapWidth = 120;
        public int mapHeight = 120;
        public float cellSize = 1.5f;

        [Header("Map Settings")]
        [Tooltip("0 = random seed each run. Any other value = deterministic map.")]
        public int seed = 0;

        [Header("Fog")]
        [Tooltip("Enable fog of war. Disable to see the full map for debugging.")]
        public bool enableFog = true;
        [Tooltip("Cells revealed around center at start.")]
        public int startingRevealRadius = 4;

        [Header("Databases")]
        public EnvironmentDatabase environmentDatabase;
        public UnitDatabase unitDatabase;
        public WorkerDatabase workerDatabase;
        public BuildingDatabase buildingDatabase;
        public CurrencyDatabase currencyDatabase;
        public ClockworkGrid.EconomyBalanceConfig economyBalanceConfig;

        [Header("Center")]
        [Tooltip("EnvironmentDatabase entry to place at dead center.")]
        public string centerEnvironmentName = "Goldmine";

        [Tooltip("Radius around center kept empty. 1 = 3x3 clearing.")]
        [Min(0)]
        public int clearingRadius = 1;

        // Drawn by custom editor — no [Header] to avoid duplicates
        [HideInInspector] [Range(0.1f, 3f)] public float mapDensity = 1.0f;
        [HideInInspector] public List<EnvironmentSpawnEntry> spawnEntries = new List<EnvironmentSpawnEntry>();
        [HideInInspector] public List<UnitSpawnEntry> unitSpawnEntries = new List<UnitSpawnEntry>();
        [HideInInspector] public List<CorruptionSpawnEntry> corruptionSpawnEntries = new List<CorruptionSpawnEntry>();

        // ─────────────────────────────────────────────────────────────────
        // Internal state
        // ─────────────────────────────────────────────────────────────────

        private string[,] planGrid;
        private int width;
        private int height;
        private Vector2Int center;
        private System.Random rng;

        // ─────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            Debug.Log("[MapGenV2] Start() running...");
            EnsureManagers();
            try
            {
                SetupDeck();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MapGenV2] SetupDeck() CRASHED: {e}");
            }

            // TitleScreenController's onGameStart UnityEvent should call RunGenerate()
            // via the Inspector. If no title screen exists, fall back to runtime hookup.
            var titleScreen = FindFirstObjectByType<ClockworkGrid.TitleScreenController>();
            if (titleScreen != null)
            {
                Debug.Log("[MapGenV2] TitleScreenController found — waiting for onGameStart event...");
            }
            else
            {
                var gate = FindFirstObjectByType<LittleCafe.GameStartGate>();
                if (gate != null)
                {
                    gate.OnGameStart += RunGenerate;
                    Debug.Log("[MapGenV2] Waiting for GameStartGate...");
                }
                else
                {
                    RunGenerate();
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Setup
        // ─────────────────────────────────────────────────────────────────

        void EnsureManagers()
        {
            if (FogManager.Instance == null)
                new GameObject("FogManager").AddComponent<FogManager>();
            if (NodeManager.Instance == null)
                new GameObject("NodeManager").AddComponent<NodeManager>();
            if (ResourceManager.Instance == null)
            {
                var rm = new GameObject("ResourceManager").AddComponent<ResourceManager>();
                rm.currencyDatabase = currencyDatabase;
            }
            else if (ResourceManager.Instance.currencyDatabase == null && currencyDatabase != null)
            {
                // Assign database to existing ResourceManager if it doesn't have one
                ResourceManager.Instance.currencyDatabase = currencyDatabase;
            }

            // Ensure furniture/entity systems exist (CafeSceneSetupV2 defers when MapGeneratorV2 is present)
            if (GridEntityManager.Instance == null)
            {
                if (FindFirstObjectByType<GridEntityManager>() == null)
                    new GameObject("GridEntityManager").AddComponent<GridEntityManager>();
            }
            if (FindFirstObjectByType<FurnitureConnectivityManager>() == null)
                new GameObject("FurnitureConnectivityManager").AddComponent<FurnitureConnectivityManager>();
            if (FindFirstObjectByType<FurnitureRemovalHandler>() == null)
                new GameObject("FurnitureRemovalHandler").AddComponent<FurnitureRemovalHandler>();

            // Ensure economy
            if (ResourceTokenManager.Instance == null)
                new GameObject("ResourceTokenManager").AddComponent<ResourceTokenManager>();

            // Auto-discover EconomyBalanceConfig if not assigned in Inspector
            if (economyBalanceConfig == null)
            {
#if UNITY_EDITOR
                var guids = UnityEditor.AssetDatabase.FindAssets("t:EconomyBalanceConfig");
                if (guids.Length > 0)
                {
                    string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    economyBalanceConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<ClockworkGrid.EconomyBalanceConfig>(assetPath);
                    Debug.Log($"[MapGenV2] Auto-discovered EconomyBalanceConfig at {assetPath}");
                }
#endif
            }

            // Ensure economy manager (centralized placement cost balancing)
            if (ClockworkGrid.EconomyManager.Instance == null)
            {
                var em = new GameObject("EconomyManager").AddComponent<ClockworkGrid.EconomyManager>();
                if (economyBalanceConfig != null)
                    em.balanceConfig = economyBalanceConfig;
            }
            else if (ClockworkGrid.EconomyManager.Instance.balanceConfig == null && economyBalanceConfig != null)
            {
                ClockworkGrid.EconomyManager.Instance.balanceConfig = economyBalanceConfig;
            }

            // Ensure resource display UI
            if (FindFirstObjectByType<ResourceDisplayUI>() == null)
                new GameObject("ResourceDisplayUI").AddComponent<ResourceDisplayUI>();

            // Ensure loot particle FX
            if (FindFirstObjectByType<ResourceLootFX>() == null)
                new GameObject("ResourceLootFX").AddComponent<ResourceLootFX>();
            if (FindObjectOfType<IconFlyFX>(true) == null)
                new GameObject("IconFlyFX").AddComponent<IconFlyFX>();

            // Ensure placement cost display (floating cost above drop location during drag)
            if (LittleCafe.PlacementCostDisplay.Instance == null)
                new GameObject("PlacementCostDisplay").AddComponent<LittleCafe.PlacementCostDisplay>();

            // Ensure gameplay recorder (analytics for balancing)
            if (LittleCafe.GameplayRecorder.Instance == null)
                new GameObject("GameplayRecorder").AddComponent<LittleCafe.GameplayRecorder>();

            // Ensure building production manager
            if (FindFirstObjectByType<BuildingProductionManager>() == null)
            {
                var bpm = new GameObject("BuildingProductionManager").AddComponent<BuildingProductionManager>();
                bpm.workerDatabase = workerDatabase;
            }
            else if (FindFirstObjectByType<BuildingProductionManager>().workerDatabase == null && workerDatabase != null)
            {
                FindFirstObjectByType<BuildingProductionManager>().workerDatabase = workerDatabase;
            }

            // Ensure hold-to-fill handler (input handler for HoldToFill buildings like Kitchen)
            if (LittleCafe.HoldToFillHandler.Instance == null)
                new GameObject("HoldToFillHandler").AddComponent<LittleCafe.HoldToFillHandler>();

            // Ensure corruption manager
            if (LittleCafe.CorruptionManager.Instance == null)
                new GameObject("CorruptionManager").AddComponent<LittleCafe.CorruptionManager>();

            // Ensure worker card fly FX
            if (FindFirstObjectByType<WorkerCardFlyFX>() == null)
                new GameObject("WorkerCardFlyFX").AddComponent<WorkerCardFlyFX>();

            // Ensure GameSFXManager (unified sound effects)
            if (GameSFXManager.Instance == null && FindFirstObjectByType<GameSFXManager>() == null)
                new GameObject("GameSFXManager").AddComponent<GameSFXManager>();

            // Interaction registry — owned by CafeSceneSetupV2 as a scene object.
            // Only populate if it exists but has no entries (e.g., databases weren't assigned on the component).
            if (InteractionRegistry.Instance != null && InteractionRegistry.Instance.AllEntries.Count == 0)
            {
                InteractionRegistry.Instance.PopulateFromDatabases(environmentDatabase, unitDatabase);
                Debug.Log("[MapGenV2] Populated InteractionRegistry entries from MapGeneratorV2 databases (scene component had none).");
            }
            else if (InteractionRegistry.Instance == null)
            {
                Debug.LogWarning("[MapGenV2] No InteractionRegistry found! Add one to the scene via CafeSceneSetupV2 or Tools > ClockworkCraft > Setup Interaction Registry.");
            }

            // Ensure UI infrastructure
            if (FindFirstObjectByType<DragDropHandler>() == null)
                new GameObject("DragDropHandler").AddComponent<DragDropHandler>();
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem");
                esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }

        void InitializeGridManager()
        {
            GridManager gm = GridManager.Instance;
            if (gm == null)
            {
                gm = FindFirstObjectByType<GridManager>();
                if (gm == null)
                {
                    // Create GridManager if none exists in scene
                    GameObject gridObj = new GameObject("GridManager");
                    gm = gridObj.AddComponent<GridManager>();
                    if (gm.GetComponent<GridVisualizer>() == null)
                        gridObj.AddComponent<GridVisualizer>();
                    Debug.Log("[MapGenV2] Created GridManager (not found in scene)");
                }
            }

            gm.gameObject.SetActive(true);
            gm.enabled = true;
            gm.InitializeGrid(mapWidth, mapHeight);
            Debug.Log($"[MapGenV2] GridManager initialized: {mapWidth}x{mapHeight}");
        }

        /// <summary>
        /// Populate the CardPool draw pool, then initialize the DockBarManager.
        /// Uses WorkerDatabase + BuildingDatabase.
        /// </summary>
        void SetupDeck()
        {
            Debug.Log("[MapGenV2] SetupDeck() starting...");

            // ── CardPool ─────────────────────────────────────────────
            if (CardPool.Instance != null)
            {
                DestroyImmediate(CardPool.Instance.gameObject);
            }
            new GameObject("CardPool").AddComponent<CardPool>();

            var deckStats = new List<UnitStats>();

            {
                // ── Normal deck (Buildings only — workers come from building production) ──
                bool hasBuildings = buildingDatabase != null && buildingDatabase.AllBuildings.Count > 0;

                if (!hasBuildings)
                {
                    Debug.LogWarning("[MapGenV2] No BuildingDatabase assigned — dock will be empty.");
                    return;
                }

                int buildingCount = 0;
                foreach (BuildingData data in buildingDatabase.AllBuildings)
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
                Debug.Log($"[MapGenV2] Added {buildingCount} buildings to deck (workers excluded — produced by buildings)");
            }

            CardPool.Instance.RegisterUnitStats(deckStats);
            Debug.Log($"[MapGenV2] Registered {deckStats.Count} total items with CardPool");

            // ── Create and register special production cards ──
            // NOTE: Feast is already in deckStats from BuildingDatabase (with proper icon, prefab, and stats).
            // FindMealCard() uses FindByName("Feast") which finds the BuildingDatabase version.

            // Fighter card: produced by Barracks building — pull from WorkerDatabase for proper icon/prefab
            WorkerData fighterData = workerDatabase != null ? workerDatabase.GetByName("Fighter") : null;
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
                Debug.LogWarning("[MapGenV2] Fighter not found in WorkerDatabase — card will have no prefab/icon");
            }
            deckStats.Add(fighterCard);

            CardPool.Instance.RegisterUnitStats(deckStats);
            Debug.Log("[MapGenV2] Registered Fighter card for Barracks production (Feast already in deck from BuildingDatabase)");

            // ── DockBarManager ───────────────────────────────────────────
            DockBarManager dockManager = FindFirstObjectByType<DockBarManager>(FindObjectsInactive.Include);
            if (dockManager == null)
            {
                Debug.LogWarning("[MapGenV2] DockBarManager not found in scene — no hand UI.");
                return;
            }

            dockManager.gameObject.SetActive(true);
            dockManager.enabled = true;

            // Free draws in ClockworkCraft mode
            dockManager.SetDrawCost(0, 0);

            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                dockManager.Initialize(canvas);
                Debug.Log("[MapGenV2] DockBarManager initialized (free draws)");
            }

            // ── Starting hand: Worker only ─────────────────────────────────
            // Player starts with a single Worker card. After placing it,
            // the draw button reveals and subsequent cards come from draws.

            if (workerDatabase != null && workerDatabase.Count > 0)
            {
                dockManager.AddStartingWorker(workerDatabase);
                Debug.Log("[MapGenV2] Added starting Worker card to hand (draw button hidden until first placement)");
            }
        }

        /// <summary>
        /// Syncs spawnEntries from EnvironmentDatabase AND unitSpawnEntries from UnitDatabase.
        /// Keeps existing weights/spacing for entries that still exist.
        /// Called by the custom editor's "Sync from Database" button.
        /// </summary>
        public void SyncSpawnEntries()
        {
            // ── Environment entries ──
            if (environmentDatabase != null)
            {
                var existing = new Dictionary<string, EnvironmentSpawnEntry>();
                foreach (var entry in spawnEntries)
                {
                    if (!string.IsNullOrEmpty(entry.environmentName))
                        existing[entry.environmentName] = entry;
                }

                var synced = new List<EnvironmentSpawnEntry>();
                foreach (var envData in environmentDatabase.AllEnvironment)
                {
                    if (!envData.active) continue; // skip inactive entries
                    if (!envData.isMapGenerated) continue; // only map-generated objects go into spawn entries
                    if (existing.TryGetValue(envData.assetName, out var kept))
                        synced.Add(kept);
                    else
                        synced.Add(CreateDefaultEntry(envData.assetName));
                }
                spawnEntries = synced;
            }

            // ── Unit entries ──
            SyncUnitSpawnEntries();
        }

        /// <summary>
        /// Creates a spawn entry with sensible defaults based on the environment name.
        /// </summary>
        EnvironmentSpawnEntry CreateDefaultEntry(string name)
        {
            string lower = name.ToLowerInvariant();
            var entry = new EnvironmentSpawnEntry { environmentName = name };

            if (lower.Contains("tree") || lower.Contains("pine") || lower.Contains("forest"))
            {
                // Trees: dominant clustered presence with natural break-offs
                entry.spawnMode     = SpawnMode.Clustered;
                entry.spawnWeight   = 12f;
                entry.fragmentation = 0.35f;   // mostly cohesive blobs with some loose trees
                entry.clusterSpread = 0.55f;   // slightly organic shape
            }
            else if (lower.Contains("water") || lower.Contains("lake") || lower.Contains("river"))
            {
                // Water: clustered pools near trees, cohesive not scattered
                entry.spawnMode     = SpawnMode.Clustered;
                entry.spawnWeight   = 6f;
                entry.fragmentation = 0.15f;   // mostly cohesive pools, very few loose tiles
                entry.clusterSpread = 0.7f;    // rounder blobs (ponds/lakes)
            }
            else if (lower.Contains("rock") || lower.Contains("stone") || lower.Contains("boulder"))
            {
                // Rocks: clustered outcrops with high fragmentation for natural rocky patches
                entry.spawnMode     = SpawnMode.Clustered;
                entry.spawnWeight   = 4f;
                entry.fragmentation = 0.55f;   // many small outcrops + loose stones
                entry.clusterSpread = 0.45f;   // organic/irregular shape
            }
            else if (lower.Contains("gold") || lower.Contains("mine") || lower.Contains("ore"))
            {
                // Goldmines: rare, well-spaced
                entry.spawnMode   = SpawnMode.Scattered;
                entry.spawnWeight = 2f;
                entry.minSpacing  = 8;
            }
            else if (lower.Contains("mountain"))
            {
                // Mountains: rare scattered landmarks
                entry.spawnMode   = SpawnMode.Scattered;
                entry.spawnWeight = 3f;
                entry.minSpacing  = 6;
            }
            else if (lower.Contains("flower") || lower.Contains("flora") || lower.Contains("bush"))
            {
                // Flowers: ring around water pools for a natural meadow feel
                entry.spawnMode    = SpawnMode.Edge;
                entry.spawnWeight  = 3f;
                entry.edgeBorderOf = "Water";
            }
            else if (lower.Contains("light") || lower.Contains("lamp") || lower.Contains("decor"))
            {
                // Decorative: light scattered presence
                entry.spawnMode   = SpawnMode.Scattered;
                entry.spawnWeight = 4f;
                entry.minSpacing  = 4;
            }
            else
            {
                // Generic default: scattered, moderate weight
                entry.spawnMode   = SpawnMode.Scattered;
                entry.spawnWeight = 5f;
                entry.minSpacing  = 0;
            }

            return entry;
        }

        // ─────────────────────────────────────────────────────────────────
        // Unit Spawn Entry Sync
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Syncs unitSpawnEntries from UnitDatabase.
        /// Keeps existing weights/settings for entries that still exist.
        /// Called by the custom editor's "Sync Units from Database" button.
        /// </summary>
        public void SyncUnitSpawnEntries()
        {
            if (unitDatabase == null) return;

            var existing = new Dictionary<string, UnitSpawnEntry>();
            foreach (var entry in unitSpawnEntries)
            {
                if (!string.IsNullOrEmpty(entry.unitName))
                    existing[entry.unitName] = entry;
            }

            var synced = new List<UnitSpawnEntry>();
            foreach (var unitData in unitDatabase.AllUnits)
            {
                if (!unitData.active) continue; // skip inactive entries
                if (!unitData.isMapGenerated) continue; // only map-generated units go into spawn entries
                if (existing.TryGetValue(unitData.assetName, out var kept))
                    synced.Add(kept);
                else
                    synced.Add(CreateDefaultUnitEntry(unitData.assetName, unitData.type));
            }

            unitSpawnEntries = synced;
        }

        /// <summary>
        /// Creates a unit spawn entry with sensible defaults based on name/type.
        /// </summary>
        UnitSpawnEntry CreateDefaultUnitEntry(string name, GameUnitType type)
        {
            string lower = name.ToLowerInvariant();
            var entry = new UnitSpawnEntry { unitName = name };

            // Beasts and bosses: scattered, rare, well-spaced
            if (type == GameUnitType.Beast || lower.Contains("beast") || lower.Contains("wolf") ||
                lower.Contains("bear") || lower.Contains("boar") || lower.Contains("spider"))
            {
                entry.spawnMode   = SpawnMode.Scattered;
                entry.spawnWeight = 3f;
                entry.minSpacing  = 5;
            }
            else if (type == GameUnitType.Boss || lower.Contains("boss") || lower.Contains("dragon"))
            {
                entry.spawnMode   = SpawnMode.Scattered;
                entry.spawnWeight = 1f;
                entry.minSpacing  = 12;
            }
            else if (type == GameUnitType.Soldier || type == GameUnitType.Archer ||
                     lower.Contains("guard") || lower.Contains("skeleton") || lower.Contains("goblin"))
            {
                // Enemy troops: small clustered packs
                entry.spawnMode     = SpawnMode.Clustered;
                entry.spawnWeight   = 4f;
                entry.fragmentation = 0.6f;
                entry.clusterSpread = 0.3f;
            }
            else
            {
                // Generic default: scattered, moderate
                entry.spawnMode   = SpawnMode.Scattered;
                entry.spawnWeight = 2f;
                entry.minSpacing  = 4;
            }

            return entry;
        }

        // ─────────────────────────────────────────────────────────────────
        // Generation
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Starts map generation. Wire to TitleScreenController's onGameStart in the Inspector.
        /// </summary>
        public void RunGenerate()
        {
            StartCoroutine(RunGenerateCoroutine());
        }

        System.Collections.IEnumerator RunGenerateCoroutine()
        {
            // Small delay after click so the UI transition plays smoothly
            yield return null; // Let the click frame finish
            yield return null; // One more frame for any UI animations to start

            InitializeGridManager();

            // Re-point camera at the (potentially new) grid center.
            GridCamera cam = GridCamera.Instance;
            if (cam == null) cam = FindFirstObjectByType<GridCamera>();
            if (cam == null)
            {
                var cameraSystem = CameraSystemLocator.Current as GridCamera;
                if (cameraSystem != null) cam = cameraSystem;
            }
            if (cam != null)
            {
                cam.PointAtGrid();
                Debug.Log("[MapGenV2] Camera re-pointed at grid center after grid init");
            }
            else
            {
                Debug.LogWarning("[MapGenV2] Could not find GridCamera to re-point after grid init");
            }

            yield return StartCoroutine(GenerateMapStaggered(seed == 0 ? Random.Range(1, 999999) : seed));
        }

        public void GenerateMap(int seed)
        {
            // Synchronous entry point — kept for compatibility
            StartCoroutine(GenerateMapStaggered(seed));
        }

        /// <summary>
        /// Coroutine version of map generation. The planning phase (pure math)
        /// runs synchronously, then the spawn phase yields every N objects
        /// to spread Instantiate calls across multiple frames.
        /// </summary>
        private System.Collections.IEnumerator GenerateMapStaggered(int seed)
        {
            if (GridManager.Instance == null)
            {
                Debug.LogError("[MapGenV2] GridManager not found!");
                yield break;
            }
            if (environmentDatabase == null)
            {
                Debug.LogError("[MapGenV2] EnvironmentDatabase not assigned!");
                yield break;
            }

            rng    = new System.Random(seed);
            width  = GridManager.Instance.Width;
            height = GridManager.Instance.Height;
            center = new Vector2Int(
                Mathf.RoundToInt((width  - 1) * 0.5f),
                Mathf.RoundToInt((height - 1) * 0.5f));

            // ── Auto-sync spawn entries if empty ────────────────────────
            if (unitDatabase != null && unitSpawnEntries.Count == 0)
            {
                Debug.Log("[MapGenV2] unitSpawnEntries empty — auto-syncing from UnitDatabase");
                SyncUnitSpawnEntries();
            }
            if (environmentDatabase != null && spawnEntries.Count == 0)
            {
                Debug.Log("[MapGenV2] spawnEntries empty — auto-syncing from EnvironmentDatabase");
                SyncSpawnEntries();
            }

            // ── Auto-sync corruption entries if empty ────────────────────
            if (unitDatabase != null && corruptionSpawnEntries.Count == 0)
            {
                Debug.Log("[MapGenV2] corruptionSpawnEntries empty — auto-syncing from UnitDatabase (Corruption type)");
                SyncCorruptionSpawnEntries();
            }

            // ── Plan (fast — pure array math, no Instantiate) ─────────
            InitPlanGrid();
            PlaceAllEntries();
            PlaceCorruptionEntities(); // runs after env + units so it respects their cells

            // ── Fog ───────────────────────────────────────────────────
            FogManager.Instance?.Initialize(width, height);
            if (enableFog)
            {
                FogManager.Instance?.RevealCross(center.x, center.y, startingRevealRadius);
            }
            else
            {
                for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    FogManager.Instance?.RevealCell(x, y);
                Debug.Log("[MapGenV2] Fog disabled — entire map revealed for debugging");
            }

            yield return null; // Let fog visuals settle

            // ── Spawn center (just one object, no need to stagger) ────
            SpawnCenter();

            // ── Spawn environment (staggered) ─────────────────────────
            yield return StartCoroutine(SpawnAllStaggered());

            // ── Spawn units (staggered) ───────────────────────────────
            yield return StartCoroutine(SpawnAllUnitsStaggered());

            // ── Spawn corruption entities (staggered) ─────────────────
            yield return StartCoroutine(SpawnAllCorruptionEntitiesStaggered());

            Debug.Log($"[MapGenV2] Map generated. Seed={seed}  Size={width}x{height}  Center=({center.x},{center.y})  Nodes={NodeManager.Instance?.NodeCount}");
        }

        // ─────────────────────────────────────────────────────────────────
        // Plan Phase
        // ─────────────────────────────────────────────────────────────────

        void InitPlanGrid()
        {
            planGrid = new string[width, height];
            planGrid[center.x, center.y] = "__center__";
        }

        /// <summary>
        /// Budget-based placement. mapDensity controls total tiles filled.
        /// Each entry's spawnWeight (1-20) determines its share of the budget.
        ///
        /// Order: Clustered → Edge → Scattered (so edges can reference clusters).
        /// Each entry gets a tile budget = (weight / totalWeight) * totalBudget.
        /// </summary>
        void PlaceAllEntries()
        {
            // Validate environment entries
            var valid = new List<(EnvironmentSpawnEntry entry, EnvironmentData data)>();
            if (environmentDatabase != null)
            {
                foreach (var entry in spawnEntries)
                {
                    if (entry.spawnWeight <= 0f) continue;
                    if (entry.spawnMode == SpawnMode.Edge && string.IsNullOrEmpty(entry.edgeBorderOf)) continue;
                    var data = environmentDatabase.GetByName(entry.environmentName);
                    if (data == null || data.prefab == null) continue;
                    valid.Add((entry, data));
                }
            }

            // Validate unit entries
            var validUnits = new List<(UnitSpawnEntry entry, UnitData data)>();
            if (unitDatabase != null)
            {
                foreach (var entry in unitSpawnEntries)
                {
                    if (entry.spawnWeight <= 0f) continue;
                    if (entry.spawnMode == SpawnMode.Edge && string.IsNullOrEmpty(entry.edgeBorderOf)) continue;
                    var data = unitDatabase.GetByName(entry.unitName);
                    if (data == null || data.prefab == null) continue;
                    validUnits.Add((entry, data));
                }
            }

            if (valid.Count == 0 && validUnits.Count == 0)
            {
                Debug.LogWarning("[MapGenV2] No valid spawn entries.");
                return;
            }

            // Calculate total tile budget from mapDensity (shared by env + units)
            int clearingSize = (2 * clearingRadius + 1) * (2 * clearingRadius + 1);
            int availableTiles = (width * height) - clearingSize;
            int totalBudget = Mathf.RoundToInt(availableTiles * mapDensity);

            // Combined weight pool: environment + unit entries share the same budget
            float totalWeight = valid.Sum(v => v.entry.spawnWeight)
                              + validUnits.Sum(v => v.entry.spawnWeight);

            // Per-entry budgets (environment)
            var entryBudgets = new Dictionary<string, int>();
            foreach (var (entry, _) in valid)
            {
                int budget = Mathf.RoundToInt((entry.spawnWeight / totalWeight) * totalBudget);
                entryBudgets[entry.environmentName] = budget;
            }

            // Per-entry budgets (units) — stored with UNIT_PREFIX key
            var unitBudgets = new Dictionary<string, int>();
            foreach (var (entry, _) in validUnits)
            {
                int budget = Mathf.RoundToInt((entry.spawnWeight / totalWeight) * totalBudget);
                unitBudgets[entry.unitName] = budget;
            }

            Debug.Log($"[MapGenV2] Density {mapDensity:P0}: total budget={totalBudget} tiles, {valid.Count} env + {validUnits.Count} units, weights sum={totalWeight:F1}");

            // ── Pass 1: Clustered ─────────────────────────────────────
            foreach (var (entry, _) in valid.Where(v => v.entry.spawnMode == SpawnMode.Clustered))
            {
                int budget = entryBudgets[entry.environmentName];
                PlaceClusters(entry, budget);
            }

            // ── Pass 2: Edge ──────────────────────────────────────────
            foreach (var (entry, _) in valid.Where(v => v.entry.spawnMode == SpawnMode.Edge))
            {
                int budget = entryBudgets[entry.environmentName];
                PlaceEdges(entry, budget);
            }

            // ── Pass 3: Scattered ─────────────────────────────────────
            var scattered = valid.Where(v => v.entry.spawnMode == SpawnMode.Scattered).ToList();
            if (scattered.Count > 0)
            {
                var scatteredBudgets = new Dictionary<string, int>();
                foreach (var (entry, _) in scattered)
                    scatteredBudgets[entry.environmentName] = entryBudgets[entry.environmentName];
                PlaceScattered(scattered, scatteredBudgets);
            }

            // ── Pass 4: Clustered units ──────────────────────────────
            foreach (var (entry, _) in validUnits.Where(v => v.entry.spawnMode == SpawnMode.Clustered))
            {
                int budget = unitBudgets[entry.unitName];
                PlaceUnitClusters(entry, budget);
            }

            // ── Pass 5: Edge units ───────────────────────────────────
            foreach (var (entry, _) in validUnits.Where(v => v.entry.spawnMode == SpawnMode.Edge))
            {
                int budget = unitBudgets[entry.unitName];
                PlaceUnitEdges(entry, budget);
            }

            // ── Pass 6: Scattered units ──────────────────────────────
            var scatteredUnits = validUnits.Where(v => v.entry.spawnMode == SpawnMode.Scattered).ToList();
            if (scatteredUnits.Count > 0)
            {
                PlaceUnitScattered(scatteredUnits, unitBudgets);
            }

            // ── Pass 7: Fill remaining budget ─────────────────────────
            // If spacing or BFS limits prevented entries from filling their
            // budgets, redistribute remaining tiles proportionally.
            // Excludes Clustered entries to prevent orphan singles.
            // Respects spacing constraints for Scattered entries.
            int placedSoFar = 0;
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (planGrid[x, y] != null && planGrid[x, y] != "__center__") placedSoFar++;

            int remainingBudget = totalBudget - placedSoFar;
            if (remainingBudget > 0)
            {
                var emptyForFill = new List<Vector2Int>();
                for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    if (planGrid[x, y] == null && !IsInClearing(x, y))
                        emptyForFill.Add(new Vector2Int(x, y));

                ShuffleList(emptyForFill);

                var fillable = valid.Where(v => v.entry.spawnMode != SpawnMode.Clustered).ToList();
                if (fillable.Count == 0) fillable = valid.ToList();

                // Build existing placement map for spacing checks
                // (scan planGrid for positions of each fillable entry)
                var fillPositions = new Dictionary<string, List<Vector2Int>>();
                foreach (var (entry, _) in fillable)
                    fillPositions[entry.environmentName] = new List<Vector2Int>();

                for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    string name = planGrid[x, y];
                    if (name != null && fillPositions.ContainsKey(name))
                        fillPositions[name].Add(new Vector2Int(x, y));
                }

                int filled = 0;
                foreach (var pos in emptyForFill)
                {
                    if (filled >= remainingBudget) break;

                    // Weighted shuffle so we try alternatives if spacing rejects first pick
                    var candidates = new List<(EnvironmentSpawnEntry entry, EnvironmentData data)>(fillable);
                    // Simple weighted shuffle inline
                    for (int i = 0; i < candidates.Count - 1; i++)
                    {
                        float tw = 0f;
                        for (int j = i; j < candidates.Count; j++) tw += candidates[j].entry.spawnWeight;
                        float r = (float)(rng.NextDouble() * tw);
                        float c = 0f;
                        int p = i;
                        for (int j = i; j < candidates.Count; j++)
                        {
                            c += candidates[j].entry.spawnWeight;
                            if (r < c) { p = j; break; }
                        }
                        if (p != i) { var tmp = candidates[i]; candidates[i] = candidates[p]; candidates[p] = tmp; }
                    }

                    bool placed = false;
                    foreach (var (entry, _) in candidates)
                    {
                        // Respect spacing for scattered entries
                        if (entry.minSpacing > 0 &&
                            IsTooClose(pos.x, pos.y, fillPositions[entry.environmentName], entry.minSpacing))
                            continue;

                        planGrid[pos.x, pos.y] = entry.environmentName;
                        fillPositions[entry.environmentName].Add(pos);
                        filled++;
                        placed = true;
                        break;
                    }
                }
                Debug.Log($"[MapGenV2] Fill pass: placed {filled} extra tiles to meet density target (budget gap was {remainingBudget})");
            }

            // Log final placement counts
            int totalPlaced = 0;
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (planGrid[x, y] != null && planGrid[x, y] != "__center__") totalPlaced++;

            Debug.Log($"[MapGenV2] Plan complete: {totalPlaced} tiles placed (budget was {totalBudget}, {(float)totalPlaced / availableTiles:P1} actual coverage)");
        }

        // ── Clustered placement ──────────────────────────────────────
        //
        // Fragmentation (0-1) controls the feel:
        //   0.0 = 2 large cohesive blobs, no loose pieces
        //   0.5 = ~5 medium blobs + 15% loose break-off tiles nearby
        //   1.0 = ~12 small blobs + 40% loose scatter near edges
        //
        // Spread (0-1) controls blob shape only (round vs stringy).

        void PlaceClusters(EnvironmentSpawnEntry entry, int tileBudget)
        {
            if (tileBudget <= 0) return;

            float frag = Mathf.Clamp01(entry.fragmentation);
            float spread = Mathf.Clamp01(entry.clusterSpread);

            // Derive cluster count from fragmentation: 3 at frag=0, up to 12 at frag=1
            // Minimum 3 ensures decent spatial coverage on a 40x40 map
            int clusterCount = Mathf.Max(3, Mathf.RoundToInt(Mathf.Lerp(3f, 12f, frag)));

            // Derive loose scatter fraction: 0% at frag<0.25, up to 40% at frag=1
            // Low frag entries (like water pools) get NO loose singles
            float looseFraction = frag < 0.25f ? 0f : Mathf.Lerp(0f, 0.4f, frag);
            int looseBudget = Mathf.RoundToInt(tileBudget * looseFraction);
            int blobBudget  = tileBudget - looseBudget;

            // ── Phase 1: BFS blobs with zone-based seeding ──────────
            // Divide the map into zones to ensure spatial coverage.
            // Each cluster seed tries to land in a different zone first.
            int blobPlaced = 0;
            var allClusterCells = new List<Vector2Int>();

            // Build zone-based seed candidates for better spatial distribution
            int zonesPerSide = Mathf.Max(2, Mathf.CeilToInt(Mathf.Sqrt(clusterCount)));
            var zoneSeedCandidates = GetZoneSeeds(zonesPerSide);

            int maxAttempts = clusterCount + 10;
            for (int c = 0; c < maxAttempts && blobPlaced < blobBudget; c++)
            {
                int targetSize;
                if (c < clusterCount)
                {
                    int remainingClusters = clusterCount - c;
                    targetSize = Mathf.Max(1, Mathf.RoundToInt((float)(blobBudget - blobPlaced) / remainingClusters));
                    int minSize = Mathf.Max(1, Mathf.RoundToInt(targetSize * 0.7f));
                    int maxSize = Mathf.RoundToInt(targetSize * 1.3f);
                    targetSize = minSize + (maxSize > minSize ? rng.Next(maxSize - minSize + 1) : 0);
                }
                else
                {
                    targetSize = blobBudget - blobPlaced;
                }
                targetSize = Mathf.Min(targetSize, blobBudget - blobPlaced);

                // Pick seed: use zone-based for first seeds, random for overflow
                Vector2Int seed = Vector2Int.zero;
                bool foundSeed = false;

                if (c < zoneSeedCandidates.Count)
                {
                    // Try from zone candidates first
                    var zoneList = zoneSeedCandidates[c];
                    foreach (var candidate in zoneList)
                    {
                        if (planGrid[candidate.x, candidate.y] == null && !IsInClearing(candidate.x, candidate.y))
                        {
                            seed = candidate;
                            foundSeed = true;
                            break;
                        }
                    }
                }

                // Fallback to random if zone seed failed
                if (!foundSeed)
                {
                    for (int attempt = 0; attempt < 80; attempt++)
                    {
                        int sx = rng.Next(width);
                        int sy = rng.Next(height);
                        if (planGrid[sx, sy] == null && !IsInClearing(sx, sy))
                        {
                            seed = new Vector2Int(sx, sy);
                            foundSeed = true;
                            break;
                        }
                    }
                }
                if (!foundSeed) break;

                // BFS expansion — spread controls SHAPE, not growth limit.
                // Phase A: grow with spread shaping (may stall if spread is low)
                // Phase B: if stalled, force-grow from placed cells to hit target
                var frontier = new List<Vector2Int> { seed };
                var visited  = new HashSet<Vector2Int> { seed };
                var deferred = new List<Vector2Int>(); // cells rejected by spread
                planGrid[seed.x, seed.y] = entry.environmentName;
                allClusterCells.Add(seed);
                int grown = 1;

                Vector2Int[] dirs = {
                    new Vector2Int(1, 0), new Vector2Int(-1, 0),
                    new Vector2Int(0, 1), new Vector2Int(0, -1)
                };

                // Phase A: shaped growth
                while (grown < targetSize && frontier.Count > 0)
                {
                    int fi = rng.Next(frontier.Count);
                    var current = frontier[fi];
                    frontier.RemoveAt(fi);

                    foreach (var d in dirs)
                    {
                        if (grown >= targetSize) break;
                        var nb = current + d;
                        if (nb.x < 0 || nb.x >= width || nb.y < 0 || nb.y >= height) continue;
                        if (visited.Contains(nb)) continue;
                        visited.Add(nb);
                        if (planGrid[nb.x, nb.y] != null || IsInClearing(nb.x, nb.y)) continue;

                        // Spread shapes the blob — rejected cells are deferred, not lost
                        if ((float)rng.NextDouble() > spread)
                        {
                            deferred.Add(nb);
                            continue;
                        }

                        planGrid[nb.x, nb.y] = entry.environmentName;
                        frontier.Add(nb);
                        allClusterCells.Add(nb);
                        grown++;
                    }
                }

                // Phase B: force-grow from deferred cells to hit target
                // This ensures spread doesn't limit quantity, only shape
                if (grown < targetSize && deferred.Count > 0)
                {
                    ShuffleList(deferred);
                    foreach (var nb in deferred)
                    {
                        if (grown >= targetSize) break;
                        if (planGrid[nb.x, nb.y] != null) continue;
                        planGrid[nb.x, nb.y] = entry.environmentName;
                        allClusterCells.Add(nb);
                        grown++;

                        // Also expand from this cell to fill more
                        foreach (var d in dirs)
                        {
                            if (grown >= targetSize) break;
                            var nb2 = nb + d;
                            if (nb2.x < 0 || nb2.x >= width || nb2.y < 0 || nb2.y >= height) continue;
                            if (visited.Contains(nb2)) continue;
                            visited.Add(nb2);
                            if (planGrid[nb2.x, nb2.y] != null || IsInClearing(nb2.x, nb2.y)) continue;
                            planGrid[nb2.x, nb2.y] = entry.environmentName;
                            allClusterCells.Add(nb2);
                            grown++;
                        }
                    }
                }

                blobPlaced += grown;
            }

            // ── Phase 2: Loose break-off pieces near clusters ─────────
            // Skipped entirely for low-frag entries (e.g. water pools) to prevent orphan singles
            int loosePlaced = 0;
            if (looseBudget > 0 && allClusterCells.Count > 0)
            {
                var looseCandidates = new HashSet<Vector2Int>();
                var clusterSet = new HashSet<Vector2Int>(allClusterCells);

                foreach (var cell in allClusterCells)
                {
                    for (int dx = -3; dx <= 3; dx++)
                    for (int dy = -3; dy <= 3; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = cell.x + dx, ny = cell.y + dy;
                        if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                        var candidate = new Vector2Int(nx, ny);
                        if (planGrid[nx, ny] != null) continue;
                        if (IsInClearing(nx, ny)) continue;
                        if (clusterSet.Contains(candidate)) continue;
                        int dist = Mathf.Abs(dx) + Mathf.Abs(dy);
                        if (dist >= 2)
                            looseCandidates.Add(candidate);
                    }
                }

                var looseList = new List<Vector2Int>(looseCandidates);
                ShuffleList(looseList);

                int toPlace = Mathf.Min(looseBudget, looseList.Count);
                for (int i = 0; i < toPlace; i++)
                {
                    var pos = looseList[i];
                    if (planGrid[pos.x, pos.y] != null) continue;
                    planGrid[pos.x, pos.y] = entry.environmentName;
                    loosePlaced++;
                }
            }

            int totalPlaced = blobPlaced + loosePlaced;
            Debug.Log($"[MapGenV2] Clustered '{entry.environmentName}': {totalPlaced} tiles ({blobPlaced} in blobs + {loosePlaced} loose) [budget={tileBudget}, frag={frag:F1}]");
        }

        /// <summary>
        /// Divides the map into zones and returns shuffled seed candidates per zone.
        /// Ensures cluster seeds are spread across the whole map, not clumped.
        /// </summary>
        List<List<Vector2Int>> GetZoneSeeds(int zonesPerSide)
        {
            int zoneW = Mathf.Max(1, width / zonesPerSide);
            int zoneH = Mathf.Max(1, height / zonesPerSide);

            var zones = new List<List<Vector2Int>>();

            for (int zy = 0; zy < zonesPerSide; zy++)
            for (int zx = 0; zx < zonesPerSide; zx++)
            {
                var candidates = new List<Vector2Int>();
                int startX = zx * zoneW;
                int startY = zy * zoneH;
                int endX = (zx == zonesPerSide - 1) ? width : startX + zoneW;
                int endY = (zy == zonesPerSide - 1) ? height : startY + zoneH;

                for (int x = startX; x < endX; x++)
                for (int y = startY; y < endY; y++)
                    candidates.Add(new Vector2Int(x, y));

                ShuffleList(candidates);
                zones.Add(candidates);
            }

            // Shuffle zones so cluster assignment is random across map
            ShuffleList(zones);
            return zones;
        }

        // ── Edge placement ───────────────────────────────────────────

        void PlaceEdges(EnvironmentSpawnEntry entry, int tileBudget)
        {
            if (tileBudget <= 0) return;

            // Collect all valid border tiles first
            var candidates = new List<Vector2Int>();
            Vector2Int[] dirs = {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1)
            };

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (planGrid[x, y] != null) continue;
                if (IsInClearing(x, y)) continue;

                // Check if any cardinal neighbor is the target border type
                bool adjacentToTarget = false;
                foreach (var d in dirs)
                {
                    int nx = x + d.x, ny = y + d.y;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                    if (planGrid[nx, ny] == entry.edgeBorderOf)
                    {
                        adjacentToTarget = true;
                        break;
                    }
                }

                if (adjacentToTarget)
                    candidates.Add(new Vector2Int(x, y));
            }

            // Shuffle candidates and place up to budget
            ShuffleList(candidates);
            int placed = Mathf.Min(tileBudget, candidates.Count);
            for (int i = 0; i < placed; i++)
            {
                var pos = candidates[i];
                planGrid[pos.x, pos.y] = entry.environmentName;
            }

            Debug.Log($"[MapGenV2] Edge '{entry.environmentName}' bordering '{entry.edgeBorderOf}': {placed} tiles (budget was {tileBudget}, {candidates.Count} candidates)");
        }

        // ── Scattered placement ──────────────────────────────────────
        //
        // Each entry has its own tile budget. We iterate empty tiles and
        // distribute them proportionally, respecting per-entry budgets
        // and spacing constraints.

        void PlaceScattered(List<(EnvironmentSpawnEntry entry, EnvironmentData data)> scattered, Dictionary<string, int> budgets)
        {
            int totalScatteredBudget = budgets.Values.Sum();
            if (totalScatteredBudget <= 0) return;

            // Collect all empty non-clearing tiles and shuffle them
            var emptyTiles = new List<Vector2Int>();
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (planGrid[x, y] == null && !IsInClearing(x, y))
                    emptyTiles.Add(new Vector2Int(x, y));

            ShuffleList(emptyTiles);

            // Track placed positions per entry (for spacing checks)
            var placedPositions = new Dictionary<string, List<Vector2Int>>();
            var remaining = new Dictionary<string, int>();
            foreach (var (entry, _) in scattered)
            {
                placedPositions[entry.environmentName] = new List<Vector2Int>();
                remaining[entry.environmentName] = budgets[entry.environmentName];
            }

            int totalPlaced = 0;

            foreach (var pos in emptyTiles)
            {
                if (totalPlaced >= totalScatteredBudget) break;

                // Build a shuffled candidate list weighted by spawnWeight (only entries with budget left)
                // This ensures that if the first pick fails spacing, we try others for this tile
                var candidates = new List<EnvironmentSpawnEntry>();
                foreach (var (entry, _) in scattered)
                    if (remaining[entry.environmentName] > 0)
                        candidates.Add(entry);

                if (candidates.Count == 0) break;

                // Weighted shuffle: build weighted list, then pick in order
                // Use weighted random selection but try ALL candidates if first pick fails
                WeightedShuffle(candidates);

                bool placed = false;
                foreach (var candidate in candidates)
                {
                    // Spacing check
                    if (candidate.minSpacing > 0 &&
                        IsTooClose(pos.x, pos.y, placedPositions[candidate.environmentName], candidate.minSpacing))
                        continue;

                    planGrid[pos.x, pos.y] = candidate.environmentName;
                    placedPositions[candidate.environmentName].Add(pos);
                    remaining[candidate.environmentName]--;
                    totalPlaced++;
                    placed = true;
                    break;
                }
                // If no candidate passed spacing, tile stays empty — that's fine
            }

            // Log per-entry results
            foreach (var (entry, _) in scattered)
            {
                int budget = budgets[entry.environmentName];
                int placed = placedPositions[entry.environmentName].Count;
                Debug.Log($"[MapGenV2] Scattered '{entry.environmentName}': {placed} tiles (budget was {budget})");
            }
        }

        /// <summary>
        /// Weighted shuffle: reorders list so higher-weight entries tend to come first,
        /// but with randomness. Uses weighted random selection without replacement.
        /// </summary>
        void WeightedShuffle(List<EnvironmentSpawnEntry> list)
        {
            for (int i = 0; i < list.Count - 1; i++)
            {
                // Weighted pick from remaining items [i..end]
                float totalW = 0f;
                for (int j = i; j < list.Count; j++)
                    totalW += list[j].spawnWeight;

                float roll = (float)(rng.NextDouble() * totalW);
                float cumulative = 0f;
                int picked = i;
                for (int j = i; j < list.Count; j++)
                {
                    cumulative += list[j].spawnWeight;
                    if (roll < cumulative) { picked = j; break; }
                }

                // Swap picked item to position i
                if (picked != i)
                {
                    var tmp = list[i];
                    list[i] = list[picked];
                    list[picked] = tmp;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Spawn Phase
        // ─────────────────────────────────────────────────────────────────

        void SpawnCenter()
        {
            if (string.IsNullOrEmpty(centerEnvironmentName)) return;

            EnvironmentData envData = environmentDatabase.GetByName(centerEnvironmentName);
            if (envData == null || envData.prefab == null)
            {
                Debug.LogWarning($"[MapGenV2] Center '{centerEnvironmentName}' not found in database or has no prefab.");
                return;
            }

            Vector3 worldPos = GridManager.Instance.GridToWorldPosition(center.x, center.y);
            worldPos.y += 0.01f; // Lift slightly to avoid shadow clipping with ground plane
            GameObject obj = Instantiate(envData.prefab, worldPos, Quaternion.identity);
            obj.name = envData.assetName; // Match InteractionRegistry lookup key

            if (obj.TryGetComponent<ResourceNode>(out var node))
            {
                node.hp              = envData.hp;
                node.lootHpCost      = envData.lootHpCost;
                node.lootYield       = envData.lootYield;
                node.lootBonusAmount = envData.lootYield;
                node.isInteractable  = InteractionRegistry.Instance != null
                                       ? InteractionRegistry.Instance.IsUnlocked(envData.assetName) : true;
                node.resourceType    = envData.lootResourceType != ResourceType.None
                                       ? envData.lootResourceType
                                       : GuessResourceType(centerEnvironmentName);
                node.Initialize(center.x, center.y);
                node.tier = 1;
                NodeManager.Instance?.RegisterNode(node);
            }

            if (GridEntityManager.Instance != null)
                GridEntityManager.Instance.AttachFromEnvironmentData(obj, envData);

            GridManager.Instance?.PlaceUnit(center.x, center.y, obj, CellState.Resource);
            TriggerAppearAnimation(obj);

            Debug.Log($"[MapGenV2] Center '{centerEnvironmentName}' at ({center.x},{center.y})");
        }

        void SpawnAll()
        {
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                string envName = planGrid[x, y];
                if (envName == null || envName.StartsWith("__")) continue;  // skip __center__, __footprint__, etc.
                if (envName.StartsWith(UNIT_PREFIX)) continue; // Units are spawned by SpawnAllUnits()

                EnvironmentData envData = environmentDatabase.GetByName(envName);
                if (envData == null || envData.prefab == null)
                {
                    Debug.LogWarning($"[MapGenV2] No prefab for '{envName}' at ({x},{y}).");
                    continue;
                }

                Vector3 worldPos = GridManager.Instance.GridToWorldPosition(x, y);
                worldPos.y += 0.01f; // Lift slightly to avoid shadow clipping with ground plane
                // Random 90° rotation for visual variety
                Quaternion randomRot = Quaternion.Euler(0f, 90f * rng.Next(4), 0f);
                GameObject obj = Instantiate(envData.prefab, worldPos, randomRot);
                obj.name = envData.assetName;

                // ── ResourceNode ─────────────────────────────────────
                if (obj.TryGetComponent<ResourceNode>(out var node))
                {
                    node.hp              = envData.hp;
                    node.lootHpCost      = envData.lootHpCost;
                    node.lootYield       = envData.lootYield;
                    node.lootBonusAmount = envData.lootYield;
                    node.isInteractable  = InteractionRegistry.Instance != null
                                           ? InteractionRegistry.Instance.IsUnlocked(envData.assetName) : true;
                    node.resourceType    = envData.lootResourceType != ResourceType.None
                                           ? envData.lootResourceType
                                           : GuessResourceType(envName);
                    node.Initialize(x, y);

                    float dist = Vector2Int.Distance(new Vector2Int(x, y), center);
                    node.tier = dist < 10f ? 1
                              : dist < 20f ? (rng.NextDouble() < 0.5 ? 1 : 2)
                              :              (rng.NextDouble() < 0.4 ? 2 : 3);

                    NodeManager.Instance?.RegisterNode(node);
                }

                // ── Fog visibility (skip when fog is off — objects stay visible)
                if (enableFog)
                {
                    var fogHideable = obj.AddComponent<FogHideable>();
                    fogHideable.Initialize(x, y);
                }

                // ── Entity components ────────────────────────────────
                if (GridEntityManager.Instance != null)
                    GridEntityManager.Instance.AttachFromEnvironmentData(obj, envData);

                // ── Appear animation (only if visible) ───────────────
                if (obj.activeSelf)
                    TriggerAppearAnimation(obj);

                // ── Grid registration ────────────────────────────────
                GridManager.Instance?.PlaceUnit(x, y, obj, CellState.Resource);
            }
        }

        /// <summary>
        /// Staggered version of SpawnAll — yields every N objects to avoid frame spikes.
        /// </summary>
        System.Collections.IEnumerator SpawnAllStaggered()
        {
            const int BATCH_SIZE = 25; // Spawn this many per frame
            int count = 0;

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                string envName = planGrid[x, y];
                if (envName == null || envName == "__center__") continue;
                if (envName.StartsWith(UNIT_PREFIX)) continue;

                EnvironmentData envData = environmentDatabase.GetByName(envName);
                if (envData == null || envData.prefab == null) continue;

                Vector3 worldPos = GridManager.Instance.GridToWorldPosition(x, y);
                worldPos.y += 0.01f;
                Quaternion randomRot = Quaternion.Euler(0f, 90f * rng.Next(4), 0f);
                GameObject obj = Instantiate(envData.prefab, worldPos, randomRot);
                obj.name = envData.assetName;

                if (obj.TryGetComponent<ResourceNode>(out var node))
                {
                    node.hp              = envData.hp;
                    node.lootHpCost      = envData.lootHpCost;
                    node.lootYield       = envData.lootYield;
                    node.lootBonusAmount = envData.lootYield;
                    node.isInteractable  = InteractionRegistry.Instance != null
                                           ? InteractionRegistry.Instance.IsUnlocked(envData.assetName) : true;
                    node.resourceType    = envData.lootResourceType != ResourceType.None
                                           ? envData.lootResourceType
                                           : GuessResourceType(envName);
                    node.Initialize(x, y);

                    float dist = Vector2Int.Distance(new Vector2Int(x, y), center);
                    node.tier = dist < 10f ? 1
                              : dist < 20f ? (rng.NextDouble() < 0.5 ? 1 : 2)
                              :              (rng.NextDouble() < 0.4 ? 2 : 3);

                    NodeManager.Instance?.RegisterNode(node);
                }

                if (enableFog)
                {
                    var fogHideable = obj.AddComponent<FogHideable>();
                    fogHideable.Initialize(x, y);
                }

                if (GridEntityManager.Instance != null)
                    GridEntityManager.Instance.AttachFromEnvironmentData(obj, envData);

                if (obj.activeSelf)
                    TriggerAppearAnimation(obj);

                GridManager.Instance?.PlaceUnit(x, y, obj, CellState.Resource);

                count++;
                if (count % BATCH_SIZE == 0)
                    yield return null; // Breathe — let the frame render
            }
        }

        /// <summary>
        /// Staggered version of SpawnAllUnits.
        /// </summary>
        System.Collections.IEnumerator SpawnAllUnitsStaggered()
        {
            if (unitDatabase == null) yield break;

            const int BATCH_SIZE = 15; // Units are heavier (actor + health), smaller batches
            int unitCount = 0;

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                string planName = planGrid[x, y];
                if (planName == null || !planName.StartsWith(UNIT_PREFIX)) continue;

                string unitName = planName.Substring(UNIT_PREFIX.Length);
                UnitData unitData = unitDatabase.GetByName(unitName);
                if (unitData == null || unitData.prefab == null) continue;

                // Build a GridShape for this unit — use legacy gridSize since UnitData
                // is out of scope for the full GridShape migration.
                GridShape unitShape = GridShape.Rectangle(
                    Mathf.Max(1, unitData.gridSize.x),
                    Mathf.Max(1, unitData.gridSize.y));

                // Position at footprint center (handles multi-cell units correctly)
                Vector3 worldPos = GridManager.Instance.GetOffsetFootprintCenter(x, y, unitShape, 0);
                worldPos.y += 0.01f;
                Quaternion randomRot = Quaternion.Euler(0f, 90f * rng.Next(4), 0f);
                GameObject obj = Instantiate(unitData.prefab, worldPos, randomRot);
                obj.name = unitData.assetName;

                if (unitData.visualScale != 1f)
                    obj.transform.localScale = Vector3.one * unitData.visualScale;

                var furniture = obj.GetComponent<FurnitureObject>();
                if (furniture == null)
                    furniture = obj.AddComponent<FurnitureObject>();
                furniture.GridX = x;
                furniture.GridY = y;
                furniture.Shape = unitShape;
                furniture.CurrentRotation = 0;

                if (unitData.lootResourceType != ResourceType.None)
                {
                    ResourceNode node = obj.GetComponent<ResourceNode>();
                    if (node == null)
                        node = obj.AddComponent<ResourceNode>();

                    node.hp              = unitData.hp;
                    node.lootHpCost      = unitData.lootHpCost;
                    node.lootYield       = unitData.lootYield;
                    node.lootBonusAmount = unitData.lootYield;
                    node.resourceType    = unitData.lootResourceType;
                    node.isInteractable  = InteractionRegistry.Instance != null
                                           ? InteractionRegistry.Instance.IsUnlocked(unitData.assetName) : false;
                    node.Initialize(x, y);
                }

                if (enableFog)
                {
                    var fogHideable = obj.AddComponent<FogHideable>();
                    fogHideable.Initialize(x, y);
                }

                if (GridEntityManager.Instance != null)
                    GridEntityManager.Instance.AttachFromUnitData(obj, unitData);

                if (obj.activeSelf)
                    TriggerAppearAnimation(obj);

                CellState cellState = unitData.isEnemy ? CellState.EnemyUnit : CellState.PlayerUnit;
                GridManager.Instance?.PlaceWithOffsets(x, y, unitShape, 0, obj, cellState);

                unitCount++;
                if (unitCount % BATCH_SIZE == 0)
                    yield return null;
            }

            if (unitCount > 0)
                Debug.Log($"[MapGenV2] Spawned {unitCount} units on map");
        }

        void TriggerAppearAnimation(GameObject obj)
        {
            Transform animHolder = obj.transform.Find("AnimatorHolder");
            Animator animator = animHolder != null
                ? animHolder.GetComponent<Animator>()
                : obj.GetComponentInChildren<Animator>();

            if (animator != null)
                animator.SetTrigger("appear");
        }

        // ─────────────────────────────────────────────────────────────────
        // Unit Planning + Spawning
        // ─────────────────────────────────────────────────────────────────

        // Unit plan entries are stored with a "unit:" prefix in planGrid
        // so they don't collide with environment names.
        const string UNIT_PREFIX = "unit:";

        void PlaceUnitClusters(UnitSpawnEntry entry, int tileBudget)
        {
            if (tileBudget <= 0) return;
            // Reuse the same cluster logic but with unit prefix
            string planName = UNIT_PREFIX + entry.unitName;
            float frag = Mathf.Clamp01(entry.fragmentation);
            float spread = Mathf.Clamp01(entry.clusterSpread);
            int clusterCount = Mathf.Max(2, Mathf.RoundToInt(Mathf.Lerp(2f, 8f, frag)));

            int placed = 0;
            Vector2Int[] dirs = {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1)
            };

            for (int c = 0; c < clusterCount + 5 && placed < tileBudget; c++)
            {
                int targetSize = Mathf.Max(1, (tileBudget - placed) / Mathf.Max(1, clusterCount - c));

                // Find seed on empty tile
                Vector2Int seed = Vector2Int.zero;
                bool found = false;
                for (int a = 0; a < 80; a++)
                {
                    int sx = rng.Next(width), sy = rng.Next(height);
                    if (planGrid[sx, sy] == null && !IsInClearing(sx, sy))
                    {
                        seed = new Vector2Int(sx, sy);
                        found = true;
                        break;
                    }
                }
                if (!found) break;

                // BFS expansion
                var frontier = new List<Vector2Int> { seed };
                var visited = new HashSet<Vector2Int> { seed };
                planGrid[seed.x, seed.y] = planName;
                int grown = 1;
                placed++;

                while (grown < targetSize && frontier.Count > 0)
                {
                    int fi = rng.Next(frontier.Count);
                    var current = frontier[fi];
                    frontier.RemoveAt(fi);

                    foreach (var d in dirs)
                    {
                        if (grown >= targetSize || placed >= tileBudget) break;
                        var nb = current + d;
                        if (nb.x < 0 || nb.x >= width || nb.y < 0 || nb.y >= height) continue;
                        if (visited.Contains(nb)) continue;
                        visited.Add(nb);
                        if (planGrid[nb.x, nb.y] != null || IsInClearing(nb.x, nb.y)) continue;

                        if ((float)rng.NextDouble() > spread) continue;

                        planGrid[nb.x, nb.y] = planName;
                        frontier.Add(nb);
                        grown++;
                        placed++;
                    }
                }
            }

            Debug.Log($"[MapGenV2] Clustered unit '{entry.unitName}': {placed} tiles (budget was {tileBudget})");
        }

        void PlaceUnitEdges(UnitSpawnEntry entry, int tileBudget)
        {
            if (tileBudget <= 0) return;
            string planName = UNIT_PREFIX + entry.unitName;

            // Check for both environment and unit targets
            string borderTarget = entry.edgeBorderOf;
            string unitBorderTarget = UNIT_PREFIX + borderTarget;

            var candidates = new List<Vector2Int>();
            Vector2Int[] dirs = {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1)
            };

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (planGrid[x, y] != null) continue;
                if (IsInClearing(x, y)) continue;

                bool adjacent = false;
                foreach (var d in dirs)
                {
                    int nx = x + d.x, ny = y + d.y;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                    string cell = planGrid[nx, ny];
                    if (cell == borderTarget || cell == unitBorderTarget)
                    {
                        adjacent = true;
                        break;
                    }
                }
                if (adjacent) candidates.Add(new Vector2Int(x, y));
            }

            ShuffleList(candidates);
            int placed = Mathf.Min(tileBudget, candidates.Count);
            for (int i = 0; i < placed; i++)
                planGrid[candidates[i].x, candidates[i].y] = planName;

            Debug.Log($"[MapGenV2] Edge unit '{entry.unitName}' near '{borderTarget}': {placed} tiles (budget was {tileBudget})");
        }

        void PlaceUnitScattered(List<(UnitSpawnEntry entry, UnitData data)> scattered, Dictionary<string, int> budgets)
        {
            int totalBudget = 0;
            foreach (var (entry, _) in scattered)
                totalBudget += budgets.ContainsKey(entry.unitName) ? budgets[entry.unitName] : 0;
            if (totalBudget <= 0) return;

            var emptyTiles = new List<Vector2Int>();
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (planGrid[x, y] == null && !IsInClearing(x, y))
                    emptyTiles.Add(new Vector2Int(x, y));

            ShuffleList(emptyTiles);

            var placedPositions = new Dictionary<string, List<Vector2Int>>();
            var remaining = new Dictionary<string, int>();
            foreach (var (entry, _) in scattered)
            {
                placedPositions[entry.unitName] = new List<Vector2Int>();
                remaining[entry.unitName] = budgets.ContainsKey(entry.unitName) ? budgets[entry.unitName] : 0;
            }

            int totalPlaced = 0;
            foreach (var pos in emptyTiles)
            {
                if (totalPlaced >= totalBudget) break;

                // Try each unit type weighted
                var candidates = scattered.Where(s => remaining[s.entry.unitName] > 0).ToList();
                if (candidates.Count == 0) break;

                // Simple weighted pick
                float tw = candidates.Sum(c => c.entry.spawnWeight);
                float roll = (float)(rng.NextDouble() * tw);
                float cum = 0f;
                (UnitSpawnEntry entry, UnitData data) picked = candidates[0];
                foreach (var c in candidates)
                {
                    cum += c.entry.spawnWeight;
                    if (roll < cum) { picked = c; break; }
                }

                // Spacing check
                if (picked.entry.minSpacing > 0 &&
                    IsTooClose(pos.x, pos.y, placedPositions[picked.entry.unitName], picked.entry.minSpacing))
                    continue;

                planGrid[pos.x, pos.y] = UNIT_PREFIX + picked.entry.unitName;
                placedPositions[picked.entry.unitName].Add(pos);
                remaining[picked.entry.unitName]--;
                totalPlaced++;
            }

            foreach (var (entry, _) in scattered)
            {
                int budget = budgets.ContainsKey(entry.unitName) ? budgets[entry.unitName] : 0;
                int placed = placedPositions[entry.unitName].Count;
                Debug.Log($"[MapGenV2] Scattered unit '{entry.unitName}': {placed} tiles (budget was {budget})");
            }
        }

        /// <summary>
        /// Spawns all planned unit entries from the planGrid.
        /// </summary>
        void SpawnAllUnits()
        {
            if (unitDatabase == null) return;

            int unitCount = 0;
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                string planName = planGrid[x, y];
                if (planName == null || !planName.StartsWith(UNIT_PREFIX)) continue;

                string unitName = planName.Substring(UNIT_PREFIX.Length);
                UnitData unitData = unitDatabase.GetByName(unitName);
                if (unitData == null || unitData.prefab == null)
                {
                    Debug.LogWarning($"[MapGenV2] No prefab for unit '{unitName}' at ({x},{y}).");
                    continue;
                }

                Vector3 worldPos = GridManager.Instance.GridToWorldPosition(x, y);
                worldPos.y += 0.01f;

                // Random facing for visual variety
                Quaternion randomRot = Quaternion.Euler(0f, 90f * rng.Next(4), 0f);
                GameObject obj = Instantiate(unitData.prefab, worldPos, randomRot);
                obj.name = unitData.assetName;

                // Scale from database
                if (unitData.visualScale != 1f)
                    obj.transform.localScale = Vector3.one * unitData.visualScale;

                // ── FurnitureObject for grid position tracking ───────
                // GridEntityActor relies on FurnitureObject.GridX/GridY for
                // position tracking, scanning, and movement.
                var furniture = obj.GetComponent<FurnitureObject>();
                if (furniture == null)
                    furniture = obj.AddComponent<FurnitureObject>();
                furniture.GridX = x;
                furniture.GridY = y;
                furniture.GridSize = unitData.gridSize;

                // ── ResourceNode for loot ─────────────────────────────
                if (unitData.lootResourceType != ResourceType.None)
                {
                    ResourceNode node = obj.GetComponent<ResourceNode>();
                    if (node == null)
                        node = obj.AddComponent<ResourceNode>();

                    node.hp              = unitData.hp;
                    node.lootHpCost      = unitData.lootHpCost;
                    node.lootYield       = unitData.lootYield;
                    node.lootBonusAmount = unitData.lootYield;
                    node.resourceType    = unitData.lootResourceType;
                    node.isInteractable  = InteractionRegistry.Instance != null
                                           ? InteractionRegistry.Instance.IsUnlocked(unitData.assetName) : false;
                    node.Initialize(x, y);
                }

                // ── Fog visibility ────────────────────────────────────
                if (enableFog)
                {
                    var fogHideable = obj.AddComponent<FogHideable>();
                    fogHideable.Initialize(x, y);
                }

                // ── Entity components (health, actor, loot) ───────────
                if (GridEntityManager.Instance != null)
                    GridEntityManager.Instance.AttachFromUnitData(obj, unitData);

                // ── Appear animation ──────────────────────────────────
                if (obj.activeSelf)
                    TriggerAppearAnimation(obj);

                // ── Grid registration (as EnemyUnit) ──────────────────
                CellState cellState = unitData.isEnemy ? CellState.EnemyUnit : CellState.PlayerUnit;
                GridManager.Instance?.PlaceUnit(x, y, obj, cellState);

                unitCount++;
            }

            if (unitCount > 0)
                Debug.Log($"[MapGenV2] Spawned {unitCount} units on map");
        }

        // ─────────────────────────────────────────────────────────────────
        // Corruption Entities — Planning
        // ─────────────────────────────────────────────────────────────────

        // Corruption entities use a "corruption:" prefix in planGrid so they
        // don't collide with environment names or the "unit:" prefix.
        const string CORRUPTION_PREFIX = "corruption:";

        /// <summary>
        /// Syncs corruptionSpawnEntries from UnitDatabase entries with type == Corruption,
        /// adding an entry for each asset not yet represented. Called automatically when
        /// the list is empty or via the editor Sync button.
        /// </summary>
        public void SyncCorruptionSpawnEntries()
        {
            if (unitDatabase == null) return;

            // Remove invalid entries (empty name, no longer in database, or inactive)
            var validNames = new System.Collections.Generic.HashSet<string>();
            foreach (var data in unitDatabase.GetByType(LittleCafe.GameUnitType.Corruption))
            {
                if (data.active && data.isMapGenerated)
                    validNames.Add(data.assetName);
            }
            corruptionSpawnEntries.RemoveAll(e =>
                string.IsNullOrEmpty(e.entityName) || !validNames.Contains(e.entityName));

            // Add missing entries + update prefabs on existing ones
            foreach (var data in unitDatabase.GetByType(LittleCafe.GameUnitType.Corruption))
            {
                if (!data.active || !data.isMapGenerated) continue;
                var existing = corruptionSpawnEntries.Find(e => e.entityName == data.assetName);
                if (existing != null)
                {
                    existing.prefab = data.prefab;
                }
                else
                {
                    corruptionSpawnEntries.Add(new CorruptionSpawnEntry
                    {
                        entityName = data.assetName,
                        prefab     = data.prefab
                    });
                }
            }
        }

        /// <summary>
        /// Plans corruption entity placement into planGrid.
        /// Called after PlaceAllEntries() so corruption entities respect
        /// already-placed environment and unit cells.
        /// </summary>
        void PlaceCorruptionEntities()
        {
            if (corruptionSpawnEntries == null) return;

            foreach (var entry in corruptionSpawnEntries)
            {
                if (entry.spawnCount <= 0) continue;

                if (entry.prefab == null)
                {
                    Debug.LogWarning($"[MapGenV2] Corruption entry '{entry.entityName}' has no prefab — skipping.");
                    continue;
                }

                string planName = CORRUPTION_PREFIX + entry.entityName;

                switch (entry.spawnMode)
                {
                    case SpawnMode.Scattered:
                        PlaceCorruptionScattered(entry, planName);
                        break;
                    case SpawnMode.Clustered:
                        PlaceCorruptionClustered(entry, planName);
                        break;
                    case SpawnMode.Edge:
                        PlaceCorruptionEdge(entry, planName);
                        break;
                }
            }
        }

        void PlaceCorruptionScattered(CorruptionSpawnEntry entry, string planName)
        {
            // Build candidate list: empty cells, outside clearing, beyond minDistFromCenter
            var candidates = new List<Vector2Int>();
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (planGrid[x, y] != null) continue;
                if (IsInClearing(x, y)) continue;

                int dx = Mathf.Abs(x - center.x);
                int dy = Mathf.Abs(y - center.y);
                if (Mathf.Max(dx, dy) < entry.minDistFromCenter) continue;

                candidates.Add(new Vector2Int(x, y));
            }

            // Fisher-Yates shuffle for determinism with the map's seeded RNG
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = candidates[i]; candidates[i] = candidates[j]; candidates[j] = tmp;
            }

            // Place with Chebyshev spacing constraint
            var placed = new List<Vector2Int>();
            foreach (var pos in candidates)
            {
                if (placed.Count >= entry.spawnCount) break;

                bool tooClose = false;
                foreach (var p in placed)
                {
                    int dx = Mathf.Abs(pos.x - p.x);
                    int dy = Mathf.Abs(pos.y - p.y);
                    if (Mathf.Max(dx, dy) < entry.minSpacing) { tooClose = true; break; }
                }
                if (tooClose) continue;

                planGrid[pos.x, pos.y] = planName;
                placed.Add(pos);
            }

            if (placed.Count < entry.spawnCount)
                Debug.LogWarning($"[MapGenV2] Corruption '{entry.entityName}': only placed {placed.Count}/{entry.spawnCount} (Scattered) — not enough valid candidates.");
            else
                Debug.Log($"[MapGenV2] Corruption '{entry.entityName}': planned {placed.Count} (Scattered).");
        }

        void PlaceCorruptionClustered(CorruptionSpawnEntry entry, string planName)
        {
            Vector2Int[] dirs = {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1)
            };

            // Reuse the existing BFS cluster logic, treating spawnCount as the tile budget.
            int tileBudget = entry.spawnCount;
            if (tileBudget <= 0) return;

            float frag   = Mathf.Clamp01(entry.fragmentation);
            float spread = Mathf.Clamp01(entry.clusterSpread);
            int clusterCount = Mathf.Max(2, Mathf.RoundToInt(Mathf.Lerp(2f, 8f, frag)));
            int tilesPerCluster = Mathf.Max(1, tileBudget / clusterCount);
            int placed = 0;

            for (int c = 0; c < clusterCount && placed < tileBudget; c++)
            {
                Vector2Int seed = Vector2Int.zero;
                bool found = false;
                for (int a = 0; a < 80; a++)
                {
                    int sx = rng.Next(width), sy = rng.Next(height);
                    int ddx = Mathf.Abs(sx - center.x);
                    int ddy = Mathf.Abs(sy - center.y);
                    if (planGrid[sx, sy] == null && !IsInClearing(sx, sy)
                        && Mathf.Max(ddx, ddy) >= entry.minDistFromCenter)
                    {
                        seed = new Vector2Int(sx, sy);
                        found = true;
                        break;
                    }
                }
                if (!found) continue;

                // BFS blob
                var queue   = new Queue<Vector2Int>();
                var visited = new HashSet<Vector2Int>();
                queue.Enqueue(seed);
                visited.Add(seed);
                int grown = 0;

                while (queue.Count > 0 && grown < tilesPerCluster && placed < tileBudget)
                {
                    var current = queue.Dequeue();
                    if (planGrid[current.x, current.y] == null && !IsInClearing(current.x, current.y))
                    {
                        planGrid[current.x, current.y] = planName;
                        placed++;
                        grown++;
                    }
                    foreach (var d in dirs)
                    {
                        var nb = current + d;
                        if (nb.x < 0 || nb.x >= width || nb.y < 0 || nb.y >= height) continue;
                        if (visited.Contains(nb)) continue;
                        visited.Add(nb);
                        if (planGrid[nb.x, nb.y] != null || IsInClearing(nb.x, nb.y)) continue;
                        if ((float)rng.NextDouble() > spread) continue;
                        queue.Enqueue(nb);
                    }
                }
            }

            Debug.Log($"[MapGenV2] Corruption '{entry.entityName}': planned {placed}/{tileBudget} (Clustered).");
        }

        void PlaceCorruptionEdge(CorruptionSpawnEntry entry, string planName)
        {
            if (string.IsNullOrEmpty(entry.edgeBorderOf))
            {
                Debug.LogWarning($"[MapGenV2] Corruption '{entry.entityName}' uses Edge mode but edgeBorderOf is empty — falling back to Scattered.");
                PlaceCorruptionScattered(entry, planName);
                return;
            }

            Vector2Int[] dirs = {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1)
            };

            // Collect empty cells adjacent to the border target
            var edgeCells = new List<Vector2Int>();
            string borderTarget      = entry.edgeBorderOf;
            string unitBorderTarget  = UNIT_PREFIX + borderTarget;
            string corrBorderTarget  = CORRUPTION_PREFIX + borderTarget;

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (planGrid[x, y] != null) continue;
                if (IsInClearing(x, y)) continue;
                int ddx = Mathf.Abs(x - center.x);
                int ddy = Mathf.Abs(y - center.y);
                if (Mathf.Max(ddx, ddy) < entry.minDistFromCenter) continue;

                bool adjacent = false;
                foreach (var d in dirs)
                {
                    int nx = x + d.x, ny = y + d.y;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                    string nb = planGrid[nx, ny];
                    if (nb == borderTarget || nb == unitBorderTarget || nb == corrBorderTarget)
                    {
                        adjacent = true;
                        break;
                    }
                }
                if (adjacent) edgeCells.Add(new Vector2Int(x, y));
            }

            ShuffleList(edgeCells);

            var placed = new List<Vector2Int>();
            foreach (var pos in edgeCells)
            {
                if (placed.Count >= entry.spawnCount) break;

                bool tooClose = false;
                foreach (var p in placed)
                {
                    int ddx = Mathf.Abs(pos.x - p.x);
                    int ddy = Mathf.Abs(pos.y - p.y);
                    if (Mathf.Max(ddx, ddy) < entry.minSpacing) { tooClose = true; break; }
                }
                if (tooClose) continue;

                planGrid[pos.x, pos.y] = planName;
                placed.Add(pos);
            }

            Debug.Log($"[MapGenV2] Corruption '{entry.entityName}': planned {placed.Count}/{entry.spawnCount} (Edge near '{borderTarget}').");
        }

        // ─────────────────────────────────────────────────────────────────
        // Corruption Entities — Spawning
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Reads all "corruption:" planGrid entries and instantiates the corresponding
        /// prefabs. Stats are serialized on the CorruptionHeart prefab itself.
        /// </summary>
        System.Collections.IEnumerator SpawnAllCorruptionEntitiesStaggered()
        {
            const int BATCH_SIZE = 10;
            int spawnCount = 0;

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                string planName = planGrid[x, y];
                if (planName == null || !planName.StartsWith(CORRUPTION_PREFIX)) continue;

                string entityName = planName.Substring(CORRUPTION_PREFIX.Length);
                var entry = corruptionSpawnEntries.Find(e => e.entityName == entityName);
                if (entry == null || entry.prefab == null) continue;

                Vector3 worldPos = GridManager.Instance.GridToWorldPosition(x, y);
                worldPos.y += 0.01f;
                GameObject obj = Instantiate(entry.prefab, worldPos, Quaternion.identity);
                obj.name = $"{entityName}_{spawnCount}";

                // Stats are serialized on the prefab's CorruptionHeart component — just set grid position,
                // inject UnitDatabase so the heart can spawn spikes, and pass the initial corruption radius
                var heart = obj.GetComponent<LittleCafe.CorruptionHeart>();
                if (heart != null)
                {
                    heart.GridPosition = new Vector2Int(x, y);
                    heart.UnitDatabase = unitDatabase;
                    heart.InitialCorruptionRadius = entry.initialCorruptionRadius;
                }

                if (enableFog)
                {
                    var fogHideable = obj.AddComponent<FogHideable>();
                    fogHideable.Initialize(x, y);
                }

                GridManager.Instance?.PlaceUnit(x, y, obj, CellState.EnemyUnit);

                spawnCount++;
                if (spawnCount % BATCH_SIZE == 0)
                    yield return null;
            }

            if (spawnCount > 0)
                Debug.Log($"[MapGenV2] Spawned {spawnCount} corruption entities.");
        }

        // ─────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────

        bool IsInClearing(int x, int y)
        {
            if (x == center.x && y == center.y) return false;

            // Chebyshev distance for a square clearing zone
            // clearingRadius=1 → 3x3 clear area (1 tile around center in all directions)
            int dx = Mathf.Abs(x - center.x);
            int dy = Mathf.Abs(y - center.y);
            return Mathf.Max(dx, dy) <= clearingRadius;
        }

        bool IsTooClose(int x, int y, List<Vector2Int> placed, int minSpacing)
        {
            // Spacing is a SOFT guideline, not a hard rule.
            // Higher override chance = more natural clumping + variation.
            // Closer tiles have slightly lower override chance to prevent
            // everything piling on top of each other.
            int sqThreshold = minSpacing * minSpacing;
            foreach (var p in placed)
            {
                int dx = x - p.x;
                int dy = y - p.y;
                int sqDist = dx * dx + dy * dy;
                if (sqDist <= sqThreshold)
                {
                    // Scale override chance by how close we are:
                    // Adjacent (dist=1): 30% chance to allow
                    // At spacing boundary: 45% chance to allow
                    float t = sqThreshold > 1 ? (float)sqDist / sqThreshold : 0f;
                    float overrideChance = Mathf.Lerp(0.30f, 0.45f, t);
                    if ((float)rng.NextDouble() < overrideChance)
                        continue; // break the rule this time
                    return true;
                }
            }
            return false;
        }

        void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        static ResourceType GuessResourceType(string envName)
        {
            if (envName == null) return ResourceType.None;
            string lower = envName.ToLowerInvariant();
            if (lower.Contains("tree") || lower.Contains("wood"))  return ResourceType.Wood;
            if (lower.Contains("gold") || lower.Contains("mine"))  return ResourceType.Gold;
            if (lower.Contains("farm") || lower.Contains("food"))  return ResourceType.Food;
            if (lower.Contains("rock") || lower.Contains("stone")) return ResourceType.Stone;
            if (lower.Contains("water") || lower.Contains("lake") || lower.Contains("river")) return ResourceType.Water;
            if (lower.Contains("clay") || lower.Contains("mud"))     return ResourceType.Clay;
            if (lower.Contains("flower") || lower.Contains("flora")) return ResourceType.Flowers;
            return ResourceType.None;
        }
    }
}
