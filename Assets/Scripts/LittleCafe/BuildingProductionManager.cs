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
    ///
    /// Partial class — implementation split across:
    ///   BuildingProductionManager.cs          — core: fields, lifecycle, public API
    ///   BuildingProductionManager.Tick.cs     — OnIntervalTick, PickRandomWorker
    ///   BuildingProductionManager.Bubbles.cs  — insert/need bubble logic
    ///   BuildingProductionManager.Timer.cs    — timer canvas, fill, billboard, sprite utilities
    ///   BuildingProductionManager.Popup.cs    — spawn popup, animate, tap detection
    ///   BuildingProductionManager.Rewards.cs  — reward collection, card draw helpers
    ///   BuildingProductionManager.HoldToFill.cs — HoldToFill public API
    /// </summary>
    public partial class BuildingProductionManager : MonoBehaviour
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

        // Cached circle sprites for radial fill (generated once, shared with Timer partial)
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
    }
}
