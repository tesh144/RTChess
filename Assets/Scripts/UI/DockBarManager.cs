using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

namespace ClockworkGrid
{
    /// <summary>
    /// Manages the dock bar UI at the bottom of the screen.
    /// Handles drawing units into the dock and removing them when placed.
    /// Iteration 11: Now supports editor-created UI elements
    /// </summary>
    public class DockBarManager : MonoBehaviour
    {
        [Header("Prefab References")]
        [SerializeField] private GameObject unitIconPrefab; // Custom card design prefab

        [Header("Editor UI References (Optional - assign to use existing UI)")]
        [SerializeField] private GameObject dockBarHolder; // The dock bar panel/GameObject to show/hide
        [SerializeField] private GameObject gatchaButtonHolder; // The gatcha/draw button container to show/hide
        [SerializeField] private Transform dockIconsContainer; // Parent for red card holders
        [SerializeField] private Button drawButton; // White button on the right
        [SerializeField] private TextMeshProUGUI drawButtonText; // Text on draw button
        [SerializeField] private TextMeshProUGUI costNumberText; // Cost display on gatcha button (CostNumber TMP)
        [SerializeField] private Image costFillImage; // Fill bar showing time until cost decrease

        [Header("Draw Cost Settings")]
        [SerializeField] private int baseDrawCost = 6; // Starting cost for the first draw
        [SerializeField] private int costIncrement = 1; // How much cost increases per draw
        [SerializeField] private int costDecreaseInterval = 0; // Intervals between cost decreases (0 = disabled)

        [Header("Runtime Creation Settings (if no UI references assigned)")]
        [SerializeField] private bool createUIAtRuntime = false;

        [Header("Animation Settings")]
        [SerializeField] private bool enableSlideAnimation = true;
        [SerializeField] private float slideUpDistance = 150f; // Distance to slide from below
        [SerializeField] private float slideUpDuration = 0.6f; // Animation duration

        // UI References (assigned from editor OR created at runtime)
        private RectTransform dockBarContainer;
        private Vector2 originalAnchoredPosition;
        private Image backgroundImage;
        private RectTransform dockIconsPanel;
        private HorizontalLayoutGroup layoutGroup;
        private GameObject dealButtonObj;

        // Unit tracking
        private List<UnitIcon> unitIcons = new List<UnitIcon>();
        private GameObject[] availableUnitPrefabs;

        // Draw cost tracking (Iteration 10: Self-sufficient, no HandManager)
        private int drawCount = 0;
        private int ticksSinceCostDecrease = 0;

        // Singleton pattern
        public static DockBarManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // Hide ONLY the gatcha button at start (dock bar with cards stays visible)
            // Gatcha button appears after wave starts
            if (gatchaButtonHolder != null && (WaveManager.Instance == null || !WaveManager.Instance.HasWaveStarted))
            {
                Debug.Log("[DockBarManager] Hiding gatcha button holder at start");
                gatchaButtonHolder.SetActive(false);
            }
            else if (gatchaButtonHolder == null)
            {
                Debug.LogError("[DockBarManager] gatchaButtonHolder is not assigned! Please assign it in the Inspector.");
            }

            // Dock bar holder stays visible throughout (or assign nothing if entire dock bar is one object)
        }

        /// <summary>
        /// Initialize the dock bar with UI hierarchy (Iteration 10: No HandManager)
        /// </summary>
        public void Initialize(Canvas canvas)
        {
            // Use editor UI if assigned, otherwise create at runtime
            if (dockIconsContainer != null && drawButton != null)
            {
                SetupEditorUI();
            }
            else if (createUIAtRuntime)
            {
                CreateDockBarUI(canvas);
            }
            else
            {
                Debug.LogError("DockBarManager: No UI references assigned and createUIAtRuntime is false!");
                return;
            }

            UpdateDealButtonDisplay();

            // Subscribe to token changes to update button state
            if (ResourceTokenManager.Instance != null)
            {
                ResourceTokenManager.Instance.OnTokensChanged += OnTokensChanged;
            }

            // Subscribe to interval timer for cost decrease over time
            if (costDecreaseInterval > 0 && IntervalTimer.Instance != null)
            {
                IntervalTimer.Instance.OnIntervalTick += OnIntervalTickCostDecrease;
            }

            // Cache RectTransform and original position for animation
            if (dockBarContainer == null)
            {
                dockBarContainer = GetComponent<RectTransform>();
            }
            if (dockBarContainer != null)
            {
                originalAnchoredPosition = dockBarContainer.anchoredPosition;
            }

            // Add starting soldier card to dock
            AddStartingCard();
        }

