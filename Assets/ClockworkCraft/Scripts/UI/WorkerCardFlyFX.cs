using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using ClockworkGrid;
using ClockworkCraft;

namespace LittleCafe
{
    /// <summary>
    /// Spawns a worker icon that bursts from a building and flies to the dock bar.
    /// On arrival, creates a worker card in the player's hand via DockBarManager.
    ///
    /// Follows the same animation pattern as ResourceLootFX:
    ///   Phase 1 — Burst upward from building position
    ///   Phase 2 — Brief hang at peak
    ///   Phase 3 — Arc fly toward dock bar
    ///   Phase 4 — Arrival: add worker card to hand
    ///
    /// Singleton — auto-created by MapGeneratorV2.EnsureManagers().
    /// </summary>
    public class WorkerCardFlyFX : MonoBehaviour
    {
        public static WorkerCardFlyFX Instance { get; private set; }

        [Header("Burst Settings")]
        [Tooltip("How far the icon pops up from the building (world units).")]
        public float burstRadius = 1.0f;
        [Tooltip("How high the icon pops up initially.")]
        public float burstHeight = 2.0f;
        [Tooltip("Duration of the initial pop-out phase (seconds).")]
        public float burstDuration = 0.3f;
        [Tooltip("Brief hang time at peak before flying to dock.")]
        public float hangDuration = 0.2f;

        [Header("Fly Settings")]
        [Tooltip("Duration of the fly-to-dock phase (seconds).")]
        public float flyDuration = 0.6f;
        [Tooltip("Curve height during fly phase (screen pixels).")]
        public float flyCurveHeight = 100f;

        [Header("Visual")]
        [Tooltip("Size of the particle icon.")]
        public float iconSize = 96f;

        // Pool
        private Canvas canvas;
        private RectTransform canvasRect;
        private Camera mainCamera;
        private readonly Queue<GameObject> pool = new Queue<GameObject>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
                canvasRect = canvas.GetComponent<RectTransform>();
            mainCamera = Camera.main;
        }

        // ─────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Spawn a worker icon that flies from worldPosition to the dock bar.
        /// On arrival, adds the worker card to DockBarManager.
        /// </summary>
        public void SpawnWorkerFly(Vector3 worldPosition, WorkerData workerData, int index = 0)
        {
            if (canvas == null || mainCamera == null) return;
            if (workerData == null) return;

            StartCoroutine(WorkerFlyCoroutine(worldPosition, workerData, index));
        }

        /// <summary>
        /// Spawn a card icon that flies from worldPosition to the dock bar.
        /// On arrival, adds the UnitStats card directly to DockBarManager.
        /// Used by RandomBuilding production output (Statue building).
        /// </summary>
        public void SpawnCardFly(Vector3 worldPosition, ClockworkGrid.UnitStats cardStats, int index = 0)
        {
            if (canvas == null || mainCamera == null) return;
            if (cardStats == null) return;

            StartCoroutine(CardFlyCoroutine(worldPosition, cardStats, index));
        }

        // ─────────────────────────────────────────────────────────────────
        // Fly Coroutine
        // ─────────────────────────────────────────────────────────────────

        private IEnumerator WorkerFlyCoroutine(Vector3 worldPos, WorkerData workerData, int index)
        {
            // Small stagger for multiple spawns
            if (index > 0)
                yield return new WaitForSeconds(index * 0.08f);

            // Convert world position to screen position
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);
            if (screenPos.z < 0) yield break; // Behind camera

            // Create or reuse particle
            GameObject obj = GetFromPool();
            RectTransform rect = obj.GetComponent<RectTransform>();
            CanvasGroup cg = obj.GetComponent<CanvasGroup>();
            if (cg == null) cg = obj.AddComponent<CanvasGroup>();

            // Set the worker icon
            Image img = obj.GetComponent<Image>();
            if (img != null && workerData.icon != null)
            {
                img.sprite = workerData.icon;
                img.color = Color.white;
            }

            cg.alpha = 1f;
            obj.SetActive(true);

