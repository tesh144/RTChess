#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// Controls the Draw button and all its related UI (cost display, cooldown timer).
    /// Owns:
    ///   - The draw button itself (Button_Battle)
    ///   - Button text ("Draw")
    ///   - Cooldown timer bubble (Label_Tag03_Time)
    ///   - Cost number text (shows draw price)
    ///   - Cost fill bar (shows time until cost decrease)
    ///   - Show/hide of the entire draw button area
    ///
    /// On click: calls DockBarManager.OnDrawButtonClicked(), then starts cooldown.
    /// Subscribes to token changes to keep cost display updated.
    /// </summary>
    public class DrawButtonController : MonoBehaviour
    {
        [Header("Button")]
        [Tooltip("The Button_Battle component.")]
        [SerializeField] private Button drawButton;

        [Tooltip("The TMP text child of Button_Battle.")]
        [SerializeField] private TextMeshProUGUI buttonText;

        [Header("Cooldown Timer")]
        [Tooltip("The Label_Tag03_Time bubble above the button.")]
        [SerializeField] private GameObject timerBubble;

        [Tooltip("TMP text inside the timer bubble.")]
        [SerializeField] private TextMeshProUGUI timerText;

        [Tooltip("Seconds before the player can draw again.")]
        [SerializeField] private float cooldownDuration = 15f;

        [Header("Cost Display")]
        [Tooltip("TMP showing the current draw cost number.")]
        [SerializeField] private TextMeshProUGUI costNumberText;

        [Tooltip("Fill bar showing time until draw cost decreases.")]
        [SerializeField] private Image costFillImage;

        // Internal state
        private bool isOnCooldown = false;
        private float cooldownRemaining = 0f;
        private Coroutine cooldownCoroutine;

        // ── Lifecycle ───────────────────────────────────────────────

        private void Start()
        {
            if (buttonText != null)
                buttonText.text = "Draw";

            if (timerBubble != null)
                timerBubble.SetActive(false);

            // Button click is wired as a persistent serialized listener via editor tool.
            // No runtime AddListener here.

            UpdateCostDisplay();
        }

        private void OnEnable()
        {
            if (ResourceTokenManager.Instance != null)
                ResourceTokenManager.Instance.OnTokensChanged += OnTokensChanged;
        }

        private void OnDisable()
        {
            if (cooldownCoroutine != null)
            {
                StopCoroutine(cooldownCoroutine);
                cooldownCoroutine = null;
            }

            if (ResourceTokenManager.Instance != null)
                ResourceTokenManager.Instance.OnTokensChanged -= OnTokensChanged;
        }

        // ── Draw Action ─────────────────────────────────────────────

        /// <summary>
        /// Called by Button_Battle's onClick (persistent serialized listener).
        /// Triggers a draw and starts cooldown.
        /// </summary>
        public void OnDrawButtonClicked()
        {
            if (isOnCooldown) return;

            // Button click SFX
            if (GameSFXManager.Instance != null)
                GameSFXManager.Instance.PlayButtonClick();

            // Button press animation (punch scale)
            if (drawButton != null)
                StartCoroutine(ButtonPunchAnimation(drawButton.transform));

            // Camera shake
            CameraSystemLocator.Current?.Shake(0.1f, 0.15f);

            if (DockBarManager.Instance != null)
            {
                DockBarManager.Instance.OnDrawButtonClicked();
            }
            else
            {
                Debug.LogWarning("[DrawButtonController] DockBarManager not found!");
                return;
            }

            // Start cooldown
            if (cooldownCoroutine != null)
                StopCoroutine(cooldownCoroutine);
            cooldownCoroutine = StartCoroutine(CooldownRoutine());
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

        // ── Cost Display ────────────────────────────────────────────

        /// <summary>Update cost number text and affordability color.</summary>
        public void UpdateCostDisplay()
        {
            if (costNumberText == null || DockBarManager.Instance == null) return;

            int cost = DockBarManager.Instance.GetCurrentDrawCost();
            bool canAfford = ResourceTokenManager.Instance != null &&
                             ResourceTokenManager.Instance.HasEnoughTokens(cost);

            costNumberText.text = cost.ToString();
            costNumberText.color = canAfford ? Color.white : new Color(1f, 0.3f, 0.3f);
        }

        /// <summary>Update the cost fill bar (called by DockBarManager when fill changes).</summary>
        public void UpdateCostFill(float fillAmount)
        {
            if (costFillImage != null)
                costFillImage.fillAmount = Mathf.Clamp01(fillAmount);
        }

        private void OnTokensChanged(int newTotal)
        {
            UpdateCostDisplay();
        }

        // ── Visibility ──────────────────────────────────────────────

        /// <summary>Show the draw button area.</summary>
        public void Show()
        {
            gameObject.SetActive(true);
        }

        /// <summary>Hide the draw button area.</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // ── Cooldown ────────────────────────────────────────────────

        private IEnumerator CooldownRoutine()
        {
            isOnCooldown = true;
            cooldownRemaining = cooldownDuration;

            if (drawButton != null)
                drawButton.interactable = false;

            if (timerBubble != null)
                timerBubble.SetActive(true);

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
                drawButton.interactable = true;

            if (timerBubble != null)
                timerBubble.SetActive(false);

            if (timerText != null)
                timerText.text = "";

            cooldownCoroutine = null;
            Debug.Log("[DrawButtonController] Cooldown complete — draw available");
        }

        /// <summary>Get remaining cooldown seconds.</summary>
        public float GetCooldownRemaining() => cooldownRemaining;

        /// <summary>Whether the draw button is currently on cooldown.</summary>
        public bool IsOnCooldown => isOnCooldown;

        /// <summary>The RectTransform of the actual draw button (for fly-in animations).</summary>
        public RectTransform ButtonRect => drawButton != null ? drawButton.GetComponent<RectTransform>() : GetComponent<RectTransform>();
    }
}
