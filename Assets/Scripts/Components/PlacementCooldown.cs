using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Component attached to units placed from dock.
    /// Prevents unit from acting for N intervals (cooldown period).
    /// Provides visual feedback during cooldown.
    /// </summary>
    public class PlacementCooldown : MonoBehaviour
    {
        private int remainingIntervals;
        private Renderer[] renderers;
        private Color[] originalColors;
        private TextMesh hpTextMesh;
        private TextMesh hpTextShadow;

        public bool IsOnCooldown => remainingIntervals > 0;

        /// <summary>
        /// Start the placement cooldown
        /// </summary>
        public void StartCooldown(int intervals)
        {
            remainingIntervals = intervals;
            CacheRenderers();
            ApplyGreyedOutEffect();

            // Subscribe to interval timer
            if (IntervalTimer.Instance != null)
            {
                IntervalTimer.Instance.OnIntervalTick += OnIntervalTick;
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from interval timer
            if (IntervalTimer.Instance != null)
            {
                IntervalTimer.Instance.OnIntervalTick -= OnIntervalTick;
            }
        }

        private void OnIntervalTick(int intervalCount)
        {
            remainingIntervals--;

            if (remainingIntervals <= 0)
            {
                EndCooldown();
            }
        }

        private void CacheRenderers()
        {
            renderers = GetComponentsInChildren<Renderer>();
            originalColors = new Color[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].material != null)
                {
                    originalColors[i] = renderers[i].material.color;
                }
            }

            // Cache HP text meshes to preserve their colors
            TextMesh[] textMeshes = GetComponentsInChildren<TextMesh>();
            foreach (TextMesh tm in textMeshes)
            {
                if (tm.name == "HPText")
                    hpTextMesh = tm;
                else if (tm.name == "HPTextShadow")
                    hpTextShadow = tm;
            }
        }

        private void ApplyGreyedOutEffect()
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;

                // Don't grey out HP text
                if (renderers[i].name == "HPText" || renderers[i].name == "HPTextShadow")
                    continue;

                // Darken the color to 50%
                Color c = renderers[i].material.color;
                c.r *= 0.5f;
                c.g *= 0.5f;
                c.b *= 0.5f;
                renderers[i].material.color = c;
            }
        }

        private void RestoreOriginalColors()
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;

                // Don't restore HP text colors (they manage themselves)
                if (renderers[i].name == "HPText" || renderers[i].name == "HPTextShadow")
                    continue;

                if (i < originalColors.Length)
                {
                    renderers[i].material.color = originalColors[i];
                }
            }
        }

        private void EndCooldown()
        {
            RestoreOriginalColors();

            // Unsubscribe from interval timer
            if (IntervalTimer.Instance != null)
            {
                IntervalTimer.Instance.OnIntervalTick -= OnIntervalTick;
            }

            // Remove this component
            Destroy(this);
        }
    }
}
