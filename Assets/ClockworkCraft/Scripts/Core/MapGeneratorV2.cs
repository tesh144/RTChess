// SpawnMode, EnvironmentSpawnEntry, UnitSpawnEntry, CorruptionSpawnEntry moved to SpawnEntryData.cs
#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System.Collections.Generic;

using ClockworkGrid;   // GridManager, FogManager, CellState
using LittleCafe;      // EnvironmentDatabase, EnvironmentData, GridEntityManager

namespace ClockworkCraft
{
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
        public ClockworkGrid.PlacementCostsDatabase economyBalanceConfig;

        [Header("Bubble Prefab")]
        [Tooltip("WorldCanvas_Popups prefab — used for POI bubbles and as the default for both Insert and Collect building bubbles. Individual overrides can be set directly on BuildingProductionManager.")]
        public GameObject buildingBubblePrefab;

        [Header("Center")]
        [Tooltip("EnvironmentDatabase entry to place at dead center.")]
        public string centerEnvironmentName = "Goldmine";

        [Tooltip("Cardinal-only clearing radius around center goldmine. 2 = cross shape extending 2 tiles N/S/E/W. Separate from per-entry clearFromCenter.")]
        [Min(0)]
        public int clearCenterCardinal = 2;

        [Header("Environment Desaturation")]
        [Tooltip("Saturation amount before first worker interaction (0 = grayscale, 1 = full color)")]
        [Range(0f, 1f)]
        public float defaultEnvironmentDesaturatedValue = 0.5f;

        [Tooltip("Saturation amount after first worker interaction")]
        [Range(0f, 1f)]
        public float defaultEnvironmentFullColorValue = 1f;

        [Tooltip("Seconds for desaturation -> full-color transition")]
        [Min(0f)]
        public float defaultEnvironmentTransitionDuration = 0.3f;

        [Tooltip("Enable desaturation and colorization behavior on units (e.g. animals)")]
        public bool enableUnitDesaturation = false;

        // Drawn by custom editor — no [Header] to avoid duplicates
        [HideInInspector] [Range(0.1f, 3f)] public float mapDensity = 1.0f;
        [HideInInspector] public List<EnvironmentSpawnEntry> spawnEntries = new List<EnvironmentSpawnEntry>();
        [HideInInspector] public List<UnitSpawnEntry> unitSpawnEntries = new List<UnitSpawnEntry>();
        [HideInInspector] public List<CorruptionSpawnEntry> corruptionSpawnEntries = new List<CorruptionSpawnEntry>();

        // ─────────────────────────────────────────────────────────────────
        // Internal state
        // ─────────────────────────────────────────────────────────────────

        private string[,] planGrid;
        private string[,] onTopPlanGrid;
        private int width;
        private int height;
        private Vector2Int center;
        private System.Random rng;

        /// <summary>All connected same-type groups found after placement. Public read-only for POIManager etc.</summary>
        public IReadOnlyList<EnvironmentGathering> DetectedGatherings => detectedGatherings;
        private readonly List<EnvironmentGathering> detectedGatherings = new List<EnvironmentGathering>();

