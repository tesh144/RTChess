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
            SetupIntervalTimer();
            SetupTokenManager();
            SetupSoldierPrefab();
            SetupEnemySoldierPrefab();
            SetupResourceNodePrefab();
            SetupUI();
            SetupDockBar();
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

        private void SetupSoldierPrefab()
        {
            soldierPrefab = UnitModelBuilder.CreateSoldierModel(playerColor);
            SoldierUnit soldierUnit = soldierPrefab.AddComponent<SoldierUnit>();

            SetPrivateField(soldierUnit, "maxHP", soldierHP);
            SetPrivateField(soldierUnit, "attackDamage", soldierAttackDamage);
            SetPrivateField(soldierUnit, "attackRange", soldierAttackRange);
            SetPrivateField(soldierUnit, "attackIntervalMultiplier", soldierAttackInterval);
            SetPrivateField(soldierUnit, "resourceCost", soldierResourceCost);
            SetPrivateField(soldierUnit, "team", Team.Player);

            soldierPrefab.SetActive(false);
            soldierPrefab.name = "SoldierPrefab";
        }

        private void SetupEnemySoldierPrefab()
        {
            enemySoldierPrefab = UnitModelBuilder.CreateSoldierModel(enemyColor);
            SoldierUnit enemyUnit = enemySoldierPrefab.AddComponent<SoldierUnit>();

            SetPrivateField(enemyUnit, "maxHP", soldierHP);
            SetPrivateField(enemyUnit, "attackDamage", soldierAttackDamage);
            SetPrivateField(enemyUnit, "attackRange", soldierAttackRange);
            SetPrivateField(enemyUnit, "attackIntervalMultiplier", soldierAttackInterval);
            SetPrivateField(enemyUnit, "resourceCost", soldierResourceCost);
            SetPrivateField(enemyUnit, "team", Team.Enemy);

            enemySoldierPrefab.SetActive(false);
            enemySoldierPrefab.name = "EnemySoldierPrefab";
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

            resourceNodePrefab.SetActive(false);
            resourceNodePrefab.name = "ResourceNodePrefab";
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

            // Token counter (top-right corner)
            GameObject tokenObj = new GameObject("TokenText");
            tokenObj.transform.SetParent(canvasObj.transform, false);

            TextMeshProUGUI tokenText = tokenObj.AddComponent<TextMeshProUGUI>();
            tokenText.text = "Tokens: 0";
            tokenText.fontSize = 28;
            tokenText.color = new Color(1f, 0.9f, 0.2f);
            tokenText.alignment = TextAlignmentOptions.TopRight;
            tokenText.fontStyle = FontStyles.Bold;

            RectTransform tokenRect = tokenObj.GetComponent<RectTransform>();
            tokenRect.anchorMin = new Vector2(1, 1);
            tokenRect.anchorMax = new Vector2(1, 1);
            tokenRect.pivot = new Vector2(1, 1);
            tokenRect.anchoredPosition = new Vector2(-20, -20);
            tokenRect.sizeDelta = new Vector2(300, 50);

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

            // Hook up token UI
            TokenUI tokenUI = canvasObj.AddComponent<TokenUI>();
            SetPrivateField(tokenUI, "tokenText", tokenText);
        }

        private void SetupDockBar()
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            // Create DragDropHandler singleton
            GameObject dragHandlerObj = new GameObject("DragDropHandler");
            dragHandlerObj.AddComponent<DragDropHandler>();

            // Create HandManager (Phase 2)
            GameObject handObj = new GameObject("HandManager");
            HandManager handManager = handObj.AddComponent<HandManager>();
            handManager.Initialize(soldierPrefab, soldierPrefab, soldierPrefab); // TODO: Add Ninja and Ogre prefabs

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
