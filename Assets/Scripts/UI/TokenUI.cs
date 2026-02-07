using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ClockworkGrid
{
    /// <summary>
    /// Enhanced token display with visual container, icon, and animations.
    /// Phase 6: Redesigned with better visuals and smooth number transitions.
    /// </summary>
    public class TokenUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI tokenText;
        [SerializeField] private Image tokenIcon;
        [SerializeField] private RectTransform container;

        [Header("Animation Settings")]
        [SerializeField] private float animationSpeed = 5f;
        [SerializeField] private float pulseScale = 1.15f;
        [SerializeField] private float pulseDuration = 0.3f;

        // Animation state
        private int displayedTokens = 0;
        private int targetTokens = 0;
        private float pulseTimer = 0f;
        private Vector3 originalScale;

        private void Start()
        {
            if (container != null)
            {
                originalScale = container.localScale;
            }

            if (ResourceTokenManager.Instance != null)
            {
                ResourceTokenManager.Instance.OnTokensChanged += UpdateDisplay;
                // Display current token count immediately
                displayedTokens = ResourceTokenManager.Instance.CurrentTokens;
                targetTokens = displayedTokens;
                UpdateText();
            }
            else
            {
                UpdateDisplay(0);
            }
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

            // Pulse animation
            if (pulseTimer > 0f)
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
            else if (container != null && container.localScale != originalScale)
            {
                container.localScale = originalScale;
            }
        }

        private void UpdateDisplay(int newTotal)
        {
            bool gained = newTotal > targetTokens;
            targetTokens = newTotal;

            // Trigger pulse effect if tokens increased
            if (gained)
            {
                pulseTimer = pulseDuration;
            }

            // Update icon color based on token amount
            UpdateIconColor(newTotal);
        }

        private void UpdateText()
        {
            if (tokenText != null)
            {
                tokenText.text = displayedTokens.ToString();
            }
        }

        private void UpdateIconColor(int tokenCount)
        {
            if (tokenIcon == null) return;

            // Color-code based on token count
            Color targetColor;
            if (tokenCount >= 10)
            {
                // Wealthy: Gold
                targetColor = new Color(1f, 0.9f, 0.2f);
            }
            else if (tokenCount >= 5)
            {
                // Moderate: Yellow
                targetColor = new Color(1f, 1f, 0.5f);
            }
            else if (tokenCount >= 1)
            {
                // Low: Orange
                targetColor = new Color(1f, 0.6f, 0.2f);
            }
            else
            {
                // Broke: Red
                targetColor = new Color(1f, 0.3f, 0.3f);
            }

            tokenIcon.color = targetColor;
        }
    }
}
