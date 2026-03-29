#pragma warning disable CS0414, CS0219, CS0618
using System.Collections;
using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Animation methods for GameCardUI (partial class).
    /// Contains idle breathing, badge wobble, and appear animations.
    /// </summary>
    public partial class GameCardUI
    {
        // ── Card Idle Animation ────────────────────────────────────

        /// <summary>
        /// Starts the subtle idle breathing animation on this card.
        /// Called after the card has settled into the dock.
        /// Snapshots the current position as the idle base so it doesn't fight the layout.
        /// </summary>
        public void StartIdleAnimation()
        {
            if (cardIdleCoroutine != null) StopCoroutine(cardIdleCoroutine);
            cardIdleCoroutine = StartCoroutine(CardIdleLoop());
        }

        /// <summary>
        /// Stops the idle animation and resets scale to original.
        /// </summary>
        public void StopIdleAnimation()
        {
            if (cardIdleCoroutine != null)
            {
                StopCoroutine(cardIdleCoroutine);
                cardIdleCoroutine = null;
            }
            if (rectTransform != null)
                rectTransform.localScale = originalScale;
        }

        /// <summary>
        /// Plays a pop-in appear animation when a card is added to the dock.
        /// Scale from 0 → overshoot → settle, with a slight upward bounce.
        /// </summary>
        public void PlayAppearAnimation()
        {
            // Ensure the GameObject is active — StartCoroutine fails on inactive objects
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            // StartCoroutine requires the entire hierarchy to be active.
            // During startup (SetupDeck), the card panel may still be inactive.
            // Fall back gracefully — the card will just appear without animation.
            if (!gameObject.activeInHierarchy)
                return;

            if (appearCoroutine != null) StopCoroutine(appearCoroutine);
            appearCoroutine = StartCoroutine(AppearAnimationRoutine());
        }

        // ── Animation Coroutines ──────────────────────────────────────

        /// <summary>
        /// Badge wobble: subtle scale pulse + gentle rotation oscillation.
        /// Runs continuously while the "new" badge is visible.
        /// </summary>
        private IEnumerator BadgeWobbleLoop()
        {
            float time = 0f;
            while (true)
            {
                time += Time.deltaTime;

                // Scale pulse: 1.0 → 1.12 → 1.0 over ~1.2s
                float scaleT = Mathf.Sin(time * 5.2f) * 0.5f + 0.5f; // 0..1
                float scale = 1f + scaleT * 0.12f;

                // Gentle rotation: ±6 degrees
                float rotation = Mathf.Sin(time * 3.7f) * 6f;

                if (_redBadge != null)
                {
                    _redBadge.transform.localScale = new Vector3(scale, scale, 1f);
                    _redBadge.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
                }

                yield return null;
            }
        }

        /// <summary>
        /// Card idle: periodic little bounce every 2-3 seconds.
        /// Quick scale pop (1.0 → 1.08 → 1.0) then waits.
        /// Each card has a random phase offset so they don't all bounce at once.
        /// </summary>
        private IEnumerator CardIdleLoop()
        {
            // Initial delay based on phase offset so cards don't sync
            yield return new WaitForSeconds(idlePhaseOffset / (Mathf.PI * 2f) * 2f);

            while (true)
            {
                if (!isDragging && rectTransform != null)
                {
                    // Quick bounce: 0.15s up, 0.15s down
                    float bounceDuration = 0.15f;
                    float bounceScale = 1.08f;

                    // Scale up
                    float elapsed = 0f;
                    while (elapsed < bounceDuration)
                    {
                        elapsed += Time.deltaTime;
                        float t = Mathf.Clamp01(elapsed / bounceDuration);
                        float ease = t * (2f - t); // ease-out quad
                        rectTransform.localScale = originalScale * Mathf.Lerp(1f, bounceScale, ease);
                        yield return null;
                    }

                    // Scale back down
                    elapsed = 0f;
                    while (elapsed < bounceDuration)
                    {
                        elapsed += Time.deltaTime;
                        float t = Mathf.Clamp01(elapsed / bounceDuration);
                        float ease = t * t; // ease-in quad
                        rectTransform.localScale = originalScale * Mathf.Lerp(bounceScale, 1f, ease);
                        yield return null;
                    }

                    rectTransform.localScale = originalScale;
                }

                // Wait 2-3 seconds before next bounce
                yield return new WaitForSeconds(UnityEngine.Random.Range(2f, 3f));
            }
        }

        /// <summary>
        /// Appear animation: scale pop from small to overshoot to settle.
        /// Runs once when the card first appears in the dock.
        /// </summary>
        private IEnumerator AppearAnimationRoutine()
        {
            float duration = 0.4f;
            float elapsed = 0f;

            Vector3 startScale = originalScale * 0.3f;
            Vector3 overshootScale = originalScale * 1.12f;
            Vector3 targetScale = originalScale;

            if (rectTransform != null)
                rectTransform.localScale = startScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Overshoot ease: quick ramp up with a bounce
                float scale;
                if (t < 0.6f)
                {
                    // Fast grow to overshoot
                    float subT = t / 0.6f;
                    subT = 1f - (1f - subT) * (1f - subT); // ease-out quad
                    scale = Mathf.Lerp(0.3f, 1.12f, subT);
                }
                else
                {
                    // Settle from overshoot to target
                    float subT = (t - 0.6f) / 0.4f;
                    subT = subT * subT * (3f - 2f * subT); // ease-in-out
                    scale = Mathf.Lerp(1.12f, 1f, subT);
                }

                if (rectTransform != null)
                    rectTransform.localScale = originalScale * scale;

                yield return null;
            }

            if (rectTransform != null)
                rectTransform.localScale = originalScale;

            // Start idle animation after appear settles
            StartIdleAnimation();
        }

        // ── Debug ────────────────────────────────────────────────────

        /// <summary>Logs the full element tree to the console.</summary>
        public void DebugLogHierarchy()
        {
            Debug.Log($"[GameCardUI] {elementList.Count} elements indexed:");
            foreach (var elem in elementList)
                Debug.Log($"  {elem.path} {elem.components}");
        }
    }
}
