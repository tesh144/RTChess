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

        [Header("Desaturation Settings")]
        [Tooltip("Saturation amount before the first interaction (0 = grayscale, 1 = full color)")]
        [Range(0f,1f)]
        public float DesaturatedValue = 0.5f;

        [Tooltip("Saturation amount after the first interaction")]
        [Range(0f,1f)]
        public float FullColorValue = 1f;

        [Tooltip("Seconds for the transition from desaturated to full color")]
        public float TransitionDuration = 0.3f;

        private Renderer[] renderers;
        private MaterialPropertyBlock mpb;
        private bool hasColorized;

        void Start()
        {
            renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            mpb = new MaterialPropertyBlock();

            // Apply initial desaturation to all renderers
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].GetPropertyBlock(mpb);
                mpb.SetFloat(SaturationID, DesaturatedValue);
                renderers[i].SetPropertyBlock(mpb);
            }
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
