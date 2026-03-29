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
    /// Per-level configuration for the draw button (gacha).
    /// Synced from the DrawButton Google Sheet by SheetSyncEditor.
    /// </summary>
    [System.Serializable]
    public class DrawButtonEntry
    {
        [Tooltip("Draw level (0 = first press, 1 = second, etc.)")]
        public int order;

        [Tooltip("What to output. Specific name (Home, Worker, Fighter, etc.), 'None' for RandomBuilding, or 'RandomTier0'-'RandomTier3' for tier-filtered draws.")]
        public string outputName = "None";

        [Tooltip("Currency type used for the cost.")]
        public ResourceType costCurrency = ResourceType.Gold;

        [Tooltip("Amount of the cost currency required. 0 = free.")]
        public int costValue = 0;

        [Tooltip("Cooldown in seconds after this draw.")]
        public float cooldown = 15f;
    }

    /// <summary>
    /// Controls the Draw/Sacrifice button (gacha) and all its related UI.
    /// Tracks the current draw level — each successful draw levels up.
    /// Per-level output, cost, and cooldown are driven by the DrawButton sheet.
    ///
    /// UI hierarchy (Button_Main):
    ///   - Label_Tag03_Time   → cooldown timer bubble (Icon + Text)
    ///   - Label_Tag03_Buy    → cost/upgrade bubble (Text + Icon + Cost)
    ///   - Icon               → crown/main icon
    ///   - Text               → "Level X" (current draw level)
    ///
    /// States:
    ///   - Cooldown:   Time tag visible, Buy tag hidden, button disabled
    ///   - Ready+Cost: Time tag hidden, Buy tag visible, button enabled
    ///   - Ready+Free: Both tags hidden, button enabled
    ///
    /// On click: resolves per-level output, spends cost via ResourceManager,
    /// adds card to hand via DockBarManager, increments level, starts cooldown.
    /// </summary>
    public class DrawButtonController : MonoBehaviour
    {
        [Header("Button")]
        [Tooltip("The Button_Main component.")]
        [SerializeField] private Button drawButton;

        [Tooltip("The TMP text child of Button_Main (shows 'Level X').")]
        [SerializeField] private TextMeshProUGUI buttonText;

        [Tooltip("The crown/main Icon on the button (hidden during cooldown).")]
        [SerializeField] private GameObject buttonIcon;

        [Header("Cooldown Timer Tag (Label_Tag03_Time)")]
        [Tooltip("The Label_Tag03_Time GameObject (shown during cooldown).")]
        [SerializeField] private GameObject timerBubble;

        [Tooltip("TMP text inside the timer tag (countdown number).")]
        [SerializeField] private TextMeshProUGUI timerText;

        [Header("Cost/Upgrade Tag (Label_Tag03_Buy)")]
        [Tooltip("The Label_Tag03_Buy GameObject (shown when ready and has cost).")]
        [SerializeField] private GameObject costBubble;

        [Tooltip("TMP text showing cost number inside Label_Tag03_Buy ('Cost' child).")]
        [SerializeField] private TextMeshProUGUI costNumberText;

        [Tooltip("Icon Image inside Label_Tag03_Buy (currency icon).")]
        [SerializeField] private Image costIcon;

        [Header("Draw Level Data (synced from DrawButton sheet)")]
        [Tooltip("Per-level draw configuration. Index = draw level.")]
        [SerializeField] private List<DrawButtonEntry> drawLevels = new List<DrawButtonEntry>();

        [Header("Database References")]
        [Tooltip("WorkerDatabase for resolving Worker/Fighter outputs.")]
        [SerializeField] private WorkerDatabase workerDatabase;

        // Internal state
        private int currentLevel = 0;
        private bool isOnCooldown = false;
        private float cooldownRemaining = 0f;
        private Coroutine cooldownCoroutine;
        private Color originalButtonTextColor = Color.white;
        private static readonly Color cooldownTextColor = new Color(0.55f, 0.65f, 0.7f); // grey to match disabled bg

        /// <summary>Current draw level (read-only).</summary>
        public int CurrentLevel => currentLevel;

        /// <summary>Total configured levels.</summary>
        public int MaxLevel => drawLevels.Count;

        // ── Lifecycle ───────────────────────────────────────────────

        private bool initialized = false;

        private void OnEnable()
        {
            // One-time setup
            if (!initialized)
            {
                initialized = true;

                if (buttonText != null)
                    originalButtonTextColor = buttonText.color;

                // Start hidden — DockBarManager calls Show() after first card placement
                Hide();

                UpdateLevelText();
                RefreshTagVisibility();
            }

            if (ResourceManager.Instance != null)
                ResourceManager.Instance.OnResourceChanged += OnResourceChanged;
        }

        private void OnDisable()
        {
            if (cooldownCoroutine != null)
            {
                StopCoroutine(cooldownCoroutine);
                cooldownCoroutine = null;
            }

            if (ResourceManager.Instance != null)
                ResourceManager.Instance.OnResourceChanged -= OnResourceChanged;
        }

        // ── Draw Action ─────────────────────────────────────────────

        /// <summary>
        /// Get the current level's entry, clamping to the last entry if level exceeds the list.
        /// </summary>
        private DrawButtonEntry GetCurrentEntry()
        {
            if (drawLevels.Count == 0) return null;
            int idx = Mathf.Min(currentLevel, drawLevels.Count - 1);
            return drawLevels[idx];
        }

        /// <summary>
        /// Called by Button_Main's onClick (persistent serialized listener).
        /// Resolves per-level output, checks cost, adds card, levels up, starts cooldown.
        /// </summary>
        public void OnDrawButtonClicked()
        {
            if (isOnCooldown) return;

            DockBarManager dock = DockBarManager.Instance;
            if (dock == null)
            {
                Debug.LogWarning("[DrawButton] DockBarManager not found!");
                return;
            }

            if (dock.IsHandFull)
            {
                Debug.Log("[DrawButton] Hand is full");
                if (GameSFXManager.Instance != null)
                    GameSFXManager.Instance.PlayHandFull();
                return;
            }

            DrawButtonEntry entry = GetCurrentEntry();
            if (entry == null)
            {
                Debug.LogWarning("[DrawButton] No draw levels configured");
                return;
            }

            // Check cost
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            int effectiveCost = DevCheatMenu.FreeCosts ? 0 : entry.costValue;
#else
            int effectiveCost = entry.costValue;
#endif
            if (effectiveCost > 0 && ResourceManager.Instance != null)
            {
                int available = ResourceManager.Instance.GetResource(entry.costCurrency);
                if (available < effectiveCost)
                {
                    Debug.Log($"[DrawButton] Can't afford draw — need {effectiveCost} {entry.costCurrency}, have {available}");
                    if (GameSFXManager.Instance != null)
                        GameSFXManager.Instance.PlayError();
                    return;
                }

                // Spend the cost
                ResourceManager.Instance.AddResource(entry.costCurrency, -effectiveCost);
            }

            // Button click SFX
            if (GameSFXManager.Instance != null)
                GameSFXManager.Instance.PlayButtonClick();

            // Button press animation (punch scale)
            if (drawButton != null)
                StartCoroutine(ButtonPunchAnimation(drawButton.transform));

            // Camera shake
            CameraSystemLocator.Current?.Shake(0.1f, 0.15f);

            // Resolve and deliver the output
            bool success = ResolveOutput(entry, dock);

            if (success)
            {
                currentLevel++;
                Debug.Log($"[DrawButton] Draw successful — level now {currentLevel}");
            }

            // Start cooldown (use this level's cooldown duration)
            if (cooldownCoroutine != null)
                StopCoroutine(cooldownCoroutine);
            cooldownCoroutine = StartCoroutine(CooldownRoutine(entry.cooldown));

            UpdateLevelText();
        }

        /// <summary>
        /// Resolve the output for a draw level entry and add it to the dock.
        /// Returns true if a card was successfully added.
        /// </summary>
        private bool ResolveOutput(DrawButtonEntry entry, DockBarManager dock)
        {
            string output = entry.outputName;
            if (string.IsNullOrEmpty(output)) output = "None";

            // ── None → RandomBuilding (unfiltered) ──
            if (output.Equals("None", System.StringComparison.OrdinalIgnoreCase))
            {
                return DrawRandomAndAdd(dock);
            }

            // ── TierXBuilding / TierXUnit → tier-filtered draw by source type ──
            if (output.StartsWith("Tier", System.StringComparison.OrdinalIgnoreCase) &&
                (output.EndsWith("Building", System.StringComparison.OrdinalIgnoreCase) ||
                 output.EndsWith("Unit", System.StringComparison.OrdinalIgnoreCase)))
            {
                bool isBuilding = output.EndsWith("Building", System.StringComparison.OrdinalIgnoreCase);
                string tierStr = output.Substring(4, 1); // "Tier0Building" → "0"
                if (int.TryParse(tierStr, out int tier) && CardPool.Instance != null)
                {
                    UnitStats card = isBuilding
                        ? CardPool.Instance.DrawRandomBuildingByTier(tier)
                        : CardPool.Instance.DrawRandomUnitByTier(tier);
                    if (card != null)
                    {
                        UnitStats clone = Instantiate(card);
                        clone.name = card.unitName;
                        dock.AddCard(clone, markAsNew: true, animateFromDraw: true);
                        if (GameSFXManager.Instance != null)
                            GameSFXManager.Instance.PlayCardDraw();
                        return true;
                    }
                }
                return DrawRandomAndAdd(dock);
            }

            // ── RandomTier0-3 (legacy) → cumulative tier draw ──
            if (output.StartsWith("RandomTier", System.StringComparison.OrdinalIgnoreCase))
            {
                string tierStr = output.Substring("RandomTier".Length);
                if (int.TryParse(tierStr, out int maxTier))
                {
                    if (CardPool.Instance == null) return false;
                    UnitStats card = CardPool.Instance.DrawRandomUnitUpToTier(maxTier);
                    if (card != null)
                    {
                        UnitStats clone = Instantiate(card);
                        clone.name = card.unitName;
                        dock.AddCard(clone, markAsNew: true, animateFromDraw: true);
                        if (GameSFXManager.Instance != null)
                            GameSFXManager.Instance.PlayCardDraw();
                        return true;
                    }
                }
                return DrawRandomAndAdd(dock);
            }

            // ── RandomBuilding (legacy explicit) ──
            if (output.Equals("RandomBuilding", System.StringComparison.OrdinalIgnoreCase))
            {
                return DrawRandomAndAdd(dock);
            }

            // ── Worker → from WorkerDatabase ──
            if (output.Equals("Worker", System.StringComparison.OrdinalIgnoreCase))
            {
                WorkerData wd = workerDatabase != null ? workerDatabase.GetByName("Worker") : null;
                if (wd != null && wd.prefab != null)
                {
                    dock.AddWorkerCard(wd, animateFromDraw: true);
                    if (GameSFXManager.Instance != null)
                        GameSFXManager.Instance.PlayCardDraw();
                    return true;
                }
                Debug.LogWarning("[DrawButton] Worker not found in WorkerDatabase");
                return false;
            }

            // ── Fighter → from WorkerDatabase ──
            if (output.Equals("Fighter", System.StringComparison.OrdinalIgnoreCase))
            {
                // Fighter is registered in CardPool (created in SetupDeck from WorkerDatabase)
                if (CardPool.Instance != null)
                {
                    UnitStats fighter = CardPool.Instance.FindByName("Fighter");
                    if (fighter != null)
                    {
                        UnitStats clone = Instantiate(fighter);
                        clone.name = fighter.unitName;
                        dock.AddCard(clone, markAsNew: true, animateFromDraw: true);
                        if (GameSFXManager.Instance != null)
                            GameSFXManager.Instance.PlayCardDraw();
                        return true;
                    }
                }
                Debug.LogWarning("[DrawButton] Fighter card not found in CardPool");
                return false;
            }

            // ── Specific building/card name → find in CardPool ──
            if (CardPool.Instance != null)
            {
                UnitStats card = CardPool.Instance.FindByName(output);
                if (card != null)
                {
                    UnitStats clone = Instantiate(card);
                    clone.name = card.unitName;
                    dock.AddCard(clone, markAsNew: true, animateFromDraw: true);
                    if (GameSFXManager.Instance != null)
                        GameSFXManager.Instance.PlayCardDraw();
                    return true;
                }
            }

            Debug.LogWarning($"[DrawButton] Output '{output}' not found — falling back to random");
            return DrawRandomAndAdd(dock);
        }

        /// <summary>Draw a random building and add to dock. Returns true on success.</summary>
        private bool DrawRandomAndAdd(DockBarManager dock)
        {
            if (CardPool.Instance == null) return false;
            UnitStats card = CardPool.Instance.DrawRandomUnit();
            if (card != null)
            {
                UnitStats clone = Instantiate(card);
                clone.name = card.unitName;
                dock.AddCard(clone, markAsNew: true, animateFromDraw: true);
                if (GameSFXManager.Instance != null)
                    GameSFXManager.Instance.PlayCardDraw();
                return true;
            }
            return false;
        }

        /// <summary>Quick punch-scale animation on a transform (press feedback).</summary>
        private IEnumerator ButtonPunchAnimation(Transform target)
        {
            Vector3 originalScale = target.localScale;
            Vector3 punchScale = originalScale * 0.85f;

            float duration = 0.12f;
            float half = duration * 0.5f;

            // Shrink
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                target.localScale = Vector3.Lerp(originalScale, punchScale, t / half);
                yield return null;
            }

            // Expand back
            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                target.localScale = Vector3.Lerp(punchScale, originalScale, t / half);
                yield return null;
            }

            target.localScale = originalScale;
        }

        /// <summary>Quick bounce when cooldown ends to draw attention.</summary>
        private IEnumerator ReadyBounceAnimation(Transform target)
        {
            Vector3 original = target.localScale;
            Vector3 big = original * 1.2f;
            float duration = 0.25f;
            float half = duration * 0.5f;

            // Scale up
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                target.localScale = Vector3.Lerp(original, big, t / half);
                yield return null;
            }

            // Settle back with slight overshoot
            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                float p = t / half;
                float ease = 1f + 2.7f * Mathf.Pow(p - 1f, 3f) + 1.7f * Mathf.Pow(p - 1f, 2f);
                target.localScale = Vector3.Lerp(big, original, ease);
                yield return null;
            }
            target.localScale = original;
        }

        // ── Tag & Level Display ──────────────────────────────────────

        /// <summary>
        /// Update the button text to show "Level X" based on the current draw level.
        /// Level is 1-indexed for display (internal level 0 = "Level 1").
        /// </summary>
        private void UpdateLevelText()
        {
            if (buttonText != null)
                buttonText.text = $"Level {currentLevel + 1}";
        }

        /// <summary>
        /// Master method that controls which tag is visible based on state:
        ///   - Cooldown:   Time tag on, Buy tag off
        ///   - Ready+Cost: Time tag off, Buy tag on (with cost number + icon)
        ///   - Ready+Free: Both tags off
        /// Also updates cost number and affordability color.
        /// </summary>
        private void RefreshTagVisibility()
        {
            if (isOnCooldown)
            {
                // Cooldown state — timer tag handles its own text in CooldownRoutine
                if (timerBubble != null) timerBubble.SetActive(true);
                if (costBubble != null) costBubble.SetActive(false);
                return;
            }

            // Not on cooldown — hide timer tag
            if (timerBubble != null) timerBubble.SetActive(false);

            DrawButtonEntry entry = GetCurrentEntry();
            int cost = entry != null ? entry.costValue : 0;

            if (cost > 0)
            {
                // Show buy/upgrade tag
                if (costBubble != null) costBubble.SetActive(true);

                bool canAfford = true;
                if (ResourceManager.Instance != null)
                    canAfford = ResourceManager.Instance.GetResource(entry.costCurrency) >= cost;

                if (costNumberText != null)
                {
                    costNumberText.text = cost.ToString();
                    costNumberText.color = canAfford ? Color.white : new Color(1f, 0.3f, 0.3f);
                }

                // Update the currency icon in the buy tag
                if (costIcon != null)
                {
                    Sprite coinSprite = ResourceDisplayUI.GetIconForResource(entry.costCurrency);
                    if (coinSprite != null)
                        costIcon.sprite = coinSprite;
                }
            }
            else
            {
                // Free draw — hide both tags
                if (costBubble != null) costBubble.SetActive(false);
            }
        }

        /// <summary>
        /// Toggle cooldown visuals on the button itself:
        ///   - Cooldown ON:  text goes grey, crown icon hidden
        ///   - Cooldown OFF: text restores original color, crown icon visible
        /// </summary>
        private void SetCooldownVisuals(bool onCooldown)
        {
            if (buttonText != null)
                buttonText.color = onCooldown ? cooldownTextColor : originalButtonTextColor;

            if (buttonIcon != null)
                buttonIcon.SetActive(!onCooldown);
        }

        /// <summary>Public API for external callers (e.g. DockBarManager) to refresh cost display.</summary>
        public void UpdateCostDisplay() => RefreshTagVisibility();

        private void OnResourceChanged(ResourceType type, int newAmount)
        {
            DrawButtonEntry entry = GetCurrentEntry();
            if (entry != null && type == entry.costCurrency)
                RefreshTagVisibility();
        }

        /// <summary>Get the current draw cost (for external display).</summary>
        public int GetCurrentDrawCost()
        {
            DrawButtonEntry entry = GetCurrentEntry();
            return entry != null ? entry.costValue : 0;
        }

        // ── Visibility ──────────────────────────────────────────────

        /// <summary>Show the draw button with a pop-in scale animation.</summary>
        public void Show()
        {
            if (drawButton == null) return;
            drawButton.gameObject.SetActive(true);
            StartCoroutine(PopInAnimation(drawButton.transform));
        }

        private IEnumerator PopInAnimation(Transform target)
        {
            Vector3 fullScale = Vector3.one;
            target.localScale = Vector3.zero;

            float duration = 0.3f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = t / duration;
                // Overshoot ease-out for a bouncy pop
                float ease = 1f + 2.7f * Mathf.Pow(p - 1f, 3f) + 1.7f * Mathf.Pow(p - 1f, 2f);
                target.localScale = fullScale * ease;
                yield return null;
            }
            target.localScale = fullScale;
        }

        /// <summary>Hide the draw button area.</summary>
        public void Hide()
        {
            if (drawButton != null)
                drawButton.gameObject.SetActive(false);
        }

        // ── Cooldown ────────────────────────────────────────────────

        private IEnumerator CooldownRoutine(float duration)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (LittleCafe.DevCheatMenu.InstantProduction) duration = 1f;
