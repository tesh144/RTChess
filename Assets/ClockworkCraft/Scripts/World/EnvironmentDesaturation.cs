#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System.Collections;

namespace ClockworkCraft
{
    /// <summary>
    /// Makes environment objects appear 50% desaturated until a worker first interacts with them.
    /// On first hit, transitions to full colour over 0.3s using MaterialPropertyBlock.
    ///
    /// All game materials use Custom/UnlitSaturation which exposes a _Saturation property.
    /// This component drives that property per-renderer via MaterialPropertyBlock — no material
    /// instances created, GPU batching preserved.
    ///
    /// Attached by GridEntityManager.AttachFromEnvironmentData().
    /// Triggered by GridEntityHealth.TakeDamage() calling Colorize().
    /// </summary>
    public class EnvironmentDesaturation : MonoBehaviour
    {
        private static readonly int SaturationID = Shader.PropertyToID("_Saturation");
        private static readonly int TintColorID = Shader.PropertyToID("_TintColor");
        private static readonly int TintStrengthID = Shader.PropertyToID("_TintStrength");

        [Header("Desaturation Settings")]
        [Tooltip("Saturation amount before the first interaction (0 = grayscale, 1 = full color)")]
        [Range(0f,1f)]
        public float DesaturatedValue = 0.65f;

        [Tooltip("Saturation amount after the first interaction")]
        [Range(0f,1f)]
        public float FullColorValue = 1f;

        [Tooltip("Seconds for the transition from desaturated to full color")]
        public float TransitionDuration = 0.3f;

        [Header("Tint")]
        [Tooltip("Color to tint this object (e.g. pink for corruption). White = no tint.")]
        public Color TintColor = Color.white;

        [Tooltip("Strength of the tint overlay (0 = none, 1 = full tint)")]
        [Range(0f,1f)]
        public float TintStrength = 0f;

        private Renderer[] renderers;
        private MaterialPropertyBlock mpb;
        private bool hasColorized;
        public bool HasColorized => hasColorized;

        void Start()
        {
            renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            mpb = new MaterialPropertyBlock();

            // Apply initial desaturation and tint to all renderers
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].GetPropertyBlock(mpb);
                mpb.SetFloat(SaturationID, DesaturatedValue);
                mpb.SetColor(TintColorID, TintColor);
                mpb.SetFloat(TintStrengthID, TintStrength);
                renderers[i].SetPropertyBlock(mpb);
            }
        }

        /// <summary>
        /// Apply a tint color and strength at runtime. Use to mark objects as corrupted, etc.
        /// </summary>
        public void SetTint(Color color, float strength)
        {
            TintColor = color;
            TintStrength = strength;

            if (renderers == null || mpb == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].GetPropertyBlock(mpb);
                mpb.SetColor(TintColorID, color);
                mpb.SetFloat(TintStrengthID, strength);
                renderers[i].SetPropertyBlock(mpb);
            }
        }

        /// <summary>
        /// Remove any tint. Resets to white / strength 0.
        /// </summary>
        public void ClearTint()
        {
            SetTint(Color.white, 0f);
        }

        /// <summary>
        /// Transition from desaturated to full colour. Called on first hit.
        /// Safe to call multiple times — only triggers once.
        /// </summary>
        public void Colorize()
        {
            if (hasColorized) return;
            if (renderers == null || renderers.Length == 0) return;

            hasColorized = true;
            StartCoroutine(ColorizeCoroutine());
        }

        private IEnumerator ColorizeCoroutine()
        {
            float elapsed = 0f;

            while (elapsed < TransitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / TransitionDuration);
                float eased = 1f - (1f - t) * (1f - t);
                float saturation = Mathf.Lerp(DesaturatedValue, FullColorValue, eased);

                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    renderers[i].GetPropertyBlock(mpb);
                    mpb.SetFloat(SaturationID, saturation);
                    renderers[i].SetPropertyBlock(mpb);
                }

                yield return null;
            }

            // Snap to final value
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].GetPropertyBlock(mpb);
                mpb.SetFloat(SaturationID, FullColorValue);
                renderers[i].SetPropertyBlock(mpb);
            }
        }
    }
}