        /// <summary>
        /// Add a starting soldier card to the dock at game start.
        /// </summary>
        private void AddStartingCard()
        {
            if (RaritySystem.Instance != null)
            {
                // Get Soldier unit stats
                UnitStats soldierStats = RaritySystem.Instance.GetUnitStats(UnitType.Soldier);
                if (soldierStats != null)
                {
                    AddUnitToDock(soldierStats);
                    Debug.Log("[DockBarManager] Added starting Soldier card to dock");
                }
                else
                {
                    Debug.LogWarning("[DockBarManager] Could not find Soldier stats for starting card");
                }
            }
            else
            {
                Debug.LogWarning("[DockBarManager] RaritySystem.Instance is null, cannot add starting card");
            }
        }

        private void OnDestroy()
        {
            if (ResourceTokenManager.Instance != null)
            {
                ResourceTokenManager.Instance.OnTokensChanged -= OnTokensChanged;
            }
            if (IntervalTimer.Instance != null)
            {
                IntervalTimer.Instance.OnIntervalTick -= OnIntervalTickCostDecrease;
            }
        }

        /// <summary>
        /// Show the dock bar with slide-up animation after countdown completes.
        /// </summary>
        public void ShowWithAnimation()
        {
            // Show both holders if assigned
            if (dockBarHolder != null)
            {
                dockBarHolder.SetActive(true);
            }

            if (gatchaButtonHolder != null)
            {
                gatchaButtonHolder.SetActive(true);
            }

            if (enableSlideAnimation && dockBarContainer != null)
            {
                StartCoroutine(SlideUpAnimation());
            }
        }

        /// <summary>
        /// Animate dock bar sliding up from below the screen.
        /// </summary>
        private System.Collections.IEnumerator SlideUpAnimation()
        {
            if (dockBarContainer == null) yield break;

            // Start position (below screen)
            Vector2 startPos = originalAnchoredPosition - new Vector2(0, slideUpDistance);
            Vector2 endPos = originalAnchoredPosition;

            dockBarContainer.anchoredPosition = startPos;

            float elapsed = 0f;
            while (elapsed < slideUpDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / slideUpDuration;

                // Smooth ease-out curve
                float smoothT = 1f - Mathf.Pow(1f - t, 3f);

                dockBarContainer.anchoredPosition = Vector2.Lerp(startPos, endPos, smoothT);
                yield return null;
            }

            // Ensure final position is exact
            dockBarContainer.anchoredPosition = endPos;
        }

        /// <summary>
        /// Setup using editor-created UI elements
        /// </summary>
        private void SetupEditorUI()
        {
            // Use assigned container for icons
            dockIconsPanel = dockIconsContainer.GetComponent<RectTransform>();

            // Clear any pre-existing mock-up cards from editor
            ClearDockContainerChildren();

            // Ensure HorizontalLayoutGroup exists
            layoutGroup = dockIconsContainer.GetComponent<HorizontalLayoutGroup>();
            if (layoutGroup == null)
            {
                layoutGroup = dockIconsContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
                layoutGroup.spacing = 10f;
                layoutGroup.padding = new RectOffset(10, 10, 10, 10);
                layoutGroup.childAlignment = TextAnchor.MiddleCenter;
                layoutGroup.childControlWidth = false;
                layoutGroup.childControlHeight = false;
                layoutGroup.childForceExpandWidth = false;
                layoutGroup.childForceExpandHeight = false;
            }

            // Setup draw button
            if (drawButton != null)
            {
                drawButton.onClick.AddListener(OnDealButtonClicked);

                // Find or create button text
                if (drawButtonText == null)
                {
                    drawButtonText = drawButton.GetComponentInChildren<TextMeshProUGUI>();
                }

                if (drawButtonText == null)
                {
                    // Create text if it doesn't exist
                    GameObject textObj = new GameObject("ButtonText");
                    RectTransform textRect = textObj.AddComponent<RectTransform>();
                    textRect.SetParent(drawButton.transform, false);
                    textRect.anchorMin = Vector2.zero;
                    textRect.anchorMax = Vector2.one;
                    textRect.sizeDelta = Vector2.zero;

                    drawButtonText = textObj.AddComponent<TextMeshProUGUI>();
                    drawButtonText.fontSize = 20;
                    drawButtonText.color = Color.white;
                    drawButtonText.alignment = TextAlignmentOptions.Center;
                    drawButtonText.fontStyle = FontStyles.Bold;
                }
            }

            Debug.Log("DockBarManager: Using editor-created UI elements");
        }

