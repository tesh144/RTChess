using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

namespace ClockworkGrid
{
    /// <summary>
    /// Manages the dock bar UI at the bottom of the screen.
    /// Handles drawing units into the dock and removing them when placed.
    /// </summary>
    public class DockBarManager : MonoBehaviour
    {
        // UI References
        private RectTransform dockBarContainer;
        private Image backgroundImage;
        private RectTransform dockIconsPanel;
        private HorizontalLayoutGroup layoutGroup;
        private GameObject dealButtonObj;

        // Unit tracking
        private List<UnitIcon> unitIcons = new List<UnitIcon>();
        private GameObject[] availableUnitPrefabs;

        // Draw cost configuration
        [SerializeField] private int baseDealCost = 3;
        private int drawCount = 0;

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
        /// Initialize the dock bar with UI hierarchy and unit prefabs
        /// </summary>
        public void Initialize(Canvas canvas, GameObject[] unitPrefabs)
        {
            availableUnitPrefabs = unitPrefabs;
            CreateDockBarUI(canvas);
            UpdateDealButtonDisplay();

            // Subscribe to token changes to update button state
            if (ResourceTokenManager.Instance != null)
            {
                ResourceTokenManager.Instance.OnTokensChanged += OnTokensChanged;
            }
        }

        private void OnDestroy()
        {
            if (ResourceTokenManager.Instance != null)
            {
                ResourceTokenManager.Instance.OnTokensChanged -= OnTokensChanged;
            }
        }

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

            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = "Draw:\n3T";
            text.fontSize = 20;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
        }

        /// <summary>
        /// Calculate the current deal cost based on draw count
        /// Linear escalation: 3, 4, 5, 6, 7...
        /// </summary>
        public int CalculateDealCost()
        {
            return baseDealCost + drawCount;
        }

        /// <summary>
        /// Called when the Deal button is clicked
        /// </summary>
        public void OnDealButtonClicked()
        {
            int cost = CalculateDealCost();

            // Check if player has enough tokens
            if (!ResourceTokenManager.Instance.HasEnoughTokens(cost))
            {
                Debug.Log($"Not enough tokens! Need {cost}, have {ResourceTokenManager.Instance.CurrentTokens}");
                // TODO: Show error feedback (button shake, sound)
                return;
            }

            // Spend tokens
            ResourceTokenManager.Instance.SpendTokens(cost);

            // Increment draw count AFTER spending
            drawCount++;

            // Add random unit to dock (for now, just pick first prefab - only Soldiers in Iteration 4)
            if (availableUnitPrefabs != null && availableUnitPrefabs.Length > 0)
            {
                int randomIndex = Random.Range(0, availableUnitPrefabs.Length);
                AddUnitToDock(availableUnitPrefabs[randomIndex]);
            }

            // Update button display
            UpdateDealButtonDisplay();
        }

        /// <summary>
        /// Add a new unit icon to the dock
        /// </summary>
        public void AddUnitToDock(GameObject unitPrefab)
        {
            // Create new unit icon
            GameObject iconObj = new GameObject($"UnitIcon_{unitIcons.Count}");
            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.SetParent(dockIconsPanel, false);
            iconRect.sizeDelta = new Vector2(70f, 70f);

            // Add Image component (placeholder for now)
            Image iconImage = iconObj.AddComponent<Image>();
            iconImage.color = GetUnitColor(unitPrefab);

            // Add UnitIcon component (will be created in Phase 3)
            UnitIcon icon = iconObj.AddComponent<UnitIcon>();
            icon.Initialize(unitPrefab, this);

            unitIcons.Add(icon);

            // Update spacing
            UpdateLayoutSpacing();

            // TODO: Animate icon sliding in from right
        }

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
            if (dealButtonObj == null) return;

            int cost = CalculateDealCost();
            bool canAfford = ResourceTokenManager.Instance != null &&
                           ResourceTokenManager.Instance.HasEnoughTokens(cost);

            // Update button text
            TextMeshProUGUI buttonText = dealButtonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = $"Draw:\n{cost}T";
                buttonText.color = canAfford ? Color.white : new Color(1f, 0.3f, 0.3f);
            }

            // Update button state
            Button button = dealButtonObj.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = canAfford;
            }
        }

        private void OnTokensChanged(int newTotal)
        {
            UpdateDealButtonDisplay();
        }

        /// <summary>
        /// Extract color from unit prefab for icon display
        /// </summary>
        private Color GetUnitColor(GameObject unitPrefab)
        {
            Renderer renderer = unitPrefab.GetComponentInChildren<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                return renderer.sharedMaterial.color;
            }
            return Color.white;
        }

        private T CreateUIObject<T>(string name, Transform parent) where T : Component
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            return obj.AddComponent<T>();
        }
    }
}
