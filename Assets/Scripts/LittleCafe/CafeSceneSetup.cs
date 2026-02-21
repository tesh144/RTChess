using UnityEngine;
using System.Collections.Generic;
using ClockworkGrid;

namespace LittleCafe
{
    [System.Serializable]
    public class EquipmentConfig
    {
        public UnitType type;
        public string displayName;
        public Rarity rarity;
        public Color color = Color.white;
        public Sprite icon;
        public GameObject prefab;
        public Vector2Int gridSize = new Vector2Int(1, 1);
    }

    /// <summary>
    /// Cafe-specific scene setup. Works like GameSetup but for the kitchen builder.
    /// Reuses the existing RTChess dock bar / gacha / drag-drop infrastructure
    /// and registers equipment cards instead of combat units.
    /// All UI (Canvas, DockBarManager, token display) already exists in the scene.
    /// </summary>
    [DefaultExecutionOrder(-100)] // Run before GameSetup so we can remove it first
    public class CafeSceneSetup : MonoBehaviour
    {
        [Header("Grid Settings")]
        [SerializeField] private int gridWidth = 20;
        [SerializeField] private int gridHeight = 20;
        [SerializeField] private float cellSize = 1.5f;

        [Header("Economy")]
        [SerializeField] private int startingTokens = 999;
        [SerializeField] private int baseDrawCost = 0;
        [SerializeField] private int costIncrement = 0;

        [Header("Equipment (configure each type here)")]
        [SerializeField] private EquipmentConfig[] equipment = new EquipmentConfig[]
        {
            new EquipmentConfig { type = UnitType.Table, displayName = "Table", rarity = Rarity.Common, color = new Color(0.55f, 0.36f, 0.96f), gridSize = new Vector2Int(2, 1) },
            new EquipmentConfig { type = UnitType.Chair, displayName = "Chair", rarity = Rarity.Common, color = new Color(0.56f, 0.93f, 0.56f) },
            new EquipmentConfig { type = UnitType.Wall, displayName = "Wall", rarity = Rarity.Common, color = new Color(0.18f, 0.18f, 0.18f) },
            new EquipmentConfig { type = UnitType.Door, displayName = "Door", rarity = Rarity.Rare, color = new Color(1f, 0.85f, 0.24f) },
            new EquipmentConfig { type = UnitType.CookingStation, displayName = "Cooking Station", rarity = Rarity.Rare, color = new Color(1f, 0.42f, 0.42f), gridSize = new Vector2Int(2, 2) },
            new EquipmentConfig { type = UnitType.ServingCounter, displayName = "Serving Counter", rarity = Rarity.Rare, color = new Color(0.31f, 0.69f, 0.36f), gridSize = new Vector2Int(2, 1) },
            new EquipmentConfig { type = UnitType.WashingStation, displayName = "Washing Station", rarity = Rarity.Epic, color = new Color(0.42f, 0.80f, 1f), gridSize = new Vector2Int(2, 1) },
            new EquipmentConfig { type = UnitType.PlateRack, displayName = "Plate Rack", rarity = Rarity.Epic, color = new Color(1f, 0.41f, 0.71f) },
        };

        // Runtime-created fallback prefabs (used when config has no prefab assigned)
        private Dictionary<UnitType, GameObject> fallbackPrefabs = new Dictionary<UnitType, GameObject>();