        /// <summary>
        /// Create UI at runtime (legacy mode)
        /// </summary>
        private void CreateDockBarUI(Canvas canvas)
        {
            // Create main container
            dockBarContainer = CreateUIObject<RectTransform>("DockBarContainer", canvas.transform);
            dockBarContainer.anchorMin = new Vector2(0.5f, 0f);
            dockBarContainer.anchorMax = new Vector2(0.5f, 0f);
            dockBarContainer.pivot = new Vector2(0.5f, 0f);
            dockBarContainer.anchoredPosition = new Vector2(0f, 20f);
            dockBarContainer.sizeDelta = new Vector2(700f, 100f);

            // Add background image
            backgroundImage = dockBarContainer.gameObject.AddComponent<Image>();
            backgroundImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            // Create icons panel (holds unit icons)
            GameObject iconsPanelObj = new GameObject("DockIconsPanel");
            dockIconsPanel = iconsPanelObj.AddComponent<RectTransform>();
            dockIconsPanel.SetParent(dockBarContainer, false);
            dockIconsPanel.anchorMin = new Vector2(0f, 0f);
            dockIconsPanel.anchorMax = new Vector2(1f, 1f);
            dockIconsPanel.pivot = new Vector2(0.5f, 0.5f);
            dockIconsPanel.anchoredPosition = Vector2.zero;
            dockIconsPanel.sizeDelta = new Vector2(-150f, 0f); // Leave space for button

            // Add HorizontalLayoutGroup for auto-spacing
            layoutGroup = iconsPanelObj.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.spacing = 10f;
            layoutGroup.padding = new RectOffset(10, 10, 10, 10);
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;

            // Create Deal button on the right
            CreateDealButton();

            Debug.Log("DockBarManager: Created UI at runtime");
        }

        private void CreateDealButton()
        {
            dealButtonObj = new GameObject("DealButton");
            RectTransform buttonRect = dealButtonObj.AddComponent<RectTransform>();
            buttonRect.SetParent(dockBarContainer, false);
            buttonRect.anchorMin = new Vector2(1f, 0.5f);
            buttonRect.anchorMax = new Vector2(1f, 0.5f);
            buttonRect.pivot = new Vector2(1f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(-10f, 0f);
            buttonRect.sizeDelta = new Vector2(130f, 80f);

            // Add Button component
            Button button = dealButtonObj.AddComponent<Button>();
            Image buttonImage = dealButtonObj.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.4f, 0.2f, 1f);

            // Button colors
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.2f, 0.6f, 0.2f, 1f);
            colors.highlightedColor = new Color(0.3f, 0.8f, 0.3f, 1f);
            colors.pressedColor = new Color(0.1f, 0.4f, 0.1f, 1f);
            colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            button.colors = colors;

            // Add click listener
            button.onClick.AddListener(OnDealButtonClicked);

            // Create button text
            GameObject textObj = new GameObject("Text");
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.SetParent(buttonRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;

            drawButtonText = textObj.AddComponent<TextMeshProUGUI>();
            drawButtonText.text = "DRAW";
            drawButtonText.fontSize = 20;
            drawButtonText.color = Color.white;
            drawButtonText.alignment = TextAlignmentOptions.Center;
            drawButtonText.fontStyle = FontStyles.Bold;
        }

        /// <summary>
        /// Calculate the current deal cost (Iteration 10: Linear escalation)
        /// </summary>
        public int CalculateDealCost()
        {
            return baseDrawCost + (costIncrement * drawCount);
        }

        /// <summary>
        /// Get current draw cost (public accessor)
        /// </summary>
        public int GetCurrentDrawCost()
        {
            return CalculateDealCost();
        }

