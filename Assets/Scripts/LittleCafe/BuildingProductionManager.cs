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
    /// Manages production timers for placed buildings.
    /// Uses world-space Canvases for both the radial timer and the reward popup,
    /// so they billboard naturally and can be designed/overridden with prefabs.
    ///
    /// Timer uses IntervalTimer ticks (2s each). Timer pauses while pop-up is active.
    /// One reward at a time per building — no stacking.
    ///
    /// Each collection increases that building's effective interval by its
    /// productionIntervalBonus, so repeated use gets progressively slower.
    ///
    /// Worker output picks a random worker from WorkerDatabase each time.
    ///
    /// Singleton — auto-created by MapGeneratorV2.EnsureManagers().
    /// </summary>
    public class BuildingProductionManager : MonoBehaviour
    {
        public static BuildingProductionManager Instance { get; private set; }

        // ─── Timer Visual Settings ──────────────────────────────────────
        [Header("Radial Timer (World-Space Canvas)")]
        [Tooltip("Optional prefab for the timer Canvas. If null, a default is created programmatically.")]
        public GameObject timerPrefab;

        [Tooltip("World-space size of the timer canvas (meters).")]
        public float timerWorldSize = 0.6f;
        [Tooltip("Height above building center.")]
        public float timerHeight = 1.8f;
        [Tooltip("Background ring color (unfilled portion).")]
        public Color timerBgColor = new Color(0.15f, 0.15f, 0.15f, 0.7f);
        [Tooltip("Fill ring color (progress portion).")]
        public Color timerFillColor = new Color(0.3f, 0.85f, 0.4f, 1f);
        [Tooltip("Fill color when almost complete (final 10%).")]
        public Color timerAlmostDoneColor = new Color(1f, 0.85f, 0.2f, 1f);

        // ─── Popup Visual Settings ──────────────────────────────────────
        [Header("Reward Popup (World-Space Canvas)")]
        [Tooltip("Optional prefab for the popup Canvas. If null, a default is created programmatically.")]
        public GameObject popupPrefab;

        [Tooltip("Height above building center where popup floats.")]
        public float popupHeight = 2.0f;
        [Tooltip("World-space size of the popup canvas (meters).")]
        public float popupWorldSize = 1.0f;
        [Tooltip("Bob amplitude (world units).")]
        public float bobAmplitude = 0.1f;
        [Tooltip("Bob speed.")]
        public float bobSpeed = 2f;
        [Tooltip("Glow/frame color behind icon.")]
        public Color popupGlowColor = new Color(1f, 0.9f, 0.5f, 0.6f);

        // ─── Database ───────────────────────────────────────────────────
        [Header("Database References")]
        [Tooltip("WorkerDatabase — a random worker is chosen each production cycle.")]
        public WorkerDatabase workerDatabase;

        // ─── Debug ──────────────────────────────────────────────────────
        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;

        // ─────────────────────────────────────────────────────────────────
        // Internal State
        // ─────────────────────────────────────────────────────────────────

        private class ProductionEntry
        {
            public GameObject buildingObj;
            public ProductionOutputType outputType;
            public float baseInterval;
            public float intervalBonus;
            public ResourceType producedResourceType;
            public int amount;

            // Timer state
            public float elapsedTime;
            public int collectCount;
            public bool isReady;
            public WorkerData pendingWorker;
            public UnitStats pendingCard; // For RandomBuilding output

            // Delay: timer starts hidden, reveals after first tick
            public bool timerRevealed;

            // World-space timer canvas
            public GameObject timerCanvasObj;
            public Image timerFillImage;
            public TextMeshProUGUI timerCountText;

            // Cached height from RefHeight (for popup/timer positioning)
            public float objectTopHeight = -1f;

            // World-space popup canvas
            public GameObject popupCanvasObj;
            public Image popupIconImage;
            public SphereCollider popupCollider;

            public float EffectiveInterval => baseInterval + (intervalBonus * collectCount);
        }

        private readonly List<ProductionEntry> entries = new List<ProductionEntry>();
        private Camera mainCamera;

        // Cached circle sprites for radial fill (generated once)
        private static Sprite circleSprite;
        private static Sprite ringSprite;

        // ─────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            mainCamera = Camera.main;

            if (IntervalTimer.Instance != null)
                IntervalTimer.Instance.OnIntervalTick += OnIntervalTick;
        }

        void OnDestroy()
        {
            if (IntervalTimer.Instance != null)
                IntervalTimer.Instance.OnIntervalTick -= OnIntervalTick;
        }

        void Update()
        {
            HandlePopupTap();
            UpdateTimerFill();
            AnimatePopups();
        }

        void LateUpdate()
        {
            BillboardAll();
        }

        // ─────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────

        public void RegisterBuilding(GameObject buildingObj, UnitStats stats)
        {
            if (stats == null || stats.productionOutputType == ProductionOutputType.None) return;
            if (stats.productionInterval <= 0f) return;

            var entry = new ProductionEntry
            {
                buildingObj = buildingObj,
                outputType = stats.productionOutputType,
                baseInterval = stats.productionInterval,
                intervalBonus = stats.productionIntervalBonus,
                producedResourceType = stats.producedResourceType,
                amount = stats.productionAmount,
                elapsedTime = 0f,
                collectCount = 0,
                isReady = false,
                pendingWorker = null
            };

            // Cache RefHeight for positioning timer/popup above the object
            entry.objectTopHeight = GridEntityHPBar.GetTopOfObject(buildingObj.transform, 1.5f);
            entry.timerRevealed = false;

            CreateTimerCanvas(entry);

            // Start timer hidden — will reveal after 1 tick so the player can appreciate the object
            if (entry.timerCanvasObj != null)
                entry.timerCanvasObj.SetActive(false);

            entries.Add(entry);

            if (verboseLogging)
                Debug.Log($"[BuildingProduction] Registered '{buildingObj.name}' — produces {stats.productionOutputType} every {stats.productionInterval}s (bonus +{stats.productionIntervalBonus}s per collect, topHeight={entry.objectTopHeight:F1})");
        }

        public void UnregisterBuilding(GameObject buildingObj)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i].buildingObj == buildingObj)
                {
                    DestroyEntryVisuals(entries[i]);
                    entries.RemoveAt(i);
                    if (verboseLogging)
                        Debug.Log($"[BuildingProduction] Unregistered '{buildingObj.name}'");
                    return;
                }
            }
        }

        private void DestroyEntryVisuals(ProductionEntry entry)
        {
            if (entry.popupCanvasObj != null) Destroy(entry.popupCanvasObj);
            if (entry.timerCanvasObj != null) Destroy(entry.timerCanvasObj);
        }

        // ─────────────────────────────────────────────────────────────────
        // World-Space Timer Canvas
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Get the world-space height for timer/popup above a building.
        /// Uses cached RefHeight, plus an offset.
        /// </summary>
        private float GetTimerY(ProductionEntry entry)
        {
            float baseHeight = entry.objectTopHeight > 0f ? entry.objectTopHeight : timerHeight;
            return baseHeight + 0.3f; // Small gap above the object top
        }

        private float GetPopupY(ProductionEntry entry)
        {
            float baseHeight = entry.objectTopHeight > 0f ? entry.objectTopHeight : popupHeight;
            return baseHeight + 0.5f; // Slightly higher than timer
        }

        private void CreateTimerCanvas(ProductionEntry entry)
        {
            GameObject canvasObj;

            if (timerPrefab != null)
            {
                canvasObj = Instantiate(timerPrefab);
                canvasObj.name = $"ProductionTimer_{entry.buildingObj.name}";

                // Try to find the fill image in the prefab hierarchy
                entry.timerFillImage = FindChildImage(canvasObj, "Fill");
            }
            else
            {
                canvasObj = CreateDefaultTimerCanvas(entry);
            }

            // Position above building using RefHeight
            canvasObj.transform.position = entry.buildingObj.transform.position + Vector3.up * GetTimerY(entry);
            entry.timerCanvasObj = canvasObj;

            // Update count text to show initial collect count
            UpdateCountText(entry);
        }

        private GameObject CreateDefaultTimerCanvas(ProductionEntry entry)
        {
            EnsureCircleSprites();

            // Root with world-space Canvas
            GameObject root = new GameObject($"ProductionTimer_{entry.buildingObj.name}");
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;

            RectTransform canvasRect = root.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(100f, 100f); // 100 canvas units
            canvasRect.localScale = Vector3.one * (timerWorldSize / 100f); // Scale to world size

            // Disable raycasting on the canvas (we handle taps ourselves)
            GraphicRaycaster gr = root.AddComponent<GraphicRaycaster>();
            gr.enabled = false;

            // Background ring (full circle, dark) — uses ring sprite
            GameObject bgObj = new GameObject("Background");
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.SetParent(canvasRect, false);
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.sprite = ringSprite;
            bgImage.color = timerBgColor;
            bgImage.type = Image.Type.Filled;
            bgImage.fillMethod = Image.FillMethod.Radial360;
            bgImage.fillAmount = 1f;
            bgImage.raycastTarget = false;

            // Fill ring (radial progress) — uses ring sprite for donut shape
            GameObject fillObj = new GameObject("Fill");
            RectTransform fillRect = fillObj.AddComponent<RectTransform>();
            fillRect.SetParent(canvasRect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;

            Image fillImage = fillObj.AddComponent<Image>();
            fillImage.sprite = ringSprite;
            fillImage.color = timerFillColor;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Radial360;
            fillImage.fillOrigin = (int)Image.Origin360.Top;
            fillImage.fillClockwise = true;
            fillImage.fillAmount = 0f;
            fillImage.raycastTarget = false;

            entry.timerFillImage = fillImage;

            // Collection count text in the center of the donut
            GameObject countObj = new GameObject("CountText");
            RectTransform countRect = countObj.AddComponent<RectTransform>();
            countRect.SetParent(canvasRect, false);
            // Center within the donut hole (inner 55% of radius)
            countRect.anchorMin = new Vector2(0.25f, 0.25f);
            countRect.anchorMax = new Vector2(0.75f, 0.75f);
            countRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI countText = countObj.AddComponent<TextMeshProUGUI>();
            countText.text = "0";
            countText.fontSize = 42;
            countText.color = Color.white;
            countText.alignment = TextAlignmentOptions.Center;
            countText.fontStyle = FontStyles.Bold;
            countText.enableAutoSizing = false;
            countText.raycastTarget = false;
            // Outline for readability against the ring
            countText.outlineWidth = 0.2f;
            countText.outlineColor = new Color(0f, 0f, 0f, 0.6f);

            entry.timerCountText = countText;

            return root;
        }

        /// <summary>
        /// Update the collection count text shown in the center of the donut timer.
        /// Color shifts white → yellow → orange → red as collect count rises,
        /// giving a clear visual signal that the timer is getting slower.
        /// </summary>
        private void UpdateCountText(ProductionEntry entry)
        {
            if (entry.timerCountText == null) return;

            entry.timerCountText.text = entry.collectCount.ToString();

            // Color escalation: white → yellow → orange → red over 6 collections
            float t = Mathf.Clamp01(entry.collectCount / 6f);
            Color countColor;
            if (t < 0.5f)
                countColor = Color.Lerp(Color.white, new Color(1f, 0.85f, 0.2f), t * 2f); // white → yellow
            else
                countColor = Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(0.9f, 0.4f, 0.25f), (t - 0.5f) * 2f); // yellow → orange-red
            entry.timerCountText.color = countColor;
        }

        // ─────────────────────────────────────────────────────────────────
        // Timer Fill & Position Update
        // ─────────────────────────────────────────────────────────────────

        private void UpdateTimerFill()
        {
            foreach (var entry in entries)
            {
                if (entry.buildingObj == null || entry.timerCanvasObj == null) continue;

                // Hide timer when ready (popup showing instead), or if not yet revealed
                bool shouldShow = entry.timerRevealed && !entry.isReady;
                entry.timerCanvasObj.SetActive(shouldShow);
                if (!shouldShow) continue;

                // Keep timer above building (in case building moves) — uses RefHeight
                entry.timerCanvasObj.transform.position =
                    entry.buildingObj.transform.position + Vector3.up * GetTimerY(entry);

                // Update fill
                float progress = Mathf.Clamp01(entry.elapsedTime / entry.EffectiveInterval);
                if (entry.timerFillImage != null)
                {
                    entry.timerFillImage.fillAmount = progress;
                    entry.timerFillImage.color = progress >= 0.9f ? timerAlmostDoneColor : timerFillColor;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Billboard (world-space canvases face camera)
        // ─────────────────────────────────────────────────────────────────

        private void BillboardAll()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return;

            Quaternion camRotation = mainCamera.transform.rotation;

            foreach (var entry in entries)
            {
                // Billboard timer
                if (entry.timerCanvasObj != null && entry.timerCanvasObj.activeSelf)
                    entry.timerCanvasObj.transform.rotation = camRotation;

                // Billboard popup
                if (entry.popupCanvasObj != null && entry.popupCanvasObj.activeSelf)
                    entry.popupCanvasObj.transform.rotation = camRotation;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Timer Tick
        // ─────────────────────────────────────────────────────────────────

        private void OnIntervalTick(int intervalCount)
        {
            float tickDuration = IntervalTimer.Instance != null
                ? IntervalTimer.Instance.IntervalDuration
                : 2f;

            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];

                if (entry.buildingObj == null)
                {
                    DestroyEntryVisuals(entry);
                    entries.RemoveAt(i);
                    continue;
                }

                if (entry.isReady) continue;

                // Reveal timer after first tick (delayed so player can appreciate the object)
                if (!entry.timerRevealed && entry.timerCanvasObj != null)
                {
                    entry.timerRevealed = true;
                    entry.timerCanvasObj.SetActive(true);
                    StartCoroutine(TimerAppearAnimation(entry.timerCanvasObj));
                }

                entry.elapsedTime += tickDuration;

                if (entry.elapsedTime >= entry.EffectiveInterval)
                {
                    entry.elapsedTime = 0f;
                    entry.isReady = true;

                    if (entry.outputType == ProductionOutputType.Worker)
                        entry.pendingWorker = PickRandomWorker();
                    else if (entry.outputType == ProductionOutputType.RandomBuilding)
                        entry.pendingCard = DrawRandomBuilding();

                    // SFX: production timer complete
                    if (GameSFXManager.Instance != null)
                        GameSFXManager.Instance.PlayTimerComplete();

                    SpawnPopup(entry);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Random Worker Selection
        // ─────────────────────────────────────────────────────────────────

        private WorkerData PickRandomWorker()
        {
            if (workerDatabase == null || workerDatabase.Count == 0)
            {
                Debug.LogWarning("[BuildingProduction] No WorkerDatabase or empty — can't pick random worker");
                return null;
            }

            var workers = workerDatabase.AllWorkers;
            List<WorkerData> validWorkers = new List<WorkerData>();
            foreach (var w in workers)
            {
                if (w.prefab != null) validWorkers.Add(w);
            }

            if (validWorkers.Count == 0) return null;
            return validWorkers[Random.Range(0, validWorkers.Count)];
        }

        // ─────────────────────────────────────────────────────────────────
        // World-Space Popup
        // ─────────────────────────────────────────────────────────────────

        private void SpawnPopup(ProductionEntry entry)
        {
            if (entry.buildingObj == null) return;

            Sprite rewardIcon = null;
            if (entry.outputType == ProductionOutputType.Worker && entry.pendingWorker != null)
                rewardIcon = entry.pendingWorker.icon;
            else if (entry.outputType == ProductionOutputType.Currency)
                rewardIcon = ResourceDisplayUI.GetIconForResource(entry.producedResourceType);
            else if (entry.outputType == ProductionOutputType.RandomBuilding && entry.pendingCard != null)
                rewardIcon = entry.pendingCard.iconSprite;

            GameObject canvasObj;

            if (popupPrefab != null)
            {
                canvasObj = Instantiate(popupPrefab);
                canvasObj.name = $"ProductionPopup_{entry.buildingObj.name}";
                entry.popupIconImage = FindChildImage(canvasObj, "Icon");
            }
            else
            {
                canvasObj = CreateDefaultPopupCanvas(entry);
            }

            canvasObj.transform.position = entry.buildingObj.transform.position + Vector3.up * GetPopupY(entry);

            // Set the icon sprite
            if (entry.popupIconImage != null && rewardIcon != null)
            {
                entry.popupIconImage.sprite = rewardIcon;
                entry.popupIconImage.enabled = true;
            }

            // Collider for tap detection — on a separate unscaled child
            // so it doesn't inherit the Canvas's tiny localScale
            GameObject colliderHolder = new GameObject("TapCollider");
            colliderHolder.transform.SetParent(canvasObj.transform, false);
            // Reset scale to world-space 1:1 (undo parent canvas scale)
            float canvasScale = canvasObj.transform.localScale.x;
            if (canvasScale > 0f)
                colliderHolder.transform.localScale = Vector3.one / canvasScale;

            SphereCollider col = colliderHolder.AddComponent<SphereCollider>();
            col.radius = 0.6f; // ~0.6 world units tap target
            col.isTrigger = false; // Must be non-trigger for Physics.Raycast

            entry.popupCanvasObj = canvasObj;
            entry.popupCollider = col;

            // SFX
            if (GameSFXManager.Instance != null)
                GameSFXManager.Instance.PlayPopupAppear();

            StartCoroutine(PopupSpawnAnimation(canvasObj));

            if (verboseLogging)
            {
                string rewardName = "";
                if (entry.outputType == ProductionOutputType.Worker && entry.pendingWorker != null)
                    rewardName = $" (worker: {entry.pendingWorker.GetCleanName()})";
                else if (entry.outputType == ProductionOutputType.RandomBuilding && entry.pendingCard != null)
                    rewardName = $" (card: {entry.pendingCard.unitName})";

                float nextInterval = entry.baseInterval + (entry.intervalBonus * (entry.collectCount + 1));
                Debug.Log($"[BuildingProduction] Pop-up ready on '{entry.buildingObj.name}'{rewardName}" +
                          $" — next interval will be {nextInterval}s");
            }
        }

        private GameObject CreateDefaultPopupCanvas(ProductionEntry entry)
        {
            EnsureCircleSprites();

            GameObject root = new GameObject($"ProductionPopup_{entry.buildingObj.name}");
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 60;

            RectTransform canvasRect = root.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(100f, 100f);
            canvasRect.localScale = Vector3.one * (popupWorldSize / 100f);

            GraphicRaycaster gr = root.AddComponent<GraphicRaycaster>();
            gr.enabled = false;

            // Subtle drop shadow (dark circle, offset slightly down)
            GameObject shadowObj = new GameObject("Shadow");
            RectTransform shadowRect = shadowObj.AddComponent<RectTransform>();
            shadowRect.SetParent(canvasRect, false);
            shadowRect.anchorMin = new Vector2(-0.05f, -0.08f);
            shadowRect.anchorMax = new Vector2(1.05f, 0.97f);
            shadowRect.sizeDelta = Vector2.zero;

            Image shadowImage = shadowObj.AddComponent<Image>();
            shadowImage.sprite = circleSprite;
            shadowImage.color = new Color(0f, 0f, 0f, 0.18f);
            shadowImage.raycastTarget = false;

            // Green rim ring (signals "ready to collect")
            GameObject rimObj = new GameObject("Rim");
            RectTransform rimRect = rimObj.AddComponent<RectTransform>();
            rimRect.SetParent(canvasRect, false);
            rimRect.anchorMin = new Vector2(-0.04f, -0.04f);
            rimRect.anchorMax = new Vector2(1.04f, 1.04f);
            rimRect.sizeDelta = Vector2.zero;

            Image rimImage = rimObj.AddComponent<Image>();
            rimImage.sprite = ringSprite;
            rimImage.color = new Color(0.3f, 0.8f, 0.4f, 0.85f);
            rimImage.raycastTarget = false;

            // Clean white base circle
            GameObject bgObj = new GameObject("Background");
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.SetParent(canvasRect, false);
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.sprite = circleSprite;
            bgImage.color = Color.white;
            bgImage.raycastTarget = false;

            // Icon image (worker sprite or reward icon, centered with padding)
            GameObject iconObj = new GameObject("Icon");
            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.SetParent(canvasRect, false);
            iconRect.anchorMin = new Vector2(0.15f, 0.15f);
            iconRect.anchorMax = new Vector2(0.85f, 0.85f);
            iconRect.sizeDelta = Vector2.zero;

            Image iconImage = iconObj.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            // Hidden until sprite is assigned — prevents white square
            iconImage.enabled = false;

            entry.popupIconImage = iconImage;
            return root;
        }

        /// <summary>
        /// Animate the timer bubble into existence: scale 0 → overshoot → settle.
        /// Called on first tick after building placement.
        /// </summary>
        private IEnumerator TimerAppearAnimation(GameObject timerObj)
        {
            if (timerObj == null) yield break;

            float baseScale = timerObj.transform.localScale.x;
            float duration = 0.3f;
            float elapsed = 0f;

            timerObj.transform.localScale = Vector3.zero;

            while (elapsed < duration && timerObj != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float scale;
                if (t < 0.6f)
                    scale = Mathf.Lerp(0f, baseScale * 1.2f, t / 0.6f);
                else
                    scale = Mathf.Lerp(baseScale * 1.2f, baseScale, (t - 0.6f) / 0.4f);

                timerObj.transform.localScale = Vector3.one * scale;
                yield return null;
            }

            if (timerObj != null)
                timerObj.transform.localScale = Vector3.one * baseScale;
        }

        private IEnumerator PopupSpawnAnimation(GameObject popup)
        {
            if (popup == null) yield break;

            float baseScale = popup.transform.localScale.x;
            float duration = 0.3f;
            float elapsed = 0f;

            // Start from zero
            popup.transform.localScale = Vector3.zero;

            while (elapsed < duration && popup != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float scale;
                if (t < 0.6f)
                    scale = Mathf.Lerp(0f, baseScale * 1.3f, t / 0.6f);
                else
                    scale = Mathf.Lerp(baseScale * 1.3f, baseScale, (t - 0.6f) / 0.4f);

                popup.transform.localScale = Vector3.one * scale;
                yield return null;
            }

            if (popup != null)
                popup.transform.localScale = Vector3.one * baseScale;
        }

        // ─────────────────────────────────────────────────────────────────
        // Bob Animation (for ready popups)
        // ─────────────────────────────────────────────────────────────────

        private void AnimatePopups()
        {
            float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;

            foreach (var entry in entries)
            {
                if (!entry.isReady || entry.popupCanvasObj == null || entry.buildingObj == null) continue;

                Vector3 basePos = entry.buildingObj.transform.position + Vector3.up * GetPopupY(entry);
                entry.popupCanvasObj.transform.position = basePos + Vector3.up * bob;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Tap Detection (physics raycast against popup colliders)
        // ─────────────────────────────────────────────────────────────────

        private void HandlePopupTap()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            if (DragDropHandler.Instance != null && DragDropHandler.Instance.IsDragging) return;

            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return;

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 100f)) return;

            GameObject hitObj = hit.collider.gameObject;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!entry.isReady) continue;

                // Check 1: tapped the popup canvas or its children (TapCollider)
                if (entry.popupCanvasObj != null &&
                    (hitObj == entry.popupCanvasObj ||
                     hitObj.transform.IsChildOf(entry.popupCanvasObj.transform)))
                {
                    CollectReward(entry);
                    return;
                }

                // Check 2: tapped the building itself (or any child mesh/collider)
                if (entry.buildingObj != null &&
                    (hitObj == entry.buildingObj ||
                     hitObj.transform.IsChildOf(entry.buildingObj.transform)))
                {
                    CollectReward(entry);
                    return;
                }
            }

            // Check 3: tapped a ground tile — resolve to grid cell → occupant → building
            if (GridManager.Instance != null)
            {
                int gx, gy;
                if (GridManager.Instance.WorldToGridPosition(hit.point, out gx, out gy))
                {
                    GameObject occupant = GridManager.Instance.GetCellOccupant(gx, gy);
                    if (occupant != null)
                    {
                        for (int i = 0; i < entries.Count; i++)
                        {
                            var entry = entries[i];
                            if (entry.isReady && entry.buildingObj == occupant)
                            {
                                CollectReward(entry);
                                return;
                            }
                        }
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Reward Collection
        // ─────────────────────────────────────────────────────────────────

        private void CollectReward(ProductionEntry entry)
        {
            if (entry.buildingObj == null) return;

            // Use RefHeight for loot/worker spawn position (top of object, not base)
            float topY = entry.objectTopHeight > 0f ? entry.objectTopHeight : 1.5f;
            Vector3 buildingWorldPos = entry.buildingObj.transform.position + Vector3.up * topY;
            bool collected = false;

            switch (entry.outputType)
            {
                case ProductionOutputType.Worker:
                    collected = CollectWorkerReward(entry, buildingWorldPos);
                    break;

                case ProductionOutputType.Currency:
                    CollectCurrencyReward(entry, buildingWorldPos);
                    collected = true;
                    break;

                case ProductionOutputType.RandomBuilding:
                    collected = CollectRandomBuildingReward(entry, buildingWorldPos);
                    break;
            }

            if (!collected)
            {
                // SFX + visual alert: hand full
                if (GameSFXManager.Instance != null)
                    GameSFXManager.Instance.PlayHandFull();
                if (DockBarManager.Instance != null)
                    DockBarManager.Instance.ShowHandFullPopup(Camera.main.WorldToScreenPoint(buildingWorldPos));
                return;
            }

            // SFX: reward successfully collected
            if (GameSFXManager.Instance != null)
                GameSFXManager.Instance.PlayRewardCollect();

            entry.collectCount++;

            if (entry.popupCanvasObj != null)
                Destroy(entry.popupCanvasObj);
            entry.popupCanvasObj = null;
            entry.popupIconImage = null;
            entry.popupCollider = null;
            entry.isReady = false;
            entry.elapsedTime = 0f;
            entry.pendingWorker = null;
            entry.pendingCard = null;
            entry.timerRevealed = false; // Re-delay the timer by 1 tick

            // Hide timer until next tick reveals it
            if (entry.timerCanvasObj != null)
                entry.timerCanvasObj.SetActive(false);

            // Reset the timer fill
            if (entry.timerFillImage != null)
                entry.timerFillImage.fillAmount = 0f;

            // Update collection count text in donut center
            UpdateCountText(entry);

            Animator anim = entry.buildingObj.GetComponentInChildren<Animator>();
            if (anim != null)
                anim.SetTrigger("interact");

            if (verboseLogging)
            {
                float prevInterval = entry.baseInterval + (entry.intervalBonus * (entry.collectCount - 1));
                Debug.Log($"[BuildingProduction] Collected #{entry.collectCount} from '{entry.buildingObj.name}' — " +
                          $"previous interval was {prevInterval}s, NEXT interval = {entry.EffectiveInterval}s " +
                          $"(base {entry.baseInterval} + bonus {entry.intervalBonus} x {entry.collectCount} collections)");
            }
        }

        private bool CollectWorkerReward(ProductionEntry entry, Vector3 worldPos)
        {
            DockBarManager dock = DockBarManager.Instance;
            if (dock == null)
            {
                Debug.LogWarning("[BuildingProduction] No DockBarManager — can't deliver worker card");
                return false;
            }

            if (dock.GetCardCount() >= DockBarManager.MAX_HAND_SIZE)
            {
                Debug.Log("[BuildingProduction] Hand full — pop-up stays until there's room");
                return false;
            }

            WorkerData wd = entry.pendingWorker;
            if (wd == null)
            {
                wd = PickRandomWorker();
                if (wd == null) return false;
            }

            WorkerCardFlyFX flyFX = WorkerCardFlyFX.Instance;
            if (flyFX != null)
            {
                for (int i = 0; i < entry.amount; i++)
                    flyFX.SpawnWorkerFly(worldPos, wd, i);
            }
            else
            {
                dock.AddWorkerCard(wd);
            }

            return true;
        }

        private void CollectCurrencyReward(ProductionEntry entry, Vector3 worldPos)
        {
            ResourceLootFX lootFX = ResourceLootFX.Instance;
            if (lootFX != null)
            {
                lootFX.SpawnLoot(worldPos, entry.producedResourceType, entry.amount);
            }
            else
            {
                ResourceManager.Instance?.AddResource(entry.producedResourceType, entry.amount);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Random Card Draw (Statue building — replaces draw button)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Draw a random card from the RaritySystem pool.
        /// Same as what the draw button does.
        /// </summary>
        private UnitStats DrawRandomBuilding()
        {
            if (RaritySystem.Instance == null)
            {
                Debug.LogWarning("[BuildingProduction] No RaritySystem — can't draw random card");
                return null;
            }

            UnitStats drawn = RaritySystem.Instance.DrawRandomUnit();
            if (drawn != null && verboseLogging)
                Debug.Log($"[BuildingProduction] Drew random card: {drawn.unitName} ({drawn.rarity})");
            return drawn;
        }

        /// <summary>
        /// Collect a random card reward: fly the card from the building to the dock bar.
        /// Returns false if hand is full.
        /// </summary>
        private bool CollectRandomBuildingReward(ProductionEntry entry, Vector3 worldPos)
        {
            DockBarManager dock = DockBarManager.Instance;
            if (dock == null)
            {
                Debug.LogWarning("[BuildingProduction] No DockBarManager — can't deliver card");
                return false;
            }

            if (dock.GetCardCount() >= DockBarManager.MAX_HAND_SIZE)
            {
                Debug.Log("[BuildingProduction] Hand full — pop-up stays until there's room");
                return false;
            }

            UnitStats card = entry.pendingCard;
            if (card == null)
            {
                // Fallback: draw now if pending card was lost
                card = DrawRandomBuilding();
                if (card == null) return false;
            }

            // Fly the card icon from building to dock bar
            WorkerCardFlyFX flyFX = WorkerCardFlyFX.Instance;
            if (flyFX != null)
            {
                flyFX.SpawnCardFly(worldPos, card, 0);
            }
            else
            {
                // Direct add (no fly animation)
                dock.AddCard(card, markAsNew: true);
            }

            return true;
        }

        // ─────────────────────────────────────────────────────────────────
        // Utility
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Find a child Image by name within a prefab hierarchy.
        /// </summary>
        private Image FindChildImage(GameObject root, string childName)
        {
            Transform child = root.transform.Find(childName);
            if (child != null)
                return child.GetComponent<Image>();

            // Deep search
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == childName)
                    return t.GetComponent<Image>();
            }
            return null;
        }

        // ─────────────────────────────────────────────────────────────────
        // Runtime Sprite Generation (circle + ring for radial fill)
        // ─────────────────────────────────────────────────────────────────

        private static void EnsureCircleSprites()
        {
            if (circleSprite != null && ringSprite != null) return;

            const int size = 128;
            float center = (size - 1) * 0.5f;
            float outerRadius = center;
            float innerRadius = center * 0.55f; // Ring thickness ≈ 45% of radius

            // --- Filled circle sprite ---
            if (circleSprite == null)
            {
                Texture2D circleTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                circleTex.filterMode = FilterMode.Bilinear;
                Color32[] circlePixels = new Color32[size * size];
                Color32 white = new Color32(255, 255, 255, 255);
                Color32 clear = new Color32(0, 0, 0, 0);

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x - center;
                        float dy = y - center;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);

                        // Anti-aliased edge (1px feather)
                        float alpha = Mathf.Clamp01(outerRadius - dist);
                        circlePixels[y * size + x] = alpha >= 1f ? white :
                            alpha > 0f ? new Color32(255, 255, 255, (byte)(alpha * 255)) : clear;
                    }
                }

                circleTex.SetPixels32(circlePixels);
                circleTex.Apply(false, true);
                circleSprite = Sprite.Create(circleTex,
                    new Rect(0, 0, size, size),
                    new Vector2(0.5f, 0.5f), 100f);
                circleSprite.name = "GeneratedCircle";
            }

            // --- Ring (donut) sprite ---
            if (ringSprite == null)
            {
                Texture2D ringTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                ringTex.filterMode = FilterMode.Bilinear;
                Color32[] ringPixels = new Color32[size * size];
                Color32 white = new Color32(255, 255, 255, 255);
                Color32 clear = new Color32(0, 0, 0, 0);

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x - center;
                        float dy = y - center;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);

                        // Outer edge AA
                        float outerAlpha = Mathf.Clamp01(outerRadius - dist);
                        // Inner edge AA (hollow center)
                        float innerAlpha = Mathf.Clamp01(dist - innerRadius);
                        float alpha = Mathf.Min(outerAlpha, innerAlpha);

                        ringPixels[y * size + x] = alpha >= 1f ? white :
                            alpha > 0f ? new Color32(255, 255, 255, (byte)(alpha * 255)) : clear;
                    }
                }

                ringTex.SetPixels32(ringPixels);
                ringTex.Apply(false, true);
                ringSprite = Sprite.Create(ringTex,
                    new Rect(0, 0, size, size),
                    new Vector2(0.5f, 0.5f), 100f);
                ringSprite.name = "GeneratedRing";
            }
        }
    }
}