        private void Awake()
        {
            // DestroyImmediate removes just the GameSetup COMPONENT before its Awake() fires.
            // The GameObject and all scene UI (Canvas, DockBarManager, etc.) stays alive.
            // We run at ExecutionOrder -100 so our Awake is guaranteed first.
            GameSetup gs = FindObjectOfType<GameSetup>(true);
            if (gs != null)
                DestroyImmediate(gs);

            // Destroy combat/wave systems — these don't exist in the cafe game.
            // DestroyImmediate prevents their Awake singletons from persisting.
            DestroyComponent<WaveManager>();
            DestroyComponent<FogManager>();
            DestroyComponent<GridExpansionManager>();
            DestroyComponent<GameOverManager>();
            DestroyComponent<ControlSchemeOverlay>();

            // DO NOT destroy these — we're reusing them:
            // DockBarManager, DragDropHandler, ResourceTokenManager, RaritySystem, IntervalTimer

            SetupGrid();
            SetupCamera();
            EnsureIntervalTimer();
            SetupTokenManager();
            SetupEquipmentPrefabs();
            SetupRaritySystem();
            EnsureEventSystem();
            EnsureDragDropHandler();
            SetupLighting();

            Debug.Log("[CafeSceneSetup] Awake complete — grid, tokens, equipment, rarity ready");
        }

        private void Start()
        {
            InitializeDockBar();

            // Hide UI and pause timer until the player interacts
            DockBarManager.Instance.HideUI();
            IntervalTimer.Instance.Pause();

            // Wait for first click / keypress
            GameObject gateObj = new GameObject("GameStartGate");
            GameStartGate gate = gateObj.AddComponent<GameStartGate>();
            gate.OnGameStart += OnGameStarted;

            Debug.Log("[CafeSceneSetup] Waiting for player to start...");
        }

        private void OnGameStarted()
        {
            RevealStartingTiles();
            DockBarManager.Instance.ShowWithAnimation();
            IntervalTimer.Instance.Resume();
            Debug.Log("[CafeSceneSetup] Game started!");
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

            // Use hardcoded size — serialized fields can be stale from older defaults
            const int cafeGridSize = 50;
            SetPrivateField(gm, "gridWidth", cafeGridSize);
            SetPrivateField(gm, "gridHeight", cafeGridSize);
            SetPrivateField(gm, "cellSize", cellSize);

            if (gm.GetComponent<GridVisualizer>() == null)
                gm.gameObject.AddComponent<GridVisualizer>();

            gm.InitializeGrid();

            Debug.Log($"[CafeSceneSetup] Grid initialized: {cafeGridSize}x{cafeGridSize}, cellSize={cellSize}");
        }

        // --- Camera ---

        private void SetupCamera()
        {
            Camera cam = FindObjectOfType<Camera>(true);
            if (cam == null)
            {
                Debug.LogError("[CafeSceneSetup] No camera found in scene!");
                return;
            }

            // Remove RTChess camera systems
            CameraController oldCC = cam.GetComponent<CameraController>();
            if (oldCC != null)
                DestroyImmediate(oldCC);

            // Remove any stale GridCamera from a previous play session
            GridCamera oldGridCam = cam.GetComponent<GridCamera>();
            if (oldGridCam != null)
                DestroyImmediate(oldGridCam);

            // Add GridCamera and point at grid
            GridCamera gridCam = cam.gameObject.AddComponent<GridCamera>();
            gridCam.PointAtGrid();

            Debug.Log($"[CafeSceneSetup] GridCamera attached to '{cam.gameObject.name}'");
        }

        // --- Token Economy ---

        private void SetupTokenManager()
        {
            if (ResourceTokenManager.Instance == null)
            {
                new GameObject("ResourceTokenManager").AddComponent<ResourceTokenManager>();
            }
            ResourceTokenManager.Instance.AddTokens(startingTokens);
            Debug.Log($"[CafeSceneSetup] Gave {startingTokens} starting tokens");
        }

        // --- Equipment Prefabs ---

