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
        [SerializeField] private GameObject soldierPrefabOverride; // Drag a prefab here, or leave empty for procedural
        [SerializeField] private float soldierRarityWeight = 60f;
        [SerializeField] private int soldierHP = 10;
        [SerializeField] private int soldierAttackDamage = 3;
        [SerializeField] private int soldierAttackRange = 1;
        [SerializeField] private int soldierAttackInterval = 2;
        [SerializeField] private int soldierResourceCost = 3;
        [SerializeField] private int soldierRevealRadius = 1;

        [Header("Ogre Stats (Epic)")]
        [SerializeField] private GameObject ogrePrefabOverride; // Drag a prefab here, or leave empty for procedural
        [SerializeField] private float ogreRarityWeight = 5f;
        [SerializeField] private int ogreHP = 20;
        [SerializeField] private int ogreAttackDamage = 5;
        [SerializeField] private int ogreAttackRange = 2;
        [SerializeField] private int ogreAttackInterval = 4;
        [SerializeField] private int ogreResourceCost = 6;
        [SerializeField] private int ogreRevealRadius = 1;
        [SerializeField] private float ogreModelScale = 1.3f;
        [SerializeField] private Color ogreColor = new Color(0.8f, 0.4f, 1f);

        [Header("Ninja Stats (Rare)")]
        [SerializeField] private GameObject ninjaPrefabOverride; // Drag a prefab here, or leave empty for procedural
        [SerializeField] private float ninjaRarityWeight = 35f;
        [SerializeField] private int ninjaHP = 5;
        [SerializeField] private int ninjaAttackDamage = 1;
        [SerializeField] private int ninjaAttackRange = 1;
        [SerializeField] private int ninjaAttackInterval = 1;
        [SerializeField] private int ninjaResourceCost = 4;
        [SerializeField] private int ninjaRevealRadius = 2;
        [SerializeField] private float ninjaModelScale = 0.8f;
        [SerializeField] private Color ninjaColor = new Color(1f, 0.3f, 0.3f);

        [Header("Wave Settings")]
        [SerializeField] private int intervalsPerWave = 20;
        [SerializeField] private int baseEnemyCount = 2;
        [SerializeField] private float enemyScaling = 1.2f;
        [SerializeField] private int maxWaves = 20;
        [SerializeField] private float baseDowntime = 10f;
        [SerializeField] private float minDowntime = 5f;
        [SerializeField] private int downtimeDecreaseWave = 10;

        [Header("Resource Node Stats (Level 1)")]
        [SerializeField] private int resourceNodeHP = 10;
        [SerializeField] private int resourceTokensPerHit = 1;
        [SerializeField] private int resourceBonusTokens = 3;

        [Header("Iteration 8: Multi-Level Resources")]
        [SerializeField] private int level2HP = 20;
        [SerializeField] private int level2TokensPerHit = 1;
        [SerializeField] private int level2BonusTokens = 6;
        [SerializeField] private int level3HP = 50;
        [SerializeField] private int level3TokensPerHit = 2;
        [SerializeField] private int level3BonusTokens = 9;

        [Header("Visual Settings")]
        [SerializeField] private Color playerColor = new Color(0.2f, 0.5f, 1f);
        [SerializeField] private Color enemyColor = new Color(1f, 0.3f, 0.3f);
        [SerializeField] private Color resourceColor = new Color(0.2f, 0.85f, 0.4f);
        [SerializeField] private float cameraHeight = 12f;
        [SerializeField] private float cameraTiltAngle = 15f;

        private GameObject soldierPrefab;
        private GameObject ogrePrefab;
        private GameObject ninjaPrefab;
        private GameObject enemySoldierPrefab;
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
            SetupResourceSpawner(); // Iteration 8: Initialize spawning system
            SetupRaritySystem(); // Iteration 6: Must be before WaveManager/HandManager
            SetupWaveManager();
            SetupUI();
            SetupDockBar();
            SetupWaveTimelineUI();
            SetupGameOverManager();
            SetupDebugPanel();
            SetupDebugMenu();
            SetupDebugPlacer();
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
            cam.orthographicSize = (gridHeight * cellSize) * 0.8f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.12f);
            cam.clearFlags = CameraClearFlags.SolidColor;

            // Position camera above grid looking down with slight tilt
            float tiltRad = cameraTiltAngle * Mathf.Deg2Rad;
            float verticalOffset = cameraHeight * Mathf.Cos(tiltRad);
            float horizontalOffset = cameraHeight * Mathf.Sin(tiltRad);

            camObj.transform.position = new Vector3(0f, verticalOffset, -horizontalOffset);
            camObj.transform.LookAt(Vector3.zero, Vector3.up);
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
            level1ResourcePrefab = ResourceNodeModelBuilder.CreateResourceNodeModel(new Color(0.2f, 0.85f, 0.4f)); // Green
            ResourceNode node1 = level1ResourcePrefab.AddComponent<ResourceNode>();
            SetPrivateField(node1, "maxHP", resourceNodeHP);
            SetPrivateField(node1, "level", 1);
            SetPrivateField(node1, "tokensPerHit", resourceTokensPerHit);
            SetPrivateField(node1, "bonusTokens", resourceBonusTokens);
            SetPrivateField(node1, "gridSize", new Vector2Int(1, 1));
            level1ResourcePrefab.AddComponent<HPBarOverlay>();
            level1ResourcePrefab.SetActive(false);
            level1ResourcePrefab.name = "ResourceNode_Level1";

            // Level 2: 2x1 (medium, yellow-green, larger scale)
            level2ResourcePrefab = ResourceNodeModelBuilder.CreateResourceNodeModel(new Color(0.6f, 0.9f, 0.3f)); // Yellow-green
            level2ResourcePrefab.transform.localScale = Vector3.one * 1.5f; // Larger
            ResourceNode node2 = level2ResourcePrefab.AddComponent<ResourceNode>();
            SetPrivateField(node2, "maxHP", level2HP);
            SetPrivateField(node2, "level", 2);
            SetPrivateField(node2, "tokensPerHit", level2TokensPerHit);
            SetPrivateField(node2, "bonusTokens", level2BonusTokens);
            SetPrivateField(node2, "gridSize", new Vector2Int(2, 1)); // Horizontal by default, can be rotated
            level2ResourcePrefab.AddComponent<HPBarOverlay>();
            level2ResourcePrefab.SetActive(false);
            level2ResourcePrefab.name = "ResourceNode_Level2";

            // Level 3: 2x2 (large, blue-green, much larger scale)
            level3ResourcePrefab = ResourceNodeModelBuilder.CreateResourceNodeModel(new Color(0.2f, 0.7f, 0.9f)); // Blue-green
            level3ResourcePrefab.transform.localScale = Vector3.one * 2.0f; // Much larger
            ResourceNode node3 = level3ResourcePrefab.AddComponent<ResourceNode>();
            SetPrivateField(node3, "maxHP", level3HP);
            SetPrivateField(node3, "level", 3);
            SetPrivateField(node3, "tokensPerHit", level3TokensPerHit);
            SetPrivateField(node3, "bonusTokens", level3BonusTokens);
            SetPrivateField(node3, "gridSize", new Vector2Int(2, 2));
            level3ResourcePrefab.AddComponent<HPBarOverlay>();
            level3ResourcePrefab.SetActive(false);
            level3ResourcePrefab.name = "ResourceNode_Level3";

            // Keep legacy reference for backward compatibility
            resourceNodePrefab = level1ResourcePrefab;

            Debug.Log("Created 3 resource node levels: L1 (1x1), L2 (2x1), L3 (2x2)");
        }

        /// <summary>
        /// Iteration 8: Initialize Resource Spawner system.
        /// </summary>
        private void SetupResourceSpawner()
        {
            GameObject spawnerObj = new GameObject("ResourceSpawner");
            ResourceSpawner spawner = spawnerObj.AddComponent<ResourceSpawner>();

            // Set spawn interval (every 10 intervals = 20 seconds)
            SetPrivateField(spawner, "spawnIntervalCount", 10);

            // Set node caps
            SetPrivateField(spawner, "maxTotalNodes", 5);
            SetPrivateField(spawner, "maxLevel2Nodes", 2);
            SetPrivateField(spawner, "maxLevel3Nodes", 1);

            // Set probabilities (60/35/5 distribution)
            SetPrivateField(spawner, "level1Probability", 60f);
            SetPrivateField(spawner, "level2Probability", 35f);
            SetPrivateField(spawner, "level3Probability", 5f);

            // Set spawn preferences
            SetPrivateField(spawner, "preferFoggedSpawns", true);
            SetPrivateField(spawner, "foggedSpawnWeight", 70f);

            // Assign prefabs
            SetPrivateField(spawner, "level1Prefab", level1ResourcePrefab);
            SetPrivateField(spawner, "level2Prefab", level2ResourcePrefab);
            SetPrivateField(spawner, "level3Prefab", level3ResourcePrefab);

            // Initialize spawner (spawns initial resource in revealed area)
            spawner.Initialize();

            Debug.Log("ResourceSpawner initialized: Spawns every 10 intervals, max 5 nodes (L2 max: 2, L3 max: 1)");
        }

        /// <summary>
        /// Iteration 6: Create all three unit type prefabs (Soldier, Ogre, Ninja)
        /// </summary>
        private void SetupUnitPrefabs()
        {
            // Create Soldier prefab (use override if assigned, otherwise generate procedurally)
            soldierPrefab = CreateUnitPrefab(
                soldierPrefabOverride, "SoldierPrefab", playerColor, 1f,
                soldierHP, soldierAttackDamage, soldierAttackRange,
                soldierAttackInterval, soldierResourceCost);

            // Create Ogre prefab (use override if assigned, otherwise generate procedurally)
            ogrePrefab = CreateUnitPrefab(
                ogrePrefabOverride, "OgrePrefab", ogreColor, ogreModelScale,
                ogreHP, ogreAttackDamage, ogreAttackRange,
                ogreAttackInterval, ogreResourceCost);

            // Create Ninja prefab (use override if assigned, otherwise generate procedurally)
            ninjaPrefab = CreateUnitPrefab(
                ninjaPrefabOverride, "NinjaPrefab", ninjaColor, ninjaModelScale,
                ninjaHP, ninjaAttackDamage, ninjaAttackRange,
                ninjaAttackInterval, ninjaResourceCost);

            // Store enemy variant (same prefabs, different team applied at spawn)
            enemySoldierPrefab = soldierPrefab;

            Debug.Log("Created 3 unit type prefabs: Soldier, Ogre, Ninja");
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
                prefab.transform.localScale = Vector3.one * scale;
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

            // Add HPBarOverlay if not already present
            if (prefab.GetComponent<HPBarOverlay>() == null)
                prefab.AddComponent<HPBarOverlay>();

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
            soldierStats.modelScale = 1f;
            soldierStats.unitPrefab = soldierPrefab;
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
            ogreStats.unitColor = ogreColor;
            ogreStats.modelScale = ogreModelScale;
            ogreStats.unitPrefab = ogrePrefab;
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
            ninjaStats.unitColor = ninjaColor;
            ninjaStats.modelScale = ninjaModelScale;
            ninjaStats.unitPrefab = ninjaPrefab;
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
            // Create EventSystem for UI input (required for button clicks)
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Create canvas
            GameObject canvasObj = new GameObject("UICanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            // VERTICAL INTERVAL TIMER BAR (left edge)
            // Background strip
            GameObject intervalBarBg = new GameObject("IntervalBarBackground");
            intervalBarBg.transform.SetParent(canvasObj.transform, false);
            Image barBgImage = intervalBarBg.AddComponent<Image>();
            barBgImage.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            barBgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.7f);

            RectTransform barBgRect = intervalBarBg.GetComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0, 0);
            barBgRect.anchorMax = new Vector2(0, 1);
            barBgRect.pivot = new Vector2(0, 0.5f);
            barBgRect.anchoredPosition = new Vector2(0, 0);
            barBgRect.sizeDelta = new Vector2(25f, -40f); // 25px wide, 20px padding top/bottom

            // Vertical fill bar (fills upward)
            GameObject intervalBarFill = new GameObject("IntervalBarFill");
            intervalBarFill.transform.SetParent(intervalBarBg.transform, false);
            Image fillImage = intervalBarFill.AddComponent<Image>();
            fillImage.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            fillImage.color = new Color(0f, 0.83f, 1f, 0.95f); // Bright cyan #00D4FF
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Vertical;
            fillImage.fillOrigin = (int)Image.OriginVertical.Bottom; // Fill from bottom upward

            RectTransform fillRect = intervalBarFill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            // Interval count number (small, at top of bar)
            GameObject intervalTextObj = new GameObject("IntervalText");
            intervalTextObj.transform.SetParent(intervalBarBg.transform, false);

            TextMeshProUGUI intervalText = intervalTextObj.AddComponent<TextMeshProUGUI>();
            intervalText.text = "0";
            intervalText.fontSize = 16;
            intervalText.color = Color.white;
            intervalText.alignment = TextAlignmentOptions.Center;
            intervalText.fontStyle = FontStyles.Bold;

            RectTransform intervalTextRect = intervalTextObj.GetComponent<RectTransform>();
            intervalTextRect.anchorMin = new Vector2(0, 1);
            intervalTextRect.anchorMax = new Vector2(1, 1);
            intervalTextRect.pivot = new Vector2(0.5f, 1);
            intervalTextRect.anchoredPosition = new Vector2(0, 5f);
            intervalTextRect.sizeDelta = new Vector2(0, 30f);

            // TOKEN DISPLAY (top-right corner, Phase 6 redesign)
            // Container with background
            GameObject tokenContainerObj = new GameObject("TokenContainer");
            tokenContainerObj.transform.SetParent(canvasObj.transform, false);

            RectTransform tokenContainer = tokenContainerObj.AddComponent<RectTransform>();
            tokenContainer.anchorMin = new Vector2(1, 1);
            tokenContainer.anchorMax = new Vector2(1, 1);
            tokenContainer.pivot = new Vector2(1, 1);
            tokenContainer.anchoredPosition = new Vector2(-20, -20);
            tokenContainer.sizeDelta = new Vector2(140f, 60f);

            // Background panel
            Image containerBg = tokenContainerObj.AddComponent<Image>();
            containerBg.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            containerBg.color = new Color(0.15f, 0.15f, 0.2f, 0.9f); // Dark purple-gray

            // Token icon (circular coin)
            GameObject iconObj = new GameObject("TokenIcon");
            iconObj.transform.SetParent(tokenContainerObj.transform, false);

            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0, 0.5f);
            iconRect.anchorMax = new Vector2(0, 0.5f);
            iconRect.pivot = new Vector2(0, 0.5f);
            iconRect.anchoredPosition = new Vector2(15f, 0f);
            iconRect.sizeDelta = new Vector2(40f, 40f);

            Image tokenIcon = iconObj.AddComponent<Image>();
            tokenIcon.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            tokenIcon.color = new Color(1f, 0.9f, 0.2f); // Gold
            tokenIcon.type = Image.Type.Filled;
            tokenIcon.fillMethod = Image.FillMethod.Radial360;
            tokenIcon.fillOrigin = (int)Image.Origin360.Top;

            // Inner circle (creates coin effect)
            GameObject innerCircleObj = new GameObject("InnerCircle");
            innerCircleObj.transform.SetParent(iconObj.transform, false);

            RectTransform innerRect = innerCircleObj.AddComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.sizeDelta = new Vector2(-10f, -10f); // Inset by 5px on each side

            Image innerCircle = innerCircleObj.AddComponent<Image>();
            innerCircle.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            innerCircle.color = new Color(0.8f, 0.7f, 0.1f); // Darker gold

            // Token count text
            GameObject tokenTextObj = new GameObject("TokenText");
            tokenTextObj.transform.SetParent(tokenContainerObj.transform, false);

            TextMeshProUGUI tokenText = tokenTextObj.AddComponent<TextMeshProUGUI>();
            tokenText.text = "10";
            tokenText.fontSize = 36;
            tokenText.color = new Color(1f, 0.95f, 0.8f); // Light cream
            tokenText.alignment = TextAlignmentOptions.MidlineRight;
            tokenText.fontStyle = FontStyles.Bold;

            RectTransform tokenTextRect = tokenTextObj.GetComponent<RectTransform>();
            tokenTextRect.anchorMin = new Vector2(0.4f, 0);
            tokenTextRect.anchorMax = new Vector2(1f, 1f);
            tokenTextRect.pivot = new Vector2(1, 0.5f);
            tokenTextRect.anchoredPosition = new Vector2(-10f, 0f);
            tokenTextRect.sizeDelta = Vector2.zero;

            // WAVE TIMELINE (top-center, below token counter)
            GameObject timelineContainerObj = new GameObject("WaveTimelineContainer");
            timelineContainerObj.transform.SetParent(canvasObj.transform, false);

            RectTransform timelineContainer = timelineContainerObj.AddComponent<RectTransform>();
            timelineContainer.anchorMin = new Vector2(0.5f, 1f);
            timelineContainer.anchorMax = new Vector2(0.5f, 1f);
            timelineContainer.pivot = new Vector2(0.5f, 1f);
            timelineContainer.anchoredPosition = new Vector2(0f, -70f); // Below token text
            timelineContainer.sizeDelta = new Vector2(600f, 80f);

            // Background
            Image timelineBg = timelineContainerObj.AddComponent<Image>();
            timelineBg.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            timelineBg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

            // Progress fill bar (shows current position in timeline)
            GameObject progressObj = new GameObject("ProgressFill");
            progressObj.transform.SetParent(timelineContainerObj.transform, false);

            Image progressFill = progressObj.AddComponent<Image>();
            progressFill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            progressFill.color = new Color(0f, 0.83f, 1f, 0.5f); // Semi-transparent cyan
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;

            RectTransform progressRect = progressObj.GetComponent<RectTransform>();
            progressRect.anchorMin = Vector2.zero;
            progressRect.anchorMax = Vector2.one;
            progressRect.sizeDelta = Vector2.zero;

            // Markers panel (holds wave markers)
            GameObject markersPanelObj = new GameObject("MarkersPanel");
            markersPanelObj.transform.SetParent(timelineContainerObj.transform, false);

            RectTransform markersPanel = markersPanelObj.AddComponent<RectTransform>();
            markersPanel.anchorMin = Vector2.zero;
            markersPanel.anchorMax = Vector2.one;
            markersPanel.sizeDelta = Vector2.zero;

            // Wave info text (above timeline)
            GameObject waveInfoObj = new GameObject("WaveInfoText");
            waveInfoObj.transform.SetParent(timelineContainerObj.transform, false);

            TextMeshProUGUI waveInfoText = waveInfoObj.AddComponent<TextMeshProUGUI>();
            waveInfoText.text = "Wave 1: 40s";
            waveInfoText.fontSize = 18;
            waveInfoText.color = Color.white;
            waveInfoText.alignment = TextAlignmentOptions.Center;
            waveInfoText.fontStyle = FontStyles.Bold;

            RectTransform waveInfoRect = waveInfoObj.GetComponent<RectTransform>();
            waveInfoRect.anchorMin = new Vector2(0f, 1f);
            waveInfoRect.anchorMax = new Vector2(1f, 1f);
            waveInfoRect.pivot = new Vector2(0.5f, 0f);
            waveInfoRect.anchoredPosition = new Vector2(0f, 5f);
            waveInfoRect.sizeDelta = new Vector2(0f, 25f);

            // Add WaveTimelineUI component
            WaveTimelineUI waveTimelineUI = canvasObj.AddComponent<WaveTimelineUI>();
            SetPrivateField(waveTimelineUI, "timelineContainer", timelineContainer);
            SetPrivateField(waveTimelineUI, "timelineBackground", timelineBg);
            SetPrivateField(waveTimelineUI, "progressFill", progressFill);
            SetPrivateField(waveTimelineUI, "markersPanel", markersPanel);
            SetPrivateField(waveTimelineUI, "waveInfoText", waveInfoText);

            // Instructions text (bottom-center)
            GameObject instructionsObj = new GameObject("InstructionsText");
            instructionsObj.transform.SetParent(canvasObj.transform, false);

            TextMeshProUGUI instructions = instructionsObj.AddComponent<TextMeshProUGUI>();
            instructions.text = "Left-click: Player Soldier (3 tokens)  |  Right-click: Resource  |  Middle/Shift+Left: Enemy Soldier";
            instructions.fontSize = 16;
            instructions.color = new Color(1f, 1f, 1f, 0.6f);
            instructions.alignment = TextAlignmentOptions.Bottom;

            RectTransform instrRect = instructionsObj.GetComponent<RectTransform>();
            instrRect.anchorMin = new Vector2(0.5f, 0);
            instrRect.anchorMax = new Vector2(0.5f, 0);
            instrRect.pivot = new Vector2(0.5f, 0);
            instrRect.anchoredPosition = new Vector2(0, 20);
            instrRect.sizeDelta = new Vector2(500, 40);

            // Hook up IntervalUI
            IntervalUI intervalUI = canvasObj.AddComponent<IntervalUI>();
            SetPrivateField(intervalUI, "intervalText", intervalText);
            SetPrivateField(intervalUI, "verticalBar", fillImage);

            // Hook up token UI (Phase 6)
            TokenUI tokenUI = canvasObj.AddComponent<TokenUI>();
            SetPrivateField(tokenUI, "tokenText", tokenText);
            SetPrivateField(tokenUI, "tokenIcon", tokenIcon);
            SetPrivateField(tokenUI, "container", tokenContainer);
        }

        private void SetupDockBar()
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            // Create DragDropHandler singleton
            GameObject dragHandlerObj = new GameObject("DragDropHandler");
            dragHandlerObj.AddComponent<DragDropHandler>();

            // Create HandManager (Phase 2, updated Iteration 6)
            GameObject handObj = new GameObject("HandManager");
            HandManager handManager = handObj.AddComponent<HandManager>();
            handManager.Initialize(); // Iteration 6: Uses RaritySystem

            // Give player starting hand (3 Soldiers - Phase 2 spec)
            handManager.GiveStartingHand();

            // Create DockBarManager
            GameObject dockObj = new GameObject("DockBarManager");
            dockObj.transform.SetParent(canvas.transform, false);

            DockBarManager dockManager = dockObj.AddComponent<DockBarManager>();
            dockManager.Initialize(canvas, handManager);
        }

        private void SetupDebugPanel()
        {
            // Find the canvas (created in SetupUI)
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            // Create debug panel root (hidden by default)
            GameObject panelRoot = new GameObject("DebugPanelRoot");
            panelRoot.transform.SetParent(canvas.transform, false);

            Image panelBg = panelRoot.AddComponent<Image>();
            panelBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

            RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(400, 350);

            // Title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(panelRoot.transform, false);
            TextMeshProUGUI title = titleObj.AddComponent<TextMeshProUGUI>();
            title.text = "DEBUG PANEL";
            title.fontSize = 28;
            title.color = Color.yellow;
            title.alignment = TextAlignmentOptions.Center;
            title.fontStyle = FontStyles.Bold;

            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.anchoredPosition = new Vector2(0, -20);
            titleRect.sizeDelta = new Vector2(-40, 40);

            // Buttons container
            float yPos = -80;
            float buttonHeight = 45;
            float spacing = 10;

            // Pause button
            Button pauseBtn = CreateDebugButton(panelRoot.transform, "Pause", yPos);
            TextMeshProUGUI pauseBtnText = pauseBtn.GetComponentInChildren<TextMeshProUGUI>();
            yPos -= buttonHeight + spacing;

            // Speed buttons row
            GameObject speedRow = new GameObject("SpeedRow");
            speedRow.transform.SetParent(panelRoot.transform, false);
            RectTransform speedRowRect = speedRow.AddComponent<RectTransform>();
            speedRowRect.anchorMin = new Vector2(0, 1);
            speedRowRect.anchorMax = new Vector2(1, 1);
            speedRowRect.pivot = new Vector2(0.5f, 1);
            speedRowRect.anchoredPosition = new Vector2(0, yPos);
            speedRowRect.sizeDelta = new Vector2(-40, buttonHeight);

            Button speed1x = CreateSmallButton(speedRow.transform, "1x", 0);
            Button speed2x = CreateSmallButton(speedRow.transform, "2x", 1);
            Button speed4x = CreateSmallButton(speedRow.transform, "4x", 2);
            yPos -= buttonHeight + spacing;

            // Speed label
            GameObject speedLabelObj = new GameObject("SpeedLabel");
            speedLabelObj.transform.SetParent(panelRoot.transform, false);
            TextMeshProUGUI speedLabel = speedLabelObj.AddComponent<TextMeshProUGUI>();
            speedLabel.text = "Speed: 1x";
            speedLabel.fontSize = 20;
            speedLabel.color = Color.white;
            speedLabel.alignment = TextAlignmentOptions.Center;

            RectTransform speedLabelRect = speedLabelObj.GetComponent<RectTransform>();
            speedLabelRect.anchorMin = new Vector2(0, 1);
            speedLabelRect.anchorMax = new Vector2(1, 1);
            speedLabelRect.pivot = new Vector2(0.5f, 1);
            speedLabelRect.anchoredPosition = new Vector2(0, yPos);
            speedLabelRect.sizeDelta = new Vector2(-40, 30);
            yPos -= 40 + spacing;

            // Add 100 Tokens button
            Button addTokensBtn = CreateDebugButton(panelRoot.transform, "+100 Tokens", yPos);
            yPos -= buttonHeight + spacing;

            // Clear All button
            Button clearBtn = CreateDebugButton(panelRoot.transform, "Clear All", yPos);

            // Add DebugPanel component
            DebugPanel debugPanel = canvas.gameObject.AddComponent<DebugPanel>();
            SetPrivateField(debugPanel, "panelRoot", panelRoot);
            SetPrivateField(debugPanel, "pauseButton", pauseBtn);
            SetPrivateField(debugPanel, "clearAllButton", clearBtn);
            SetPrivateField(debugPanel, "add100TokensButton", addTokensBtn);
            SetPrivateField(debugPanel, "speed1xButton", speed1x);
            SetPrivateField(debugPanel, "speed2xButton", speed2x);
            SetPrivateField(debugPanel, "speed4xButton", speed4x);
            SetPrivateField(debugPanel, "pauseButtonText", pauseBtnText);
            SetPrivateField(debugPanel, "speedText", speedLabel);

            // Create hidden tap button (top-right corner)
            GameObject hiddenBtnObj = new GameObject("HiddenDebugButton");
            hiddenBtnObj.transform.SetParent(canvas.transform, false);

            Image hiddenImg = hiddenBtnObj.AddComponent<Image>();
            hiddenImg.color = new Color(0, 0, 0, 0.01f); // Nearly invisible

            RectTransform hiddenRect = hiddenBtnObj.GetComponent<RectTransform>();
            hiddenRect.anchorMin = new Vector2(1, 1);
            hiddenRect.anchorMax = new Vector2(1, 1);
            hiddenRect.pivot = new Vector2(1, 1);
            hiddenRect.anchoredPosition = new Vector2(-10, -10);
            hiddenRect.sizeDelta = new Vector2(100, 100);

            hiddenBtnObj.AddComponent<HiddenDebugButton>();
        }

        private Button CreateDebugButton(Transform parent, string text, float yPos)
        {
            GameObject btnObj = new GameObject($"Button_{text}");
            btnObj.transform.SetParent(parent, false);

            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.3f, 0.3f, 0.3f, 1f);

            Button btn = btnObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            colors.highlightedColor = new Color(0.4f, 0.4f, 0.4f, 1f);
            colors.pressedColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            btn.colors = colors;

            RectTransform btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0, 1);
            btnRect.anchorMax = new Vector2(1, 1);
            btnRect.pivot = new Vector2(0.5f, 1);
            btnRect.anchoredPosition = new Vector2(0, yPos);
            btnRect.sizeDelta = new Vector2(-40, 45);

            // Button text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 20;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            return btn;
        }

        private Button CreateSmallButton(Transform parent, string text, int index)
        {
            GameObject btnObj = new GameObject($"Button_{text}");
            btnObj.transform.SetParent(parent, false);

            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.3f, 0.3f, 0.3f, 1f);

            Button btn = btnObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            colors.highlightedColor = new Color(0.4f, 0.4f, 0.4f, 1f);
            colors.pressedColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            btn.colors = colors;

            RectTransform btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0, 0);
            btnRect.anchorMax = new Vector2(0, 1);
            btnRect.pivot = new Vector2(0, 0.5f);

            float width = 110;
            float spacing = 10;
            btnRect.anchoredPosition = new Vector2(index * (width + spacing), 0);
            btnRect.sizeDelta = new Vector2(width, 0);

            // Button text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 18;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            return btn;
        }

        private void SetupDebugMenu()
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            GameObject debugMenuObj = new GameObject("DebugMenu");
            debugMenuObj.transform.SetParent(canvas.transform, false);

            DebugMenu debugMenu = debugMenuObj.AddComponent<DebugMenu>();
            debugMenu.Initialize(canvas);
        }

        private void SetupDebugPlacer()
        {
            GameObject placerObj = new GameObject("DebugPlacer");
            CellDebugPlacer placer = placerObj.AddComponent<CellDebugPlacer>();
            SetPrivateField(placer, "soldierPrefab", soldierPrefab);
            SetPrivateField(placer, "enemySoldierPrefab", enemySoldierPrefab);
            SetPrivateField(placer, "resourceNodePrefab", resourceNodePrefab);
        }

        private void SetupWaveManager()
        {
            GameObject waveObj = new GameObject("WaveManager");
            WaveManager waveManager = waveObj.AddComponent<WaveManager>();

            // Pass wave configuration from inspector
            SetPrivateField(waveManager, "intervalsPerWave", intervalsPerWave);
            SetPrivateField(waveManager, "baseEnemyCount", baseEnemyCount);
            SetPrivateField(waveManager, "enemyScaling", enemyScaling);
            SetPrivateField(waveManager, "maxWaves", maxWaves);
            SetPrivateField(waveManager, "baseDowntime", baseDowntime);
            SetPrivateField(waveManager, "minDowntime", minDowntime);
            SetPrivateField(waveManager, "downtimeDecreaseWave", downtimeDecreaseWave);

            waveManager.Initialize();
        }

        private void SetupWaveTimelineUI()
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("No Canvas found for WaveTimelineUI");
                return;
            }

            GameObject timelineObj = new GameObject("WaveTimelineUI");
            WaveTimelineUI timelineUI = timelineObj.AddComponent<WaveTimelineUI>();
            timelineUI.Initialize(canvas);
        }

        private void SetupGameOverManager()
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("No Canvas found for GameOverManager");
                return;
            }

            GameObject gameOverObj = new GameObject("GameOverManager");
            GameOverManager gameOverManager = gameOverObj.AddComponent<GameOverManager>();
            gameOverManager.Initialize(canvas);
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
