#pragma warning disable CS0414, CS0219, CS0618
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
    /// Worker output (ProductionOutputType.Worker) produces specifically the "Worker" unit from WorkerDatabase.
    ///
    /// Singleton — auto-created by MapGeneratorV2.EnsureManagers().
    /// </summary>
    public class BuildingProductionManager : MonoBehaviour
    {
        public static BuildingProductionManager Instance { get; private set; }

        // ─── Timer Visual Settings ──────────────────────────────────────
        [Header("Radial Timer")]
        [Tooltip("Optional prefab for the timer Canvas. If null, a default is created programmatically.")]
        public GameObject timerPrefab;

        // Procedural timer appearance — only used when timerPrefab is null
        [HideInInspector] public float timerWorldSize = 0.6f;
        [HideInInspector] public float timerHeight = 1.8f;
        [HideInInspector] public Color timerBgColor = new Color(0.15f, 0.15f, 0.15f, 0.7f);
        [HideInInspector] public Color timerFillColor = new Color(0.3f, 0.85f, 0.4f, 1f);
        [HideInInspector] public Color timerAlmostDoneColor = new Color(1f, 0.85f, 0.2f, 1f);

        // ─── Insert Bubble ───────────────────────────────────────────────
        [Header("Insert Bubble")]
        [Tooltip("Prefab for the insert bubble shown when a building is waiting for input or resources. Must have a POIBubble component (Bubble_Insert variant). If null, no insert bubble is shown.")]
        public GameObject insertBubblePrefab;

        // ─── Collect Bubble ─────────────────────────────────────────────
        [Header("Collect Bubble")]
        [Tooltip("Prefab for the collect bubble shown when production is ready. Must have a POIBubble component (Bubble_Collect variant). If null, falls back to a procedural world-space popup.")]
        public GameObject collectBubblePrefab;

        // ─── Need Bubble ─────────────────────────────────────────────────
        [Header("Need Bubble")]
        [Tooltip("Prefab shown over buildings that want the card currently being dragged. Must have a POIBubble component (Arrow_Need variant). If null, no need bubble is shown.")]
        public GameObject needBubblePrefab;

        private const float FALLBACK_BUBBLE_SCALE = 0.005f;

        /// <summary>Bubble scale — single source of truth is POIManager.BubbleWorldScale.</summary>
        private float BubbleScale => ClockworkCraft.POIManager.Instance != null
            ? ClockworkCraft.POIManager.Instance.BubbleWorldScale
            : FALLBACK_BUBBLE_SCALE;

        // Procedural collect popup appearance — only used when collectBubblePrefab is null
        [HideInInspector] public float popupHeight = 2.0f;
        [HideInInspector] public float popupWorldSize = 1.0f;
        [HideInInspector] public float bobAmplitude = 0.1f;
        [HideInInspector] public float bobSpeed = 2f;
        [HideInInspector] public Color popupGlowColor = new Color(1f, 0.9f, 0.5f, 0.6f);

        // ─── Database ───────────────────────────────────────────────────
        [Header("Database References")]
        [Tooltip("WorkerDatabase — used to look up the 'Worker' unit for Home production.")]
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
            public ProductionInputType inputType;
            public ProductionOutputType outputType;
            public float baseInterval;
            public float intervalBonus;
            public ResourceType producedResourceType;
            public string producedCardName;
            public int amount;

            // Timer state
            public float elapsedTime;
            public int collectCount;
            public bool isReady;
            public bool waitingForInput; // Input-triggered buildings idle until fed
            public ResourceType productionCostResourceType;
            public int          productionCostAmount;
            public bool         waitingForResources; // true when building needs to spend resources before starting timer
            public bool waitingForHoldFill;
            public int holdFillProgress;
            public int productionCostIncrement;
            public WorkerData pendingWorker;
            public UnitStats pendingCard; // For RandomBuilding output

            // Delay: timer starts hidden, reveals after first tick
            public bool timerRevealed;

            // Corruption: building is paused while its tile is corrupted
            public bool isPaused;

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

            // Designed insert bubble (shown when building awaits input/resources)
            public GameObject insertBubbleObj;
            public Image insertFillImage; // Fill bar on the insert bubble

            // Need bubble (shown while player is dragging a card this building wants)
            public GameObject needBubbleObj;

            public float EffectiveInterval => baseInterval + (intervalBonus * collectCount);
            public int EffectiveFillCost => productionCostAmount + (productionCostIncrement * collectCount);
        }

        private readonly List<ProductionEntry> entries = new List<ProductionEntry>();
        private Camera mainCamera;

        // Bubble pools — avoid Instantiate/Destroy per cycle
        private readonly List<POIBubble> insertPool = new List<POIBubble>();
        private readonly List<POIBubble> collectPool = new List<POIBubble>();
        private readonly List<POIBubble> needPool = new List<POIBubble>();

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

            // Auto-find WorkerDatabase if not assigned in Inspector or by MapGeneratorV2
            if (workerDatabase == null)
                workerDatabase = Resources.FindObjectsOfTypeAll<WorkerDatabase>().Length > 0
                    ? Resources.FindObjectsOfTypeAll<WorkerDatabase>()[0]
                    : null;
        }

        void Start()
        {
            mainCamera = Camera.main;

            if (IntervalTimer.Instance != null)
                IntervalTimer.Instance.OnBar += OnIntervalTick;
        }

        /// <summary>True if the designed bubble prefab system is active (replaces legacy procedural popups).</summary>
        public bool HasBubblePrefab => insertBubblePrefab != null || collectBubblePrefab != null;

        /// <summary>Get an inactive bubble from the pool, or instantiate overflow.</summary>
        private POIBubble GetFromPool(List<POIBubble> pool, GameObject prefab)
        {
            foreach (var b in pool)
                if (!b.IsActive && !b.gameObject.activeSelf)
                    return b;

            // Overflow — create a new one
            if (prefab == null) return null;
            var obj = Instantiate(prefab, transform);
            var bubble = obj.GetComponent<POIBubble>();
            if (bubble == null) bubble = obj.AddComponent<POIBubble>();

            // WorldSpace canvas needs a camera for GraphicRaycaster (UI tap detection)
            var canvas = obj.GetComponent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera == null)
                canvas.worldCamera = Camera.main;

            // Depth-based sorting for prefab-based bubbles
            if (canvas != null && obj.GetComponent<BubbleDepthSorter>() == null)
            {
                var depthSorter = obj.AddComponent<BubbleDepthSorter>();
                depthSorter.Initialize(50);
            }

            obj.SetActive(false);
            pool.Add(bubble);
            return bubble;
        }

        /// <summary>Set the bubble prefabs at runtime (called by MapGeneratorV2 since BPM is AddComponent'd).
        /// Scale is read from POIManager.BubbleWorldScale — single source of truth.</summary>
        public void SetBubblePrefabs(GameObject insertPrefab, GameObject collectPrefab)
        {
            if (insertBubblePrefab == null) insertBubblePrefab = insertPrefab;
            if (collectBubblePrefab == null) collectBubblePrefab = collectPrefab;
            // Need bubble uses the same WorldCanvas_Popups prefab — just activates Arrow_Need child
            if (needBubblePrefab == null) needBubblePrefab = insertPrefab;
            Debug.Log($"[BuildingProduction] Bubble prefabs set — insert: {(insertBubblePrefab != null ? insertBubblePrefab.name : "NULL")}, collect: {(collectBubblePrefab != null ? collectBubblePrefab.name : "NULL")}, need: {(needBubblePrefab != null ? needBubblePrefab.name : "NULL")}");
        }

        /// <summary>Legacy single-prefab setter — sets both insert and collect to the same prefab if not already assigned.</summary>
        public void SetBubblePrefab(GameObject prefab)
            => SetBubblePrefabs(prefab, prefab);

        void OnDestroy()
        {
            if (IntervalTimer.Instance != null)
                IntervalTimer.Instance.OnBar -= OnIntervalTick;
        }

        void Update()
        {
            ClickConsumedThisFrame = false;
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
                inputType = stats.productionInputType,
                outputType = stats.productionOutputType,
                baseInterval = stats.productionInterval,
                intervalBonus = stats.productionIntervalBonus,
                producedResourceType = stats.producedResourceType,
                producedCardName = stats.producedCardName,
                amount = stats.productionAmount,
                elapsedTime = 0f,
                collectCount = 0,
                isReady = false,
                waitingForInput = (stats.productionInputType != ProductionInputType.None && stats.productionInputType != ProductionInputType.HoldToFill),
                productionCostResourceType = stats.productionCostResourceType,
                productionCostAmount       = stats.productionCostAmount,
                waitingForResources        = (stats.productionCostAmount > 0 && stats.productionInputType != ProductionInputType.HoldToFill),
                waitingForHoldFill         = (stats.productionInputType == ProductionInputType.HoldToFill),
                holdFillProgress           = 0,
                productionCostIncrement = stats.productionCostIncrement,
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

            // Show insert bubble for HoldToFill buildings only — card-input buildings
            // (Any/Worker/Fighter) show Arrow_Need dynamically during drag instead
            bool isCardInput = entry.inputType == ProductionInputType.Any ||
                               entry.inputType == ProductionInputType.Worker ||
                               entry.inputType == ProductionInputType.Fighter;
            if (!isCardInput && (entry.waitingForInput || entry.waitingForResources || entry.waitingForHoldFill))
                StartCoroutine(DelayedSpawnInsertBubble(entry, 1f));


            if (stats.productionInputType == ProductionInputType.HoldToFill)
                OnHoldFillStateChanged?.Invoke(buildingObj, true);

            if (verboseLogging)
                Debug.Log($"[BuildingProduction] Registered '{buildingObj.name}' — produces {stats.productionOutputType} every {stats.productionInterval}s (bonus +{stats.productionIntervalBonus}s per collect, topHeight={entry.objectTopHeight:F1})");

            // DIAGNOSTIC: log resource-cost fields so we can verify asset loaded correctly
            if (entry.productionCostAmount > 0 || entry.productionCostResourceType != ResourceType.None)
                Debug.Log($"[BuildingProduction] COST CHECK on register: '{buildingObj.name}' costType={(int)entry.productionCostResourceType}({entry.productionCostResourceType}) costAmount={entry.productionCostAmount} waitingForResources={entry.waitingForResources}");
        }

        /// <summary>
        /// Pause production for a corrupted building. Timer is preserved in place (not reset).
        /// Idempotent — safe to call on an already-paused building.
        /// </summary>
        public void PauseBuilding(GameObject buildingObj)
        {
            foreach (var entry in entries)
            {
                if (entry.buildingObj == buildingObj)
                {
                    entry.isPaused = true;
                    HoldToFillHandler.Instance?.InterruptIfActive(entry.buildingObj);
                    if (entry.timerCanvasObj != null)
                        entry.timerCanvasObj.SetActive(false);
                    return;
                }
            }
        }

        /// <summary>
        /// Resume production for a building cleared of corruption. Timer continues from where it paused.
        /// Idempotent — safe to call on a building that was not paused.
        /// </summary>
        public void ResumeBuilding(GameObject buildingObj)
        {
            foreach (var entry in entries)
            {
                if (entry.buildingObj == buildingObj)
                {
                    entry.isPaused = false;
                    if (entry.timerCanvasObj != null && entry.timerRevealed && !entry.isReady)
                        entry.timerCanvasObj.SetActive(true);
                    return;
                }
            }
        }

        /// <summary>
        /// Check if a placed building at the given grid cell accepts the specified input type.
        /// Used by DragDropHandler to determine valid drop-on-building targets.
        /// </summary>
        public bool IsInputBuildingAt(int gridX, int gridY, ProductionInputType requiredInput)
        {
            foreach (var entry in entries)
            {
                if (entry.buildingObj == null) continue;
                // Building with Any input accepts all card types; otherwise strict match
                if (entry.inputType != requiredInput && entry.inputType != ProductionInputType.Any) continue;

                var furniture = entry.buildingObj.GetComponent<FurnitureObject>();
                if (furniture != null && furniture.GridX == gridX && furniture.GridY == gridY)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Feed a unit into an input-triggered building, consuming the card and starting the timer.
        /// Returns true if the building accepted the input.
        /// </summary>
        public bool FeedBuilding(int gridX, int gridY, ProductionInputType inputType)
        {
            foreach (var entry in entries)
            {
                if (entry.buildingObj == null) continue;
                // Building with Any input accepts all card types; otherwise strict match
                if (entry.inputType != inputType && entry.inputType != ProductionInputType.Any) continue;
                if (!entry.waitingForInput) continue; // Already processing

                var furniture = entry.buildingObj.GetComponent<FurnitureObject>();
                if (furniture != null && furniture.GridX == gridX && furniture.GridY == gridY)
                {
                    // Start the production timer
                    entry.waitingForInput = false;
                    entry.elapsedTime = 0f;
                    entry.timerRevealed = false;
                    DismissInsertBubble(entry);

                    // Show timer
                    if (entry.timerCanvasObj != null)
                    {
                        entry.timerCanvasObj.SetActive(true);
                        StartCoroutine(TimerAppearAnimation(entry.timerCanvasObj));
                        entry.timerRevealed = true;
                    }

                    // Play bounce animation on the building (acknowledgement of worker/fighter acceptance)
                    Animator buildingAnimator = entry.buildingObj.GetComponentInChildren<Animator>();
                    if (buildingAnimator != null)
                        buildingAnimator.SetTrigger("idle_bounce");

                    if (verboseLogging)
                        Debug.Log($"[BuildingProduction] Fed {inputType} into '{entry.buildingObj.name}' — timer started ({entry.EffectiveInterval}s)");

                    return true;
                }
            }
            return false;
        }

        public void UnregisterBuilding(GameObject buildingObj)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i].buildingObj == buildingObj)
                {
                    if (entries[i].inputType == ProductionInputType.HoldToFill)
                        OnHoldFillStateChanged?.Invoke(buildingObj, false);
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
            DismissCollectBubble(entry);
            if (entry.timerCanvasObj != null) Destroy(entry.timerCanvasObj);
            DismissInsertBubble(entry);
            DismissNeedBubble(entry);
        }

        private void DismissCollectBubble(ProductionEntry entry)
        {
            if (entry.popupCanvasObj == null) return;
            var bubble = entry.popupCanvasObj.GetComponent<POIBubble>();
            if (bubble != null && bubble.IsActive)
                bubble.Dismiss();
            entry.popupCanvasObj = null;
            entry.popupIconImage = null;
        }

        // ─────────────────────────────────────────────────────────────────
        // Designed Insert Bubble (Bubble_Insert variant)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Show a Bubble_Insert above a building that is waiting for input or resources.
        /// Only used when insertBubblePrefab is assigned.
        /// </summary>
        private System.Collections.IEnumerator DelayedSpawnInsertBubble(ProductionEntry entry, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (entry != null && entry.buildingObj != null && entry.insertBubbleObj == null)
                SpawnInsertBubble(entry);
        }

        private void SpawnInsertBubble(ProductionEntry entry)
        {
            if (insertBubblePrefab == null) return;
            if (entry.buildingObj == null) return;
            if (entry.insertBubbleObj != null) return; // Already showing

            Vector3 pos = entry.buildingObj.transform.position + Vector3.up * GetPopupY(entry);

            // Dismiss any POI bubble at this building's grid position to prevent stacking
            if (ClockworkGrid.GridManager.Instance != null && entry.buildingObj != null)
            {
                if (ClockworkGrid.GridManager.Instance.WorldToGridPosition(entry.buildingObj.transform.position, out int gx, out int gy))
                    ClockworkCraft.POIManager.Instance?.DismissBubble(new Vector2Int(gx, gy));
            }

            var bubble = GetFromPool(insertPool, insertBubblePrefab);
            if (bubble == null) return;
            bubble.gameObject.name = $"InsertBubble_{entry.buildingObj.name}";
            bubble.SetTargetScale(Vector3.one * BubbleScale);
            bubble.SetAnimParams(0.25f, bobAmplitude, bobSpeed * 0.5f, 0.3f);
            bubble.Setup(BubbleType.Bubble_Insert, "", pos);

            // Set the input icon (what the building needs)
            Sprite inputIcon = ResolveInputIcon(entry);
            var iconImg = bubble.GetIconImage();
            if (iconImg != null && inputIcon != null)
            {
                iconImg.sprite = inputIcon;
                iconImg.enabled = true;
            }

            else
            {
                Debug.LogWarning($"[BuildingProduction] Insert bubble icon issue — iconImg: {(iconImg != null ? iconImg.name : "NULL")}, inputIcon: {(inputIcon != null ? inputIcon.name : "NULL")}, costType: {entry.productionCostResourceType}, inputType: {entry.inputType}");
            }

            // Cache the fill bar Image for updating in IncrementHoldFill
            entry.insertFillImage = bubble.GetFillImage();
            Debug.Log($"[BuildingProduction] Insert bubble for {entry.buildingObj.name}: fillImage={entry.insertFillImage != null}, activeChild={bubble.ActiveChild?.name ?? "NULL"}");

            // Tint the fill bar to match the requested resource color
            if (entry.insertFillImage != null && entry.productionCostResourceType != ResourceType.None)
                entry.insertFillImage.color = GetLightResourceColor(entry.productionCostResourceType);

            // Initialize fill bar to current progress
            UpdateInsertFillBar(entry);

            // Tap on the bubble = insert 1 resource; hold = continuous fill
            bubble.OnTapped += () => TapInsertOne(entry);
            bubble.OnHoldStarted += () =>
            {
                if (HoldToFillHandler.Instance != null)
                    HoldToFillHandler.Instance.StartHoldOnBuilding(entry.buildingObj);
            };
            bubble.OnHoldEnded += () =>
            {
                if (HoldToFillHandler.Instance != null)
                    HoldToFillHandler.Instance.StopHold();
            };

            entry.insertBubbleObj = bubble.gameObject;
        }

        /// <summary>Quick tap on insert bubble = insert 1 unit of the required resource.
        /// Resource is NOT deducted immediately — it flies from the bar to the bubble,
        /// and both the deduction and fill increment happen when it arrives.</summary>
        private void TapInsertOne(ProductionEntry entry)
        {
            if (entry == null || entry.buildingObj == null) return;
            if (!entry.waitingForHoldFill) return;
            if (entry.productionCostResourceType == ResourceType.None) return;
            if (ResourceManager.Instance == null) return;
            if (ResourceManager.Instance.GetResource(entry.productionCostResourceType) <= 0) return;

            // Immediate punch scale on the bubble for tactile feedback
            if (entry.insertBubbleObj != null)
                StartCoroutine(PunchScale(entry.insertBubbleObj.transform, 0.15f, 1.15f));

            // Target is the bubble, not the building
            GameObject target = entry.insertBubbleObj != null ? entry.insertBubbleObj : entry.buildingObj;

            // VFX: resource stream flies from bar to bubble — deduct + fill on arrival
            if (HoldToFillHandler.Instance != null)
            {
                HoldToFillHandler.Instance.SpawnResourceStream(target, entry.productionCostResourceType, () =>
                {
                    if (entry == null || entry.buildingObj == null || !entry.waitingForHoldFill) return;
                    if (ResourceManager.Instance == null) return;
                    if (ResourceManager.Instance.GetResource(entry.productionCostResourceType) <= 0) return;

                    ResourceManager.Instance.AddResource(entry.productionCostResourceType, -1);
                    IncrementHoldFill(entry.buildingObj);

                    // Punch the bubble on arrival
                    if (entry.insertBubbleObj != null)
                        StartCoroutine(PunchScale(entry.insertBubbleObj.transform, 0.12f, 1.1f));
                });
            }

            // SFX
            if (GameSFXManager.Instance != null)
                GameSFXManager.Instance.PlayClockTick();
        }

        private System.Collections.IEnumerator PunchScale(Transform t, float duration, float punchSize)
        {
            if (t == null) yield break;
            Vector3 original = t.localScale;
            Vector3 punched = original * punchSize;
            float elapsed = 0f;
            float half = duration * 0.4f;

            // Scale up
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                t.localScale = Vector3.Lerp(original, punched, elapsed / half);
                yield return null;
            }

            // Scale back
            elapsed = 0f;
            float rest = duration - half;
            while (elapsed < rest)
            {
                elapsed += Time.deltaTime;
                t.localScale = Vector3.Lerp(punched, original, elapsed / rest);
                yield return null;
            }
            if (t != null) t.localScale = original;
        }



        /// <summary>Update the fill bar on the insert bubble to reflect current holdFillProgress.</summary>
        private void UpdateInsertFillBar(ProductionEntry entry)
        {
            if (entry.insertFillImage == null) return;
            int cost = entry.EffectiveFillCost;
            entry.insertFillImage.fillAmount = cost > 0 ? (float)entry.holdFillProgress / cost : 0f;
        }

        /// <summary>Dismiss the insert bubble — returns to pool after fade-out.</summary>
        private void DismissInsertBubble(ProductionEntry entry)
        {
            if (entry.insertBubbleObj == null) return;

            var bubble = entry.insertBubbleObj.GetComponent<POIBubble>();
            if (bubble != null && bubble.IsActive)
                bubble.Dismiss(); // Animated fade-out → deactivates → back in pool

            entry.insertBubbleObj = null;
            entry.insertFillImage = null;
        }

        // ─────────────────────────────────────────────────────────────────
        // Need Bubble (Arrow_Need — shown during drag when building wants the dragged card)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Show Arrow_Need on all buildings that are currently waiting and accept the given input type.
        /// Called by DragDropHandler when a drag begins.</summary>
        public void ShowNeedBubbles(ProductionInputType inputType)
        {
            if (needBubblePrefab == null)
            {
                Debug.LogWarning("[BuildingProduction] ShowNeedBubbles — needBubblePrefab is NULL!");
                return;
            }
            Debug.Log($"[BuildingProduction] ShowNeedBubbles called with inputType={inputType}, entries={entries.Count}");
            foreach (var entry in entries)
            {
                bool wantsInput = (entry.waitingForInput && (entry.inputType == inputType || entry.inputType == ProductionInputType.Any))
                               || (entry.waitingForResources && inputType == ProductionInputType.Any);
                Debug.Log($"[BuildingProduction]   {entry.buildingObj?.name}: waitingForInput={entry.waitingForInput}, inputType={entry.inputType}, wantsInput={wantsInput}");
                if (wantsInput)
                    SpawnNeedBubble(entry);
            }
        }

        /// <summary>Dismiss all active need bubbles. Called by DragDropHandler when a drag ends.</summary>
        public void HideAllNeedBubbles()
        {
            foreach (var entry in entries)
                DismissNeedBubble(entry);
        }

        private void SpawnNeedBubble(ProductionEntry entry)
        {
            if (needBubblePrefab == null) return;
            if (entry.buildingObj == null) return;
            if (entry.needBubbleObj != null) return; // Already showing

            Vector3 pos = entry.buildingObj.transform.position + Vector3.up * GetPopupY(entry);

            var bubble = GetFromPool(needPool, needBubblePrefab);
            if (bubble == null) return;
            bubble.gameObject.name = $"NeedBubble_{entry.buildingObj.name}";
            bubble.SetTargetScale(Vector3.one * BubbleScale);
            bubble.SetAnimParams(0.2f, bobAmplitude * 1.2f, bobSpeed * 1.2f, 0.25f);
            bubble.Setup(BubbleType.Arrow_Need, "", pos);

            entry.needBubbleObj = bubble.gameObject;
        }

        private void DismissNeedBubble(ProductionEntry entry)
        {
            if (entry.needBubbleObj == null) return;

            var bubble = entry.needBubbleObj.GetComponent<POIBubble>();
            if (bubble != null && bubble.IsActive)
                bubble.Dismiss();

            entry.needBubbleObj = null;
        }

        /// <summary>
        /// <summary>Returns a lighter pastel version of the resource color for the fill bar.</summary>
        private static Color GetLightResourceColor(ResourceType type)
        {
            Color baseColor;
            switch (type)
            {
                case ResourceType.Wood:   baseColor = new Color(0.72f, 0.48f, 0.25f); break;
                case ResourceType.Stone:  baseColor = new Color(0.70f, 0.70f, 0.72f); break;
                case ResourceType.Gold:   baseColor = new Color(1.00f, 0.84f, 0.25f); break;
                case ResourceType.Water:  baseColor = new Color(0.30f, 0.65f, 0.95f); break;
                case ResourceType.Food:   baseColor = new Color(0.95f, 0.65f, 0.20f); break;
                case ResourceType.Meat:   baseColor = new Color(0.85f, 0.35f, 0.30f); break;
                case ResourceType.Meat2:  baseColor = new Color(0.80f, 0.30f, 0.25f); break;
                case ResourceType.Meat3:  baseColor = new Color(0.75f, 0.28f, 0.22f); break;
                case ResourceType.Copper: baseColor = new Color(0.85f, 0.55f, 0.30f); break;
                case ResourceType.Ore:    baseColor = new Color(0.65f, 0.65f, 0.70f); break;
                case ResourceType.Gem:    baseColor = new Color(0.40f, 0.85f, 1.00f); break;
                case ResourceType.Leaf:   baseColor = new Color(0.35f, 0.75f, 0.30f); break;
                case ResourceType.Grass:  baseColor = new Color(0.40f, 0.80f, 0.35f); break;
                case ResourceType.Petal:  baseColor = new Color(1.00f, 0.55f, 0.70f); break;
                case ResourceType.Fish:   baseColor = new Color(0.45f, 0.70f, 0.90f); break;
                case ResourceType.Clay:   baseColor = new Color(0.80f, 0.55f, 0.35f); break;
                default:                  baseColor = new Color(0.85f, 0.85f, 0.90f); break;
            }
            // Lighten: lerp toward white by 40%
            return Color.Lerp(baseColor, Color.white, 0.4f);
        }

        /// Resolve the icon sprite for what a building needs as input.
        /// Resource inputs use CurrencyDatabase icon, card inputs use CardPool/UnitStats icon.
        /// </summary>
        private Sprite ResolveInputIcon(ProductionEntry entry)
        {
            // Resource cost (e.g. Kitchen needs Meat)
            if (entry.productionCostResourceType != ResourceType.None)
                return ResourceDisplayUI.GetIconForResource(entry.productionCostResourceType);

            // Card input (e.g. Barracks needs Worker) — use the input type to find the icon
            switch (entry.inputType)
            {
                case ProductionInputType.Worker:
                    if (workerDatabase != null)
                    {
                        var worker = workerDatabase.GetByName("Worker");
                        if (worker != null) return worker.icon;
                    }
                    break;
                case ProductionInputType.Any:
                    // Generic card icon — try to get from CardPool or return null
                    break;
            }

            return null;
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
            return baseHeight;
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

            // Depth-based sorting: closer bubbles render in front
            var depthSorter = root.AddComponent<BubbleDepthSorter>();
            depthSorter.Initialize(50);

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
                    // buildingObj is null (Unity destroyed) — we cannot pass it to OnHoldFillStateChanged
                    // because DestroyFillBar(null) won't find the dictionary key. The fill bar canvas
                    // for this building is cleaned up by HoldToFillHandler.UpdateFillBars() stale-key
                    // removal (which runs every LateUpdate and handles destroyed-but-non-null Unity keys
                    // via Unity's overloaded == null check). No event needed here.
                    HoldToFillHandler.Instance?.InterruptIfActive(entry.buildingObj);
                    DestroyEntryVisuals(entry);
                    entries.RemoveAt(i);
                    continue;
                }

                if (entry.isReady) continue;

                // Corruption: skip this building while its tile is corrupted
                if (entry.isPaused) continue;

                // Input-triggered buildings wait until fed before starting their timer
                if (entry.waitingForInput) continue;

                // HoldToFill buildings wait until fully filled via IncrementHoldFill
                if (entry.waitingForHoldFill) continue;

                // Resource-cost buildings wait until they can afford the activation cost
                if (entry.waitingForResources && entry.inputType != ProductionInputType.HoldToFill)
                {
                    var rm = ResourceManager.Instance;
                    int have = rm != null ? rm.GetResource(entry.productionCostResourceType) : -1;
                    bool spent = rm != null && rm.SpendResources(
                        new Dictionary<ResourceType, int> { { entry.productionCostResourceType, entry.productionCostAmount } });
                    Debug.Log($"[BuildingProduction] RESOURCE GATE '{entry.buildingObj?.name}': need {entry.productionCostAmount}x {entry.productionCostResourceType}(int={(int)entry.productionCostResourceType}), have={have}, rm={(rm == null ? "NULL" : "ok")}, spent={spent}");
                    if (spent)
                    {
                        entry.waitingForResources = false;
                        DismissInsertBubble(entry);
                    }
                    else
                        continue; // not enough resources — skip tick
                }

                // Reveal timer after first tick (delayed so player can appreciate the object)
                if (!entry.timerRevealed && entry.timerCanvasObj != null)
                {
                    entry.timerRevealed = true;
                    entry.timerCanvasObj.SetActive(true);
                    StartCoroutine(TimerAppearAnimation(entry.timerCanvasObj));
                }

                entry.elapsedTime += tickDuration;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                float effectiveInterval = DevCheatMenu.InstantProduction ? 1f : entry.EffectiveInterval;