            // Convert screen point to canvas local position
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera,
                out Vector2 startLocal);

            // ── Phase 1: Burst upward ─────────────────────────────────────
            float angle = Random.Range(-30f, 30f); // Slight spread
            Vector2 burstOffset = new Vector2(
                Mathf.Sin(angle * Mathf.Deg2Rad) * burstRadius * 40f,
                burstHeight * 60f
            );

            Vector2 burstTarget = startLocal + burstOffset;

            float elapsed = 0f;
            while (elapsed < burstDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / burstDuration);
                float eased = 1f - (1f - t) * (1f - t); // Ease-out

                rect.anchoredPosition = Vector2.Lerp(startLocal, burstTarget, eased);

                float scale = Mathf.Lerp(0.3f, 1.1f, eased);
                rect.localScale = Vector3.one * scale;

                yield return null;
            }

            rect.anchoredPosition = burstTarget;
            rect.localScale = Vector3.one;

            // ── Phase 2: Hang briefly ─────────────────────────────────────
            yield return new WaitForSeconds(hangDuration);

            // ── Phase 3: Fly to dock bar (card container area) ─────────────
            Vector2 flyStart = burstTarget;

            // Target: where the next card will appear in the hand
            Vector2 flyEnd;
            DockBarManager dock = DockBarManager.Instance;
            if (dock != null && dock.CardContainer != null)
            {
                // Target the card container — that's where cards actually live
                RectTransform containerRect = dock.CardContainer.GetComponent<RectTransform>();
                if (containerRect != null)
                {
                    // Aim for the right edge of existing cards (where the new card will appear)
                    // Use the container's world position, offset right by the number of cards
                    Vector3 targetWorldPos = containerRect.position;
                    Vector3 targetScreenPos = RectTransformUtility.WorldToScreenPoint(
                        canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera,
                        targetWorldPos);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        canvasRect, targetScreenPos,
                        canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera,
                        out flyEnd);
                }
                else
                {
                    flyEnd = new Vector2(0f, -canvasRect.rect.height * 0.4f);
                }
            }
            else
            {
                flyEnd = new Vector2(0f, -canvasRect.rect.height * 0.4f);
            }

            elapsed = 0f;
            while (elapsed < flyDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / flyDuration);

                // Smooth ease-in-out
                float eased = t * t * (3f - 2f * t);

                Vector2 pos = Vector2.Lerp(flyStart, flyEnd, eased);
                float arc = Mathf.Sin(t * Mathf.PI) * flyCurveHeight;
                pos.y += arc;

                rect.anchoredPosition = pos;

                // Shrink in final 20%
                float scale = t < 0.8f ? 1f : Mathf.Lerp(1f, 0.5f, (t - 0.8f) / 0.2f);
                rect.localScale = Vector3.one * scale;

                // Fade slightly at very end
                cg.alpha = t < 0.9f ? 1f : Mathf.Lerp(1f, 0.7f, (t - 0.9f) / 0.1f);

                yield return null;
            }

            // ── Phase 4: Arrival — add worker card ────────────────────────
            if (DockBarManager.Instance != null)
            {
                DockBarManager.Instance.AddWorkerCard(workerData);
            }

            // SFX: worker card acquired
            if (ClockworkGrid.GameSFXManager.Instance != null)
                ClockworkGrid.GameSFXManager.Instance.PlaySuccess();

            // Camera shake for satisfaction
            CameraSystemLocator.Current?.Shake(0.08f, 0.15f);

            // Return to pool
            cg.alpha = 0f;
            obj.SetActive(false);
            pool.Enqueue(obj);
        }

        /// <summary>
        /// Fly coroutine for a generic UnitStats card (RandomBuilding output).
        /// Reuses the same burst → hang → arc-fly → arrival pattern as workers,
        /// but on arrival adds the card directly via DockBarManager.AddCard().
        /// </summary>
        private IEnumerator CardFlyCoroutine(Vector3 worldPos, ClockworkGrid.UnitStats cardStats, int index)
        {
            if (index > 0)
                yield return new WaitForSeconds(index * 0.08f);

            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);
            if (screenPos.z < 0) yield break;

            GameObject obj = GetFromPool();
            RectTransform rect = obj.GetComponent<RectTransform>();
            CanvasGroup cg = obj.GetComponent<CanvasGroup>();
            if (cg == null) cg = obj.AddComponent<CanvasGroup>();

            // Set the card icon
            Image img = obj.GetComponent<Image>();
            if (img != null && cardStats.iconSprite != null)
            {
                img.sprite = cardStats.iconSprite;
                img.color = Color.white;
            }

            cg.alpha = 1f;
            obj.SetActive(true);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera,
                out Vector2 startLocal);

            // Phase 1: Burst upward
            float angle = Random.Range(-30f, 30f);
            Vector2 burstOffset = new Vector2(
                Mathf.Sin(angle * Mathf.Deg2Rad) * burstRadius * 40f,
                burstHeight * 60f);
            Vector2 burstTarget = startLocal + burstOffset;

            float elapsed = 0f;
            while (elapsed < burstDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / burstDuration);
                float eased = 1f - (1f - t) * (1f - t);
                rect.anchoredPosition = Vector2.Lerp(startLocal, burstTarget, eased);
                float scale = Mathf.Lerp(0.3f, 1.1f, eased);
                rect.localScale = Vector3.one * scale;
                yield return null;
            }

            rect.anchoredPosition = burstTarget;
            rect.localScale = Vector3.one;

            // Phase 2: Hang
            yield return new WaitForSeconds(hangDuration);

            // Phase 3: Fly to dock
            Vector2 flyStart = burstTarget;
            Vector2 flyEnd;
            ClockworkGrid.DockBarManager dock = ClockworkGrid.DockBarManager.Instance;
            if (dock != null && dock.CardContainer != null)
            {
                RectTransform containerRect = dock.CardContainer.GetComponent<RectTransform>();
                if (containerRect != null)
                {
                    Vector3 targetWorldPos = containerRect.position;
                    Vector3 targetScreenPos = RectTransformUtility.WorldToScreenPoint(
                        canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera,
                        targetWorldPos);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        canvasRect, targetScreenPos,
                        canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera,
                        out flyEnd);
                }
                else
                {
                    flyEnd = new Vector2(0f, -canvasRect.rect.height * 0.4f);
                }
            }
            else
            {
                flyEnd = new Vector2(0f, -canvasRect.rect.height * 0.4f);
            }

            elapsed = 0f;
            while (elapsed < flyDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / flyDuration);
                float eased = t * t * (3f - 2f * t);
                Vector2 pos = Vector2.Lerp(flyStart, flyEnd, eased);
                float arc = Mathf.Sin(t * Mathf.PI) * flyCurveHeight;
                pos.y += arc;
                rect.anchoredPosition = pos;
                float scale = t < 0.8f ? 1f : Mathf.Lerp(1f, 0.5f, (t - 0.8f) / 0.2f);
                rect.localScale = Vector3.one * scale;
                cg.alpha = t < 0.9f ? 1f : Mathf.Lerp(1f, 0.7f, (t - 0.9f) / 0.1f);
                yield return null;
            }

            // Phase 4: Arrival — add drawn card to hand
            if (ClockworkGrid.DockBarManager.Instance != null)
            {
                ClockworkGrid.DockBarManager.Instance.AddCard(cardStats, markAsNew: true);
            }

            if (ClockworkGrid.GameSFXManager.Instance != null)
                ClockworkGrid.GameSFXManager.Instance.PlaySuccess();

            CameraSystemLocator.Current?.Shake(0.08f, 0.15f);

            cg.alpha = 0f;
            obj.SetActive(false);
            pool.Enqueue(obj);
        }

        // ─────────────────────────────────────────────────────────────────
        // Object Pool
        // ─────────────────────────────────────────────────────────────────

        private GameObject GetFromPool()
        {
            if (pool.Count > 0)
            {
                GameObject obj = pool.Dequeue();
                if (obj != null) return obj;
            }

            return CreateParticle();
        }

        private GameObject CreateParticle()
        {
            GameObject obj = new GameObject("WorkerFlyParticle");
            obj.transform.SetParent(canvas.transform, false);

            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(iconSize, iconSize);
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
            particleCanvas.sortingOrder = 110; // Above loot particles

            obj.SetActive(false);
            return obj;
        }
    }
}
