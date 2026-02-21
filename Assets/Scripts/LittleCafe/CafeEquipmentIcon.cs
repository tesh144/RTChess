using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace LittleCafe
{
    /// <summary>
    /// A single equipment card in the dock bar.
    /// Works like RTChess's UnitIcon — displays equipment info and handles drag.
    /// Dragging a card selects that equipment type in the EquipmentPlacer.
    /// </summary>
    public class CafeEquipmentIcon : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("UI Elements (auto-found or assign)")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Image colorSwatch;
        [SerializeField] private TextMeshProUGUI nameLabel;

        [Header("Hover")]
        [SerializeField] private float hoverScale = 1.15f;

        private EquipmentCardData cardData;
        private RectTransform rectTransform;
        private Vector3 originalScale;
        private bool isDragging;

        public EquipmentCardData CardData => cardData;

        public void Initialize(EquipmentCardData data)
        {
            cardData = data;
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform == null) rectTransform = gameObject.AddComponent<RectTransform>();
            originalScale = rectTransform.localScale;

            // Set visuals
            if (colorSwatch != null)
                colorSwatch.color = data.cardColor;

            if (iconImage != null && data.iconSprite != null)
                iconImage.sprite = data.iconSprite;
            else if (iconImage != null)
                iconImage.color = data.cardColor;

            if (nameLabel != null)
                nameLabel.text = data.displayName;

            gameObject.name = $"Card_{data.equipmentType}";
        }

        // --- Hover (macOS dock-style magnification) ---

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isDragging)
                rectTransform.localScale = originalScale * hoverScale;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isDragging)
                rectTransform.localScale = originalScale;
        }

        // --- Click to select ---

        public void OnPointerClick(PointerEventData eventData)
        {
            if (cardData == null || EquipmentPlacer.Instance == null) return;
            EquipmentPlacer.Instance.SelectEquipment(cardData.equipmentType);
        }

        // --- Drag to place ---

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (cardData == null || EquipmentPlacer.Instance == null) return;
            isDragging = true;
            EquipmentPlacer.Instance.SelectEquipment(cardData.equipmentType);
        }

        public void OnDrag(PointerEventData eventData)
        {
            // EquipmentPlacer handles the ghost preview via Update()
            // Nothing extra needed here — the placer tracks mouse position
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            isDragging = false;
            rectTransform.localScale = originalScale;

            // If cursor is over a valid grid cell, the EquipmentPlacer's Update
            // will have placed it on the next click. For drag-and-release placement:
            if (EquipmentPlacer.Instance != null && EquipmentPlacer.Instance.HasSelection)
            {
                EquipmentPlacer.Instance.TryPlaceAtCurrentTarget();
            }
        }
    }
}
