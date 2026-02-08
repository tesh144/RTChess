using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Fades out the control scheme overlay on the player's first interaction.
    /// Requires a CanvasGroup on the same GameObject (added automatically by GameSetup).
    /// </summary>
    public class ControlSchemeOverlay : MonoBehaviour
    {
        private bool dismissed = false;
        private bool fading = false;
        private CanvasGroup canvasGroup;
        private float fadeDuration = 0.5f;
        private float fadeTimer;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            canvasGroup.alpha = 1f;
        }

        private void Update()
        {
            if (fading)
            {
                fadeTimer += Time.deltaTime;
                canvasGroup.alpha = 1f - Mathf.Clamp01(fadeTimer / fadeDuration);

                if (canvasGroup.alpha <= 0f)
                {
                    fading = false;
                    gameObject.SetActive(false);
                }
                return;
            }

            if (dismissed) return;

            // Any key, mouse click, or scroll dismisses the overlay
            if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.mouseScrollDelta.y != 0f)
            {
                dismissed = true;
                fading = true;
                fadeTimer = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                Debug.Log("[ControlSchemeOverlay] Fading out on first interaction");
            }
        }
    }
}
