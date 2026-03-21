using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using LittleCafe;
using ClockworkCraft;

namespace ClockworkGrid
{
    /// <summary>
    /// Manages the card hand at the bottom of the screen.
    /// Responsibilities:
    ///   - Card hand: instantiating, tracking, and removing GameCardUI cards
    ///   - Draw cost: calculating escalating cost, spending tokens
    ///   - Show/hide the card hand area
    ///
    /// Does NOT own the draw button UI — that belongs to DrawButtonController.
    /// </summary>
    public class DockBarManager : MonoBehaviour
    {
        [Header("Card Prefab")]
        [Tooltip("Card_Prefab with GameCardUI component. Instantiated for each drawn card.")]
        [SerializeField] private GameObject cardPrefab;

        [Header("Card Hand")]
        [Tooltip("Parent transform where drawn cards are placed (e.g. Button_MainMenu).")]
        [SerializeField] private Transform cardContainer;

        [Header("Draw Cost")]
        [SerializeField] private int baseDrawCost = 6;
        [SerializeField] private int costIncrement = 1;
        [Tooltip("Interval ticks between automatic cost decreases (0 = disabled).")]
        [SerializeField] private int costDecreaseInterval = 0;

        [Header("Slide Animation")]
        [SerializeField] private bool enableSlideAnimation = true;
        [SerializeField] private float slideUpDistance = 150f;
        [SerializeField] private float slideUpDuration = 0.6f;

        // ── Runtime State ───────────────────────────────────────────

        private List<GameCardUI> handCards = new List<GameCardUI>();
        private RectTransform dockBarRect;
        private Vector2 originalAnchoredPosition;
        private RectTransform cardPanel;
        private HorizontalLayoutGroup layoutGroup;

        // Hand size limit
        public const int MAX_HAND_SIZE = 5;

        // Draw cost tracking
        private int drawCount = 0;
        private int ticksSinceCostDecrease = 0;

        // Cached reference to the draw button controller (found at runtime)
        private DrawButtonController drawButtonController;

        // ── Singleton ───────────────────────────────────────────────

        public static DockBarManager Instance { get; private set; }

        /// <summary>The transform where cards are instantiated (for fly-to targeting).</summary>
        public Transform CardContainer => cardContainer;

        // ── Lifecycle ───────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (ResourceTokenManager.Instance != null)
                ResourceTokenManager.Instance.OnTokensChanged -= OnTokensChanged;

            if (IntervalTimer.Instance != null)
                IntervalTimer.Instance.OnIntervalTick -= OnIntervalTickCostDecrease;
        }

        // ── Initialization ──────────────────────────────────────────

        /// <summary>
        /// Initialize the card hand. Sets up card container layout and subscribes to events.
        /// </summary>
        public void Initialize(Canvas canvas)
        {
            if (cardContainer == null)
            {
                Debug.LogError("[DockBarManager] cardContainer is not assigned!");
                return;
            }

            cardPanel = cardContainer.GetComponent<RectTransform>();

            // Clear any editor mock-up cards
            ClearCardContainer();

            // Ensure HorizontalLayoutGroup exists
            layoutGroup = cardContainer.GetComponent<HorizontalLayoutGroup>();
            if (layoutGroup == null)
            {
                layoutGroup = cardContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
                layoutGroup.spacing = 10f;
                layoutGroup.padding = new RectOffset(10, 10, 10, 10);
                layoutGroup.childAlignment = TextAnchor.MiddleCenter;
                layoutGroup.childControlWidth = false;
                layoutGroup.childControlHeight = false;
                layoutGroup.childForceExpandWidth = false;
                layoutGroup.childForceExpandHeight = false;
            }

            // Find DrawButtonController in scene (for cost display updates)
            drawButtonController = FindObjectOfType<DrawButtonController>(true);

            // Hide draw button — buildings are now produced by map objects, not manual draws
            if (drawButtonController != null)
                drawButtonController.gameObject.SetActive(false);

            // Subscribe to token changes
            if (ResourceTokenManager.Instance != null)
                ResourceTokenManager.Instance.OnTokensChanged += OnTokensChanged;

            // Subscribe to interval timer for cost decrease over time
            if (costDecreaseInterval > 0 && IntervalTimer.Instance != null)
                IntervalTimer.Instance.OnIntervalTick += OnIntervalTickCostDecrease;

            // Cache RectTransform for slide animation
            if (dockBarRect == null)
                dockBarRect = GetComponent<RectTransform>();
            if (dockBarRect != null)
                originalAnchoredPosition = dockBarRect.anchoredPosition;

            NotifyCostChanged();
            Debug.Log("[DockBarManager] Initialized");
        }

        // ── Draw Cost ───────────────────────────────────────────────