        /// <summary>
        /// Called when the Deal button is clicked (Iteration 10: Self-sufficient)
        /// </summary>
        public void OnDealButtonClicked()
        {
            int cost = CalculateDealCost();

            // Check if player has enough tokens
            if (ResourceTokenManager.Instance == null || !ResourceTokenManager.Instance.HasEnoughTokens(cost))
            {
                Debug.Log($"Failed to draw unit - not enough tokens (need {cost})");
                ShowErrorFeedback($"Not Enough Tokens! ({cost} needed)");
                return;
            }

            // Spend tokens
            ResourceTokenManager.Instance.SpendTokens(cost);

            // Increment draw count AFTER spending
            drawCount++;
            ticksSinceCostDecrease = 0; // Reset cost-decrease timer on draw
            UpdateCostFill(); // Reset fill bar to full

            // Draw a random unit from RaritySystem
            if (RaritySystem.Instance != null)
            {
                UnitStats drawnStats = RaritySystem.Instance.DrawRandomUnit();
                if (drawnStats != null)
                {
                    AddUnitToDock(drawnStats);
                    Debug.Log($"Drew {drawnStats.unitName} ({drawnStats.rarity}) - Cost was {cost}T");

                    // Screen shake feedback on successful draw
                    if (CameraController.Instance != null)
                    {
                        CameraController.Instance.Shake(0.12f, 0.2f);
                    }
                }
            }

            // Update button display
            UpdateDealButtonDisplay();
        }

        /// <summary>
        /// Add a new unit icon to the dock (using UnitStats)
        /// </summary>
        public void AddUnitToDock(UnitStats unitStats)
        {
            GameObject iconObj;
            UnitIcon icon;

            // Use prefab if assigned, otherwise create runtime UI
            if (unitIconPrefab != null)
            {
                // Instantiate custom prefab
                iconObj = Instantiate(unitIconPrefab, dockIconsPanel, false);
                iconObj.name = $"UnitIcon_{unitIcons.Count}";

                // Get or add UnitIcon component
                icon = iconObj.GetComponent<UnitIcon>();
                if (icon == null)
                {
                    icon = iconObj.AddComponent<UnitIcon>();
                }
            }
            else
            {
                // Fallback: Create runtime UI (legacy behavior)
                iconObj = new GameObject($"UnitIcon_{unitIcons.Count}");
                RectTransform iconRect = iconObj.AddComponent<RectTransform>();
                iconRect.SetParent(dockIconsPanel, false);
                iconRect.sizeDelta = new Vector2(70f, 70f);

                // Add Image component with unit-specific color
                Image iconImage = iconObj.AddComponent<Image>();
                iconImage.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
                iconImage.color = GetUnitColorByType(unitStats.unitType);

                // Add UnitIcon component
                icon = iconObj.AddComponent<UnitIcon>();
            }

            icon.Initialize(unitStats, this);
            unitIcons.Add(icon);

            // Update spacing
            UpdateLayoutSpacing();

            // TODO: Animate icon sliding in from right
        }

        // REMOVED: OnHandChanged() - No longer needed (Iteration 10: Self-sufficient)

        /// <summary>
        /// Remove a unit icon from the dock (called after placement)
        /// </summary>
        public void RemoveUnitIcon(UnitIcon icon)
        {
            if (unitIcons.Contains(icon))
            {
                unitIcons.Remove(icon);
                Destroy(icon.gameObject);
                UpdateLayoutSpacing();
            }
        }

        /// <summary>
        /// Update layout spacing based on number of icons
        /// </summary>
        private void UpdateLayoutSpacing()
        {
            if (layoutGroup == null) return;

            int count = unitIcons.Count;
            if (count <= 5)
                layoutGroup.spacing = 10f;
            else if (count <= 8)
                layoutGroup.spacing = 8f;
            else
                layoutGroup.spacing = 5f;
        }

