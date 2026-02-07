using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ClockworkGrid
{
    /// <summary>
    /// Represents a draggable unit icon in the dock bar.
    /// Each icon is a consumable instance of a unit.
    /// </summary>
    public class UnitIcon : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        private GameObject unitPrefab;
        private DockBarManager dockManager;
        private RectTransform rectTransform;
        private Vector3 originalScale;
        private Vector2 originalPosition;
        private bool isDragging = false;

        [SerializeField] private float hoverScale = 1.3f;

        public GameObject UnitPrefab => unitPrefab;

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
    }
}