        /// <summary>Override draw cost at runtime (e.g., free draws in ClockworkCraft).</summary>
        public void SetDrawCost(int baseCost, int increment)
        {
            baseDrawCost = baseCost;
            costIncrement = increment;
            drawCount = 0;
            Debug.Log($"[DockBarManager] Draw cost set to base={baseCost}, increment={increment}");
            NotifyCostChanged();
        }

        /// <summary>Calculate the current draw cost.</summary>
        public int CalculateDrawCost()
        {
            return baseDrawCost + (costIncrement * drawCount);
        }

        /// <summary>Get current draw cost (public accessor).</summary>
        public int GetCurrentDrawCost()
        {
            return CalculateDrawCost();
        }

        /// <summary>Tell DrawButtonController the cost changed so it can update its display.</summary>
        private void NotifyCostChanged()
        {
            if (drawButtonController != null)
                drawButtonController.UpdateCostDisplay();
        }

        // ── Draw Action ─────────────────────────────────────────────

        /// <summary>
        /// Execute a draw: spend tokens, pull from RaritySystem, add card to hand.
        /// Called by DrawButtonController.
        /// </summary>
        public void OnDrawButtonClicked()
        {
            if (IsHandFull)
            {
                Debug.Log("[DockBarManager] Draw failed — hand is full");
                if (GameSFXManager.Instance != null)
                    GameSFXManager.Instance.PlayHandFull();
                ShowHandFullPopupAtCursor();
                return;
            }

            int cost = CalculateDrawCost();

            if (ResourceTokenManager.Instance == null || !ResourceTokenManager.Instance.HasEnoughTokens(cost))
            {
                Debug.Log($"[DockBarManager] Draw failed — not enough tokens (need {cost})");
                if (GameSFXManager.Instance != null)
                    GameSFXManager.Instance.PlayError();
                return;
            }

            ResourceTokenManager.Instance.SpendTokens(cost);

            drawCount++;
            ticksSinceCostDecrease = 0;
            UpdateCostFill();

            if (RaritySystem.Instance != null)
            {
                UnitStats drawnStats = RaritySystem.Instance.DrawRandomUnit();
                if (drawnStats != null)
                {
                    AddCard(drawnStats, markAsNew: true, animateFromDraw: true);
                    Debug.Log($"[DockBarManager] Drew {drawnStats.unitName} ({drawnStats.rarity}) — cost was {cost}T");

                    if (GameSFXManager.Instance != null)
                        GameSFXManager.Instance.PlayCardDraw();

                    CameraSystemLocator.Current?.Shake(0.12f, 0.2f);
                }
            }

            NotifyCostChanged();
        }

        // ── Card Management ─────────────────────────────────────────

        /// <summary>Whether the hand is at maximum capacity.</summary>
        public bool IsHandFull => handCards.Count >= MAX_HAND_SIZE;

        /// <summary>Add a new card to the hand. Optionally mark it as "new" with a badge.</summary>
        /// <param name="animateFromDraw">If true, card flies in from the draw button position.</param>
        public void AddCard(UnitStats unitStats, bool markAsNew = false, bool animateFromDraw = false)
        {
            if (handCards.Count >= MAX_HAND_SIZE)
            {
                Debug.LogWarning("[DockBarManager] Hand full — can't add card");
                if (GameSFXManager.Instance != null)
                    GameSFXManager.Instance.PlayHandFull();
                ShowHandFullPopupAtCursor();
                return;
            }

            GameObject cardObj;
            GameCardUI card;

            if (cardPrefab != null)
            {
                cardObj = Instantiate(cardPrefab, cardPanel, false);
                cardObj.name = $"Card_{handCards.Count}";

                card = cardObj.GetComponent<GameCardUI>();
                if (card == null)
                    card = cardObj.AddComponent<GameCardUI>();
            }
            else
            {
                // Minimal fallback
                cardObj = new GameObject($"Card_{handCards.Count}");
                RectTransform cardRect = cardObj.AddComponent<RectTransform>();
                cardRect.SetParent(cardPanel, false);
                cardRect.sizeDelta = new Vector2(70f, 70f);
                cardObj.AddComponent<Image>().color = Color.white;
                card = cardObj.AddComponent<GameCardUI>();
            }

            card.Initialize(unitStats, this);
            if (markAsNew) card.SetNew(true);
            handCards.Add(card);

            if (GameSFXManager.Instance != null)
                GameSFXManager.Instance.PlayCardSlideIn();

            UpdateLayoutSpacing();

            // Fly-in animation from draw button, or appear animation for non-drawn cards
            if (animateFromDraw && drawButtonController != null)
            {
                StartCoroutine(CardFlyInAnimation(card, cardObj.GetComponent<RectTransform>()));
            }
            else
            {
                // No fly-in — play appear pop + start idle
                card.PlayAppearAnimation();
            }
        }