        private void SetupEquipmentPrefabs()
        {
            foreach (EquipmentConfig config in equipment)
            {
                GameObject prefab;

                if (config.prefab != null)
                {
                    // Clone the assigned prefab so we can add CafeEquipment without modifying the asset
                    prefab = Instantiate(config.prefab);
                    prefab.name = config.prefab.name;
                }
                else
                {
                    // No prefab assigned — create a colored cube as fallback
                    prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    prefab.name = config.displayName + "Prefab";

                    float height = cellSize * 0.6f;
                    float scaleX = cellSize * config.gridSize.x * 0.8f;
                    float scaleZ = cellSize * config.gridSize.y * 0.8f;
                    prefab.transform.localScale = new Vector3(scaleX, height, scaleZ);

                    MeshRenderer renderer = prefab.GetComponent<MeshRenderer>();
                    Material mat = new Material(Shader.Find("Standard"));
                    mat.color = config.color;
                    renderer.material = mat;
                }

                // Ensure CafeEquipment is on every prefab (reveals adjacent fog on placement)
                if (prefab.GetComponent<CafeEquipment>() == null)
                    prefab.AddComponent<CafeEquipment>();

                prefab.SetActive(false);
                fallbackPrefabs[config.type] = prefab;
            }

            Debug.Log($"[CafeSceneSetup] Equipment prefabs ready ({equipment.Length} types, {fallbackPrefabs.Count} prepared)");
        }

        private GameObject GetPrefabForType(UnitType type)
        {
            return fallbackPrefabs.GetValueOrDefault(type);
        }

        private Color GetColorForType(UnitType type)
        {
            foreach (EquipmentConfig config in equipment)
            {
                if (config.type == type)
                    return config.color;
            }
            return Color.white;
        }

        // --- Rarity System ---

        private void SetupRaritySystem()
        {
            if (RaritySystem.Instance == null)
            {
                new GameObject("RaritySystem").AddComponent<RaritySystem>();
            }

            List<UnitStats> equipmentStats = new List<UnitStats>();

            foreach (EquipmentConfig config in equipment)
            {
                UnitStats stats = ScriptableObject.CreateInstance<UnitStats>();
                stats.unitType = config.type;
                stats.unitName = config.displayName;
                stats.rarity = config.rarity;
                stats.iconSprite = config.icon;
                stats.unitColor = config.color;
                stats.unitPrefab = GetPrefabForType(config.type);
                stats.resourceCost = 0;
                stats.gridSize = config.gridSize;
                stats.modelScale = 1f;
                stats.enemyPrefab = null;
                equipmentStats.Add(stats);
            }

            RaritySystem.Instance.RegisterUnitStats(equipmentStats);
            Debug.Log($"[CafeSceneSetup] Registered {equipmentStats.Count} equipment types with RaritySystem");
        }

        // --- Dock Bar (find existing scene objects, initialize for cafe) ---

        private void InitializeDockBar()
        {
            DockBarManager dockManager = FindObjectOfType<DockBarManager>(true);
            if (dockManager == null)
            {
                Debug.LogWarning("[CafeSceneSetup] DockBarManager not found in scene! It should already exist.");
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
                Debug.LogWarning("[CafeSceneSetup] No Canvas found in scene! DockBarManager needs a Canvas.");
                return;
            }

            dockManager.Initialize(canvas);

            // Add a starting card so the dock isn't empty
            if (equipment.Length > 0)
            {
                UnitStats startingStats = RaritySystem.Instance.GetUnitStats(equipment[0].type);
                if (startingStats != null)
                {
                    dockManager.AddUnitToDock(startingStats);
                    Debug.Log($"[CafeSceneSetup] Added starting {equipment[0].displayName} card to dock");
                }
            }
        }

        // --- Infrastructure ---

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
                Debug.Log("[CafeSceneSetup] Created DragDropHandler");
            }
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

            // Reveal a 3x3 block centered on the grid
            int cx = gm.Width / 2;
            int cy = gm.Height / 2;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    gm.RevealTile(cx + dx, cy + dy, immediate: false);
                }
            }
        }

        // --- Helpers ---

        private void DestroyComponent<T>() where T : MonoBehaviour
        {
            T comp = FindObjectOfType<T>(true);
            if (comp != null)
                DestroyImmediate(comp);
        }

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
            Debug.LogWarning($"[CafeSceneSetup] Could not find field '{fieldName}' on {target.GetType().Name}");
        }
    }
}
