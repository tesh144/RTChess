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
        [SerializeField] private Transform dockIconsContainer; // Parent for red card holders
        [SerializeField] private Button drawButton; // White button on the right
        [SerializeField] private TextMeshProUGUI drawButtonText; // Text on draw button

        [Header("Runtime Creation Settings (if no UI references assigned)")]
        [SerializeField] private bool createUIAtRuntime = false;

        // UI References (assigned from editor OR created at runtime)
        private RectTransform dockBarContainer;
        private Image backgroundImage;
        private RectTransform dockIconsPanel;
        private HorizontalLayoutGroup layoutGroup;
        private GameObject dealButtonObj;

        // Unit tracking
        private List<UnitIcon> unitIcons = new List<UnitIcon>();
        private GameObject[] availableUnitPrefabs;

        // HandManager integration
        private HandManager handManager;

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

        /// <summary>
        /// Initialize the dock bar with UI hierarchy and HandManager
        /// </summary>
        public void Initialize(Canvas canvas, HandManager manager)
        {
            handManager = manager;

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

            // Subscribe to hand changes to update UI
            if (handManager != null)
            {
                handManager.OnHandChanged += OnHandChanged;
                // Display starting hand
                OnHandChanged();
            }
        }

        private void OnDestroy()
        {
            if (ResourceTokenManager.Instance != null)
            {
                ResourceTokenManager.Instance.OnTokensChanged -= OnTokensChanged;
            }

            if (handManager != null)
            {
                handManager.OnHandChanged -= OnHandChanged;
            }
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
            drawButtonText.text = "Draw:\n3T";
            drawButtonText.fontSize = 20;
            drawButtonText.color = Color.white;
            drawButtonText.alignment = TextAlignmentOptions.Center;
            drawButtonText.fontStyle = FontStyles.Bold;
        }

        /// <summary>
        /// Calculate the current deal cost (delegates to HandManager)
        /// </summary>
        public int CalculateDealCost()
        {
            if (handManager != null)
                return handManager.CalculateDrawCost();
            return 3; // Fallback
        }

        /// <summary>
        /// Called when the Deal button is clicked
        /// </summary>
        public void OnDealButtonClicked()
        {
            if (handManager == null) return;

            // Try to draw a unit (HandManager handles cost check and token spending)
            bool success = handManager.DrawUnit();

            if (!success)
            {
                // TODO: Show error feedback (button shake, sound)
                Debug.Log("Failed to draw unit - check tokens or hand size");
            }

            // Update button display
            UpdateDealButtonDisplay();
        }

        /// <summary>
        /// Add a new unit icon to the dock (using UnitData)
        /// </summary>
        public void AddUnitToDock(UnitData unitData)
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
                iconImage.color = GetUnitColorByType(unitData.Type);

                // Add UnitIcon component
                icon = iconObj.AddComponent<UnitIcon>();
            }

            icon.Initialize(unitData, this);
            unitIcons.Add(icon);

            // Update spacing
            UpdateLayoutSpacing();

            // TODO: Animate icon sliding in from right
        }

        /// <summary>
        /// Called when the hand changes (unit added or removed)
        /// </summary>
        private void OnHandChanged()
        {
            if (handManager == null) return;

            // Clear current icons
            foreach (UnitIcon icon in unitIcons)
            {
                if (icon != null && icon.gameObject != null)
                    Destroy(icon.gameObject);
            }
            unitIcons.Clear();

            // Rebuild from current hand
            List<UnitData> hand = handManager.GetHand();
            foreach (UnitData unitData in hand)
            {
                AddUnitToDock(unitData);
            }
        }

        /// <summary>
        /// Remove a unit icon from the dock (called after placement)
        /// </summary>
        public void RemoveUnitIcon(UnitIcon icon)
        {
            if (unitIcons.Contains(icon))
            {
                unitIcons.Remove(icon);

                // Remove from HandManager
                if (handManager != null && icon.UnitData != null)
                {
                    handManager.RemoveFromHand(icon.UnitData);
                }

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

            // Check if hand is full
            bool handFull = handManager != null && handManager.GetHandSize() >= 10;

            // Update button text
            if (handFull)
            {
                buttonText.text = "Hand\nFull!";
                buttonText.color = new Color(1f, 0.5f, 0f); // Orange for hand full
            }
            else
            {
                buttonText.text = $"Draw:\n{cost}T";
                buttonText.color = canAfford ? Color.white : new Color(1f, 0.3f, 0.3f);
            }

            // Update button state (disabled if can't afford OR hand is full)
            button.interactable = canAfford && !handFull;
        }

        private void OnTokensChanged(int newTotal)
        {
            UpdateDealButtonDisplay();
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
    }
}
