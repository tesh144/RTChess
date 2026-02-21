using UnityEngine;
using System.Collections;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// The clockwork brain for active grid entities.
    /// Subscribes to IntervalTimer and each tick: rotates, scans facing direction,
    /// and interacts with whatever it finds (strong or weak interaction).
    ///
    /// Attach to any placed object with isActive=true in its database entry.
    /// Works alongside GridEntityHealth (for HP/damage) and FurnitureObject (for grid state).
    /// </summary>
    public class GridEntityActor : MonoBehaviour
    {
        [Header("Clockwork Settings")]
        [SerializeField] private Facing currentFacing = Facing.North;
        [SerializeField] private int attackRange = 1;
        [SerializeField] private int attackIntervalMultiplier = 1;

        [Header("Rotation")]
        [SerializeField] private bool rotateClockwise = true;

        // Cached references
        private FurnitureObject furnitureObject;
        private GridEntityHealth health;
        private Animator animator;
        private Transform animatorHolder;

        // Rotation animation
        private Coroutine rotationCoroutine;
        private const float ROTATION_DURATION = 0.25f;

        // Interaction animation timing
        private Coroutine interactionCoroutine;
        private const float INTERACTION_DELAY = 0.1f; // Delay after rotation before scanning

        // State
        private bool isInitialized = false;

        // --- Public Accessors ---
        public Facing CurrentFacing => currentFacing;
        public int AttackRange => attackRange;

        // ---------------------------------------------------------------
        // Initialization
        // ---------------------------------------------------------------

        /// <summary>
        /// Configure actor from database values. Called by GridEntityManager after attaching.
        /// </summary>
        public void Initialize(bool clockwise = true, int range = 1, int intervalMultiplier = 1)
        {
            rotateClockwise = clockwise;
            attackRange = range;
            attackIntervalMultiplier = intervalMultiplier;
            currentFacing = Facing.North;

            CacheReferences();
            ApplyFacingRotation(instant: true);

            isInitialized = true;
            Debug.Log($"[GridEntityActor] {gameObject.name} initialized: facing={currentFacing}, range={attackRange}, clockwise={rotateClockwise}");
        }

        private void CacheReferences()
        {
            furnitureObject = GetComponent<FurnitureObject>();
            health = GetComponent<GridEntityHealth>();

            // Find the AnimatorHolder child (PEPO prefab convention)
            animatorHolder = transform.Find("AnimatorHolder");
            if (animatorHolder != null)
            {
                animator = animatorHolder.GetComponent<Animator>();
            }

            if (furnitureObject == null)
                Debug.LogWarning($"[GridEntityActor] No FurnitureObject on {gameObject.name} — grid position unknown");
            if (animatorHolder == null)
                Debug.LogWarning($"[GridEntityActor] No AnimatorHolder on {gameObject.name} — animations won't play");
        }

        // ---------------------------------------------------------------
        // IntervalTimer Subscription
        // ---------------------------------------------------------------

        private void OnEnable()
        {
            if (IntervalTimer.Instance != null)
            {
                IntervalTimer.Instance.OnIntervalTick += OnIntervalTick;
            }
        }

        private void OnDisable()
        {
            if (IntervalTimer.Instance != null)
            {
                IntervalTimer.Instance.OnIntervalTick -= OnIntervalTick;
            }

            // Stop any running coroutines
            if (rotationCoroutine != null)
            {
                StopCoroutine(rotationCoroutine);
                rotationCoroutine = null;
            }
            if (interactionCoroutine != null)
            {
                StopCoroutine(interactionCoroutine);
                interactionCoroutine = null;
            }
        }

        // ---------------------------------------------------------------
        // Clockwork Tick
        // ---------------------------------------------------------------

        private void OnIntervalTick(int intervalCount)
        {
            if (!isInitialized) return;
            if (health != null && health.IsDestroyed) return;

            // Respect interval multiplier (e.g., only act every 2nd tick)
            if (attackIntervalMultiplier > 1 && intervalCount % attackIntervalMultiplier != 0)
                return;

            // The clockwork sequence: rotate → wait → scan → interact
            if (interactionCoroutine != null)
                StopCoroutine(interactionCoroutine);
            interactionCoroutine = StartCoroutine(ClockworkTickCoroutine());
        }

        /// <summary>
        /// Full clockwork tick sequence: rotate, brief pause, scan, interact.
        /// </summary>
        private IEnumerator ClockworkTickCoroutine()
        {
            // Step 1: Rotate to next facing
            Rotate();

            // Step 2: Wait for rotation animation to mostly complete
            yield return new WaitForSeconds(ROTATION_DURATION + INTERACTION_DELAY);

            // Step 3: Scan and interact
            ScanAndInteract();

            interactionCoroutine = null;
        }

        // ---------------------------------------------------------------
        // Rotation
        // ---------------------------------------------------------------

        private void Rotate()
        {
            // Advance facing direction
            currentFacing = rotateClockwise
                ? currentFacing.RotateClockwise()
                : currentFacing.RotateCounterClockwise();

            // Animate the rotation smoothly
            ApplyFacingRotation(instant: false);
        }

        /// <summary>
        /// Apply the current facing as a Y rotation on the AnimatorHolder.
        /// </summary>
        private void ApplyFacingRotation(bool instant)
        {
            if (animatorHolder == null) return;

            float targetYRotation = currentFacing.ToYRotation();
            Quaternion targetRotation = Quaternion.Euler(0f, targetYRotation, 0f);

            if (instant)
            {
                animatorHolder.localRotation = targetRotation;
                return;
            }

            // Smooth animated rotation
            if (rotationCoroutine != null)
                StopCoroutine(rotationCoroutine);
            rotationCoroutine = StartCoroutine(RotateCoroutine(targetRotation));
        }

        private IEnumerator RotateCoroutine(Quaternion targetRotation)
        {
            Quaternion startRotation = animatorHolder.localRotation;
            float elapsed = 0f;

            while (elapsed < ROTATION_DURATION)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / ROTATION_DURATION);

                // Ease-in-out curve
                float easedT = t * t * (3f - 2f * t);
                animatorHolder.localRotation = Quaternion.Slerp(startRotation, targetRotation, easedT);

                yield return null;
            }

            // Snap to exact final rotation
            animatorHolder.localRotation = targetRotation;
            rotationCoroutine = null;
        }

        // ---------------------------------------------------------------
        // Scan & Interact
        // ---------------------------------------------------------------

        /// <summary>
        /// Look in the facing direction, find the first occupant, and interact.
        /// </summary>
        private void ScanAndInteract()
        {
            if (furnitureObject == null) return;

            GridManager gm = GridManager.Instance;
            if (gm == null) return;

            // Get grid offset for current facing
            currentFacing.ToGridOffset(out int dx, out int dy);

            int startX = furnitureObject.GridX;
            int startY = furnitureObject.GridY;

            // Walk forward cell by cell up to attack range
            for (int step = 1; step <= attackRange; step++)
            {
                int checkX = startX + (dx * step);
                int checkY = startY + (dy * step);

                // Bounds check
                if (checkX < 0 || checkX >= gm.Width || checkY < 0 || checkY >= gm.Height)
                    break;

                GameObject occupant = gm.GetCellOccupant(checkX, checkY);
                if (occupant == null) continue;

                // Found something — try to interact with it
                GridEntityHealth targetHealth = occupant.GetComponent<GridEntityHealth>();

                if (targetHealth != null && !targetHealth.IsDestroyed)
                {
                    // Valid target — strong interaction (attack)
                    PerformStrongInteraction(targetHealth, checkX, checkY);
                    return;
                }
                else
                {
                    // Occupant exists but can't be damaged — weak interaction
                    PerformWeakInteraction(checkX, checkY);
                    return;
                }
            }

            // Nothing found in range — do nothing (idle)
        }

        // ---------------------------------------------------------------
        // Interactions
        // ---------------------------------------------------------------

        /// <summary>
        /// Strong interaction: valid target. Play attack animation and deal damage.
        /// </summary>
        private void PerformStrongInteraction(GridEntityHealth target, int targetX, int targetY)
        {
            // Face the target (in case of multi-cell objects or range > 1)
            FaceTarget(targetX, targetY);

            // Play interact_strong animation
            if (animator != null)
            {
                animator.SetTrigger("interact_strong");
            }

            // Deal damage using our attack power
            int attackPower = health != null ? health.AttackPower : 1;
            int damageDealt = target.TakeDamage(attackPower);

            Debug.Log($"[GridEntityActor] {gameObject.name} → STRONG interact → {target.gameObject.name} for {damageDealt} damage (target HP: {target.CurrentHP}/{target.MaxHP})");
        }

        /// <summary>
        /// Weak interaction: occupant exists but can't be damaged.
        /// </summary>
        private void PerformWeakInteraction(int targetX, int targetY)
        {
            // Face the target
            FaceTarget(targetX, targetY);

            // Play interact_weak animation
            if (animator != null)
            {
                animator.SetTrigger("interact_weak");
            }

            Debug.Log($"[GridEntityActor] {gameObject.name} → WEAK interact → cell ({targetX},{targetY})");
        }

        /// <summary>
        /// Rotate the AnimatorHolder to face a specific grid cell.
        /// Used before playing interaction animations (all objects face along local Z).
        /// </summary>
        private void FaceTarget(int targetX, int targetY)
        {
            if (animatorHolder == null || furnitureObject == null) return;

            GridManager gm = GridManager.Instance;
            if (gm == null) return;

            Vector3 myWorldPos = gm.GridToWorldPosition(furnitureObject.GridX, furnitureObject.GridY);
            Vector3 targetWorldPos = gm.GridToWorldPosition(targetX, targetY);

            Vector3 direction = (targetWorldPos - myWorldPos).normalized;
            if (direction.sqrMagnitude > 0.001f)
            {
                // Snap rotation to face target (the interaction animation handles the visual movement)
                animatorHolder.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
        }
    }
}
