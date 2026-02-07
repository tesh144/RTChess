using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace ClockworkGrid
{
    /// <summary>
    /// Bootstraps the entire scene from scratch.
    /// Attach this to an empty GameObject in the scene. On Play, it creates:
    /// - Grid manager + visualizer
    /// - Interval timer
    /// - Resource token manager
    /// - Camera (top-down orthographic)
    /// - UI canvas with interval counter and token display
    /// - Debug placer for click-to-spawn units and resources
    /// - Soldier and resource node prefab templates
    /// </summary>
    public class GameSetup : MonoBehaviour
    {
        [Header("Grid Settings")]
        [SerializeField] private int gridWidth = 11; // Iteration 7: Larger grid for exploration
        [SerializeField] private int gridHeight = 11;
        [SerializeField] private float cellSize = 1.5f;

        [Header("Interval Settings")]
        [SerializeField] private float baseIntervalDuration = 2.0f;

        [Header("Soldier Stats (Common)")]
        [SerializeField] private GameObject soldierPlayerPrefab; // Drag player prefab, or leave empty for procedural
        [SerializeField] private GameObject soldierEnemyPrefab; // Drag enemy prefab, or leave empty for procedural
        [SerializeField] private float soldierRarityWeight = 60f;
        [SerializeField] private int soldierHP = 10;
        [SerializeField] private int soldierAttackDamage = 3;
        [SerializeField] private int soldierAttackRange = 1;
        [SerializeField] private int soldierAttackInterval = 2;
        [SerializeField] private int soldierResourceCost = 3;
        [SerializeField] private int soldierRevealRadius = 1;
        [SerializeField] private float soldierModelScale = 1f;

        [Header("Ogre Stats (Epic)")]
        [SerializeField] private GameObject ogrePlayerPrefab; // Drag player prefab, or leave empty for procedural
        [SerializeField] private GameObject ogreEnemyPrefab; // Drag enemy prefab, or leave empty for procedural
        [SerializeField] private float ogreRarityWeight = 5f;
        [SerializeField] private int ogreHP = 20;
        [SerializeField] private int ogreAttackDamage = 5;
        [SerializeField] private int ogreAttackRange = 2;
        [SerializeField] private int ogreAttackInterval = 4;
        [SerializeField] private int ogreResourceCost = 6;
        [SerializeField] private int ogreRevealRadius = 1;
        [SerializeField] private float ogreModelScale = 1.3f;

        [Header("Ninja Stats (Rare)")]
        [SerializeField] private GameObject ninjaPlayerPrefab; // Drag player prefab, or leave empty for procedural
        [SerializeField] private GameObject ninjaEnemyPrefab; // Drag enemy prefab, or leave empty for procedural
        [SerializeField] private float ninjaRarityWeight = 35f;
        [SerializeField] private int ninjaHP = 5;
        [SerializeField] private int ninjaAttackDamage = 1;
        [SerializeField] private int ninjaAttackRange = 1;
        [SerializeField] private int ninjaAttackInterval = 1;
        [SerializeField] private int ninjaResourceCost = 4;
        [SerializeField] private int ninjaRevealRadius = 2;
        [SerializeField] private float ninjaModelScale = 0.8f;

        [Header("Wave Sequence - Iteration 10")]
        [Tooltip("Each entry = 1 interval tick. 0=Nothing, 1=Enemies, 2=Resources, 3=Boss. Edit this array to design your wave pattern!")]
        [SerializeField] private WaveEntry[] waveSequence = new WaveEntry[]
        {
            // Default 15-entry test sequence
            new WaveEntry { spawnType = SpawnType.Nothing },
            new WaveEntry { spawnType = SpawnType.Nothing },
            new WaveEntry { spawnType = SpawnType.Resources, resourceNodeCount = 2, resourceNodeLevel = 1 },
            new WaveEntry { spawnType = SpawnType.Nothing },
            new WaveEntry { spawnType = SpawnType.Enemies, enemySoldierCount = 2 },
            new WaveEntry { spawnType = SpawnType.Nothing },
            new WaveEntry { spawnType = SpawnType.Nothing },
            new WaveEntry { spawnType = SpawnType.Enemies, enemySoldierCount = 1, enemyNinjaCount = 1 },
            new WaveEntry { spawnType = SpawnType.Nothing },
            new WaveEntry { spawnType = SpawnType.Resources, resourceNodeCount = 1, resourceNodeLevel = 1 },
            new WaveEntry { spawnType = SpawnType.Nothing },
            new WaveEntry { spawnType = SpawnType.Nothing },
            new WaveEntry { spawnType = SpawnType.Enemies, enemySoldierCount = 3 },
            new WaveEntry { spawnType = SpawnType.Nothing },
            new WaveEntry { spawnType = SpawnType.Boss, bossCount = 1, bossHP = 30, bossDamage = 5 }
        };

        [Tooltip("Ticks between wave advances (4 for 4-sided game = 1 wave per full round)")]
        [SerializeField] private int ticksPerWaveAdvance = 4;

        [Header("Resource Nodes")]
        [SerializeField] private GameObject resourceNodeLevel1Prefab; // Drag prefab, or leave empty for procedural
        [SerializeField] private int level1HP = 10;
        [SerializeField] private int level1TokensPerHit = 1;
        [SerializeField] private int level1BonusTokens = 3;
        [Space(5)]
        [SerializeField] private GameObject resourceNodeLevel2Prefab; // Drag prefab, or leave empty for procedural
        [SerializeField] private int level2HP = 20;
        [SerializeField] private int level2TokensPerHit = 1;
        [SerializeField] private int level2BonusTokens = 6;
        [Space(5)]
        [SerializeField] private GameObject resourceNodeLevel3Prefab; // Drag prefab, or leave empty for procedural
        [SerializeField] private int level3HP = 50;
        [SerializeField] private int level3TokensPerHit = 2;
        [SerializeField] private int level3BonusTokens = 9;

        [Header("Visual Settings")]
        [SerializeField] private Color playerColor = new Color(0.2f, 0.5f, 1f);
        [SerializeField] private Color enemyColor = new Color(1f, 0.3f, 0.3f);
        [SerializeField] private Color resourceColor = new Color(0.2f, 0.85f, 0.4f);
        [SerializeField] private float unitHPOverlayScale = 1.5f; // Scale of HP bar + text above units
        [SerializeField] private Vector3 cameraRotation = new Vector3(51.15f, 42.7f, 0f);
        [SerializeField] private float cameraOrthoSize = 8.8f;
        [SerializeField] private float cameraPanSpeed = 5f;

        // Player prefabs (runtime-created)
        private GameObject soldierPrefab;
        private GameObject ogrePrefab;
        private GameObject ninjaPrefab;

        // Enemy prefabs (runtime-created, separate visuals from player)
        private GameObject enemySoldierPrefab;
        private GameObject enemyOgrePrefab;
        private GameObject enemyNinjaPrefab;

        private GameObject resourceNodePrefab;

        // Iteration 8: Multi-level resource prefabs
        private GameObject level1ResourcePrefab;
        private GameObject level2ResourcePrefab;
        private GameObject level3ResourcePrefab;

        private void Awake()
        {
            SetupCamera();
            SetupGrid();
            SetupFogOfWar();
            SetupGridExpansion(); // Iteration 9: Grid expansion system
            SetupIntervalTimer();
            SetupTokenManager();
            SetupUnitPrefabs(); // Iteration 6: Create all unit prefabs
            SetupResourceNodePrefabs(); // Iteration 8: Create all 3 resource levels
            SetupRaritySystem(); // Iteration 6: Must be before WaveManager
            SetupWaveManager(); // Iteration 10: Handles ALL spawning (enemies + resources)
            SetupUI();
            SetupDockBar();
            // SetupWaveTimelineUI(); // REMOVED: Timeline UI now manually created in Unity Editor scene
            SetupGameOverManager();
            SetupDebugPanel();
            SetupDebugMenu();
            SetupDebugPlacer();
            // SetupTimelineTestButton(); // Removed: Timeline integrates automatically with WaveManager
            SetupLighting();
        }

        private void SetupCamera()
        {
            // Remove any existing camera
            Camera existingCam = Camera.main;
            if (existingCam != null)
            {
                DestroyImmediate(existingCam.gameObject);
            }

            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            Camera cam = camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();

            cam.orthographic = true;
            cam.orthographicSize = cameraOrthoSize;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.12f);
            cam.clearFlags = CameraClearFlags.SolidColor;

            // Set rotation from inspector
            Quaternion rot = Quaternion.Euler(cameraRotation);
            camObj.transform.rotation = rot;

            // Auto-center camera on grid origin (0,0,0)
            // For ortho camera, position along the forward axis doesn't affect view size,
            // but we need enough distance so nothing clips the near plane.
            Vector3 forward = rot * Vector3.forward;
            camObj.transform.position = -forward * 20f;

            // Add camera pan controller
            CameraPan pan = camObj.AddComponent<CameraPan>();
            SetPrivateField(pan, "panSpeed", cameraPanSpeed);
        }

        private void SetupGrid()
        {
            GameObject gridObj = new GameObject("GridManager");
            GridManager gridManager = gridObj.AddComponent<GridManager>();
            gridObj.AddComponent<GridVisualizer>();

            SetPrivateField(gridManager, "gridWidth", gridWidth);
            SetPrivateField(gridManager, "gridHeight", gridHeight);
            SetPrivateField(gridManager, "cellSize", cellSize);

            gridManager.InitializeGrid();
        }

        private void SetupFogOfWar()
        {
            // Create FogManager (Iteration 7: simplified fog system)
            GameObject fogManagerObj = new GameObject("FogManager");
            FogManager fogManager = fogManagerObj.AddComponent<FogManager>();

            // Create FogGridVisualizer for fog overlay visuals
            GameObject fogVisualizerObj = new GameObject("FogGridVisualizer");
            FogGridVisualizer fogVisualizer = fogVisualizerObj.AddComponent<FogGridVisualizer>();

            // Initialize both systems
            if (GridManager.Instance != null)
            {
                fogManager.Initialize(GridManager.Instance.Width, GridManager.Instance.Height);
                fogVisualizer.Initialize(GridManager.Instance, fogManager);
            }
        }

        private void SetupIntervalTimer()
        {
            GameObject timerObj = new GameObject("IntervalTimer");
            IntervalTimer timer = timerObj.AddComponent<IntervalTimer>();
            SetPrivateField(timer, "baseIntervalDuration", baseIntervalDuration);
        }

        private void SetupTokenManager()
        {
            GameObject tokenObj = new GameObject("ResourceTokenManager");
            tokenObj.AddComponent<ResourceTokenManager>();
        }


        /// <summary>
        /// Iteration 8: Create all three resource node levels (1x1, 2x1, 2x2).
        /// </summary>
        private void SetupResourceNodePrefabs()
        {
            // Level 1: 1x1 (small, green)
            level1ResourcePrefab = CreateResourceNodePrefab(
                resourceNodeLevel1Prefab, "ResourceNode_Level1",
                new Color(0.2f, 0.85f, 0.4f), 1f,
                1, level1HP, level1TokensPerHit, level1BonusTokens, new Vector2Int(1, 1));

            // Level 2: 2x1 (medium, yellow-green, larger scale)
            level2ResourcePrefab = CreateResourceNodePrefab(
                resourceNodeLevel2Prefab, "ResourceNode_Level2",
                new Color(0.6f, 0.9f, 0.3f), 1.5f,
                2, level2HP, level2TokensPerHit, level2BonusTokens, new Vector2Int(2, 1));

            // Level 3: 2x2 (large, blue-green, much larger scale)
            level3ResourcePrefab = CreateResourceNodePrefab(
                resourceNodeLevel3Prefab, "ResourceNode_Level3",
                new Color(0.2f, 0.7f, 0.9f), 2.0f,
                3, level3HP, level3TokensPerHit, level3BonusTokens, new Vector2Int(2, 2));

            // Keep legacy reference for backward compatibility
            resourceNodePrefab = level1ResourcePrefab;

            Debug.Log("Created 3 resource node levels: L1 (1x1), L2 (2x1), L3 (2x2)");
        }

        private GameObject CreateResourceNodePrefab(
            GameObject overridePrefab, string prefabName, Color color, float scale,
            int level, int hp, int tokensPerHit, int bonusTokens, Vector2Int gridSize)
        {
            GameObject prefab;

            if (overridePrefab != null)
            {
                prefab = Instantiate(overridePrefab);
                prefab.transform.localScale = overridePrefab.transform.localScale * scale;
                prefab.transform.localPosition = Vector3.zero;
            }
            else
            {
                prefab = ResourceNodeModelBuilder.CreateResourceNodeModel(color);
                prefab.transform.localScale = Vector3.one * scale;
            }

            ResourceNode node = prefab.GetComponent<ResourceNode>();
            if (node == null) node = prefab.AddComponent<ResourceNode>();
            SetPrivateField(node, "maxHP", hp);
            SetPrivateField(node, "level", level);
            SetPrivateField(node, "tokensPerHit", tokensPerHit);
            SetPrivateField(node, "bonusTokens", bonusTokens);
            SetPrivateField(node, "gridSize", gridSize);

            if (prefab.GetComponent<HPBarOverlay>() == null)
                prefab.AddComponent<HPBarOverlay>();

            prefab.SetActive(false);
            prefab.name = prefabName;

            return prefab;
        }

        // REMOVED: SetupResourceSpawner() - Replaced by WaveManager (Iteration 10)
        // WaveManager now handles ALL spawning via wave entries

        /// <summary>
        /// Iteration 6: Create all three unit type prefabs (Soldier, Ogre, Ninja)
        /// </summary>
        private void SetupUnitPrefabs()
        {
            // --- Player prefabs ---
            soldierPrefab = CreateUnitPrefab(
                soldierPlayerPrefab, "SoldierPrefab", playerColor, soldierModelScale,
                soldierHP, soldierAttackDamage, soldierAttackRange,
                soldierAttackInterval, soldierResourceCost);

            ogrePrefab = CreateUnitPrefab(
                ogrePlayerPrefab, "OgrePrefab", playerColor, ogreModelScale,
                ogreHP, ogreAttackDamage, ogreAttackRange,
                ogreAttackInterval, ogreResourceCost);

            ninjaPrefab = CreateUnitPrefab(
                ninjaPlayerPrefab, "NinjaPrefab", playerColor, ninjaModelScale,
                ninjaHP, ninjaAttackDamage, ninjaAttackRange,
                ninjaAttackInterval, ninjaResourceCost);

            // --- Enemy prefabs (use enemyColor for procedural fallback) ---
            enemySoldierPrefab = CreateUnitPrefab(
                soldierEnemyPrefab, "EnemySoldierPrefab", enemyColor, soldierModelScale,
                soldierHP, soldierAttackDamage, soldierAttackRange,
                soldierAttackInterval, soldierResourceCost);

            enemyOgrePrefab = CreateUnitPrefab(
                ogreEnemyPrefab, "EnemyOgrePrefab", enemyColor, ogreModelScale,
                ogreHP, ogreAttackDamage, ogreAttackRange,
                ogreAttackInterval, ogreResourceCost);

            enemyNinjaPrefab = CreateUnitPrefab(
                ninjaEnemyPrefab, "EnemyNinjaPrefab", enemyColor, ninjaModelScale,
                ninjaHP, ninjaAttackDamage, ninjaAttackRange,
                ninjaAttackInterval, ninjaResourceCost);

            Debug.Log("Created 6 unit prefabs: 3 player + 3 enemy");
        }

        /// <summary>
        /// Creates a unit prefab. If an override prefab is assigned, clones it and configures stats.
        /// Otherwise, generates a procedural model from primitives.
        /// </summary>
        private GameObject CreateUnitPrefab(
            GameObject overridePrefab, string prefabName, Color color, float scale,
            int hp, int attackDamage, int attackRange, int attackInterval, int resourceCost)
        {
            GameObject prefab;

            if (overridePrefab != null)
            {
                // Use the inspector-assigned prefab (clone it so we don't modify the asset)
                prefab = Instantiate(overridePrefab);
                // Preserve the prefab's original scale and multiply by inspector scale factor
                // (FBX models often have scale 100 baked in)
                prefab.transform.localScale = overridePrefab.transform.localScale * scale;
                // Reset position (will be set when spawning)
                prefab.transform.localPosition = Vector3.zero;
            }
            else
            {
                // Fall back to procedural model generation
                prefab = UnitModelBuilder.CreateSoldierModel(color);
                prefab.transform.localScale = Vector3.one * scale;
            }

            // Add SoldierUnit component if not already present
            SoldierUnit unit = prefab.GetComponent<SoldierUnit>();
            if (unit == null) unit = prefab.AddComponent<SoldierUnit>();
            SetPrivateField(unit, "maxHP", hp);
            SetPrivateField(unit, "attackDamage", attackDamage);
            SetPrivateField(unit, "attackRange", attackRange);
            SetPrivateField(unit, "attackIntervalMultiplier", attackInterval);
            SetPrivateField(unit, "resourceCost", resourceCost);

            // Add HPBarOverlay if not already present, and set scale
            HPBarOverlay hpBar = prefab.GetComponent<HPBarOverlay>();
            if (hpBar == null) hpBar = prefab.AddComponent<HPBarOverlay>();
            SetPrivateField(hpBar, "overlayScale", unitHPOverlayScale);

            prefab.SetActive(false);
            prefab.name = prefabName;

            return prefab;
        }

        /// <summary>
        /// Iteration 6: Set up RaritySystem and register all unit stats
        /// </summary>
        private void SetupRaritySystem()
        {
            GameObject rarityObj = new GameObject("RaritySystem");
            RaritySystem raritySystem = rarityObj.AddComponent<RaritySystem>();

            // Create UnitStats for each unit type
            List<UnitStats> allStats = new List<UnitStats>();

            // Soldier Stats (Common)
            UnitStats soldierStats = ScriptableObject.CreateInstance<UnitStats>();
            soldierStats.unitType = UnitType.Soldier;
            soldierStats.unitName = "Soldier";
            soldierStats.rarity = Rarity.Common;
            soldierStats.maxHP = soldierHP;
            soldierStats.attackDamage = soldierAttackDamage;
            soldierStats.attackRange = soldierAttackRange;
            soldierStats.attackIntervalMultiplier = soldierAttackInterval;
            soldierStats.resourceCost = soldierResourceCost;
            soldierStats.revealRadius = soldierRevealRadius;
            soldierStats.unitColor = playerColor;
            soldierStats.modelScale = soldierModelScale;
            soldierStats.unitPrefab = soldierPrefab;
            soldierStats.enemyPrefab = enemySoldierPrefab;
            allStats.Add(soldierStats);

            // Ogre Stats (Epic)
            UnitStats ogreStats = ScriptableObject.CreateInstance<UnitStats>();
            ogreStats.unitType = UnitType.Ogre;
            ogreStats.unitName = "Ogre";
            ogreStats.rarity = Rarity.Epic;
            ogreStats.maxHP = ogreHP;
            ogreStats.attackDamage = ogreAttackDamage;
            ogreStats.attackRange = ogreAttackRange;
            ogreStats.attackIntervalMultiplier = ogreAttackInterval;
            ogreStats.resourceCost = ogreResourceCost;
            ogreStats.revealRadius = ogreRevealRadius;
            ogreStats.unitColor = playerColor;
            ogreStats.modelScale = ogreModelScale;
            ogreStats.unitPrefab = ogrePrefab;
            ogreStats.enemyPrefab = enemyOgrePrefab;
            allStats.Add(ogreStats);

            // Ninja Stats (Rare)
            UnitStats ninjaStats = ScriptableObject.CreateInstance<UnitStats>();
            ninjaStats.unitType = UnitType.Ninja;
            ninjaStats.unitName = "Ninja";
            ninjaStats.rarity = Rarity.Rare;
            ninjaStats.maxHP = ninjaHP;
            ninjaStats.attackDamage = ninjaAttackDamage;
            ninjaStats.attackRange = ninjaAttackRange;
            ninjaStats.attackIntervalMultiplier = ninjaAttackInterval;
            ninjaStats.resourceCost = ninjaResourceCost;
            ninjaStats.revealRadius = ninjaRevealRadius;
            ninjaStats.unitColor = playerColor;
            ninjaStats.modelScale = ninjaModelScale;
            ninjaStats.unitPrefab = ninjaPrefab;
            ninjaStats.enemyPrefab = enemyNinjaPrefab;
            allStats.Add(ninjaStats);

            // Pass rarity weights from inspector
            SetPrivateField(raritySystem, "commonWeight", soldierRarityWeight);
            SetPrivateField(raritySystem, "rareWeight", ninjaRarityWeight);
            SetPrivateField(raritySystem, "epicWeight", ogreRarityWeight);

            // Register with RaritySystem
            raritySystem.RegisterUnitStats(allStats);

            Debug.Log("RaritySystem initialized with 3 unit types");
        }

        private void SetupUI()
        {
            // UI is now manually created in Unity Editor!
            // This method just ensures EventSystem exists and finds UI components

            // Create EventSystem if missing (required for button clicks)
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // All UI should be manually created in the scene following the guide in:
            // SETUP_UI_HIERARCHY.md (to be created)

            Debug.Log("SetupUI: UI should be manually created in scene hierarchy");
        }

        private void SetupDockBar()
        {
            // DockBarManager and DragDropHandler should be manually created in Unity Editor!
            // Iteration 10: HandManager removed - DockBarManager is now self-sufficient

            // Find DragDropHandler (should exist as standalone GameObject)
            DragDropHandler dragHandler = FindObjectOfType<DragDropHandler>();
            if (dragHandler == null)
            {
                Debug.LogWarning("DragDropHandler not found in scene. Please create it manually.");
            }

            // Find DockBarManager (should exist under UICanvas)
            DockBarManager dockManager = FindObjectOfType<DockBarManager>();
            if (dockManager == null)
            {
                Debug.LogWarning("DockBarManager not found in scene. Please create it manually under UICanvas.");
                return;
            }

            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                dockManager.Initialize(canvas);
            }

            Debug.Log("SetupDockBar: Found and initialized dock bar components");
        }

        private void SetupDebugPanel()
        {
            // DebugPanel and HiddenDebugButton should be manually created in Unity Editor!
            // This method just finds them if they exist

            DebugPanel debugPanel = FindObjectOfType<DebugPanel>();
            if (debugPanel == null)
            {
                Debug.LogWarning("DebugPanel not found in scene. Debug panel will not be available.");
            }

            HiddenDebugButton hiddenButton = FindObjectOfType<HiddenDebugButton>();
            if (hiddenButton == null)
            {
                Debug.LogWarning("HiddenDebugButton not found in scene. Debug panel will not be toggleable.");
            }

            Debug.Log("SetupDebugPanel: Checked for debug panel components");
        }

        private void SetupDebugMenu()
        {
            // DebugMenu should be manually created in Unity Editor!
            // This method just finds it and initializes if needed

            DebugMenu debugMenu = FindObjectOfType<DebugMenu>();
            if (debugMenu == null)
            {
                Debug.LogWarning("DebugMenu not found in scene. Debug menu will not be available.");
                return;
            }

            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                debugMenu.Initialize(canvas);
            }

            Debug.Log("SetupDebugMenu: Found and initialized debug menu");
        }

        private void SetupDebugPlacer()
        {
            // CellDebugPlacer should be manually created in Unity Editor!
            // This method just finds it and assigns prefab references if needed

            CellDebugPlacer placer = FindObjectOfType<CellDebugPlacer>();
            if (placer == null)
            {
                Debug.LogWarning("CellDebugPlacer not found in scene. Debug placement will not be available.");
                return;
            }

            // Assign prefab references using reflection
            SetPrivateField(placer, "soldierPrefab", soldierPrefab);
            SetPrivateField(placer, "enemySoldierPrefab", enemySoldierPrefab);
            SetPrivateField(placer, "resourceNodePrefab", resourceNodePrefab);

            Debug.Log("SetupDebugPlacer: Found and configured debug placer");
        }

        private void SetupWaveManager()
        {
            // Add WaveManager component to this GameObject (GameSetup)
            // This allows waveSequence to be edited in GameSetup Inspector!
            WaveManager waveManager = gameObject.AddComponent<WaveManager>();

            // Pass the wave sequence from GameSetup Inspector
            waveManager.waveSequence = waveSequence;
            SetPrivateField(waveManager, "resourceNodePrefab", resourceNodePrefab);

            waveManager.Initialize(ticksPerWaveAdvance);

            Debug.Log($"SetupWaveManager: Initialized WaveManager with {waveSequence.Length} wave entries, {ticksPerWaveAdvance} ticks per advance");
        }

        // SetupWaveTimelineUI() REMOVED: Timeline UI is now manually created in Unity Editor
        // To set up the timeline UI:
        // 1. Add a GameObject to the UICanvas in the scene hierarchy
        // 2. Name it "SpawnTimelineContainer"
        // 3. Add the SpawnTimelineUI component
        // 4. Assign the UI references in the Inspector
        // See SpawnTimelineUI.cs for details

        private void SetupGameOverManager()
        {
            // GameOverManager should be manually created in Unity Editor!
            // This method just finds it and initializes if needed

            GameOverManager gameOverManager = FindObjectOfType<GameOverManager>();
            if (gameOverManager == null)
            {
                Debug.LogWarning("GameOverManager not found in scene. Game over screen will not work.");
                return;
            }

            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("No Canvas found for GameOverManager");
                return;
            }

            gameOverManager.Initialize(canvas);
            Debug.Log("SetupGameOverManager: Found and initialized game over manager");
        }

        private void SetupLighting()
        {
            // Remove existing directional light if any
            Light[] existingLights = FindObjectsOfType<Light>();
            foreach (Light l in existingLights)
            {
                DestroyImmediate(l.gameObject);
            }

            // Main directional light
            GameObject lightObj = new GameObject("Directional Light");
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.9f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Ambient light
            RenderSettings.ambientLight = new Color(0.3f, 0.3f, 0.4f);
        }

        /// <summary>
        /// Iteration 9: Initialize grid expansion system.
        /// </summary>
        private void SetupGridExpansion()
        {
            GameObject expansionObj = new GameObject("GridExpansionManager");
            GridExpansionManager expansion = expansionObj.AddComponent<GridExpansionManager>();

            // Initialize with skipTutorial = true for now (can add tutorial later)
            expansion.Initialize(skipTutorial: true);

            Debug.Log("GridExpansionManager initialized");
        }

        /// <summary>
        /// Helper to set private/serialized fields on MonoBehaviours at runtime.
        /// </summary>
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
            Debug.LogWarning($"Could not find field '{fieldName}' on {target.GetType().Name}");
        }
    }
}
