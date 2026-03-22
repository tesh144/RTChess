#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using TMPro;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// Waits for the player's first interaction (click or keypress) before
    /// firing OnGameStart. Shows a pulsing "Click anywhere to start" prompt.
    /// Destroys itself after triggering.
    /// </summary>
    public class GameStartGate : MonoBehaviour
    {
        public System.Action OnGameStart;

        private TextMeshProUGUI promptText;
        private GameObject promptGO;
        private float elapsed;
        private float gracePeriod = 0.5f; // Ignore input for first 0.5s

        private void Start()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[GameStartGate] No Canvas found — triggering immediately");
                TriggerStart();
                return;
            }

            // Create centered prompt text
            promptGO = new GameObject("StartPrompt");
            promptGO.transform.SetParent(canvas.transform, false);

            promptText = promptGO.AddComponent<TextMeshProUGUI>();
            promptText.text = "Click anywhere to start";
            promptText.fontSize = 36;
            promptText.alignment = TextAlignmentOptions.Center;
            promptText.color = new Color(1f, 1f, 1f, 0.9f);

            // Center on screen
            RectTransform rt = promptGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(600f, 60f);
            rt.anchoredPosition = Vector2.zero;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;

            // Pulse alpha
            if (promptText != null)
            {
                float alpha = Mathf.Lerp(0.4f, 1f, (Mathf.Sin(Time.time * 2f) + 1f) * 0.5f);
                promptText.color = new Color(1f, 1f, 1f, alpha);
            }

            // Wait for grace period before accepting input
            if (elapsed < gracePeriod) return;

            if (Input.anyKeyDown)
            {
                TriggerStart();
            }
        }

        private void TriggerStart()
        {
            // Play a satisfying click sound when the player starts the game
            if (GameSFXManager.Instance != null)
                GameSFXManager.Instance.PlayButtonClick();

            OnGameStart?.Invoke();

            if (promptGO != null)
                Destroy(promptGO);

            Destroy(gameObject);
        }
    }
}
