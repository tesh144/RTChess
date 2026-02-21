using UnityEngine;
using ClockworkGrid;
using LittleCafe;    // GridCamera, CameraPan live in LittleCafe namespace

namespace ClockworkCraft
{
    /// <summary>
    /// Bootstrap script for the ClockworkCraft scene.
    /// Mirrors the CafeSceneSetupV2 pattern: configure everything in Awake via code,
    /// no manual scene wiring needed beyond dragging in the databases.
    ///
    /// Add ONE of these to any scene.  It will:
    ///   1. Read map size from MapGenerationSettings and configure GridManager to match.
    ///   2. Replace any old camera controller with GridCamera (orbiting + zoom).
    ///   3. Ensure IntervalTimer, FogManager, NodeManager, ResourceManager exist.
    ///   4. MapGenerator runs its own Start() (order 100) and generates the map.
    ///
    /// [DefaultExecutionOrder(-100)] so this Awake runs before every other script.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class ClockworkCraftSceneSetup : MonoBehaviour
    {
        [Header("Required")]
        [Tooltip("The MapGenerator in the scene — needed to read grid size from its settings.")]
        [SerializeField] private MapGenerator mapGenerator;

        [Header("Grid Tile Prefabs (optional)")]
        [Tooltip("Leave empty to use default grey/white cubes.")]
        [SerializeField] private GameObject gridTilePrefabA;
        [SerializeField] private GameObject gridTilePrefabB;
        [SerializeField] private float cellSize = 1.5f;

        private void Awake()
        {
            // Auto-find MapGenerator if not assigned
            if (mapGenerator == null)
                mapGenerator = FindObjectOfType<MapGenerator>(true);

            if (mapGenerator == null)
                Debug.LogWarning("[ClockworkCraftSetup] MapGenerator not found in scene. Grid will use default 40x40.");

            SetupGrid();
            SetupCamera();
            EnsureIntervalTimer();
            EnsureFogManager();
            EnsureNodeManager();
            EnsureResourceManager();

            Debug.Log("[ClockworkCraftSetup] Awake complete — map generation will run in MapGenerator.Start()");
        }

        // ── Grid ─────────────────────────────────────────────────────────────

        private void SetupGrid()
        {
            GridManager gm = FindObjectOfType<GridManager>(true);
            if (gm == null)
            {
                Debug.LogWarning("[ClockworkCraftSetup] No GridManager found. Creating one.");
                gm = new GameObject("GridManager").AddComponent<GridManager>();
            }

            gm.gameObject.SetActive(true);

            // Read grid dimensions from MapGenerationSettings so GridManager always
            // matches the map that MapGenerator will produce.
            int w = 40, h = 40;
            if (mapGenerator != null && mapGenerator.settings != null)
            {
                w = mapGenerator.settings.mapWidth;
                h = mapGenerator.settings.mapHeight;
            }

            SetPrivateField(gm, "gridWidth",  w);
            SetPrivateField(gm, "gridHeight", h);
            SetPrivateField(gm, "cellSize",   cellSize);

            if (gridTilePrefabA != null) SetPrivateField(gm, "gridTilePrefabA", gridTilePrefabA);
            if (gridTilePrefabB != null) SetPrivateField(gm, "gridTilePrefabB", gridTilePrefabB);

            Debug.Log($"[ClockworkCraftSetup] GridManager configured {w}x{h}, cellSize={cellSize}");
        }

        // ── Camera ───────────────────────────────────────────────────────────

        private void SetupCamera()
        {
            Camera cam = FindObjectOfType<Camera>(true);
            if (cam == null)
            {
                Debug.LogError("[ClockworkCraftSetup] No Camera found in scene!");
                return;
            }

            // Strip old RTChess camera controller if present
            var oldRTC = cam.GetComponent<CameraController>();
            if (oldRTC != null) DestroyImmediate(oldRTC);

            // Replace any stale GridCamera with a fresh one
            var oldGC = cam.GetComponent<GridCamera>();
            if (oldGC != null) DestroyImmediate(oldGC);

            // Add the LittleCafe orbiting camera and point it at the grid center
            GridCamera gridCam = cam.gameObject.AddComponent<GridCamera>();
            gridCam.PointAtGrid();

            // CameraPan lets the player WASD/middle-mouse drag to move the pivot
            if (cam.GetComponent<CameraPan>() == null)
                cam.gameObject.AddComponent<CameraPan>();

            Debug.Log($"[ClockworkCraftSetup] GridCamera + CameraPan attached to '{cam.gameObject.name}'");
        }

        // ── Manager singletons ───────────────────────────────────────────────

        private void EnsureIntervalTimer()
        {
            if (IntervalTimer.Instance == null)
            {
                new GameObject("IntervalTimer").AddComponent<IntervalTimer>();
                Debug.Log("[ClockworkCraftSetup] Created IntervalTimer");
            }
        }

        private void EnsureFogManager()
        {
            if (FogManager.Instance == null)
            {
                new GameObject("FogManager").AddComponent<FogManager>();
                Debug.Log("[ClockworkCraftSetup] Created FogManager");
            }
        }

        private void EnsureNodeManager()
        {
            if (NodeManager.Instance == null)
            {
                new GameObject("NodeManager").AddComponent<NodeManager>();
                Debug.Log("[ClockworkCraftSetup] Created NodeManager");
            }
        }

        private void EnsureResourceManager()
        {
            if (ResourceManager.Instance == null)
            {
                new GameObject("ResourceManager").AddComponent<ResourceManager>();
                Debug.Log("[ClockworkCraftSetup] Created ResourceManager");
            }
        }

        // ── Reflection helper (same as CafeSceneSetupV2) ────────────────────

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
            Debug.LogWarning($"[ClockworkCraftSetup] Could not find field '{fieldName}' on {target.GetType().Name}");
        }
    }
}
