using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Visual component for fog overlay on a single grid cell.
    /// Fades out when cell is revealed and destroys itself.
    /// Iteration 7: Fog of War & Scouting
    /// </summary>
    public class FogVisual : MonoBehaviour
    {
        [Header("Fog Settings")]
        [SerializeField] private float fadeOutDuration = 0.3f;
        [SerializeField] private float fogOpacity = 0.85f;

        private Renderer fogRenderer;
        private Material fogMaterial;
        private bool isRevealing = false;
        private float revealTimer = 0f;
        private Color startColor;
        private Color targetColor;

        private void Awake()
        {
            fogRenderer = GetComponent<Renderer>();
            if (fogRenderer != null)
            {
                // Create instance material to avoid affecting other fog visuals
                fogMaterial = new Material(fogRenderer.sharedMaterial);
                fogRenderer.material = fogMaterial;

                // Set initial fog color (dark grey with high opacity)
                Color fogColor = new Color(0.1f, 0.1f, 0.1f, fogOpacity);
                fogMaterial.color = fogColor;
                startColor = fogColor;
                targetColor = new Color(fogColor.r, fogColor.g, fogColor.b, 0f); // Fully transparent
            }
        }

        private void Update()
        {
            if (isRevealing)
            {
                revealTimer += Time.deltaTime;
                float t = Mathf.Clamp01(revealTimer / fadeOutDuration);

                // Smooth ease-out
                t = 1f - (1f - t) * (1f - t);

                if (fogMaterial != null)
                {
                    Color currentColor = Color.Lerp(startColor, targetColor, t);
                    fogMaterial.color = currentColor;
                }

                // Destroy after fade completes
                if (t >= 1f)
                {
                    Destroy(gameObject);
                }
            }
        }

        /// <summary>
        /// Start reveal animation (fade out)
        /// </summary>
        public void Reveal()
        {
            if (!isRevealing)
            {
                isRevealing = true;
                revealTimer = 0f;
            }
        }

        /// <summary>
        /// Set custom fade duration
        /// </summary>
        public void SetFadeDuration(float duration)
        {
            fadeOutDuration = duration;
        }

        /// <summary>
        /// Set custom fog opacity
        /// </summary>
        public void SetFogOpacity(float opacity)
        {
            fogOpacity = Mathf.Clamp01(opacity);
            if (fogMaterial != null)
            {
                Color c = fogMaterial.color;
                c.a = fogOpacity;
                fogMaterial.color = c;
                startColor = c;
                targetColor = new Color(c.r, c.g, c.b, 0f);
            }
        }

        private void OnDestroy()
        {
            // Clean up material instance
            if (fogMaterial != null)
            {
                Destroy(fogMaterial);
            }
        }
    }
}
