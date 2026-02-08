using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

namespace ClockworkGrid
{
    /// <summary>
    /// Enhanced token display with visual container, icon, and animations.
    /// Hidden on start; pops in when player first earns currency.
    /// </summary>
    public class TokenUI : MonoBehaviour
    {
        public static TokenUI Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI tokenText;
        [SerializeField] private Image tokenIcon;
        [SerializeField] private RectTransform container;

        [Header("Animation Settings")]
        [SerializeField] private float animationSpeed = 5f;
        [SerializeField] private float pulseScale = 1.15f;
        [SerializeField] private float pulseDuration = 0.3f;

        [Header("Reveal Animation")]
        [SerializeField] private float revealDuration = 0.4f;
        [SerializeField] private float revealBounce = 1.25f;

        // Animation state
        private int displayedTokens = 0;
        private int targetTokens = 0;
        private float pulseTimer = 0f;
        private Vector3 originalScale;

        // Reveal state
        private bool hasRevealed = false;
        private bool readyToReveal = false;
        private CanvasGroup canvasGroup;

        private void Awake()
        {
            Instance = this;

            // Use CanvasGroup for reliable hide/show (auto-add if missing)
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // Hide immediately
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        /// <summary>
        /// Get the screen position of the token icon (fly target for coin effects).
        /// </summary>
        public Vector3 GetIconScreenPosition()
        {
            if (tokenIcon != null)
                return tokenIcon.rectTransform.position; // In screen-space overlay, this is screen pos
            if (container != null)
                return container.position;
            return Vector3.zero;
        }

        /// <summary>
        /// Get the token icon sprite so flying coins can match it.
        /// </summary>
        public Sprite GetIconSprite()
        {
            return tokenIcon != null ? tokenIcon.sprite : null;
        }

        /// <summary>
        /// Get the current token icon color.
        /// </summary>
        public Color GetIconColor()
        {
            return Color.white; // Natural sprite color, no tint
        }

        private void Start()
        {
            if (container != null)
            {
                originalScale = container.localScale;
            }

            if (ResourceTokenManager.Instance != null)
            {
                ResourceTokenManager.Instance.OnTokensChanged += UpdateDisplay;
                // Sync display with current tokens (but stay hidden)
                displayedTokens = ResourceTokenManager.Instance.CurrentTokens;
                targetTokens = displayedTokens;
                UpdateText();
            }
            else
            {
                UpdateDisplay(0);
            }

            // Allow reveal starting next frame (skip initial starting-token events)
            StartCoroutine(EnableRevealNextFrame());
        }

        private IEnumerator EnableRevealNextFrame()
        {
            yield return null;
            readyToReveal = true;
        }

        private void OnDestroy()
        {
            if (ResourceTokenManager.Instance != null)
            {
                ResourceTokenManager.Instance.OnTokensChanged -= UpdateDisplay;
            }
        }

        private void Update()
        {
            // Animate number changes
            if (displayedTokens != targetTokens)
            {
                // Smooth lerp towards target
                float diff = targetTokens - displayedTokens;
                float step = Mathf.Sign(diff) * Mathf.Max(1, Mathf.Abs(diff) * animationSpeed * Time.deltaTime);

                displayedTokens = Mathf.RoundToInt(Mathf.MoveTowards(displayedTokens, targetTokens, Mathf.Abs(step)));
                UpdateText();
            }

            // Pulse animation (only when revealed)
            if (hasRevealed && pulseTimer > 0f)
            {
                pulseTimer -= Time.deltaTime;

                if (container != null)
                {
                    float t = pulseTimer / pulseDuration;
                    // Ease out
                    t = 1f - t;
                    t = 1f - (t * t);

                    float scale = Mathf.Lerp(1f, pulseScale, 1f - t);
                    container.localScale = originalScale * scale;
                }
            }
            else if (hasRevealed && container != null && container.localScale != originalScale)
            {
                container.localScale = originalScale;
            }
        }

        private void UpdateDisplay(int newTotal)
        {
            bool gained = newTotal > targetTokens;
            targetTokens = newTotal;

            // Reveal UI on first token gain (after initial frame)
            if (gained && !hasRevealed && readyToReveal)
            {
                hasRevealed = true;
                StartCoroutine(RevealAnimation());
            }

            // Trigger pulse effect if tokens increased (only when already visible)
            if (gained && hasRevealed)
            {
                pulseTimer = pulseDuration;
            }

        }

        private IEnumerator RevealAnimation()
        {
            float elapsed = 0f;
            while (elapsed < revealDuration)
            {
                float t = elapsed / revealDuration;

                // Fade in alpha via CanvasGroup
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Clamp01(t * 2.5f); // Fade in during first 40%
                }

                // Bounce scale on container
                if (container != null)
                {
                    float scale;
                    if (t < 0.6f)
                    {
                        float st = t / 0.6f;
                        scale = Mathf.Lerp(0f, revealBounce, st * st);
                    }
                    else
                    {
                        float st = (t - 0.6f) / 0.4f;
                        scale = Mathf.Lerp(revealBounce, 1f, st);
                    }
                    container.localScale = originalScale * scale;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Ensure final state
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = true;
            }
            if (container != null)
            {
                container.localScale = originalScale;
            }
        }

        private void UpdateText()
        {
            if (tokenText != null)
            {
                tokenText.text = displayedTokens.ToString();
            }
        }

    }
}
