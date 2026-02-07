using UnityEngine;
using TMPro;

namespace ClockworkGrid
{
    /// <summary>
    /// Displays the player's current resource token count.
    /// Subscribes to ResourceTokenManager.OnTokensChanged for updates.
    /// </summary>
    public class TokenUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI tokenText;

        private void Start()
        {
            if (ResourceTokenManager.Instance != null)
            {
                ResourceTokenManager.Instance.OnTokensChanged += UpdateDisplay;
            }
            UpdateDisplay(0);
        }

        private void OnDestroy()
        {
            if (ResourceTokenManager.Instance != null)
            {
                ResourceTokenManager.Instance.OnTokensChanged -= UpdateDisplay;
            }
        }

        private void UpdateDisplay(int newTotal)
        {
            if (tokenText != null)
            {
                tokenText.text = $"Tokens: {newTotal}";
            }
        }
    }
}
