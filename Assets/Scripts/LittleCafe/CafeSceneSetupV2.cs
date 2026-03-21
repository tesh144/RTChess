using UnityEngine;
using System.Collections.Generic;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// Updated cafe scene setup using FurnitureDatabase.
    /// Simplified version that loads furniture from database instead of generating fallbacks.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class CafeSceneSetupV2 : MonoBehaviour
    {
        [Header("Furniture Database")]
        [SerializeField] private FurnitureDatabase furnitureDatabase;

        [Header("Grid Settings")]
        [SerializeField] private int gridSize = 50;
        [SerializeField] private float cellSize = 1.5f;
        [SerializeField] private GameObject gridTilePrefabA; // Tile 1
        [SerializeField] private GameObject gridTilePrefabB; // Tile 2

        [Header("Economy")]
        [SerializeField] private int startingTokens = 999;
        [SerializeField] private int baseDrawCost = 0;
        [SerializeField] private int costIncrement = 0;

        [Header("Game Systems")]
        [Tooltip("Interaction unlock registry — controls which objects workers can target. Add as a component on this GameObject or a child.")]
        [SerializeField] private ClockworkCraft.InteractionRegistry interactionRegistry;

        /// <summary>True when MapGeneratorV2 is in the scene and owns grid/deck/fog.</summary>
        private bool deferToMapGen = false;

        private void Awake()
        {
            deferToMapGen = FindObjectOfType<ClockworkCraft.MapGeneratorV2>(true) != null;

            // Ensure GameStateManager exists (execution order -110 should have created it,
            // but if it's not in the scene we create one as fallback)
            EnsureGameStateManager();

            // Shared infrastructure — always needed regardless of mode
            SetupGrid();
            SetupCamera();
            EnsureIntervalTimer();
            SetupTokenManager();
            EnsureEventSystem();
            EnsureDragDropHandler();
            SetupLighting();
            SetupFurnitureSystems();
            EnsureInteractionRegistry();

            // Deck setup — only in standalone cafe mode (MapGeneratorV2 owns it in ClockworkCraft)
            if (!deferToMapGen)
            {
                SetupRaritySystem();
            }

            // Disable GUI Pro Kit's demo PanelControl — we manage panels ourselves
            var panelControl = FindObjectOfType<LayerLab.PanelControl>(true);
            if (panelControl != null)
            {
                panelControl.enabled = false;
                panelControl.gameObject.SetActive(false);
            }

            Debug.Log($"[CafeSceneSetupV2] Awake complete — deferToMapGen={deferToMapGen}");
        }

        private void Start()
        {
            // Dock bar init — only in standalone cafe mode
            if (!deferToMapGen)
            {
                InitializeDockBar();
            }

            // Hide Lobby panel entirely until game starts (Title screen is the only visible panel)
            if (UIThemeManager.Instance != null)
                UIThemeManager.Instance.HidePanel("Lobby");

            if (IntervalTimer.Instance != null) IntervalTimer.Instance.Pause();

            // TitleScreenController must already exist in the scene with all serialized references
            // wired via: Tools > ClockworkCraft > Setup Title Screen
            // No runtime creation, no runtime AddListener — everything is in the Inspector.
            var titleScreen = FindObjectOfType<TitleScreenController>(true);

            if (titleScreen != null)
            {
                titleScreen.Initialize();
                Debug.Log("[CafeSceneSetupV2] TitleScreenController found — waiting for Play button...");
            }
            else
            {
                Debug.LogWarning("[CafeSceneSetupV2] No TitleScreenController in scene! " +
                    "Run 'Tools > ClockworkCraft > Setup Title Screen' to create one.");
            }
        }

        /// <summary>
        /// Called when the game should start (title screen exit, or click-to-start).
        /// Wire this to TitleScreenController's onGameStart UnityEvent in the Inspector.
        /// </summary>
        public void OnGameStarted()
        {
            // Transition game state — this notifies MusicSystem and all other listeners
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.TransitionTo(GameState.Playing);
            }

            // Grant starting resources NOW (not during init) so the
            // currency bar doesn't show gold before the player clicks to start
            if (ClockworkCraft.ResourceManager.Instance != null)
                ClockworkCraft.ResourceManager.Instance.GrantStartingResources();

            // Show Lobby panel (was hidden in Start)
            if (UIThemeManager.Instance != null)
                UIThemeManager.Instance.ShowPanel("Lobby");

            if (deferToMapGen)
            {
                // MapGeneratorV2 owns grid init, fog, and deck — just show UI and resume clock
                if (DockBarManager.Instance != null) DockBarManager.Instance.ShowWithAnimation();
                if (IntervalTimer.Instance != null) IntervalTimer.Instance.Resume();
                Debug.Log("[CafeSceneSetupV2] Game started! (MapGeneratorV2 handles grid/fog/deck)");
                return;
            }

            // Standalone cafe mode — initialize grid now
            GridManager gm = GridManager.Instance;
            if (gm != null)
            {
                gm.InitializeGrid();
                Debug.Log("[CafeSceneSetupV2] Grid initialized on game start");
            }

            RevealStartingTiles();
            if (DockBarManager.Instance != null) DockBarManager.Instance.ShowWithAnimation();
            if (IntervalTimer.Instance != null) IntervalTimer.Instance.Resume();
            Debug.Log("[CafeSceneSetupV2] Game started!");
        }

        // --- Grid ---

        private void SetupGrid()
        {
            GridManager gm = FindObjectOfType<GridManager>(true);
            if (gm == null)
            {
                GameObject gridObj = new GameObject("GridManager");
                gm = gridObj.AddComponent<GridManager>();
            }

            gm.gameObject.SetActive(true);
            gm.enabled = true;

            SetPrivateField(gm, "gridWidth", gridSize);
            SetPrivateField(gm, "gridHeight", gridSize);
            SetPrivateField(gm, "cellSize", cellSize);

            // Assign tile prefabs for checkerboard pattern
            if (gridTilePrefabA != null && gridTilePrefabB != null)
            {
                SetPrivateField(gm, "gridTilePrefabA", gridTilePrefabA);
                SetPrivateField(gm, "gridTilePrefabB", gridTilePrefabB);
                Debug.Log("[CafeSceneSetupV2] Assigned tile prefabs for checkerboard");
            }
            else
            {
                Debug.LogWarning("[CafeSceneSetupV2] Grid tile prefabs not assigned! Assign Tile 1 and Tile 2 in Inspector.");
            }

            if (gm.GetComponent<GridVisualizer>() == null)
                gm.gameObject.AddComponent<GridVisualizer>();

            // Don't initialize grid yet - wait for player to click to start
            // gm.InitializeGrid() will be called in OnGameStarted()

            Debug.Log($"[CafeSceneSetupV2] Grid prepared (not yet initialized): {gridSize}x{gridSize}, cellSize={cellSize}");
        }

        // --- Camera ---

        private void SetupCamera()
        {
            Camera cam = FindObjectOfType<Camera>(true);
            if (cam == null)
            {
                Debug.LogError("[CafeSceneSetupV2] No camera found in scene!");
                return;
            }

            GridCamera oldGridCam = cam.GetComponent<GridCamera>();
            if (oldGridCam != null)
                DestroyImmediate(oldGridCam);

            // Add GridCamera and point at grid
            GridCamera gridCam = cam.gameObject.AddComponent<GridCamera>();
            gridCam.PointAtGrid();

            // Add CameraPan for WASD/arrow key and middle-mouse drag movement
            if (cam.GetComponent<CameraPan>() == null)
            {
                cam.gameObject.AddComponent<CameraPan>();
            }

            Debug.Log($"[CafeSceneSetupV2] GridCamera and CameraPan attached to '{cam.gameObject.name}'");
        }

        // --- Token Economy ---

        private void SetupTokenManager()
        {
            if (ResourceTokenManager.Instance == null)
            {
                new GameObject("ResourceTokenManager").AddComponent<ResourceTokenManager>();
            }
            ResourceTokenManager.Instance.AddTokens(startingTokens);
            Debug.Log($"[CafeSceneSetupV2] Gave {startingTokens} starting tokens");
        }

        // --- Rarity System ---

        private void SetupRaritySystem()
        {
            if (furnitureDatabase == null)
            {
                Debug.LogWarning("[CafeSceneSetupV2] No FurnitureDatabase assigned! Dock will be empty.");
                return;
            }

            // Destroy old RaritySystem (contains combat units)
            if (RaritySystem.Instance != null)
            {
                DestroyImmediate(RaritySystem.Instance.gameObject);
                Debug.Log("[CafeSceneSetupV2] Destroyed old RaritySystem");
            }

            // Create fresh RaritySystem for furniture
            new GameObject("RaritySystem").AddComponent<RaritySystem>();

            List<UnitStats> furnitureStats = new List<UnitStats>();

            foreach (FurnitureData data in furnitureDatabase.AllFurniture)
            {
                if (data.prefab == null) continue; // Skip if prefab not generated yet

                UnitStats stats = ScriptableObject.CreateInstance<UnitStats>();
                stats.unitType = ConvertFurnitureTypeToUnitType(data.type);
                stats.unitName = data.GetCleanName();
                stats.rarity = Rarity.Common; // All furniture is common for now
                stats.drawWeight = data.drawWeight; // Per-item weight from FurnitureData
                stats.furnitureTypeOverride = (int)data.type; // Override prefab's serialized type at runtime
                stats.iconSprite = data.icon;
                stats.unitColor = Color.white; // TODO: Generate icons with colors
                stats.unitPrefab = data.prefab;
                stats.resourceCost = 0; // Free placement in cafe mode
                stats.gridSize = data.gridSize;
                stats.modelScale = data.visualScale;
                stats.enemyPrefab = null;
                stats.isActive = data.isActive; // From database (furniture defaults to false)
                stats.maxHP = 0; // Furniture has no HP
                stats.attackDamage = 0; // Furniture has no attack
                furnitureStats.Add(stats);
            }

            RaritySystem.Instance.RegisterUnitStats(furnitureStats);
            Debug.Log($"[CafeSceneSetupV2] Registered {furnitureStats.Count} furniture types with RaritySystem");
        }

        /// <summary>
        /// Convert FurnitureType to UnitType for backwards compatibility with RaritySystem.
        /// </summary>
        private UnitType ConvertFurnitureTypeToUnitType(FurnitureType furnitureType)
        {
            // Use unused UnitType values or create a mapping
            // For now, just cast (they're both enums)
            // This is a temporary hack until we refactor RaritySystem to support furniture
            return UnitType.Soldier; // Default to Soldier for now
        }

        // --- Dock Bar ---

        private void InitializeDockBar()
        {
            DockBarManager dockManager = FindObjectOfType<DockBarManager>(true);
            if (dockManager == null)
            {
                Debug.LogWarning("[CafeSceneSetupV2] DockBarManager not found in scene!");
                return;
            }

            dockManager.gameObject.SetActive(true);
            dockManager.enabled = true;

            // Override draw cost for cafe (free draws by default)
            SetPrivateField(dockManager, "baseDrawCost", baseDrawCost);
            SetPrivateField(dockManager, "costIncrement", costIncrement);

            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[CafeSceneSetupV2] No Canvas found in scene!");
                return;
            }

            dockManager.Initialize(canvas);

            // Add starting furniture card if database is populated
            if (furnitureDatabase != null && furnitureDatabase.Count > 0)
            {
                FurnitureData firstFurniture = furnitureDatabase.AllFurniture[0];
                if (firstFurniture.prefab != null)
                {
                    UnitStats startingStats = RaritySystem.Instance.GetUnitStats(UnitType.Soldier); // TODO: Fix this mapping
                    if (startingStats != null)
                    {
                        dockManager.AddCard(startingStats);
                        Debug.Log($"[CafeSceneSetupV2] Added starting furniture card to dock");
                    }
                }
            }
        }

        // --- Infrastructure ---

        private void EnsureGameStateManager()
        {
            if (GameStateManager.Instance == null)
            {
                var existing = FindObjectOfType<GameStateManager>(true);
                if (existing == null)
                {
                    new GameObject("GameStateManager").AddComponent<GameStateManager>();
                    Debug.Log("[CafeSceneSetupV2] Created GameStateManager (initial state: TitleScreen)");
                }
            }
        }

        private void EnsureIntervalTimer()
        {
            if (IntervalTimer.Instance == null)
            {
                new GameObject("IntervalTimer").AddComponent<IntervalTimer>();
            }
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }

        private void EnsureDragDropHandler()
        {
            if (FindObjectOfType<DragDropHandler>(true) == null)
            {
                new GameObject("DragDropHandler").AddComponent<DragDropHandler>();
                Debug.Log("[CafeSceneSetupV2] Created DragDropHandler");
            }
        }

        /// <summary>
        /// Create furniture connectivity, seating, and debug visualization managers.
        /// </summary>
        private void SetupFurnitureSystems()
        {
            // Furniture Connectivity Manager (tracks groups of connected furniture)
            if (FindObjectOfType<FurnitureConnectivityManager>() == null)
            {
                GameObject fcmObj = new GameObject("FurnitureConnectivityManager");
                fcmObj.AddComponent<FurnitureConnectivityManager>();
            }

            // Table Seating Manager (tracks chair-to-table attachments)
            if (FindObjectOfType<TableSeatingManager>() == null)
            {
                GameObject tsmObj = new GameObject("TableSeatingManager");
                tsmObj.AddComponent<TableSeatingManager>();
            }

            // Debug Visualizer (colored arcs and seating markers — toggle in Inspector)
            if (FindObjectOfType<FurnitureConnectionDebugVisualizer>() == null)
            {
                GameObject visObj = new GameObject("FurnitureDebugVisualizer");
                visObj.AddComponent<FurnitureConnectionDebugVisualizer>();
            }

            // Furniture Removal Handler (tap-and-hold to remove placed furniture)
            if (FindObjectOfType<FurnitureRemovalHandler>() == null)
            {
                GameObject removalObj = new GameObject("FurnitureRemovalHandler");
                removalObj.AddComponent<FurnitureRemovalHandler>();
            }

            // Grid Entity Manager (attaches health/actor components on placement)
            if (FindObjectOfType<GridEntityManager>() == null)
            {
                GameObject gemObj = new GameObject("GridEntityManager");
                gemObj.AddComponent<GridEntityManager>();
            }

            Debug.Log("[CafeSceneSetupV2] Furniture systems initialized");
        }

        /// <summary>
        /// Ensure the InteractionRegistry exists. Prefers a serialized scene reference;
        /// falls back to finding one in the scene; creates one as a last resort.
        /// </summary>
        private void EnsureInteractionRegistry()
        {
            // Already assigned in Inspector
            if (interactionRegistry != null && ClockworkCraft.InteractionRegistry.Instance != null)
            {
                Debug.Log("[CafeSceneSetupV2] InteractionRegistry already assigned and active");
                return;
            }

            // Try to find one in the scene
            var found = FindObjectOfType<ClockworkCraft.InteractionRegistry>(true);
            if (found != null)
            {
                interactionRegistry = found;
                Debug.Log("[CafeSceneSetupV2] Found existing InteractionRegistry in scene");
                return;
            }

            // Create one as fallback (shouldn't normally happen if scene is set up properly)
            Debug.LogWarning("[CafeSceneSetupV2] No InteractionRegistry in scene — creating one at runtime. " +
                "For best results, add InteractionRegistry as a component on this GameObject via Tools > ClockworkCraft > Setup Interaction Registry.");
            var registry = gameObject.AddComponent<ClockworkCraft.InteractionRegistry>();
            interactionRegistry = registry;
        }

        private void SetupLighting()
        {
            if (FindObjectsOfType<Light>().Length == 0)
            {
                GameObject lightObj = new GameObject("Directional Light");
                Light light = lightObj.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.95f, 0.9f);
                light.intensity = 1.2f;
                light.shadows = LightShadows.Soft;
                lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
        }

        private void RevealStartingTiles()
        {
            GridManager gm = GridManager.Instance;
            if (gm == null) return;

            // Reveal a 3x3 block centered on the TRUE grid center
            // Grid cells go from 0 to (Width-1), so center is at (Width-1)/2
            int cx = Mathf.RoundToInt((gm.Width - 1) * 0.5f);
            int cy = Mathf.RoundToInt((gm.Height - 1) * 0.5f);

            Debug.Log($"[CafeSceneSetupV2] Revealing starting 3x3 area centered at ({cx}, {cy})");

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    gm.RevealTile(cx + dx, cy + dy, immediate: false);
                }
            }
        }

        // --- Helpers ---

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(fieldName,
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }
                type = type.BaseType;
            }
            Debug.LogWarning($"[CafeSceneSetupV2] Could not find field '{fieldName}' on {target.GetType().Name}");
        }
    }
}