        // ─────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // Prevent GridManager from auto-initializing in Start() with its default
            // inspector dimensions. We'll call InitializeGrid(mapWidth, mapHeight) later
            // with the correct size. Awake() is guaranteed to run before any Start().
            var gm = FindFirstObjectByType<GridManager>();
            gm?.SuppressAutoInit();
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
            SceneBootstrapper.EnsureAll(
                currencyDatabase, environmentDatabase, unitDatabase,
                workerDatabase, buildingDatabase, economyBalanceConfig,
                buildingBubblePrefab);
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
            DeckSetup.Initialize(buildingDatabase, workerDatabase, environmentDatabase);
        }

        public void SyncSpawnEntries()
        {
            if (environmentDatabase != null)
                spawnEntries = SpawnEntrySyncer.SyncEnvironmentEntries(spawnEntries, environmentDatabase);
            SyncUnitSpawnEntries();
        }

        public void SyncUnitSpawnEntries()
        {
            if (unitDatabase != null)
                unitSpawnEntries = SpawnEntrySyncer.SyncUnitEntries(unitSpawnEntries, unitDatabase);
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

            // ── Always sync spawn entries from databases ─────────────────
            // Ensures prefab references stay in sync even if the serialized lists
            // have stale references (e.g. wrong prefab cached from a prior sync
            // or scene override). Database is always source of truth.
            if (unitDatabase != null)
            {
                SyncUnitSpawnEntries();
                SyncCorruptionSpawnEntries();
            }
            if (environmentDatabase != null)
            {
                SyncSpawnEntries();
            }

            // ── Plan (fast — pure array math, no Instantiate) ─────────
            InitPlanGrid();
            PlaceAllEntries();
            PlaceOnTopEntries();
            PlaceCorruptionEntities(); // runs after env + units so it respects their cells
            detectedGatherings.Clear();
            detectedGatherings.AddRange(GatheringDetector.DetectGatherings(planGrid, width, height));

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

            // ── Spawn on-top environment (staggered) ─────────────────
            yield return StartCoroutine(SpawnAllOnTopStaggered());

            // ── Spawn units (staggered) ───────────────────────────────
            yield return StartCoroutine(SpawnAllUnitsStaggered());

            // ── Spawn corruption entities (staggered) ─────────────────
            yield return StartCoroutine(SpawnAllCorruptionEntitiesStaggered());

            // Pass gatherings to POI system, then initialize
            POIManager.Instance?.RegisterGatherings(detectedGatherings);
            POIManager.Instance?.Initialize();

            Debug.Log($"[MapGenV2] Map generated. Seed={seed}  Size={width}x{height}  Center=({center.x},{center.y})  Nodes={NodeManager.Instance?.NodeCount}");
        }

        // ─────────────────────────────────────────────────────────────────
        // Plan Phase
        // ─────────────────────────────────────────────────────────────────

        void InitPlanGrid()
        {
            planGrid = new string[width, height];
            planGrid[center.x, center.y] = "__center__";
            onTopPlanGrid = new string[width, height];
        }

        void PlaceAllEntries()
        {
            var planner = new MapPlanner(planGrid, rng, width, height, center, clearCenterCardinal);
            planner.PlaceAllEntries(spawnEntries, environmentDatabase, unitSpawnEntries, unitDatabase, mapDensity);
        }

        void PlaceOnTopEntries()
        {
            var planner = new MapPlanner(planGrid, rng, width, height, center, clearCenterCardinal);
            planner.PlaceOnTopEntries(spawnEntries, environmentDatabase, onTopPlanGrid);
        }

        // Gathering detection moved to GatheringDetector.cs

        // ─────────────────────────────────────────────────────────────────
        // Spawn Phase
        // ─────────────────────────────────────────────────────────────────

        void ApplyEnvironmentDesaturationDefaults(GameObject obj, bool addIfMissing = false)
        {
            if (obj == null) return;

            var desat = obj.GetComponent<ClockworkCraft.EnvironmentDesaturation>();
            if (desat == null)
            {
                if (!addIfMissing) return;
                desat = obj.AddComponent<ClockworkCraft.EnvironmentDesaturation>();
            }

            desat.DesaturatedValue   = defaultEnvironmentDesaturatedValue;
            desat.FullColorValue     = defaultEnvironmentFullColorValue;
            desat.TransitionDuration = defaultEnvironmentTransitionDuration;
        }

        /// <summary>
        /// Spawn an environment entity at a grid position using the full pipeline.
        /// Works for ANY environment type (trees, rocks, water, corruption tiles, etc.)
        /// whether called during map generation or during gameplay at runtime.
        /// </summary>
        public GameObject SpawnEnvironmentAt(int x, int y, EnvironmentData envData)
        {
            if (envData == null || envData.prefab == null) return null;
            if (GridManager.Instance == null) return null;

            Vector3 worldPos = GridManager.Instance.GridToWorldPosition(x, y);
            worldPos.y += 0.01f;
            Quaternion rot = rng != null
                ? Quaternion.Euler(0f, 90f * rng.Next(4), 0f)
                : Quaternion.identity;

            GameObject obj = Instantiate(envData.prefab, worldPos, rot);
            obj.name = envData.assetName;

            // ResourceNode setup
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
                                       : GuessResourceType(envData.assetName);
                node.Initialize(x, y);
                NodeManager.Instance?.RegisterNode(node);
            }

            // Fog
            if (enableFog)
            {
                var fogHideable = obj.GetComponent<FogHideable>();
                if (fogHideable == null) fogHideable = obj.AddComponent<FogHideable>();
                fogHideable.Initialize(x, y);
            }

            // Entity components (health, actor, loot)
            if (GridEntityManager.Instance != null)
            {
                GridEntityManager.Instance.AttachFromEnvironmentData(obj, envData);
                ApplyEnvironmentDesaturationDefaults(obj, addIfMissing: true);
            }

            // Animation
            if (obj.activeSelf)
                TriggerAppearAnimation(obj);

            // Grid layer placement
            PlaceOnCorrectLayer(x, y, obj, envData);

            return obj;
        }

        /// <summary>
        /// Place an environment object on the correct grid layer based on its EnvironmentLayerType.
        /// Surface types (Water, Corruption) go on the surface layer; Objects go on the object layer.
        /// </summary>
        void PlaceOnCorrectLayer(int x, int y, GameObject obj, EnvironmentData envData)
        {
            if (GridManager.Instance == null) return;

            if (envData.layerType == EnvironmentLayerType.Surface)
            {
                // Map EnvironmentLayerType.Surface → SurfaceType based on asset name
                ClockworkGrid.SurfaceType surfaceType = ClockworkGrid.SurfaceType.Water;
                string lower = envData.assetName.ToLowerInvariant();
                if (lower.Contains("corrupt")) surfaceType = ClockworkGrid.SurfaceType.Corruption;
                else if (lower.Contains("lava")) surfaceType = ClockworkGrid.SurfaceType.Lava;

                GridManager.Instance.PlaceSurface(x, y, surfaceType, obj);
            }
            else
            {
                GridManager.Instance.PlaceUnit(x, y, obj, CellState.Resource);
            }
        }

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
            {
                GridEntityManager.Instance.AttachFromEnvironmentData(obj, envData);
                ApplyEnvironmentDesaturationDefaults(obj, addIfMissing: true);
            }

            PlaceOnCorrectLayer(center.x, center.y, obj, envData);
            TriggerAppearAnimation(obj);

            Debug.Log($"[MapGenV2] Center '{centerEnvironmentName}' at ({center.x},{center.y}) layer={envData.layerType}");
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
                if (envName.StartsWith(MapGenHelpers.UNIT_PREFIX)) continue;

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
                {
                    GridEntityManager.Instance.AttachFromEnvironmentData(obj, envData);
                    ApplyEnvironmentDesaturationDefaults(obj, addIfMissing: true);
                }

                if (obj.activeSelf)
                    TriggerAppearAnimation(obj);

                PlaceOnCorrectLayer(x, y, obj, envData);
                POIManager.Instance?.RegisterEnvPOI(new Vector2Int(x, y), envData.assetName);

                count++;
                if (count % BATCH_SIZE == 0)
                    yield return null; // Breathe — let the frame render
            }
        }

        /// <summary>
        /// Spawns "On Top" objects from the onTopPlanGrid — objects placed on the
        /// Object layer above existing Surface tiles (e.g. water lilies on water).
        /// Runs after SpawnAllStaggered so the surface GameObjects already exist.
        /// </summary>
        System.Collections.IEnumerator SpawnAllOnTopStaggered()
        {
            if (onTopPlanGrid == null) yield break;

            const int BATCH_SIZE = 25;
            int count = 0;

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                string envName = onTopPlanGrid[x, y];
                if (envName == null) continue;

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
                {
                    GridEntityManager.Instance.AttachFromEnvironmentData(obj, envData);
                    ApplyEnvironmentDesaturationDefaults(obj, addIfMissing: true);
                }

                if (obj.activeSelf)
                    TriggerAppearAnimation(obj);

                // OnTop objects go on the Object layer — the surface is already placed
                GridManager.Instance?.PlaceUnit(x, y, obj, CellState.Resource);
                POIManager.Instance?.RegisterEnvPOI(new Vector2Int(x, y), envData.assetName);

                count++;
                if (count % BATCH_SIZE == 0)
                    yield return null;
            }

            if (count > 0)
                Debug.Log($"[MapGenV2] Spawned {count} OnTop environment objects");
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
                if (planName == null || !planName.StartsWith(MapGenHelpers.UNIT_PREFIX)) continue;

                string unitName = planName.Substring(MapGenHelpers.UNIT_PREFIX.Length);
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

                if (enableUnitDesaturation)
                    ApplyEnvironmentDesaturationDefaults(obj, addIfMissing: true);

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
        // Corruption Entities — Planning
        // ─────────────────────────────────────────────────────────────────

        public void SyncCorruptionSpawnEntries()
        {
            if (unitDatabase != null)
                corruptionSpawnEntries = SpawnEntrySyncer.SyncCorruptionEntries(corruptionSpawnEntries, unitDatabase);
        }

        void PlaceCorruptionEntities()
        {
            var corrPlanner = new CorruptionPlanner(planGrid, rng, width, height, center, clearCenterCardinal);
            corrPlanner.PlaceCorruptionEntities(corruptionSpawnEntries);
        }

        // ─────────────────────────────────────────────────────────────────
        // Corruption Entities — Spawning
        // ─────────────────────────────────────────────────────────────────

        System.Collections.IEnumerator SpawnAllCorruptionEntitiesStaggered()
        {
            const int BATCH_SIZE = 10;
            int spawnCount = 0;

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                string planName = planGrid[x, y];
                if (planName == null || !planName.StartsWith(MapGenHelpers.CORRUPTION_PREFIX)) continue;

                string entityName = planName.Substring(MapGenHelpers.CORRUPTION_PREFIX.Length);
                var entry = corruptionSpawnEntries.Find(e => e.entityName == entityName);
                if (entry == null || entry.prefab == null) continue;

                Vector3 worldPos = GridManager.Instance.GridToWorldPosition(x, y);
                worldPos.y += 0.01f;
                GameObject obj = Instantiate(entry.prefab, worldPos, Quaternion.identity);
                obj.name = $"{entityName}_{spawnCount}";

                var heart = obj.GetComponent<LittleCafe.CorruptionHeart>();
                if (heart != null)
                {
                    heart.GridPosition            = new Vector2Int(x, y);
                    heart.UnitDatabase            = unitDatabase;
                    heart.InitialCorruptedRadius  = entry.initialCorruptionRadius;
                    heart.EnsureInitialized();
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

        static ResourceType GuessResourceType(string envName) => MapGenHelpers.GuessResourceType(envName);
    }
}