using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace ClockworkCraft
{
    /// <summary>
    /// Spawns a single icon that arcs in screen-space from one world position to another.
    /// General-purpose "unit picks up item from world" visual — used for meal buff and
    /// future world pickups.
    ///
    /// Scene-placed singleton. Add as a component to a manager object alongside ResourceLootFX.
    /// </summary>
    public class IconFlyFX : MonoBehaviour
    {
        public static IconFlyFX Instance { get; private set; }

        [Header("Arc Settings")]
        [Tooltip("Height of the arc curve in screen pixels.")]
        public float arcHeight = 60f;
        [Tooltip("Duration of the pop-in phase.")]
        public float popInDuration = 0.15f;
        [Tooltip("Duration of the arc travel phase.")]
        public float arcDuration = 0.4f;
        [Tooltip("Size of the icon in screen pixels.")]
        public float iconSize = 56f;

        private Canvas canvas;
        private RectTransform canvasRect;
        private Camera mainCamera;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null)
                canvasRect = canvas.GetComponent<RectTransform>();
            mainCamera = Camera.main;
        }

        /// <summary>
        /// Spawn an icon arc from worldFrom to worldTo.
        /// No-op if the canvas or camera is not available.
        /// </summary>
        public void SpawnArc(Sprite icon, Vector3 worldFrom, Vector3 worldTo)
        {
            if (canvas == null || mainCamera == null || icon == null) return;
            StartCoroutine(ArcCoroutine(icon, worldFrom, worldTo));
        }

        private IEnumerator ArcCoroutine(Sprite icon, Vector3 worldFrom, Vector3 worldTo)
        {
            // ── Create icon GameObject ──────────────────────────────────────
            GameObject iconObj = new GameObject("MealBuffIconFly");
            iconObj.transform.SetParent(canvas.transform, false);

            RectTransform rect = iconObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(iconSize, iconSize);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);

            Image img = iconObj.AddComponent<Image>();
            img.sprite = icon;
            img.color = Color.white;
            img.preserveAspect = true;
            img.raycastTarget = false;

            // Sorting: child Canvas so this renders above other UI regardless of canvas mode
            Canvas iconCanvas = iconObj.AddComponent<Canvas>();
            iconCanvas.overrideSorting = true;
            iconCanvas.sortingOrder = 100;

            // ── Phase 1: Pop-in ─────────────────────────────────────────────
            // Snapshot worldFrom to screen space once for the pop-in phase.
            Vector3 startScreen = mainCamera.WorldToScreenPoint(worldFrom);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, startScreen,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera,
                out Vector2 startLocal);

            rect.anchoredPosition = startLocal;
            rect.localScale = Vector3.zero;

            float elapsed = 0f;
            while (elapsed < popInDuration)
            {
                if (iconObj == null) yield break;
                float t = elapsed / popInDuration;
                float eased = 1f - Mathf.Pow(1f - t, 3f); // cubic ease-out
                rect.localScale = Vector3.one * eased;
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (iconObj == null) yield break;
            rect.localScale = Vector3.one;

            // ── Phase 2: Arc ────────────────────────────────────────────────
            elapsed = 0f;
            while (elapsed < arcDuration)
            {
                if (iconObj == null) yield break;
                float t = elapsed / arcDuration;

                // Re-convert each frame so the arc tracks camera movement
                Vector3 fromScreen = mainCamera.WorldToScreenPoint(worldFrom);
                Vector3 toScreen   = mainCamera.WorldToScreenPoint(worldTo);

                // Guard: if either position is behind the camera, abort the arc
                if (fromScreen.z < 0f || toScreen.z < 0f)
                {
                    if (iconObj != null) Destroy(iconObj);
                    yield break;
                }

                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, fromScreen,
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera,
                    out Vector2 fromLocal);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, toScreen,
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera,
                    out Vector2 toLocal);

                Vector2 pos = Vector2.Lerp(fromLocal, toLocal, t);
                pos.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
                rect.anchoredPosition = pos;

                // Shrink in the final 20%
                if (t > 0.8f)
                {
                    float shrinkT = (t - 0.8f) / 0.2f;
                    rect.localScale = Vector3.one * (1f - shrinkT);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (iconObj != null)
                Destroy(iconObj);
        }
    }
}
