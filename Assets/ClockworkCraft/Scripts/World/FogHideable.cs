#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using ClockworkGrid;

namespace ClockworkCraft
{
    /// <summary>
    /// Hides a spawned object when its grid cell is fogged.
    /// Shows the object with a pop-in animation when the tile is revealed.
    ///
    /// Attached by MapGenerator to all environment objects at spawn time.
    ///
    /// SUBSCRIPTION MODEL: This component subscribes to FogManager.OnCellRevealed
    /// manually in Initialize(), NOT via OnEnable/OnDisable. This is intentional —
    /// Initialize() calls SetActive(false) for fogged objects, which would trigger
    /// OnDisable and immediately unsubscribe. The manual approach keeps the
    /// subscription alive while the GameObject is deactivated, so we can hear the
    /// reveal event and reactivate.
    /// </summary>
    public class FogHideable : MonoBehaviour
    {
        private int gridX;
        private int gridY;
        private bool isRevealed;
        private bool isSubscribed;

        // Pop-in animation state
        private bool  isAnimating;
        private float animProgress;
        private const float AnimDuration = 0.4f;

        /// <summary>
        /// Set grid position and apply initial visibility based on fog state.
        /// Must be called immediately after AddComponent.
        /// </summary>
        public void Initialize(int x, int y)
        {
            gridX = x;
            gridY = y;

            // If FogManager doesn't exist, stay visible (no fog system)
            if (FogManager.Instance == null)
            {
                isRevealed = true;
                return;
            }

            // Cell already revealed → stay visible
            if (FogManager.Instance.IsCellRevealed(x, y))
            {
                isRevealed = true;
                return;
            }

            // Cell is fogged → subscribe FIRST, then deactivate
            Subscribe();
            isRevealed = false;
            gameObject.SetActive(false);
        }

        // ─────────────────────────────────────────────────────────────────
        // Manual subscription (NOT OnEnable/OnDisable — see class summary)
        // ─────────────────────────────────────────────────────────────────

        private void Subscribe()
        {
            if (isSubscribed || FogManager.Instance == null) return;
            FogManager.Instance.OnCellRevealed += HandleCellRevealed;
            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!isSubscribed) return;
            if (FogManager.Instance != null)
                FogManager.Instance.OnCellRevealed -= HandleCellRevealed;
            isSubscribed = false;
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        // ─────────────────────────────────────────────────────────────────
        // Reveal handler
        // ─────────────────────────────────────────────────────────────────

        private void HandleCellRevealed(int x, int y)
        {
            if (isRevealed) return;
            if (x != gridX || y != gridY) return;

            isRevealed = true;
            gameObject.SetActive(true);

            // Start pop-in scale animation
            transform.localScale = Vector3.zero;
            isAnimating  = true;
            animProgress = 0f;

            // No longer need reveal events
            Unsubscribe();
        }

        // ─────────────────────────────────────────────────────────────────
        // Pop-in animation (0 → 1.1 → 1.0 over 0.4s)
        // ─────────────────────────────────────────────────────────────────

        private void Update()
        {
            if (!isAnimating) return;

            animProgress += Time.deltaTime / AnimDuration;
            if (animProgress >= 1f)
            {
                animProgress = 1f;
                isAnimating  = false;
                transform.localScale = Vector3.one;
                return;
            }

            float t = animProgress;
            float scale;

            if (t < 0.7f)
            {
                // Ease-in rise to 1.1
                float nt = t / 0.7f;
                scale = nt * nt * 1.1f;
            }
            else
            {
                // Settle from 1.1 to 1.0
                float nt = (t - 0.7f) / 0.3f;
                scale = Mathf.Lerp(1.1f, 1f, nt);
            }

            transform.localScale = Vector3.one * scale;
        }
    }
}
