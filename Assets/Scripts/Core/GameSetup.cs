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
        [SerializeField] private Color resourceColor = new Color(0.2f, 0.85f, 0.4f);
        [SerializeField] private float cameraHeight = 12f;
        [SerializeField] private float cameraTiltAngle = 15f;

        private GameObject soldierPrefab;
        private GameObject resourceNodePrefab;

        private void Awake()
        {
            SetupCamera();
            SetupGrid();
            SetupIntervalTimer();
            SetupTokenManager();
            SetupSoldierPrefab();
            SetupResourceNodePrefab();
            SetupUI();
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

            SetPrivateField(soldierUnit, "hp", soldierHP);
            SetPrivateField(soldierUnit, "attackDamage", soldierAttackDamage);
            SetPrivateField(soldierUnit, "attackRange", soldierAttackRange);
            SetPrivateField(soldierUnit, "attackIntervalMultiplier", soldierAttackInterval);
            SetPrivateField(soldierUnit, "resourceCost", soldierResourceCost);

            soldierPrefab.SetActive(false);
            soldierPrefab.name = "SoldierPrefab";
        }

        private void SetupResourceNodePrefab()
        {
            resourceNodePrefab = ResourceNodeModelBuilder.CreateResourceNodeModel(
                resourceColor,
                out Transform hpBarFill,
                out Transform hpBarBg
            );

            ResourceNode node = resourceNodePrefab.AddComponent<ResourceNode>();
            SetPrivateField(node, "maxHP", resourceNodeHP);
            SetPrivateField(node, "level", 1);
            SetPrivateField(node, "tokensPerHit", resourceTokensPerHit);
            SetPrivateField(node, "bonusTokens", resourceBonusTokens);

            // HP bar is found by name ("HPBarFill") in ResourceNode.Start()

            resourceNodePrefab.SetActive(false);
            resourceNodePrefab.name = "ResourceNodePrefab";
        }

        private void SetupUI()
        {
            // Create canvas
            GameObject canvasObj = new GameObject("UICanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            // Interval text (top-left corner)
            GameObject textObj = new GameObject("IntervalText");
            textObj.transform.SetParent(canvasObj.transform, false);

            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = "Interval: 0";
            text.fontSize = 24;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 1);
            textRect.anchorMax = new Vector2(0, 1);
            textRect.pivot = new Vector2(0, 1);
            textRect.anchoredPosition = new Vector2(20, -20);
            textRect.sizeDelta = new Vector2(300, 50);

            // Progress bar background
            GameObject progressBg = new GameObject("ProgressBarBG");
            progressBg.transform.SetParent(canvasObj.transform, false);
            Image bgImage = progressBg.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            RectTransform bgRect = progressBg.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 1);
            bgRect.anchorMax = new Vector2(0, 1);
            bgRect.pivot = new Vector2(0, 1);
            bgRect.anchoredPosition = new Vector2(20, -60);
            bgRect.sizeDelta = new Vector2(200, 10);

            // Progress bar fill
            GameObject progressFill = new GameObject("ProgressBarFill");
            progressFill.transform.SetParent(progressBg.transform, false);
            Image fillImage = progressFill.AddComponent<Image>();
            fillImage.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;

            RectTransform fillRect = progressFill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

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
            instructions.text = "Left-click: Place Soldier  |  Right-click: Place Resource";
            instructions.fontSize = 18;
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
            SetPrivateField(intervalUI, "intervalText", text);
            SetPrivateField(intervalUI, "progressBar", fillImage);

            // Hook up token UI
            TokenUI tokenUI = canvasObj.AddComponent<TokenUI>();
            SetPrivateField(tokenUI, "tokenText", tokenText);
        }

        private void SetupDebugPlacer()
        {
            GameObject placerObj = new GameObject("DebugPlacer");
            CellDebugPlacer placer = placerObj.AddComponent<CellDebugPlacer>();
            SetPrivateField(placer, "soldierPrefab", soldierPrefab);
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
