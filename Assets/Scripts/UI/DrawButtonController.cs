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
    public partial class DrawButtonController : MonoBehaviour
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

        /// <summary>Hide the draw button area.</summary>
        public void Hide()
        {
            if (drawButton != null)
                drawButton.gameObject.SetActive(false);
        }

        // ── Cooldown ────────────────────────────────────────────────

        /// <summary>Get remaining cooldown seconds.</summary>
        public float GetCooldownRemaining() => cooldownRemaining;

        /// <summary>Whether the draw button is currently on cooldown.</summary>
        public bool IsOnCooldown => isOnCooldown;

        /// <summary>The RectTransform of the actual draw button (for fly-in animations).</summary>
        public RectTransform ButtonRect => drawButton != null ? drawButton.GetComponent<RectTransform>() : GetComponent<RectTransform>();
    }
}