        /// <summary>
        /// Animates a card flying from the draw button's position into its dock slot.
        /// Card starts at draw button position, scaled to 0, and flies to final position
        /// with a slight overshoot bounce.
        /// </summary>
        private IEnumerator CardFlyInAnimation(GameCardUI card, RectTransform cardRect)
        {
            if (cardRect == null) yield break;

            // Get the actual draw button's screen position
            RectTransform drawBtnRect = drawButtonController.ButtonRect;
            if (drawBtnRect == null) yield break;

            // Let layout settle for one frame so we know the card's final position
            yield return null;

            if (cardRect == null) yield break;
            Vector3 finalPos = cardRect.position;
            Vector3 finalScale = cardRect.localScale;

            // Start position: the draw button's world position
            Vector3 startPos = drawBtnRect.position;

            float duration = 0.35f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (cardRect == null) yield break;

                // Ease-out cubic for smooth deceleration
                float easeT = 1f - Mathf.Pow(1f - t, 3f);

                // Position: fly from draw button to final slot
                cardRect.position = Vector3.Lerp(startPos, finalPos, easeT);

                // Scale: grow from small to full with slight overshoot
                float scaleT;
                if (t < 0.7f)
                {
                    // Grow to 110%
                    scaleT = Mathf.Lerp(0.2f, 1.1f, t / 0.7f);
                }
                else
                {
                    // Settle back to 100%
                    float settleT = (t - 0.7f) / 0.3f;
                    scaleT = Mathf.Lerp(1.1f, 1f, settleT);
                }
                cardRect.localScale = finalScale * scaleT;

                yield return null;
            }

            // Ensure final state
            if (cardRect == null) yield break;
            cardRect.position = finalPos;
            cardRect.localScale = finalScale;