#else
                float effectiveInterval = entry.EffectiveInterval;
#endif
                if (entry.elapsedTime >= effectiveInterval)
                {
                    entry.elapsedTime = 0f;
                    entry.isReady = true;

                    if (entry.outputType == ProductionOutputType.Worker)
                        entry.pendingWorker = workerDatabase != null ? workerDatabase.GetByName("Worker") : null;
                    else if (entry.outputType == ProductionOutputType.RandomBuilding)
                        entry.pendingCard = DrawRandomBuilding();
                    else if (entry.outputType == ProductionOutputType.Fighter)
                        entry.pendingCard = FindFighterCard();
                    else if (entry.outputType == ProductionOutputType.Meal)
                        entry.pendingCard = FindMealCard();
                    else if (!string.IsNullOrEmpty(entry.producedCardName))
                        entry.pendingCard = CardPool.Instance?.FindByName(entry.producedCardName);
                    else if (IsTierBuildingOutput(entry.outputType))
                        entry.pendingCard = DrawRandomBuildingByTier(GetTierFromOutput(entry.outputType));
                    else if (IsTierUnitOutput(entry.outputType))
                        entry.pendingCard = DrawRandomUnitByTier(GetTierFromOutput(entry.outputType));

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
                // Exclude trained/special workers (Fighter) — they are produced by specific buildings only
                if (w.prefab != null && w.type != WorkerType.Fighter) validWorkers.Add(w);
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
            else if (entry.pendingCard != null)
                rewardIcon = entry.pendingCard.iconSprite;

            // Dismiss any lingering insert bubble when the collect popup appears
            DismissInsertBubble(entry);

            Vector3 popupPos = entry.buildingObj.transform.position + Vector3.up * GetPopupY(entry);

            // ── Designed Bubble path (Bubble_Collect) ──
            if (collectBubblePrefab != null)
            {
                var bubble = GetFromPool(collectPool, collectBubblePrefab);
                if (bubble == null) return;
                bubble.gameObject.name = $"ProductionPopup_{entry.buildingObj.name}";
                bubble.SetTargetScale(Vector3.one * BubbleScale);
                bubble.SetAnimParams(0.25f, bobAmplitude, bobSpeed * 0.5f, 0.3f);
                bubble.Setup(BubbleType.Bubble_Collect, "", popupPos);

                // Set the reward icon on the variant's "Icon" Image child
                var iconImg = bubble.GetIconImage();
                if (iconImg != null && rewardIcon != null)
                {
                    iconImg.sprite = rewardIcon;
                    iconImg.enabled = true;
                }
                entry.popupIconImage = iconImg;
                entry.popupCanvasObj = bubble.gameObject;

                // Use UI tap via POIBubble.OnTapped instead of physics collider
                int capturedIndex = entries.IndexOf(entry);
                bubble.OnTapped += () =>
                {
                    if (capturedIndex < entries.Count)
                        CollectReward(entries[capturedIndex]);
                };
            }
            // ── Legacy path (procedural popup) ──
            else
            {
                GameObject canvasObj = CreateDefaultPopupCanvas(entry);

                canvasObj.transform.position = popupPos;

                // Set the icon sprite
                if (entry.popupIconImage != null && rewardIcon != null)
                {
                    entry.popupIconImage.sprite = rewardIcon;
                    entry.popupIconImage.enabled = true;
                }

                // Collider for tap detection — on a separate unscaled child
                GameObject colliderHolder = new GameObject("TapCollider");
                colliderHolder.transform.SetParent(canvasObj.transform, false);
                float canvasScale = canvasObj.transform.localScale.x;
                if (canvasScale > 0f)
                    colliderHolder.transform.localScale = Vector3.one / canvasScale;

                SphereCollider col = colliderHolder.AddComponent<SphereCollider>();
                col.radius = 0.6f;
                col.isTrigger = false;

                entry.popupCanvasObj = canvasObj;
                entry.popupCollider = col;
            }

            // SFX
            if (GameSFXManager.Instance != null)
                GameSFXManager.Instance.PlayPopupAppear();

            // Designed bubbles have their own pop-in animation via POIBubble.Update —
            // only run the legacy coroutine for procedural popups.
            if (collectBubblePrefab == null)
                StartCoroutine(PopupSpawnAnimation(entry.popupCanvasObj));

            if (verboseLogging)
            {
                string rewardName = "";
                if (entry.outputType == ProductionOutputType.Worker && entry.pendingWorker != null)
                    rewardName = $" (worker: {entry.pendingWorker.GetCleanName()})";
                else if (entry.pendingCard != null)
                    rewardName = $" ({entry.outputType}: {entry.pendingCard.unitName})";

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

            // Depth-based sorting: closer bubbles render in front
            var depthSorter = root.AddComponent<BubbleDepthSorter>();
            depthSorter.Initialize(50);

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
                    ClickConsumedThisFrame = true;
                    return;
                }

                // Check 2: tapped the building itself (or any child mesh/collider)
                if (entry.buildingObj != null &&
                    (hitObj == entry.buildingObj ||
                     hitObj.transform.IsChildOf(entry.buildingObj.transform)))
                {
                    CollectReward(entry);
                    ClickConsumedThisFrame = true;
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
                                ClickConsumedThisFrame = true;
                                return;
                            }
                        }
                    }
                }
            }

            // Check 4: tapped a building waiting for hold-fill — insert 1 resource
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!entry.waitingForHoldFill) continue;

                if (entry.buildingObj != null &&
                    (hitObj == entry.buildingObj ||
                     hitObj.transform.IsChildOf(entry.buildingObj.transform)))
                {
                    TapInsertOne(entry);
                    ClickConsumedThisFrame = true;
                    return;
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

                case ProductionOutputType.Fighter:
                    collected = CollectRandomBuildingReward(entry, buildingWorldPos);
                    break;

                case ProductionOutputType.Meal:
                    collected = CollectMealReward(entry, buildingWorldPos);
                    break;

                default:
                    // All other output types produce a card (Scrap, Lizard, TreeSeed, tier draws, etc.)
                    if (entry.pendingCard != null)
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

            DismissCollectBubble(entry);
            entry.popupCollider = null;
            entry.isReady = false;
            entry.elapsedTime = 0f;
            entry.pendingWorker = null;
            entry.pendingCard = null;
            entry.timerRevealed = false; // Re-delay the timer by 1 tick

            // Input-triggered buildings return to waiting state after collection
            if (entry.inputType != ProductionInputType.None && entry.inputType != ProductionInputType.HoldToFill)
                entry.waitingForInput = true;

            // Resource-cost buildings return to waiting state after collection
            if (entry.productionCostAmount > 0 && entry.inputType != ProductionInputType.HoldToFill)
                entry.waitingForResources = true;

            // HoldToFill buildings return to waiting state after collection
            if (entry.inputType == ProductionInputType.HoldToFill)
            {
                entry.waitingForHoldFill = true;
                entry.holdFillProgress = 0;
                OnHoldFillStateChanged?.Invoke(entry.buildingObj, true);
            }

            // Show insert bubble again for non-card-input buildings returning to waiting state
            bool isCardInput2 = entry.inputType == ProductionInputType.Any ||
                                entry.inputType == ProductionInputType.Worker ||
                                entry.inputType == ProductionInputType.Fighter;
            if (!isCardInput2 && (entry.waitingForInput || entry.waitingForResources || entry.waitingForHoldFill))
                StartCoroutine(DelayedSpawnInsertBubble(entry, 1f));

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

            // Use IsHandFull which includes reservedSlots (in-flight cards count toward capacity)
            if (dock.IsHandFull)
            {
                Debug.Log("[BuildingProduction] Hand full (including in-flight) — pop-up stays until there's room");
                return false;
            }

            WorkerData wd = entry.pendingWorker;
            if (wd == null)
            {
                wd = workerDatabase != null ? workerDatabase.GetByName("Worker") : null;
                if (wd == null) return false;
            }

            WorkerCardFlyFX flyFX = WorkerCardFlyFX.Instance;
            if (flyFX != null)
            {
                bool anyReserved = false;
                for (int i = 0; i < entry.amount; i++)
                {
                    if (flyFX.SpawnWorkerFly(worldPos, wd, i))
                        anyReserved = true;
                    else
                        break; // Hand full mid-batch — stop trying
                }
                // Only consume the production entry if at least one card was reserved
                return anyReserved;
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
        /// Draw a random card from the CardPool pool.
        /// Same as what the draw button does.
        /// </summary>
        /// <summary>
        /// Draw a random card from the entire pool (no tier/source filter).
        /// Used by ProductionOutputType.RandomBuilding.
        /// </summary>
        private UnitStats DrawRandomBuilding()
        {
            if (CardPool.Instance == null)
            {
                Debug.LogWarning("[BuildingProduction] No CardPool — can't draw random card");
                return null;
            }

            UnitStats drawn = CardPool.Instance.DrawRandomUnit();
            if (drawn != null && verboseLogging)
                Debug.Log($"[BuildingProduction] Drew random card: {drawn.unitName} ({drawn.rarity})");
            return drawn;
        }

        /// <summary>
        /// Draw a random building card filtered by tier (0-3).
        /// Filters the pool to only buildings tagged with the given tier.
        /// </summary>
        private UnitStats DrawRandomBuildingByTier(int tier)
        {
            if (CardPool.Instance == null)
            {
                Debug.LogWarning("[BuildingProduction] No CardPool — can't draw tier building");
                return null;
            }

            UnitStats drawn = CardPool.Instance.DrawRandomBuildingByTier(tier);
            if (drawn != null && verboseLogging)
                Debug.Log($"[BuildingProduction] Drew tier {tier} building: {drawn.unitName}");
            return drawn;
        }

        /// <summary>
        /// Draw a random unit/worker card filtered by tier (0-3).
        /// Filters the pool to only units/workers tagged with the given tier.
        /// </summary>
        private UnitStats DrawRandomUnitByTier(int tier)
        {
            if (CardPool.Instance == null)
            {
                Debug.LogWarning("[BuildingProduction] No CardPool — can't draw tier unit");
                return null;
            }

            UnitStats drawn = CardPool.Instance.DrawRandomUnitByTier(tier);
            if (drawn != null && verboseLogging)
                Debug.Log($"[BuildingProduction] Drew tier {tier} unit: {drawn.unitName}");
            return drawn;
        }

        // ─── Tier Output Helpers ──────────────────────────────────────────

        private static bool IsTierBuildingOutput(ProductionOutputType t)
        {
            return t >= ProductionOutputType.Tier0Building && t <= ProductionOutputType.Tier3Building;
        }

        private static bool IsTierUnitOutput(ProductionOutputType t)
        {
            return t >= ProductionOutputType.Tier0Unit && t <= ProductionOutputType.Tier3Unit;
        }

        private static int GetTierFromOutput(ProductionOutputType t)
        {
            if (IsTierBuildingOutput(t)) return t - ProductionOutputType.Tier0Building;
            if (IsTierUnitOutput(t)) return t - ProductionOutputType.Tier0Unit;
            return 0;
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

            // Use IsHandFull which includes reservedSlots (in-flight cards count toward capacity)
            if (dock.IsHandFull)
            {
                Debug.Log("[BuildingProduction] Hand full (including in-flight) — pop-up stays until there's room");
                return false;
            }

            UnitStats card = entry.pendingCard;
            if (card == null)
            {
                // Fallback: re-draw now if pending card was lost
                if (entry.outputType == ProductionOutputType.RandomBuilding)
                    card = DrawRandomBuilding();
                else if (IsTierBuildingOutput(entry.outputType))
                    card = DrawRandomBuildingByTier(GetTierFromOutput(entry.outputType));
                else if (IsTierUnitOutput(entry.outputType))
                    card = DrawRandomUnitByTier(GetTierFromOutput(entry.outputType));
                else
                    card = DrawRandomBuilding(); // Last resort
                if (card == null) return false;
            }

            // Fly the card icon from building to dock bar
            WorkerCardFlyFX flyFX = WorkerCardFlyFX.Instance;
            if (flyFX != null)
            {
                // Only consume production if the slot was actually reserved
                if (!flyFX.SpawnCardFly(worldPos, card, 0))
                    return false;
            }
            else
            {
                // Direct add (no fly animation)
                dock.AddCard(card, markAsNew: true);
            }

            return true;
        }

        /// <summary>
        /// Find the Feast card (meal card) from the registered card pool.
        /// Kitchen buildings produce Feast cards that workers can pick up.
        /// </summary>
        private UnitStats FindMealCard()
        {
            if (CardPool.Instance == null) return null;
            UnitStats feast = CardPool.Instance.FindByName("Feast");
            if (feast == null)
                Debug.LogWarning("[BuildingProduction] 'Feast' card not found in CardPool pool");
            return feast;
        }

        /// <summary>
        /// Find the Fighter card from the registered card pool.
        /// Barracks buildings produce Fighter cards.
        /// </summary>
        private UnitStats FindFighterCard()
        {
            if (CardPool.Instance == null) return null;
            UnitStats fighter = CardPool.Instance.FindByName("Fighter");
            if (fighter == null)
                Debug.LogWarning("[BuildingProduction] 'Fighter' card not found in CardPool pool");
            return fighter;
        }

        /// <summary>
        /// Collect a Meal card reward: fly the card from the building to the dock bar.
        /// Returns false if hand is full.
        /// </summary>
        private bool CollectMealReward(ProductionEntry entry, Vector3 worldPos)
        {
            DockBarManager dock = DockBarManager.Instance;
            if (dock == null)
            {
                Debug.LogWarning("[BuildingProduction] No DockBarManager — can't deliver meal card");
                return false;
            }

            // Use IsHandFull which includes reservedSlots (in-flight cards count toward capacity)
            if (dock.IsHandFull)
            {
                Debug.Log("[BuildingProduction] Hand full (including in-flight) — pop-up stays until there's room");
                return false;
            }

            UnitStats card = entry.pendingCard;
            if (card == null)
            {
                card = FindMealCard();
                if (card == null) return false;
            }

            WorkerCardFlyFX flyFX = WorkerCardFlyFX.Instance;
            if (flyFX != null)
            {
                // Only consume production if the slot was actually reserved
                if (!flyFX.SpawnCardFly(worldPos, card, 0))
                    return false;
            }
            else
            {
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

        // ─────────────────────────────────────────────────────────────────
        // HoldToFill Public API
        // ─────────────────────────────────────────────────────────────────

        public struct HoldFillInfo
        {
            public int progress;
            public int effectiveCost;
            public ResourceType resourceType;
            public GameObject buildingObj;
        }

        /// <summary>DEBUG: number of registered production entries.</summary>
        public int EntryCount => entries.Count;

        /// <summary>
        /// Resolves a raycast hit object (which may be a child collider/mesh) to
        /// the registered root building GameObject. Returns null if not found.
        /// Same hierarchy-walk logic used by HandlePopupTap.
        /// </summary>
        public GameObject ResolveHitToBuilding(GameObject hitObj, Vector3 hitPoint = default)
        {
            if (hitObj == null) return null;

            // Check 1: direct match or child of a registered building
            foreach (var entry in entries)
            {
                if (entry.buildingObj == null) continue;
                if (hitObj == entry.buildingObj ||
                    hitObj.transform.IsChildOf(entry.buildingObj.transform))
                    return entry.buildingObj;
            }

            // Check 2: ground tile — resolve via grid cell occupant
            // (same as HandlePopupTap Check 3)
            if (hitPoint != default && GridManager.Instance != null)
            {
                int gx, gy;
                if (GridManager.Instance.WorldToGridPosition(hitPoint, out gx, out gy))
                {
                    GameObject occupant = GridManager.Instance.GetCellOccupant(gx, gy);
                    if (occupant != null)
                    {
                        foreach (var entry in entries)
                        {
                            if (entry.buildingObj == occupant)
                                return entry.buildingObj;
                        }
                    }
                }
            }

            return null;
        }

        public bool IsWaitingForHoldFill(GameObject building)
        {
            var entry = entries.Find(e => e.buildingObj == building);
            return entry != null && entry.waitingForHoldFill;
        }

        /// <summary>
        /// DEV CHEAT: Instantly complete all buildings that are currently running their
        /// production timer. Skips buildings waiting for card input, resource cost, or
        /// hold-to-fill (those require player action and aren't time-gated).
        /// </summary>
        public void CheatCompleteAllTimers()
        {
            foreach (var entry in entries)
            {
                if (entry == null || entry.buildingObj == null) continue;
                if (entry.waitingForInput || entry.waitingForResources || entry.waitingForHoldFill) continue;
                if (!entry.isReady)
                    entry.elapsedTime = entry.EffectiveInterval + 1f;
            }
            Debug.Log("[DevCheat] Completed all production timers.");
        }

        /// <summary>
        /// Returns true if the building uses HoldToFill input type (regardless of current production state).
        /// Used by FurnitureRemovalHandler to exclude these buildings from hold-to-remove.
        /// </summary>
        public bool IsHoldToFillBuilding(GameObject building)
        {
            var entry = entries.Find(e => e.buildingObj == building);
            return entry != null && entry.inputType == ProductionInputType.HoldToFill;
        }

        public HoldFillInfo GetHoldFillInfo(GameObject building)
        {
            var entry = entries.Find(e => e.buildingObj == building);
            if (entry == null) return default;
            return new HoldFillInfo
            {
                progress = entry.holdFillProgress,
                effectiveCost = entry.EffectiveFillCost,
                resourceType = entry.productionCostResourceType,
                buildingObj = entry.buildingObj
            };
        }

        public bool IncrementHoldFill(GameObject building)
        {
            var entry = entries.Find(e => e.buildingObj == building);
            if (entry == null || !entry.waitingForHoldFill) return false;

            entry.holdFillProgress++;
            UpdateInsertFillBar(entry);

            if (entry.holdFillProgress >= entry.EffectiveFillCost)
            {
                entry.waitingForHoldFill = false;
                entry.elapsedTime = 0f;
                entry.timerRevealed = false;
                DismissInsertBubble(entry);
                OnHoldFillStateChanged?.Invoke(building, false);
                return true;
            }
            return false;
        }

        public bool HasReadyPopupAt(GameObject building)
        {
            var entry = entries.Find(e => e.buildingObj == building);
            return entry != null && entry.isReady;
        }

        public bool ClickConsumedThisFrame { get; set; }

        public bool IsBuildingPaused(GameObject building)
        {
            var entry = entries.Find(e => e.buildingObj == building);
            return entry != null && entry.isPaused;
        }

        public event System.Action<GameObject, bool> OnHoldFillStateChanged;
    }
}
