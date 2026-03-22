#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// Lightweight component that makes allied buildings (and other non-active allied objects)
    /// do a subtle idle bounce animation each interval tick. This keeps the world feeling alive
    /// even for objects that don't have a GridEntityActor.
    ///
    /// Attached by GridEntityManager when allied=true and isActive=false.
    /// Uses the "idle_bounce" trigger on the Animator.
    /// </summary>
    public class BuildingBounceEffect : MonoBehaviour
    {
        private Animator animator;
        private int tickCounter = 0;

        void Start()
        {
            // Find animator — check AnimatorHolder child first (PEPO prefab structure)
            Transform animHolder = transform.Find("AnimatorHolder");
            if (animHolder != null)
                animator = animHolder.GetComponent<Animator>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (animator == null)
            {
                // No animator — this component is useless, remove it
                Destroy(this);
                return;
            }

            // Subscribe to interval timer
            if (IntervalTimer.Instance != null)
                IntervalTimer.Instance.OnIntervalTick += OnTick;
        }

        void OnDestroy()
        {
            if (IntervalTimer.Instance != null)
                IntervalTimer.Instance.OnIntervalTick -= OnTick;
        }

        private void OnTick(int intervalCount)
        {
            if (animator == null) return;

            // Bounce every tick to keep the world feeling alive
            tickCounter++;
            animator.SetTrigger("idle_bounce");
        }
    }
}