        /// <summary>
        /// Update the Deal button text and state
        /// </summary>
        private void UpdateDealButtonDisplay()
        {
            // Use assigned button or created button
            Button button = drawButton != null ? drawButton : (dealButtonObj != null ? dealButtonObj.GetComponent<Button>() : null);
            TextMeshProUGUI buttonText = drawButtonText;

            if (button == null || buttonText == null) return;

            int cost = CalculateDealCost();
            bool canAfford = ResourceTokenManager.Instance != null &&
                           ResourceTokenManager.Instance.HasEnoughTokens(cost);

            // Check if hand is full (max 10 units in dock)
            bool handFull = unitIcons.Count >= 10;

            // Update button text
            if (handFull)
            {
                buttonText.text = "Hand\nFull!";
                buttonText.color = new Color(1f, 0.5f, 0f); // Orange for hand full
            }
            else
            {
                buttonText.text = "DRAW";
                buttonText.color = canAfford ? Color.white : new Color(1f, 0.3f, 0.3f);
            }

            // Update button state (disabled if can't afford OR hand is full)
            button.interactable = canAfford && !handFull;

            // Update cost number text on gatcha button
            if (costNumberText != null)
            {
                costNumberText.text = cost.ToString();
                costNumberText.color = canAfford ? Color.white : new Color(1f, 0.3f, 0.3f);
            }
        }

        private void OnTokensChanged(int newTotal)
        {
            UpdateDealButtonDisplay();
        }

        private void OnIntervalTickCostDecrease(int intervalCount)
        {
            if (costDecreaseInterval <= 0 || drawCount <= 0)
            {
                UpdateCostFill();
                return;
            }

            ticksSinceCostDecrease++;
            if (ticksSinceCostDecrease >= costDecreaseInterval)
            {
                ticksSinceCostDecrease = 0;
                drawCount--;
                if (drawCount < 0) drawCount = 0;
                Debug.Log($"[DockBarManager] Cost decreased by interval. drawCount={drawCount}, cost={CalculateDealCost()}");
                UpdateDealButtonDisplay();
            }

            UpdateCostFill();
        }

        /// <summary>
        /// Update the cost fill bar. Fill = remaining time until next cost decrease.
        /// Full (1) = just drew/reset, depletes toward 0 as ticks pass.
        /// </summary>
        private void UpdateCostFill()
        {
            if (costFillImage == null) return;

            if (costDecreaseInterval <= 0 || drawCount <= 0)
            {
                costFillImage.fillAmount = 0f;
                return;
            }

            float remaining = (float)(costDecreaseInterval - ticksSinceCostDecrease) / costDecreaseInterval;
            costFillImage.fillAmount = Mathf.Clamp01(remaining);
        }

        /// <summary>
        /// Show error feedback when draw fails (e.g., not enough tokens).
        /// </summary>
        private void ShowErrorFeedback(string message)
        {
            if (drawButtonText != null)
            {
                StartCoroutine(FlashErrorText(message));
            }
        }

        /// <summary>
        /// Flash error message on draw button temporarily.
        /// </summary>
        private System.Collections.IEnumerator FlashErrorText(string errorMessage)
        {
            if (drawButtonText == null) yield break;

            // Store original text and color
            string originalText = drawButtonText.text;
            Color originalColor = drawButtonText.color;

            // Show error in red
            drawButtonText.text = errorMessage;
            drawButtonText.color = Color.red;

            yield return new WaitForSeconds(1.5f);

            // Restore original
            drawButtonText.text = originalText;
            drawButtonText.color = originalColor;
        }

        /// <summary>
        /// Clear pre-existing children from dock container (mock-up cards from editor)
        /// </summary>
        private void ClearDockContainerChildren()
        {
            if (dockIconsContainer == null) return;

            // Destroy all children (these are editor mock-ups, not runtime cards)
            for (int i = dockIconsContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = dockIconsContainer.GetChild(i);
                Destroy(child.gameObject);
            }

            Debug.Log($"DockBarManager: Cleared {dockIconsContainer.childCount} mock-up cards from dock");
        }

        /// <summary>
        /// Get color for unit type
        /// </summary>
        private Color GetUnitColorByType(UnitType type)
        {
            switch (type)
            {
                case UnitType.Soldier:
                    return new Color(0.3f, 0.5f, 1f); // Blue
                case UnitType.Ninja:
                    return new Color(0.3f, 1f, 0.5f); // Green
                case UnitType.Ogre:
                    return new Color(1f, 0.3f, 0.3f); // Red
                default:
                    return Color.white;
            }
        }

        private T CreateUIObject<T>(string name, Transform parent) where T : Component
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            return obj.AddComponent<T>();
        }

        /// <summary>
        /// Get the current number of units in the dock/hand.
        /// Used by WaveManager for lose condition checking.
        /// </summary>
        public int GetUnitCount()
        {
            return unitIcons.Count;
        }
    }
}