            // Start idle breathing after fly-in settles
            if (card != null)
                card.StartIdleAnimation();
        }

        /// <summary>Add a starting worker to the dock from a WorkerDatabase.</summary>
        public void AddStartingWorker(WorkerDatabase workerDB)
        {
            if (workerDB == null || workerDB.Count == 0)
            {
                Debug.LogWarning("[DockBarManager] No WorkerDatabase — can't add starting worker");
                return;
            }

            foreach (WorkerData wd in workerDB.AllWorkers)
            {
                if (wd.prefab != null)
                {
                    AddWorkerCard(wd);
                    Debug.Log($"[DockBarManager] Added starting worker '{wd.GetCleanName()}' to dock");
                    return;
                }
            }

            Debug.LogWarning("[DockBarManager] No workers with valid prefabs found");
        }

        /// <summary>Add a worker card from WorkerData (produced by buildings).</summary>
        public void AddWorkerCard(WorkerData workerData)
        {
            if (workerData == null) return;

            if (handCards.Count >= MAX_HAND_SIZE)
            {
                Debug.LogWarning("[DockBarManager] Hand full — can't add worker card");
                if (GameSFXManager.Instance != null)
                    GameSFXManager.Instance.PlayHandFull();
                ShowHandFullPopupAtCursor();
                return;
            }

            UnitStats stats       = ScriptableObject.CreateInstance<UnitStats>();
            stats.unitType        = UnitType.Soldier;
            stats.unitName        = workerData.GetCleanName();
            stats.rarity          = Rarity.Common;
            stats.drawWeight      = workerData.drawWeight;
            stats.iconSprite      = workerData.icon;
            stats.unitColor       = Color.white;
            stats.unitPrefab      = workerData.prefab;
            stats.resourceCost    = 0;
            stats.gridSize        = workerData.gridSize;
            stats.modelScale      = workerData.visualScale;
            stats.enemyPrefab     = null;
            stats.isActive        = workerData.isActive;
            stats.behaviorType    = workerData.behaviorType;
            stats.maxHP           = workerData.hp;
            stats.attackDamage    = workerData.attackPower;
            stats.furnitureTypeOverride = -1;
            stats.isAllied        = true;
            stats.killerAdvances  = workerData.killerAdvances;

            AddCard(stats, markAsNew: true);
            Debug.Log($"[DockBarManager] Added worker card '{workerData.GetCleanName()}'");
        }

        /// <summary>Remove a card from the hand (called after placement).</summary>
        public void RemoveCard(GameCardUI card)
        {
            if (handCards.Contains(card))
            {
                handCards.Remove(card);
                Destroy(card.gameObject);
                UpdateLayoutSpacing();
            }
        }

        /// <summary>Get the current number of cards in hand.</summary>
        public int GetCardCount()
        {
            return handCards.Count;
        }

        // ── Visibility ──────────────────────────────────────────────

        /// <summary>Hide the card hand and draw button.</summary>
        public void HideUI()
        {
            if (cardContainer != null) cardContainer.gameObject.SetActive(false);
            if (drawButtonController != null) drawButtonController.Hide();
        }

        /// <summary>Show the card hand and draw button with slide-up animation.</summary>
        public void ShowWithAnimation()
        {
            if (cardContainer != null) cardContainer.gameObject.SetActive(true);
            if (drawButtonController != null) drawButtonController.Show();

            if (enableSlideAnimation && dockBarRect != null)
                StartCoroutine(SlideUpAnimation());
        }

        private System.Collections.IEnumerator SlideUpAnimation()
        {
            if (dockBarRect == null) yield break;

            Vector2 startPos = originalAnchoredPosition - new Vector2(0, slideUpDistance);
            Vector2 endPos = originalAnchoredPosition;
            dockBarRect.anchoredPosition = startPos;

            float elapsed = 0f;
            while (elapsed < slideUpDuration)
            {
                elapsed += Time.deltaTime;
                float smoothT = 1f - Mathf.Pow(1f - (elapsed / slideUpDuration), 3f);
                dockBarRect.anchoredPosition = Vector2.Lerp(startPos, endPos, smoothT);
                yield return null;
            }

            dockBarRect.anchoredPosition = endPos;
        }

        // ── Cost Decrease Over Time ─────────────────────────────────

        private void OnTokensChanged(int newTotal)
        {
            NotifyCostChanged();
        }

        private void OnIntervalTickCostDecrease(int intervalCount)
        {
            if (costDecreaseInterval <= 0 || drawCount <= 0)
            {
                UpdateCostFill();
                return;
            }

            ticksSinceCostDecrease++;
            if (ticksSinceCostDecrease >= costDecreaseInterval)
            {
                ticksSinceCostDecrease = 0;
                drawCount--;
                if (drawCount < 0) drawCount = 0;
                Debug.Log($"[DockBarManager] Cost decreased — drawCount={drawCount}, cost={CalculateDrawCost()}");
                NotifyCostChanged();
            }

            UpdateCostFill();
        }

        private void UpdateCostFill()
        {
            if (drawButtonController == null) return;

            if (costDecreaseInterval <= 0 || drawCount <= 0)
            {
                drawButtonController.UpdateCostFill(0f);
                return;
            }

            float remaining = (float)(costDecreaseInterval - ticksSinceCostDecrease) / costDecreaseInterval;
            drawButtonController.UpdateCostFill(remaining);
        }

        // ── Internal ────────────────────────────────────────────────

        private void UpdateLayoutSpacing()
        {
            if (layoutGroup == null) return;

            int count = handCards.Count;
            if (count <= 5) layoutGroup.spacing = 10f;
            else if (count <= 8) layoutGroup.spacing = 8f;
            else layoutGroup.spacing = 5f;
        }

        private void ClearCardContainer()
        {
            if (cardContainer == null) return;

            for (int i = cardContainer.childCount - 1; i >= 0; i--)
                Destroy(cardContainer.GetChild(i).gameObject);

            Debug.Log("[DockBarManager] Cleared mock-up cards from container");
        }

        // ── Hand Full Popup ──────────────────────────────────────────

        /// <summary>
        /// Show a brief "Hand Full!" floating text at the given screen position.
        /// Floats upward and fades out over 1 second.
        /// </summary>
        public void ShowHandFullPopup(Vector2 screenPos)
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            GameObject popupObj = new GameObject("HandFullPopup");
            popupObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = popupObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(200f, 40f);

            // Position at screen point
            Camera canvasCam = (canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(), screenPos, canvasCam, out Vector2 localPoint);
            rt.anchoredPosition = localPoint;

            // Override sorting so it's on top
            Canvas overrideCanvas = popupObj.AddComponent<Canvas>();
            overrideCanvas.overrideSorting = true;
            overrideCanvas.sortingOrder = 100;

            TextMeshProUGUI tmp = popupObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "Hand Full!";
            tmp.fontSize = 24f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.4f, 0.4f, 1f);
            tmp.raycastTarget = false;
            tmp.enableAutoSizing = false;
            tmp.fontStyle = FontStyles.Bold;

            StartCoroutine(FloatAndFadePopup(rt, tmp, popupObj));
        }

        /// <summary>Show hand-full popup at the current mouse/touch position.</summary>
        public void ShowHandFullPopupAtCursor()
        {
            Vector2 pos = Input.mousePosition;
            ShowHandFullPopup(pos);
        }

        private IEnumerator FloatAndFadePopup(RectTransform rt, TextMeshProUGUI tmp, GameObject obj)
        {
            float duration = 1f;
            float elapsed = 0f;
            Vector2 startPos = rt.anchoredPosition;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Float upward
                rt.anchoredPosition = startPos + new Vector2(0f, 40f * t);

                // Fade out in second half
                float alpha = t < 0.5f ? 1f : 1f - ((t - 0.5f) * 2f);
                tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, alpha);

                yield return null;
            }

            Destroy(obj);
        }
    }
}
