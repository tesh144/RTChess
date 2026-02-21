using UnityEngine;
using System.Collections;

namespace LittleCafe
{
    /// <summary>
    /// Manages animator lifecycle to prevent interference with rotation and other systems.
    /// After animation completes, disables the animator to avoid continuous updates.
    /// Attach to the object that has the Animator component or parent of AnimatorHolder.
    /// </summary>
    public class AnimatorLifecycleManager : MonoBehaviour
    {
        [SerializeField] private float animationDuration = 0.8f; // Match your Appear animation duration
        private Animator animator;
        private bool animationPlayed = false;

        private void Start()
        {
            // Try to find animator on this object or children
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                Transform animatorHolder = transform.Find("AnimatorHolder");
                if (animatorHolder != null)
                {
                    animator = animatorHolder.GetComponent<Animator>();
                }
            }
        }

        /// <summary>
        /// Call this when starting a placement animation.
        /// </summary>
        public void PlayPlacementAnimation()
        {
            if (animator == null) return;

            animationPlayed = true;
            animator.enabled = true;
            animator.SetTrigger("appear");

            // Schedule animator disable after animation completes
            StartCoroutine(DisableAnimatorAfterDelay(animationDuration));
        }

        /// <summary>
        /// Wait for animation to complete, then disable animator.
        /// </summary>
        private IEnumerator DisableAnimatorAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay + 0.1f); // Add small buffer

            if (animator != null)
            {
                // Disable the animator to stop continuous updates
                animator.enabled = false;
                Debug.Log($"[AnimatorLifecycleManager] Disabled animator on {gameObject.name} after animation completed");
            }
        }

        /// <summary>
        /// Re-enable animator if needed for future animations.
        /// </summary>
        public void EnableAnimator()
        {
            if (animator != null)
            {
                animator.enabled = true;
            }
        }
    }
}
