#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace ClockworkCraft
{
    /// <summary>
    /// Spawns icon particles that burst out of resource nodes when hit,
    /// then fly toward the currency bar. When they arrive, the resource count increases.
    ///
    /// Uses CurrencyDatabase sprite icons when available, falls back to emoji text.
    /// Each particle = +1 resource on arrival at the bar.
    ///
    /// Flow:
    /// 1. GridEntityActor calls SpawnLoot() after dealing damage
    /// 2. Icon particles burst outward from the hit position with random spread
    /// 3. After a brief hang, they curve toward the ResourceDisplayUI panel
    /// 4. On arrival, ResourceManager.AddResource() is called for each particle
    ///
    /// Singleton — auto-created by MapGeneratorV2.EnsureManagers() alongside ResourceDisplayUI.
    /// </summary>
    public class ResourceLootFX : MonoBehaviour
    {
        public static ResourceLootFX Instance { get; private set; }

        [Header("Burst Settings")]
        [Tooltip("How far particles scatter from the hit point (world units).")]
        public float burstRadius = 1.2f;
        [Tooltip("How high particles pop up during the initial burst.")]
        public float burstHeight = 1.5f;
        [Tooltip("Duration of the initial pop-out phase (seconds).")]
        public float burstDuration = 0.3f;
        [Tooltip("Brief hang time at peak before flying to bar.")]
        public float hangDuration = 0.15f;

        [Header("Fly Settings")]
        [Tooltip("Duration of the fly-to-bar phase (seconds).")]
        public float flyDuration = 0.55f;
        [Tooltip("Curve height during fly phase (screen pixels).")]
        public float flyCurveHeight = 80f;

        [Header("Visual")]
        [Tooltip("Fixed icon size in canvas units. Adjustable in Inspector.")]
        public float iconSize = 72f;

        // Pool — separate pools for sprite-based and text-based particles
        private Canvas canvas;
        private RectTransform canvasRect;
        private Camera mainCamera;
        private readonly Queue<GameObject> spritePool = new Queue<GameObject>();
        private readonly Queue<GameObject> textPool = new Queue<GameObject>();

        /// <summary>Returns icon size in canvas units, accounting for canvas scale factor.</summary>
        private float ScaledIconSize
        {
            get
            {
                float scale = canvas != null ? canvas.scaleFactor : 1f;
                return iconSize / Mathf.Max(scale, 0.01f);
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            // Find a ScreenSpaceOverlay canvas — WorldSpace canvases have different coordinate systems
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvas = c;
                    break;
                }
            }
            // Fallback to any canvas if no overlay found
            if (canvas == null)
                canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
                canvasRect = canvas.GetComponent<RectTransform>();
            mainCamera = Camera.main;
        }

        // ─────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Spawn loot particles bursting from a world position.
        /// Called by GridEntityActor after a strong interaction deals damage.
        /// Resources are NOT added immediately — they're added when each particle arrives at the bar.
        /// </summary>
        public void SpawnLoot(Vector3 worldPosition, ResourceType resourceType, int count)
        {
            if (canvas == null || mainCamera == null) return;
            if (count <= 0 || resourceType == ResourceType.None) return;

            // Try to get sprite icon from CurrencyDatabase
            Sprite icon = ResourceDisplayUI.GetIconForResource(resourceType);

            for (int i = 0; i < count; i++)
            {
                StartCoroutine(LootParticleCoroutine(worldPosition, resourceType, icon, i, count));
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Particle Lifecycle
        // ─────────────────────────────────────────────────────────────────

        private IEnumerator LootParticleCoroutine(Vector3 worldPos, ResourceType resType, Sprite icon, int index, int total)
        {
            // Small stagger so they don't all spawn at once
            if (index > 0)
                yield return new WaitForSeconds(index * 0.04f);

            // Convert world position to screen position
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);
            if (screenPos.z < 0) yield break; // Behind camera

            // Create or reuse a particle object
            bool useSpriteMode = (icon != null);
            GameObject obj = GetFromPool(useSpriteMode);
            RectTransform rect = obj.GetComponent<RectTransform>();
            CanvasGroup cg = obj.GetComponent<CanvasGroup>();
            if (cg == null) cg = obj.AddComponent<CanvasGroup>();

            // Configure visual — always refresh size for current resolution
            float scaled = ScaledIconSize;
            rect.sizeDelta = new Vector2(scaled, scaled);

            if (useSpriteMode)
            {
                Image img = obj.GetComponent<Image>();
                if (img != null)
                {
                    img.sprite = icon;
                    img.color = Color.white;
                }
            }
            else
            {
                TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = ResourceDisplayUI.GetEmojiForResource(resType);
                    tmp.fontSize = scaled;
                }
            }

            cg.alpha = 1f;
            obj.SetActive(true);

            // Convert screen point to canvas local position
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera,
                out Vector2 startLocal);

            // ── Phase 1: Burst outward ───────────────────────────────────
            float angle = Random.Range(0f, 360f);
            float dist = Random.Range(burstRadius * 0.4f, burstRadius);
            Vector2 burstOffset = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad) * dist * 50f,
                Mathf.Sin(angle * Mathf.Deg2Rad) * dist * 30f + burstHeight * 50f
            );

            Vector2 burstTarget = startLocal + burstOffset;

            float elapsed = 0f;
            while (elapsed < burstDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / burstDuration);
                float eased = 1f - (1f - t) * (1f - t); // Ease-out

                if (obj == null) yield break;
                rect.anchoredPosition = Vector2.Lerp(startLocal, burstTarget, eased);

                float scale = Mathf.Lerp(0.3f, 1.1f, eased);
                rect.localScale = Vector3.one * scale;

                yield return null;
            }

            if (obj == null) yield break;
            rect.anchoredPosition = burstTarget;
            rect.localScale = Vector3.one;

            // ── Phase 2: Hang briefly ────────────────────────────────────
            yield return new WaitForSeconds(hangDuration);

            if (obj == null) yield break;

            // ── Phase 3: Fly to currency bar ─────────────────────────────
            Vector2 flyStart = burstTarget;

            Vector2 flyEnd;
            if (ResourceDisplayUI.Instance != null && ResourceDisplayUI.Instance.GetSlotRect(resType) != null)
            {
                // Fly to the specific currency slot for this resource type
                RectTransform barRect = ResourceDisplayUI.Instance.GetSlotRect(resType);
                Vector3 barScreenPos = RectTransformUtility.WorldToScreenPoint(
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera,
                    barRect.position);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, barScreenPos, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera,
                    out flyEnd);
            }
            else
            {
                flyEnd = new Vector2(-canvasRect.rect.width * 0.4f, -canvasRect.rect.height * 0.4f);
            }

            elapsed = 0f;
            while (elapsed < flyDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / flyDuration);

                if (obj == null) yield break;

                // Smooth ease-in-out for position — accelerates then decelerates into target
                float eased = t * t * (3f - 2f * t);

                Vector2 pos = Vector2.Lerp(flyStart, flyEnd, eased);
                float arc = Mathf.Sin(t * Mathf.PI) * flyCurveHeight;
                pos.y += arc;

                rect.anchoredPosition = pos;

                // Stay full size for 80% of the flight, then shrink in the final stretch
                float shrinkStart = 0.8f;
                float scale;
                if (t < shrinkStart)
                {
                    scale = 1f;
                }
                else
                {
                    float shrinkT = (t - shrinkStart) / (1f - shrinkStart);
                    scale = Mathf.Lerp(1f, 0.5f, shrinkT);
                }
                rect.localScale = Vector3.one * scale;

                // Stay fully opaque until the very end
                cg.alpha = t < 0.9f ? 1f : Mathf.Lerp(1f, 0.7f, (t - 0.9f) / 0.1f);

                yield return null;
            }

            if (obj == null) yield break;

            // Snap to exact target position before adding resource
            rect.anchoredPosition = flyEnd;
            rect.localScale = Vector3.one * 0.5f;

            // ── Phase 4: Arrival — add the resource ──────────────────────
            ResourceManager.Instance?.AddResource(resType, 1);

            // SFX: coin/resource collection jingle
            if (ClockworkGrid.GameSFXManager.Instance != null)
                ClockworkGrid.GameSFXManager.Instance.PlayCoinCollect();

            // Return to pool
            cg.alpha = 0f;
            obj.SetActive(false);
            if (useSpriteMode)
                spritePool.Enqueue(obj);
            else
                textPool.Enqueue(obj);
        }

        // ─────────────────────────────────────────────────────────────────
        // Object Pool
        // ─────────────────────────────────────────────────────────────────

        private GameObject GetFromPool(bool spriteMode)
        {
            Queue<GameObject> pool = spriteMode ? spritePool : textPool;

            if (pool.Count > 0)
            {
                GameObject obj = pool.Dequeue();
                if (obj != null) return obj;
            }

            return spriteMode ? CreateSpriteParticle() : CreateTextParticle();
        }

        private GameObject CreateSpriteParticle()
        {
            GameObject obj = new GameObject("LootParticle_Sprite");
            obj.transform.SetParent(canvas.transform, false);

            RectTransform rect = obj.AddComponent<RectTransform>();
            float scaled = ScaledIconSize;
            rect.sizeDelta = new Vector2(scaled, scaled);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);

            Image img = obj.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;

            CanvasGroup cg = obj.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            Canvas particleCanvas = obj.AddComponent<Canvas>();
            particleCanvas.overrideSorting = true;
            particleCanvas.sortingOrder = 100;

            obj.SetActive(false);
            return obj;
        }

        private GameObject CreateTextParticle()
        {
            GameObject obj = new GameObject("LootParticle_Text");
            obj.transform.SetParent(canvas.transform, false);

            RectTransform rect = obj.AddComponent<RectTransform>();
            float scaled = ScaledIconSize;
            rect.sizeDelta = new Vector2(scaled, scaled);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);

            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = false;
            tmp.fontSize = scaled;
            tmp.richText = true;
            tmp.raycastTarget = false;
            tmp.overflowMode = TextOverflowModes.Overflow;

            CanvasGroup cg = obj.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            Canvas particleCanvas = obj.AddComponent<Canvas>();
            particleCanvas.overrideSorting = true;
            particleCanvas.sortingOrder = 100;

            obj.SetActive(false);
            return obj;
        }
    }
}
