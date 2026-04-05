#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;

namespace LittleCafe
{
    /// <summary>
    /// Auto-updates a world-space Canvas sortingOrder based on distance to camera.
    /// Closer objects get higher sortingOrder so they render in front.
    /// Also applies partial zoom compensation so bubbles stay readable when zoomed out.
    /// Attach to any GameObject with a Canvas component.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class BubbleDepthSorter : MonoBehaviour
    {
        private Canvas canvas;
        private Transform cam;
        private Vector3 baseLocalScale;
        private bool scaleInitialized;

        /// <summary>Base sorting order. Distance offset is added on top of this.</summary>
        [SerializeField] private int baseSortingOrder = 50;

        private const float MaxSortDistance = 60f;
        private const int SortRange = 200;

        /// <summary>
        /// How much to compensate for zoom. 0 = no compensation (shrinks fully with distance),
        /// 1 = full compensation (constant screen size). 0.3 = partial, keeps spatial context.
        /// </summary>
        private const float ZoomCompensation = 0.3f;

        public void Initialize(int baseOrder)
        {
            baseSortingOrder = baseOrder;
        }

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
        }

        private void LateUpdate()
        {
            if (canvas == null) return;
            if (cam == null)
            {
                var main = Camera.main;
                if (main == null) return;
                cam = main.transform;
            }

            // Capture initial scale on first frame (after creators have set it)
            if (!scaleInitialized)
            {
                baseLocalScale = transform.localScale;
                scaleInitialized = true;
            }

            // Depth-based sorting: closer = higher sortingOrder
            float dist = Vector3.Distance(cam.position, transform.position);
            float t = Mathf.Clamp01(1f - dist / MaxSortDistance);
            canvas.sortingOrder = baseSortingOrder + Mathf.RoundToInt(t * SortRange);

            // Zoom compensation: partially scale up when zoomed out
            if (GridCamera.Instance != null)
            {
                float currentZoom = GridCamera.Instance.CurrentDistance;
                float minZoom = 5f;
                float maxZoom = 40f;
                float zoomT = Mathf.Clamp01((currentZoom - minZoom) / (maxZoom - minZoom));
                float scaleMul = 1f + zoomT * ZoomCompensation;
                transform.localScale = baseLocalScale * scaleMul;
            }
        }
    }
}
