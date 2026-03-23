#pragma warning disable CS0414, CS0219, CS0618
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

        // ── Runtime State ───────────────────────────────────────────

        private List<GameCardUI> handCards = new List<GameCardUI>();
        private RectTransform cardPanel;
        private HorizontalLayoutGroup layoutGroup;

        // Hand size limit
        public const int MAX_HAND_SIZE = 5;

        // Reserved slots: count of in-flight cards that haven't arrived yet.
        // Prevents the race condition where multiple fly-ins exceed hand capacity.
        private int reservedSlots = 0;

        // Draw cost tracking
        private int drawCount = 0;

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

            // Find DrawButtonController in scene (for fly-in animations and visibility)
            drawButtonController = FindFirstObjectByType<DrawButtonController>(FindObjectsInactive.Include);

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

            if (RaritySystem.Instance != null)
            {
                UnitStats drawnStats = RaritySystem.Instance.DrawRandomUnitByTier(0);
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

        /// <summary>Whether the hand is at maximum capacity (including reserved in-flight slots).</summary>
        public bool IsHandFull => (handCards.Count + reservedSlots) >= MAX_HAND_SIZE;

        /// <summary>
        /// Reserve a hand slot BEFORE starting a fly-in animation.
        /// Returns true if a slot was reserved, false if the hand is full.
        /// The caller MUST either call AddCard/AddWorkerCard (which consumes the reservation)
        /// or call ReleaseSlot() if the card is cancelled.
        /// </summary>
        public bool TryReserveSlot()
        {
            if ((handCards.Count + reservedSlots) >= MAX_HAND_SIZE)
                return false;
            reservedSlots++;
            return true;
        }

        /// <summary>Release a previously reserved slot (e.g. if fly-in was cancelled).</summary>
        public void ReleaseSlot()
        {
            reservedSlots = Mathf.Max(0, reservedSlots - 1);
        }

        /// <summary>
        /// Get the target world position for the next card slot in the dock bar.
        /// Uses temporary placeholders + LayoutRebuilder so the HorizontalLayoutGroup
        /// calculates the real position (accounts for alignment, spacing, padding, card size).
        /// </summary>
        public Vector3 GetNextSlotWorldPosition()
        {
            if (cardContainer == null) return Vector3.zero;
            RectTransform containerRect = cardContainer.GetComponent<RectTransform>();
            if (containerRect == null) return Vector3.zero;

            // Determine card size from the prefab
            float cardWidth = 70f, cardHeight = 70f;
            if (cardPrefab != null)
            {
                RectTransform prefabRect = cardPrefab.GetComponent<RectTransform>();
                if (prefabRect != null)
                {
                    cardWidth = prefabRect.sizeDelta.x;
                    cardHeight = prefabRect.sizeDelta.y;
                }
            }

            // Add invisible placeholder children for each reserved slot.
            var placeholders = new List<GameObject>();
            for (int i = 0; i < reservedSlots; i++)
            {
                var ph = new GameObject("_SlotTarget");
                var rt = ph.AddComponent<RectTransform>();
                rt.SetParent(cardContainer, false);
                rt.sizeDelta = new Vector2(cardWidth, cardHeight);
                placeholders.Add(ph);
            }

            // Force the layout group to recalculate with the placeholders present
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);

            // The LAST placeholder is where our most-recently-reserved card will land
            Vector3 targetPos;
            if (placeholders.Count > 0)
                targetPos = placeholders[placeholders.Count - 1].GetComponent<RectTransform>().position;
            else
                targetPos = containerRect.position;

            // Clean up all placeholders and restore the original layout
            foreach (var ph in placeholders)
                DestroyImmediate(ph);
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);

            return targetPos;
        }

        /// <summary>Add a new card to the hand. Optionally mark it as "new" with a badge.</summary>
        /// <param name="animateFromDraw">If true, card flies in from the draw button position.</param>
        /// <param name="consumeReservation">If true, consumes a previously reserved slot (from TryReserveSlot).</param>
        public void AddCard(UnitStats unitStats, bool markAsNew = false, bool animateFromDraw = false, bool consumeReservation = false, Vector3? flyFromScreenPos = null)
        {
            // Consume the reservation first (reduces reservedSlots so the count stays accurate)
            if (consumeReservation)
                reservedSlots = Mathf.Max(0, reservedSlots - 1);

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

            // Fly-in animation: from draw button, from a screen position, or just pop in
            if (animateFromDraw && drawButtonController != null)
            {
                StartCoroutine(CardFlyInAnimation(card, cardObj.GetComponent<RectTransform>(), null));
            }
            else if (flyFromScreenPos.HasValue)
            {
                StartCoroutine(CardFlyInAnimation(card, cardObj.GetComponent<RectTransform>(), flyFromScreenPos.Value));
            }
            else
            {
                // No fly-in — play appear pop + start idle
                card.PlayAppearAnimation();
            }
        }

        /// <summary>
        /// Animates a card flying into its dock slot with an upward arc.
        /// If overrideStartScreenPos is null, flies from the draw button.
        /// If provided, flies from that screen-space position (used by building production).
        /// </summary>
        private IEnumerator CardFlyInAnimation(GameCardUI card, RectTransform cardRect, Vector3? overrideStartScreenPos)
        {
            if (cardRect == null) yield break;

            // Let layout settle for one frame so we know the card's final position
            yield return null;

            if (cardRect == null) yield break;
            Vector3 finalPos = cardRect.position;
            Vector3 finalScale = cardRect.localScale;

            // Start position: override screen pos, or the draw button
            Vector3 startPos;
            if (overrideStartScreenPos.HasValue)
            {
                startPos = overrideStartScreenPos.Value;
            }
            else if (drawButtonController != null && drawButtonController.ButtonRect != null)
            {
                startPos = drawButtonController.ButtonRect.position;
            }
            else
            {
                card.PlayAppearAnimation();
                yield break;
            }

            // Arc height in world-space units (scales with screen)
            float arcHeight = Mathf.Abs(finalPos.y - startPos.y) * 0.5f + 80f;

            float duration = 0.45f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (cardRect == null) yield break;

                float easeT = t * t * (3f - 2f * t);

                Vector3 pos = Vector3.Lerp(startPos, finalPos, easeT);
                pos.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
                cardRect.position = pos;

                float scaleT;
                if (t < 0.7f)
                {
                    scaleT = Mathf.Lerp(0.2f, 1.1f, t / 0.7f);
                }
                else
                {
                    float settleT = (t - 0.7f) / 0.3f;
                    scaleT = Mathf.Lerp(1.1f, 1f, settleT);
                }
                cardRect.localScale = finalScale * scaleT;

                yield return null;
            }

            if (cardRect == null) yield break;
            cardRect.position = finalPos;
            cardRect.localScale = finalScale;

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
        /// <param name="consumeReservation">If true, consumes a previously reserved slot.</param>
        public void AddWorkerCard(WorkerData workerData, bool consumeReservation = false, bool animateFromDraw = false, Vector3? flyFromScreenPos = null)
        {
            if (workerData == null) return;

            // Consume the reservation first
            if (consumeReservation)
                reservedSlots = Mathf.Max(0, reservedSlots - 1);

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

            AddCard(stats, markAsNew: true, animateFromDraw: animateFromDraw, flyFromScreenPos: flyFromScreenPos);
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

            // Turn on the draw button after the player's first placement
            if (drawButtonController != null)
            {
                drawButtonController.Show();
                Debug.Log("[DockBarManager] Card placed — draw button activated");
            }
        }

        /// <summary>Get the current number of cards in hand.</summary>
        public int GetCardCount()
        {
            return handCards.Count;
        }

        // ── Visibility ──────────────────────────────────────────────

        /// <summary>Show the card hand area.</summary>
        public void ShowWithAnimation()
        {
            if (cardContainer != null) cardContainer.gameObject.SetActive(true);
        }

        // ── Hand Full Popup ──────────────────────────────────────────

        /// <summary>
        /// Show a brief "Hand Full!" floating text at the given screen position.
        /// Floats upward and fades out over 1 second.
        /// </summary>
        public void ShowHandFullPopup(Vector2 screenPos)
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            GameObject popupObj = new GameObject("HandFullPopup");
            popupObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = popupObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(200f, 40f);

            Camera canvasCam = (canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(), screenPos, canvasCam, out Vector2 localPoint);
            rt.anchoredPosition = localPoint;

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
                if (rt == null || obj == null)
                    yield break;

                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                rt.anchoredPosition = startPos + new Vector2(0f, 40f * t);

                float alpha = t < 0.5f ? 1f : 1f - ((t - 0.5f) * 2f);
                if (tmp != null)
                    tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, alpha);

                yield return null;
            }

            if (obj != null)
                Destroy(obj);
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
    }
}
