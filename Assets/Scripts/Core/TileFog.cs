using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Controls per-tile fog state. Tiles start lowered + scaled down (fogged).
    /// When revealed, they tween upward with a scale overshoot + brief highlight flash.
    /// </summary>
    public class TileFog : MonoBehaviour
    {
        [Header("Fog Settings")]
        [SerializeField] private float fogDropDistance = 1.5f;
        [SerializeField] private float revealDuration = 0.6f;
        [SerializeField] private float revealDurationVariation = 0.35f;

        // State
        private float revealedY;
        private float foggedY;
        private float actualRevealDuration;
        private bool isRevealed;
        private bool isAnimating;
        private float animationProgress; // 0 = fogged, 1 = revealed

        // Reveal visual state
        private Renderer tileRenderer;
        private MaterialPropertyBlock propBlock;
        private bool hasRenderer;
        private Vector3 baseScale;

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

            // Cache renderer for highlight flash
            tileRenderer = GetComponentInChildren<Renderer>();
            hasRenderer = tileRenderer != null;
            if (hasRenderer)
                propBlock = new MaterialPropertyBlock();

            baseScale = transform.localScale;

            // Move tile to fogged position + shrunk
            Vector3 pos = transform.position;
            pos.y = foggedY;
            transform.position = pos;
            transform.localScale = baseScale * 0.85f;
        }

        /// <summary>
        /// Start the reveal animation (tween from fogged to revealed).
        /// </summary>
        public void Reveal()
        {
            if (isRevealed) return;
            isRevealed = true;
            isAnimating = true;
            actualRevealDuration = revealDuration + Random.Range(-revealDurationVariation, revealDurationVariation);
            actualRevealDuration = Mathf.Max(actualRevealDuration, 0.15f);
        }

        /// <summary>
        /// Instantly reveal without animation (for starting tiles).
        /// </summary>
        public void RevealImmediate()
        {
            isRevealed = true;
            isAnimating = false;
            animationProgress = 1f;

            if (baseScale == Vector3.zero)
                baseScale = transform.localScale;

            Vector3 pos = transform.position;
            pos.y = revealedY;
            transform.position = pos;
            transform.localScale = baseScale;
        }

        private void Update()
        {
            if (!isAnimating) return;

            animationProgress += Time.deltaTime / actualRevealDuration;

            if (animationProgress >= 1f)
            {
                animationProgress = 1f;
                isAnimating = false;
                transform.localScale = baseScale;
                ClearHighlight();
            }

            // Smooth ease-out curve
            float t = 1f - Mathf.Pow(1f - animationProgress, 3f);

            // Lerp Y position
            Vector3 pos = transform.position;
            pos.y = Mathf.Lerp(foggedY, revealedY, t);
            transform.position = pos;

            // Scale: 0.85 → overshoot 1.04 → settle 1.0
            float scale;
            if (t < 0.7f)
            {
                float st = t / 0.7f;
                scale = Mathf.Lerp(0.85f, 1.04f, st);
            }
            else
            {
                float st = (t - 0.7f) / 0.3f;
                scale = Mathf.Lerp(1.04f, 1f, st);
            }
            transform.localScale = baseScale * scale;

            // Brief white highlight flash: peaks at t=0.3, fades out by t=0.7
            if (hasRenderer)
            {
                float highlightT = 0f;
                if (t < 0.3f)
                    highlightT = t / 0.3f; // fade in
                else if (t < 0.7f)
                    highlightT = 1f - (t - 0.3f) / 0.4f; // fade out
                // else 0

                if (highlightT > 0.001f)
                {
                    Color highlight = new Color(highlightT * 0.3f, highlightT * 0.3f, highlightT * 0.3f, 0f);
                    tileRenderer.GetPropertyBlock(propBlock);
                    propBlock.SetColor("_EmissionColor", highlight);
                    tileRenderer.SetPropertyBlock(propBlock);
                }
                else
                {
                    ClearHighlight();
                }
            }
        }

        private void ClearHighlight()
        {
            if (!hasRenderer) return;
            tileRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor("_EmissionColor", Color.black);
            tileRenderer.SetPropertyBlock(propBlock);
        }

        public bool IsRevealed => isRevealed;
    }
}
