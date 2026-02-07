using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
        [SerializeField] private int gridWidth = 4;
        [SerializeField] private int gridHeight = 4;
        [SerializeField] private float cellSize = 1.5f;

        [Header("Interval Settings")]
        [SerializeField] private float baseIntervalDuration = 2.0f;

        [Header("Soldier Stats")]
        [SerializeField] private int soldierHP = 10;
        [SerializeField] private int soldierAttackDamage = 3;
        [SerializeField] private int soldierAttackRange = 1;
        [SerializeField] private int soldierAttackInterval = 2;
        [SerializeField] private int soldierResourceCost = 3;

        [Header("Resource Node Stats (Level 1)")]
        [SerializeField] private int resourceNodeHP = 10;
        [SerializeField] private int resourceTokensPerHit = 1;
        [SerializeField] private int resourceBonusTokens = 3;

        [Header("Visual Settings")]
        [SerializeField] private Color playerColor = new Color(0.2f, 0.5f, 1f);
        [SerializeField] private Color enemyColor = new Color(1f, 0.3f, 0.3f);
        [SerializeField] private Color resourceColor = new Color(0.2f, 0.85f, 0.4f);
        [SerializeField] private float cameraHeight = 12f;
        [SerializeField] private float cameraTiltAngle = 15f;

        private GameObject soldierPrefab;
        private GameObject enemySoldierPrefab;
        private GameObject resourceNodePrefab;

        private void Awake()
        {
            SetupCamera();
            SetupGrid();
            SetupFogOfWar();
            SetupIntervalTimer();
            SetupTokenManager();
            SetupUnitPrefabs(); // Iteration 6: Create all unit prefabs
            SetupResourceNodePrefab();
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
            GameObject fogObj = new GameObject("FogOfWar");
            FogOfWar fogOfWar = fogObj.AddComponent<FogOfWar>();

            // Initialize with grid manager (Phase 7)
            if (GridManager.Instance != null)
            {
                fogOfWar.Initialize(GridManager.Instance);
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


        private void SetupResourceNodePrefab()
        {
            resourceNodePrefab = ResourceNodeModelBuilder.CreateResourceNodeModel(resourceColor);

            ResourceNode node = resourceNodePrefab.AddComponent<ResourceNode>();
            SetPrivateField(node, "maxHP", resourceNodeHP);
            SetPrivateField(node, "level", 1);
            SetPrivateField(node, "tokensPerHit", resourceTokensPerHit);
            SetPrivateField(node, "bonusTokens", resourceBonusTokens);

            // HP text is found by name ("HPText") in ResourceNode.Start()

            // Add HP bar overlay (Phase 5)
            resourceNodePrefab.AddComponent<HPBarOverlay>();

            resourceNodePrefab.SetActive(false);
            resourceNodePrefab.name = "ResourceNodePrefab";
        }

        /// <summary>
        /// Iteration 6: Create all three unit type prefabs (Soldier, Ogre, Ninja)
        /// </summary>
        private void SetupUnitPrefabs()
        {
            // Create Soldier prefab (existing logic, just refactored)
            soldierPrefab = UnitModelBuilder.CreateSoldierModel(new Color(0.2f, 0.5f, 1f)); // Blue
            SoldierUnit soldierUnit = soldierPrefab.AddComponent<SoldierUnit>();
            SetPrivateField(soldierUnit, "maxHP", 10);
            SetPrivateField(soldierUnit, "attackDamage", 3);
            SetPrivateField(soldierUnit, "attackRange", 1);
            SetPrivateField(soldierUnit, "attackIntervalMultiplier", 2);
            SetPrivateField(soldierUnit, "resourceCost", 3);
            soldierPrefab.AddComponent<HPBarOverlay>();
            soldierPrefab.SetActive(false);
            soldierPrefab.name = "SoldierPrefab";

            // Create Ogre prefab (tank, large, slow)
            GameObject ogrePrefab = UnitModelBuilder.CreateSoldierModel(new Color(0.8f, 0.4f, 1f)); // Purple
            ogrePrefab.transform.localScale = Vector3.one * 1.3f; // Larger
            SoldierUnit ogreUnit = ogrePrefab.AddComponent<SoldierUnit>();
            SetPrivateField(ogreUnit, "maxHP", 20);
            SetPrivateField(ogreUnit, "attackDamage", 5);
            SetPrivateField(ogreUnit, "attackRange", 2); // Range 2!
            SetPrivateField(ogreUnit, "attackIntervalMultiplier", 4); // Slow rotation
            SetPrivateField(ogreUnit, "resourceCost", 6);
            ogrePrefab.AddComponent<HPBarOverlay>();
            ogrePrefab.SetActive(false);
            ogrePrefab.name = "OgrePrefab";

            // Create Ninja prefab (fast, fragile, small)
            GameObject ninjaPrefab = UnitModelBuilder.CreateSoldierModel(new Color(1f, 0.3f, 0.3f)); // Red
            ninjaPrefab.transform.localScale = Vector3.one * 0.8f; // Smaller
            SoldierUnit ninjaUnit = ninjaPrefab.AddComponent<SoldierUnit>();
            SetPrivateField(ninjaUnit, "maxHP", 5);
            SetPrivateField(ninjaUnit, "attackDamage", 1);
            SetPrivateField(ninjaUnit, "attackRange", 1);
            SetPrivateField(ninjaUnit, "attackIntervalMultiplier", 1); // Fast rotation!
            SetPrivateField(ninjaUnit, "resourceCost", 4);
            ninjaPrefab.AddComponent<HPBarOverlay>();
            ninjaPrefab.SetActive(false);
            ninjaPrefab.name = "NinjaPrefab";

            // Store enemy variant (same prefabs, different team applied at spawn)
            enemySoldierPrefab = soldierPrefab;

            Debug.Log("Created 3 unit type prefabs: Soldier, Ogre, Ninja");
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
            soldierStats.maxHP = 10;
            soldierStats.attackDamage = 3;
            soldierStats.attackRange = 1;
            soldierStats.attackIntervalMultiplier = 2;
            soldierStats.resourceCost = 3;
            soldierStats.unitColor = new Color(0.2f, 0.5f, 1f); // Blue
            soldierStats.modelScale = 1f;
            soldierStats.unitPrefab = soldierPrefab;
            allStats.Add(soldierStats);

            // Ogre Stats (Epic)
            GameObject ogrePrefab = GameObject.Find("OgrePrefab");
            UnitStats ogreStats = ScriptableObject.CreateInstance<UnitStats>();
            ogreStats.unitType = UnitType.Ogre;
            ogreStats.unitName = "Ogre";
            ogreStats.rarity = Rarity.Epic;
            ogreStats.maxHP = 20;
            ogreStats.attackDamage = 5;
            ogreStats.attackRange = 2; // Range 2!
            ogreStats.attackIntervalMultiplier = 4;
            ogreStats.resourceCost = 6;
            ogreStats.unitColor = new Color(0.8f, 0.4f, 1f); // Purple
            ogreStats.modelScale = 1.3f;
            ogreStats.unitPrefab = ogrePrefab;
            allStats.Add(ogreStats);

            // Ninja Stats (Rare)
            GameObject ninjaPrefab = GameObject.Find("NinjaPrefab");
            UnitStats ninjaStats = ScriptableObject.CreateInstance<UnitStats>();
            ninjaStats.unitType = UnitType.Ninja;
            ninjaStats.unitName = "Ninja";
            ninjaStats.rarity = Rarity.Rare;
            ninjaStats.maxHP = 5;
            ninjaStats.attackDamage = 1;
            ninjaStats.attackRange = 1;
            ninjaStats.attackIntervalMultiplier = 1; // Fast!
            ninjaStats.resourceCost = 4;
            ninjaStats.unitColor = new Color(1f, 0.3f, 0.3f); // Red
            ninjaStats.modelScale = 0.8f;
            ninjaStats.unitPrefab = ninjaPrefab;
            allStats.Add(ninjaStats);

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
            waveManager.Initialize(); // Iteration 6: No longer needs enemy prefab
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
