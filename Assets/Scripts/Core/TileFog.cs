using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Controls per-tile fog state. Tiles start lowered (fogged).
    /// When revealed, they tween upward to normal position.
    /// Does NOT modify tile materials — the prefab's own materials/shaders handle visuals.
    /// </summary>
    public class TileFog : MonoBehaviour
    {
        [Header("Fog Settings")]
        [SerializeField] private float fogDropDistance = 1.5f; // How far below normal position fogged tiles sit
        [SerializeField] private float revealDuration = 0.6f; // Tween duration in seconds

        // State
        private float revealedY;   // Normal tile Y (top at ground level)
        private float foggedY;     // Lowered Y (in fog)
        private bool isRevealed;
        private bool isAnimating;
        private float animationProgress; // 0 = fogged, 1 = revealed

        /// <summary>
        /// Initialize fog state. Called by GridManager after tile creation.
        /// </summary>
        public void InitializeFog(float normalY, float dropDistance)
        {
            fogDropDistance = dropDistance;
            revealedY = normalY;
            foggedY = normalY - fogDropDistance;
            isRevealed = false;
            isAnimating = false;
            animationProgress = 0f;

            // Move tile to fogged position immediately
            Vector3 pos = transform.position;
            pos.y = foggedY;
            transform.position = pos;
        }

        /// <summary>
        /// Start the reveal animation (tween from fogged to revealed).
        /// </summary>
        public void Reveal()
        {
            if (isRevealed) return;
            isRevealed = true;
            isAnimating = true;
        }

        /// <summary>
        /// Instantly reveal without animation (for starting tiles).
        /// </summary>
        public void RevealImmediate()
        {
            isRevealed = true;
            isAnimating = false;
            animationProgress = 1f;

            Vector3 pos = transform.position;
            pos.y = revealedY;
            transform.position = pos;
        }

        private void Update()
        {
            if (!isAnimating) return;

            animationProgress += Time.deltaTime / revealDuration;

            if (animationProgress >= 1f)
            {
                animationProgress = 1f;
                isAnimating = false;
            }

            // Smooth ease-out curve
            float t = 1f - Mathf.Pow(1f - animationProgress, 3f);

            // Lerp Y position
            Vector3 pos = transform.position;
            pos.y = Mathf.Lerp(foggedY, revealedY, t);
            transform.position = pos;
        }

        public bool IsRevealed => isRevealed;
    }
}
