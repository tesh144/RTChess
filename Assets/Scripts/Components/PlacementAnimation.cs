using UnityEngine;
using System.Collections;

namespace ClockworkGrid
{
    /// <summary>
    /// CODE-BASED placement animation (NOT CURRENTLY USED).
    ///
    /// Currently, placement animations use Unity's Animator system with Unit_Appear animation.
    /// This script remains available for future use with objects that need code-based animations
    /// or for objects without Animator components.
    ///
    /// Animates object falling from above with scale and shadow fade-in.
    /// </summary>
    public class PlacementAnimation : MonoBehaviour
    {
        [Header("Animation Settings")]
        [SerializeField] private float dropHeight = 2f;
        [SerializeField] private float dropDuration = 0.4f;
        [SerializeField] private AnimationCurve dropCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Scale Animation")]
        [SerializeField] private bool animateScale = true;
        [SerializeField] private float startScale = 0.3f;
        [SerializeField] private float overshootScale = 1.1f;

        [Header("Shadow Fade")]
        [SerializeField] private bool fadeShadows = true;
        [SerializeField] private float shadowFadeDuration = 0.3f;

        private Vector3 targetPosition;
        private Vector3 finalScale;
        private bool isAnimating = false;
        private Animator cachedAnimator;
        private bool animatorWasEnabled;

        /// <summary>
        /// Start the placement animation from a drop height above the target position.
        /// </summary>
        public void StartPlacementAnimation(Vector3 targetWorldPosition)
        {
            if (isAnimating) return;

            // Disable Animator if present to prevent conflicts
            cachedAnimator = GetComponent<Animator>();
            if (cachedAnimator != null)
            {
                animatorWasEnabled = cachedAnimator.enabled;
                cachedAnimator.enabled = false;
            }

            targetPosition = targetWorldPosition;
            finalScale = transform.localScale;

            // Start above the target position
            transform.position = targetPosition + Vector3.up * dropHeight;

            // Start small if scale animation enabled
            if (animateScale)
            {
                transform.localScale = finalScale * startScale;
            }

            // Fade in shadows
            if (fadeShadows)
            {
                SetShadowAlpha(0f);
            }

            StartCoroutine(AnimatePlacement());
        }

        private IEnumerator AnimatePlacement()
        {
            isAnimating = true;
            float elapsed = 0f;

            Vector3 startPos = transform.position;

            while (elapsed < dropDuration)
            {
                float t = elapsed / dropDuration;
                float curveT = dropCurve.Evaluate(t);

                // Animate position (drop down)
                transform.position = Vector3.Lerp(startPos, targetPosition, curveT);

                // Animate scale (grow from small, overshoot, settle)
                if (animateScale)
                {
                    float scaleT = t;
                    float scale;

                    if (scaleT < 0.7f)
                    {
                        // Grow phase
                        float growT = scaleT / 0.7f;
                        scale = Mathf.Lerp(startScale, overshootScale, growT * growT);
                    }
                    else
                    {
                        // Settle phase
                        float settleT = (scaleT - 0.7f) / 0.3f;
                        scale = Mathf.Lerp(overshootScale, 1f, settleT);
                    }

                    transform.localScale = finalScale * scale;
                }

                // Fade in shadows
                if (fadeShadows && elapsed < shadowFadeDuration)
                {
                    float shadowT = elapsed / shadowFadeDuration;
                    SetShadowAlpha(shadowT);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Ensure final position and scale
            transform.position = targetPosition;
            transform.localScale = finalScale;

            if (fadeShadows)
            {
                SetShadowAlpha(1f);
            }

            // Re-enable Animator if it was disabled
            if (cachedAnimator != null && animatorWasEnabled)
            {
                cachedAnimator.enabled = true;
            }

            isAnimating = false;

            // Destroy this component after animation completes
            Destroy(this);
        }

        /// <summary>
        /// Set alpha on shadow materials (materials with "shadow" in name).
        /// </summary>
        private void SetShadowAlpha(float alpha)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r.name.ToLower().Contains("shadow"))
                {
                    Material mat = r.material;
                    if (mat != null)
                    {
                        Color c = mat.color;
                        c.a = alpha;
                        mat.color = c;

                        // Enable transparency if not already
                        if (alpha < 1f && mat.GetInt("_SrcBlend") != (int)UnityEngine.Rendering.BlendMode.SrcAlpha)
                        {
                            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                            mat.SetInt("_ZWrite", 0);
                            mat.DisableKeyword("_ALPHATEST_ON");
                            mat.EnableKeyword("_ALPHABLEND_ON");
                            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                            mat.renderQueue = 3000;
                        }
                    }
                }
            }
        }
    }
}
