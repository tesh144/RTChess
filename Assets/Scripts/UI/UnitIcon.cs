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
        private UnitData unitData;
        private DockBarManager dockManager;
        private RectTransform rectTransform;
        private Vector3 originalScale;
        private Vector2 originalPosition;
        private bool isDragging = false;
        private GameObject costBadge;
        private GameObject typeLabel;

        [SerializeField] private float hoverScale = 1.2f; // Phase 2: ~20% scale up

        public GameObject UnitPrefab => unitPrefab;
        public UnitData UnitData => unitData;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            originalScale = rectTransform.localScale;
        }

        public void Initialize(GameObject prefab, DockBarManager manager)
        {
            unitPrefab = prefab;
            dockManager = manager;
            originalPosition = rectTransform.anchoredPosition;
        }

        /// <summary>
        /// Initialize with UnitData (Phase 2 addition)
        /// </summary>
        public void Initialize(UnitData data, DockBarManager manager)
        {
            unitData = data;
            unitPrefab = data.Prefab;
            dockManager = manager;
            originalPosition = rectTransform.anchoredPosition;

            // Create cost badge
            CreateCostBadge(data.Cost);

            // Create type label (Bug fix: show unit type)
            CreateTypeLabel(data.Type);
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
            isDragging = true;
            originalScale = rectTransform.localScale / hoverScale; // Account for hover scale
            rectTransform.localScale = originalScale;

            // Notify DragDropHandler to create ghost preview
            if (DragDropHandler.Instance != null)
            {
                DragDropHandler.Instance.StartDrag(this, unitPrefab);
            }
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
        }

        /// <summary>
        /// Create type label (positioned above icon)
        /// </summary>
        private void CreateTypeLabel(UnitType type)
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
            typeText.text = type.ToString(); // "Soldier", "Ogre", or "Ninja"
            typeText.fontSize = 12;
            typeText.color = Color.white;
            typeText.alignment = TextAlignmentOptions.Center;
            typeText.fontStyle = FontStyles.Bold;

            // Add shadow for readability
            typeText.enableAutoSizing = false;
            typeText.outlineWidth = 0.2f;
            typeText.outlineColor = Color.black;
        }
    }
}