#endif
            isOnCooldown = true;
            cooldownRemaining = duration;

            if (drawButton != null)
                drawButton.interactable = false;

            // Show timer tag, hide buy tag
            RefreshTagVisibility();
            SetCooldownVisuals(true);

            // Reset timer text color to white for countdown display
            if (timerText != null)
                timerText.color = Color.white;

            while (cooldownRemaining > 0f)
            {
                if (timerText != null)
                    timerText.text = Mathf.CeilToInt(cooldownRemaining).ToString();

                yield return null;
                cooldownRemaining -= Time.deltaTime;
            }

            isOnCooldown = false;
            cooldownRemaining = 0f;

            if (drawButton != null)
            {
                drawButton.interactable = true;
                StartCoroutine(ReadyBounceAnimation(drawButton.transform));
            }

            // Switch from cooldown → ready state
            RefreshTagVisibility();
            SetCooldownVisuals(false);

            if (GameSFXManager.Instance != null)
                GameSFXManager.Instance.PlayDrawReady();

            cooldownCoroutine = null;
            Debug.Log("[DrawButton] Cooldown complete — draw available");
        }

        /// <summary>Get remaining cooldown seconds.</summary>
        public float GetCooldownRemaining() => cooldownRemaining;

        /// <summary>Whether the draw button is currently on cooldown.</summary>
        public bool IsOnCooldown => isOnCooldown;

        /// <summary>The RectTransform of the actual draw button (for fly-in animations).</summary>
        public RectTransform ButtonRect => drawButton != null ? drawButton.GetComponent<RectTransform>() : GetComponent<RectTransform>();
    }
}
