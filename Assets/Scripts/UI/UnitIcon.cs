#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace ClockworkGrid
{
    /// <summary>
    /// Represents a draggable unit icon in the dock bar.
    /// Each icon is a consumable instance of a unit.
    /// Shows cost badge and hover magnification (macOS dock style).
    /// </summary>
    public class UnitIcon : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        private GameObject unitPrefab;
        private UnitStats unitStats;
        private DockBarManager dockManager;
        private RectTransform rectTransform;
        private Vector3 originalScale;
        private Vector2 originalPosition;
        private bool isDragging = false;
        private GameObject costBadge;
        private GameObject typeLabel;
        private TextMeshProUGUI costTextRef; // Cached reference for dynamic color updates

        [SerializeField] private float hoverScale = 1.2f; // Phase 2: ~20% scale up
        [SerializeField] private Image characterSpriteImage; // Assign the CharacterSprite Image in prefab

        public GameObject UnitPrefab => unitPrefab;
        public UnitStats UnitStats => unitStats;

        private void Awake()
        {
            EnsureRectTransform();
        }

        /// <summary>
        /// Ensures rectTransform is cached. Safe to call multiple times.
        /// Needed because Initialize() may be called before Awake() runs.
        /// </summary>
        private void EnsureRectTransform()
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    originalScale = rectTransform.localScale;
                }
            }
        }

        public void Initialize(GameObject prefab, DockBarManager manager)
        {
            EnsureRectTransform();
            unitPrefab = prefab;
            dockManager = manager;
            originalPosition = rectTransform.anchoredPosition;
        }

        /// <summary>
        /// Initialize with UnitStats (Phase 2 addition)
        /// </summary>
        public void Initialize(UnitStats stats, DockBarManager manager)
        {
            EnsureRectTransform();
            unitStats = stats;
            unitPrefab = stats.unitPrefab;
            dockManager = manager;
            originalPosition = rectTransform.anchoredPosition;

            // Set character sprite from UnitStats
            if (characterSpriteImage != null && stats.iconSprite != null)
            {
                characterSpriteImage.sprite = stats.iconSprite;
            }

            // Try to find and populate existing UI elements in prefab
            string displayName = !string.IsNullOrEmpty(stats.unitName) ? stats.unitName : stats.unitType.ToString();
            bool foundPrefabUI = PopulatePrefabUI(stats.resourceCost, displayName);

            // Fallback: Create UI dynamically if prefab doesn't have elements
            if (!foundPrefabUI)
            {
                CreateCostBadge(stats.resourceCost);
                CreateTypeLabel(displayName);
            }

            // Show draw weight badge (bottom-left) if weight differs from default
            float weight = stats.GetEffectiveDrawWeight();
            CreateWeightBadge(weight);

            // Subscribe to token changes for cost color updates
            if (ResourceTokenManager.Instance != null)
            {
                ResourceTokenManager.Instance.OnTokensChanged += OnTokensChanged;
            }
            UpdateCostColor();
        }

        private void OnDestroy()
        {
            if (ResourceTokenManager.Instance != null)
            {
                ResourceTokenManager.Instance.OnTokensChanged -= OnTokensChanged;
            }
        }

        private void OnTokensChanged(int newTotal)
        {
            UpdateCostColor();
        }

        private void UpdateCostColor()
        {
            if (costTextRef == null || unitStats == null) return;

            bool canAfford = ResourceTokenManager.Instance != null &&
                             ResourceTokenManager.Instance.HasEnoughTokens(unitStats.resourceCost);
            costTextRef.color = canAfford ? Color.white : new Color(1f, 0.3f, 0.3f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isDragging)
            {
                // Magnify on hover (macOS dock style)
                rectTransform.localScale = originalScale * hoverScale;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isDragging)
            {
                // Restore original size
                rectTransform.localScale = originalScale;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // DEPRECATED: UnitIcon drag is replaced by GameCardUI.
            // This class is kept for backwards compatibility but should not be used for new cards.
            Debug.LogWarning("[UnitIcon] OnBeginDrag — UnitIcon is deprecated, use GameCardUI instead.");
            isDragging = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Update DragDropHandler with current mouse position
            if (DragDropHandler.Instance != null)
            {
                DragDropHandler.Instance.UpdateDrag(eventData.position);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            isDragging = false;

            // Notify DragDropHandler to attempt placement or snap back
            if (DragDropHandler.Instance != null)
            {
                DragDropHandler.Instance.EndDrag();
            }
        }

        /// <summary>
        /// Animate icon snapping back to original position
        /// </summary>
        public void SnapBackToOriginalPosition()
        {
            // TODO Phase 4: Implement smooth snap-back animation
            rectTransform.anchoredPosition = originalPosition;
            rectTransform.localScale = originalScale;
        }

        /// <summary>
        /// Try to populate existing UI elements in the prefab.
        /// Searches for TextMeshProUGUI components with specific names.
        /// Returns true if prefab UI was found and populated.
        /// </summary>
        private bool PopulatePrefabUI(int cost, string displayName)
        {
            bool foundCost = false;
            bool foundName = false;

            // Search all TextMeshProUGUI components in children
            TextMeshProUGUI[] textComponents = GetComponentsInChildren<TextMeshProUGUI>(true);

            foreach (TextMeshProUGUI textComp in textComponents)
            {
                string objName = textComp.gameObject.name.ToLower();

                // Look for cost/number display
                if (objName.Contains("cost") || objName.Contains("number") || objName.Contains("price"))
                {
                    textComp.text = cost.ToString();
                    costTextRef = textComp;
                    foundCost = true;
                }
                // Look for name/type display
                else if (objName.Contains("name") || objName.Contains("type") || objName.Contains("label"))
                {
                    textComp.text = displayName;
                    foundName = true;
                }
            }

            // Only return true if we found both elements
            return foundCost && foundName;
        }

        /// <summary>
        /// Create cost badge (dark circle with cost number)
        /// </summary>
        private void CreateCostBadge(int cost)
        {
            // Badge container (positioned below icon)
            costBadge = new GameObject("CostBadge");
            RectTransform badgeRect = costBadge.AddComponent<RectTransform>();
            badgeRect.SetParent(transform, false);
            badgeRect.anchorMin = new Vector2(0.5f, 0f);
            badgeRect.anchorMax = new Vector2(0.5f, 0f);
            badgeRect.pivot = new Vector2(0.5f, 1f);
            badgeRect.anchoredPosition = new Vector2(0f, -5f); // Just below icon
            badgeRect.sizeDelta = new Vector2(25f, 25f); // Small dark circle

            // Dark circle background
            Image badgeBg = costBadge.AddComponent<Image>();
            badgeBg.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            badgeBg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f); // Dark semi-transparent
            badgeBg.type = Image.Type.Sliced;

            // Cost text
            GameObject textObj = new GameObject("CostText");
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.SetParent(badgeRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI costText = textObj.AddComponent<TextMeshProUGUI>();
            costText.text = cost.ToString();
            costText.fontSize = 14;
            costText.color = Color.white;
            costText.alignment = TextAlignmentOptions.Center;
            costText.fontStyle = FontStyles.Bold;
            costTextRef = costText;
        }

        /// <summary>
        /// Create type label (positioned above icon)
        /// </summary>
        private void CreateTypeLabel(string displayName)
        {
            // Label container (positioned above icon)
            typeLabel = new GameObject("TypeLabel");
            RectTransform labelRect = typeLabel.AddComponent<RectTransform>();
            labelRect.SetParent(transform, false);
            labelRect.anchorMin = new Vector2(0.5f, 1f);
            labelRect.anchorMax = new Vector2(0.5f, 1f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 5f); // Just above icon
            labelRect.sizeDelta = new Vector2(70f, 20f);

            // Type text
            TextMeshProUGUI typeText = typeLabel.AddComponent<TextMeshProUGUI>();
            typeText.text = displayName; // Custom name from UnitStats
            typeText.fontSize = 12;
            typeText.color = Color.white;
            typeText.alignment = TextAlignmentOptions.Center;
            typeText.fontStyle = FontStyles.Bold;

            // Add shadow for readability
            typeText.enableAutoSizing = false;
            typeText.outlineWidth = 0.2f;
            typeText.outlineColor = Color.black;
        }

        /// <summary>
        /// Create weight badge (positioned bottom-left of icon).
        /// Shows the draw weight value so designers can see relative likelihood at a glance.
        /// </summary>
        private void CreateWeightBadge(float weight)
        {
            // Badge container (bottom-left corner)
            GameObject badge = new GameObject("WeightBadge");
            RectTransform badgeRect = badge.AddComponent<RectTransform>();
            badgeRect.SetParent(transform, false);
            badgeRect.anchorMin = new Vector2(0f, 0f);
            badgeRect.anchorMax = new Vector2(0f, 0f);
            badgeRect.pivot = new Vector2(0f, 0f);
            badgeRect.anchoredPosition = new Vector2(2f, 2f);
            badgeRect.sizeDelta = new Vector2(22f, 16f);

            // Background
            Image badgeBg = badge.AddComponent<Image>();
            badgeBg.color = new Color(0.2f, 0.7f, 0.3f, 0.85f); // Green tint

            // Weight text
            GameObject textObj = new GameObject("WeightText");
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.SetParent(badgeRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI weightText = textObj.AddComponent<TextMeshProUGUI>();
            // Display as integer if whole number, otherwise 1 decimal
            weightText.text = weight % 1 == 0 ? $"x{weight:0}" : $"x{weight:0.#}";
            weightText.fontSize = 10;
            weightText.color = Color.white;
            weightText.alignment = TextAlignmentOptions.Center;
            weightText.fontStyle = FontStyles.Bold;
        }
    }
}


