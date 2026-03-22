#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System.Collections;
using ClockworkGrid;
using TMPro;

namespace LittleCafe
{
    /// <summary>
    /// The clockwork brain for active grid entities.
    /// Subscribes to IntervalTimer and each tick executes a behavior pattern.
    ///
    /// Behavior patterns (selected via BehaviorType):
    ///   RotateAndInteract — Worker-style: rotate, scan facing direction, attack/interact with target.
    ///   RotateAndMove     — Animal-style: rotate, attempt to move one cell forward.
    ///   RotateRotateMove  — Heavy beast: rotate twice, then attempt to move one cell forward.
    ///
    /// Starvation system (RotateAndInteract only):
    ///   Workers that idle for too many consecutive ticks will starve and die.
    ///   Phase 1 (grace): Silent idle ticks with no penalty.
    ///   Phase 2 (countdown): Red floating numbers (4, 3, 2, 1) appear above the worker.
    ///   If the worker interacts with anything during either phase, the counter fully resets.
    ///   When countdown reaches 0, the worker dies via TakeDamage(currentHP).
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

        [Header("Behavior")]
        [SerializeField] private BehaviorType behaviorType = BehaviorType.RotateAndInteract;

        [Header("Rotation")]
        [SerializeField] private bool rotateClockwise = true;

        [Header("Starvation")]
        [Tooltip("Number of idle ticks before countdown begins (silent grace period).")]
        [SerializeField] private int graceThreshold = 4;
        [Tooltip("Number of countdown ticks after grace before death (visible red numbers).")]
        [SerializeField] private int countdownThreshold = 4;

        [Header("Meal Buff")]
        [Tooltip("How long the meal buff lasts in real seconds. Converted to bar ticks on grant.")]
        [SerializeField] private float mealBuffDurationSeconds = 30f;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;

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
        private const float ATTACK_CONTACT_DELAY = 0.2f; // When the attack animation actually makes contact

        // Movement timing
        private const float MOVE_DURATION = 0.3f; // How long the smooth move takes

        // State
        private bool isInitialized = false;
        private bool isFirstTick = true; // Skip rotation on first tick so we act in initial facing
        private bool isMoving = false;   // Prevent overlapping moves
        private bool rrm_isRotateTick = true; // RotateRotateMove: alternates between rotate-only and rotate+move ticks

        // Starvation state
        private int idleTickCount = 0;
        private bool isStarving = false;

        // Meal buff state
        private bool hasMealBuff = false;
        private int mealBuffTicksRemaining = 0;

        // Starvation countdown — no persistent object; each tick spawns a popup

        // --- Public Accessors ---
        public Facing CurrentFacing => currentFacing;
        public int AttackRange => attackRange;
        public BehaviorType Behavior => behaviorType;

        /// <summary>True if this worker is dying from starvation (idle too long).</summary>
        public bool IsStarving => isStarving;

        /// <summary>True if this worker currently has a meal buff active.</summary>
        public bool HasMealBuff => hasMealBuff;

        /// <summary>Number of interval ticks remaining on the meal buff.</summary>
        public int MealBuffTicksRemaining => mealBuffTicksRemaining;

        /// <summary>Current consecutive idle ticks (resets on any interaction).</summary>
        public int IdleTickCount => idleTickCount;

        // ---------------------------------------------------------------
        // Initialization
        // ---------------------------------------------------------------

        /// <summary>
        /// Configure actor from database values. Called by GridEntityManager after attaching.
        /// </summary>
        public void Initialize(bool clockwise = true, int range = 1, int intervalMultiplier = 1,
            BehaviorType behavior = BehaviorType.RotateAndInteract)
        {
            rotateClockwise = clockwise;
            attackRange = range;
            attackIntervalMultiplier = intervalMultiplier;
            behaviorType = behavior;

            CacheReferences();

            // Face nearest valid target for interact behavior, random for move behaviors
            if (behaviorType == BehaviorType.RotateAndInteract)
            {
                currentFacing = FindBestInitialFacing();
            }
            else
            {
                // Random initial facing for wandering entities (RotateAndMove, RotateRotateMove)
                Facing[] facings = { Facing.North, Facing.East, Facing.South, Facing.West };
                currentFacing = facings[Random.Range(0, facings.Length)];
            }

            ApplyFacingRotation(instant: true);

            isInitialized = true;
            if (verboseLogging)
                Debug.Log($"[GridEntityActor] {gameObject.name} initialized: behavior={behaviorType}, facing={currentFacing}, range={attackRange}, clockwise={rotateClockwise}");
        }

        /// <summary>
        /// Scan all 4 cardinal directions for the nearest valid target (occupant with health).
        /// Returns the facing toward the closest one, or North if none found.
        /// </summary>
        private Facing FindBestInitialFacing()
        {
            if (furnitureObject == null) return Facing.North;

            GridManager gm = GridManager.Instance;
            if (gm == null) return Facing.North;

            int myX = furnitureObject.GridX;
            int myY = furnitureObject.GridY;

            Facing bestFacing = Facing.North;
            int bestDist = int.MaxValue;
            bool found = false;

            Facing[] allFacings = { Facing.North, Facing.East, Facing.South, Facing.West };
            foreach (var facing in allFacings)
            {
                facing.ToGridOffset(out int dx, out int dy);

                for (int step = 1; step <= attackRange; step++)
                {
                    int checkX = myX + (dx * step);
                    int checkY = myY + (dy * step);

                    if (checkX < 0 || checkX >= gm.Width || checkY < 0 || checkY >= gm.Height)
                        break;

                    GameObject occupant = gm.GetCellOccupant(checkX, checkY);
                    if (occupant == null) continue;

                    // Check if this occupant is interactable by workers
                    GridEntityHealth targetHealth = occupant.GetComponent<GridEntityHealth>();
                    if (targetHealth != null && !targetHealth.IsDestroyed)
                    {
                        // Allied entities (workers, buildings) — skip, look through them
                        if (targetHealth.IsAllied)
                            continue;

                        if (!targetHealth.WorkerCanInteract)
                            break; // Non-interactable — blocks this direction

                        if (step < bestDist)
                        {
                            bestDist = step;
                            bestFacing = facing;
                            found = true;
                        }
                        break; // Found closest in this direction
                    }
                    else if (!found && step < bestDist)
                    {
                        // Non-damageable occupant — use as fallback if no damageable targets found
                        bestDist = step;
                        bestFacing = facing;
                    }
                    break; // First occupant blocks further scanning in this direction
                }
            }

            return bestFacing;
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
                IntervalTimer.Instance.OnBar += OnBarTick;

                // Re-subscribe half-bar if the worker was disabled while buffed
                if (hasMealBuff)
                    IntervalTimer.Instance.OnHalfBar += OnHalfBarTick;
            }
        }

        private void OnDisable()
        {
            if (IntervalTimer.Instance != null)
            {
                IntervalTimer.Instance.OnBar -= OnBarTick;
                IntervalTimer.Instance.OnHalfBar -= OnHalfBarTick; // safe no-op if not subscribed
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

        private void OnBarTick(int bar)
        {
            if (!isInitialized) return;
            if (health != null && health.IsDestroyed) return;

            // Decay meal buff on every bar tick
            if (hasMealBuff)
            {
                mealBuffTicksRemaining--;
                if (mealBuffTicksRemaining <= 0)
                    ExpireMealBuff();

                // Deliberate: no bar-tick action while buffed.
                // OnHalfBarTick handles actions; last buffed action already fired via OnHalfBarTick.
                return;
            }

            // Not buffed — normal action cadence
            if (attackIntervalMultiplier > 1 && bar % attackIntervalMultiplier != 0)
                return;

            if (interactionCoroutine != null)
                StopCoroutine(interactionCoroutine);

            switch (behaviorType)
            {
                case BehaviorType.RotateAndInteract:
                    interactionCoroutine = StartCoroutine(ClockworkTickInteract());
                    break;
                case BehaviorType.RotateAndMove:
                    interactionCoroutine = StartCoroutine(ClockworkTickMove());
                    break;
                case BehaviorType.RotateRotateMove:
                    interactionCoroutine = StartCoroutine(ClockworkTickRotateRotateMove());
                    break;
                default:
                    interactionCoroutine = StartCoroutine(ClockworkTickInteract());
                    break;
            }
        }

        // ===============================================================
        // BEHAVIOR: RotateAndInteract (Worker pattern)
        // ===============================================================

        /// <summary>
        /// Worker tick: rotate, brief pause, scan, interact.
        /// On the very first tick, skip rotation so we attack in our initial facing direction.
        /// </summary>
        private IEnumerator ClockworkTickInteract()
        {
            if (isFirstTick)
            {
                // First tick: don't rotate, attack in the direction we chose at placement
                isFirstTick = false;
                yield return new WaitForSeconds(INTERACTION_DELAY);
            }
            else
            {
                // Step 1: Rotate to next facing
                Rotate();

                // Step 2: Wait for rotation animation to mostly complete
                yield return new WaitForSeconds(ROTATION_DURATION + INTERACTION_DELAY);
            }

            // Step 3: Scan and interact (yields to wait for attack contact timing)
            yield return StartCoroutine(ScanAndInteract());

            interactionCoroutine = null;
        }

        // ===============================================================
        // BEHAVIOR: RotateAndMove (Animal pattern)
        // ===============================================================

        /// <summary>
        /// Animal tick: rotate, then attempt to move one cell forward.
        /// If the cell ahead is empty, smoothly move into it.
        /// If blocked (occupied or out of bounds), idle until next tick.
        /// </summary>
        private IEnumerator ClockworkTickMove()
        {
            if (isMoving) yield break; // Don't overlap moves

            if (isFirstTick)
            {
                isFirstTick = false;
                yield return new WaitForSeconds(INTERACTION_DELAY);
            }
            else
            {
                // Step 1: Rotate to next facing
                Rotate();

                // Step 2: Wait for rotation to complete
                yield return new WaitForSeconds(ROTATION_DURATION + INTERACTION_DELAY);
            }

            // Step 3: Try to move one cell forward
            yield return StartCoroutine(TryMoveForward());

            interactionCoroutine = null;
        }

        // ===============================================================
        // BEHAVIOR: RotateRotateMove (Heavy beast pattern)
        // ===============================================================

        /// <summary>
        /// Heavy beast behavior spread across two ticks:
        ///   Tick A (rotate-only): rotate, then wait.
        ///   Tick B (rotate+move): rotate, then attempt to move one cell forward.
        /// This gives a slower, more deliberate movement pattern.
        /// </summary>
        private IEnumerator ClockworkTickRotateRotateMove()
        {
            if (isMoving) yield break;

            if (isFirstTick)
            {
                // First tick ever: skip rotation, just try to move in starting direction
                isFirstTick = false;
                yield return new WaitForSeconds(INTERACTION_DELAY);
                yield return StartCoroutine(TryMoveForward());
                interactionCoroutine = null;
                yield break;
            }

            if (rrm_isRotateTick)
            {
                // Tick A: just rotate and wait
                Rotate();
                yield return new WaitForSeconds(ROTATION_DURATION + INTERACTION_DELAY);
                rrm_isRotateTick = false;
            }
            else
            {
                // Tick B: rotate then move
                Rotate();
                yield return new WaitForSeconds(ROTATION_DURATION + INTERACTION_DELAY);
                yield return StartCoroutine(TryMoveForward());
                rrm_isRotateTick = true;
            }

            interactionCoroutine = null;
        }

        /// <summary>
        /// Attempt to move one cell in the current facing direction.
        /// Checks if the target cell is empty and in-bounds, then smoothly moves there.
        /// Updates grid occupancy (old cell freed, new cell claimed).
        /// </summary>
        private IEnumerator TryMoveForward()
        {
            if (furnitureObject == null) yield break;

            GridManager gm = GridManager.Instance;
            if (gm == null) yield break;

            currentFacing.ToGridOffset(out int dx, out int dy);

            int oldX = furnitureObject.GridX;
            int oldY = furnitureObject.GridY;
            int newX = oldX + dx;
            int newY = oldY + dy;

            // Bounds check — bump if blocked
            if (!gm.IsValidCell(newX, newY))
            {
                if (animator != null) animator.SetTrigger("interact_weak");
                yield break;
            }

            // Check if target cell is empty — if occupied, check for wild-animal interaction
            if (!gm.IsCellEmpty(newX, newY))
            {
                // Wild animals can interact with certain objects (e.g. Feast)
                // Check the InteractionRegistry for wildAnimalInteractible flag
                GameObject occupant = gm.GetCellOccupant(newX, newY);
                if (occupant != null)
                {
                    GridEntityHealth targetHealth = occupant.GetComponent<GridEntityHealth>();
                    if (targetHealth != null && !targetHealth.IsDestroyed)
                    {
                        // Look up the occupant name in InteractionRegistry
                        string targetName = occupant.name.Replace("(Clone)", "").Trim();
                        if (ClockworkCraft.InteractionRegistry.Instance != null
                            && ClockworkCraft.InteractionRegistry.Instance.CanInteract(targetName, ClockworkCraft.InteractorType.WildAnimal))
                        {
                            // Wild animal can interact — perform attack
                            yield return StartCoroutine(PerformStrongInteraction(targetHealth, newX, newY));
                            yield break;
                        }
                    }
                }

                // Not interactible — bump
                if (animator != null) animator.SetTrigger("interact_weak");
                yield break;
            }

            // --- Execute the move ---
            isMoving = true;

            // Determine our cell state (preserve what we had)
            CellState myState = gm.GetCellState(oldX, oldY);

            // Claim new cell first (prevents race conditions with other movers)
            gm.PlaceUnit(newX, newY, gameObject, myState);

            // Free old cell
            gm.RemoveUnit(oldX, oldY);

            // Update FurnitureObject grid position
            furnitureObject.GridX = newX;
            furnitureObject.GridY = newY;

            // Reveal fog around new position
            if (FogManager.Instance != null)
            {
                FogManager.Instance.RevealCell(newX, newY);
                // Also reveal immediate neighbors
                FogManager.Instance.RevealCell(newX + 1, newY);
                FogManager.Instance.RevealCell(newX - 1, newY);
                FogManager.Instance.RevealCell(newX, newY + 1);
                FogManager.Instance.RevealCell(newX, newY - 1);
            }

            // Smoothly animate the world position
            Vector3 startPos = gm.GridToWorldPosition(oldX, oldY);
            Vector3 endPos = gm.GridToWorldPosition(newX, newY);
            startPos.y += 0.01f; // Shadow clipping offset
            endPos.y += 0.01f;

            float elapsed = 0f;
            while (elapsed < MOVE_DURATION)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / MOVE_DURATION);
                // Ease-in-out for smooth movement
                float easedT = t * t * (3f - 2f * t);
                transform.position = Vector3.Lerp(startPos, endPos, easedT);
                yield return null;
            }

            // Snap to final position
            transform.position = endPos;

            isMoving = false;
        }

        // ===============================================================
        // Rotation (shared by all behaviors)
        // ===============================================================

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
        /// Apply the current facing as a Y rotation on the ROOT transform.
        /// We rotate the root (not AnimatorHolder) because the Animator controls
        /// AnimatorHolder and would override code-driven rotation. Rotating the
        /// root means animation clips (which push along local Z) correctly follow
        /// the facing direction.
        /// </summary>
        private void ApplyFacingRotation(bool instant)
        {
            float targetYRotation = currentFacing.ToYRotation();
            Quaternion targetRotation = Quaternion.Euler(0f, targetYRotation, 0f);

            if (instant)
            {
                transform.rotation = targetRotation;
                return;
            }

            // Smooth animated rotation
            if (rotationCoroutine != null)
                StopCoroutine(rotationCoroutine);
            rotationCoroutine = StartCoroutine(RotateCoroutine(targetRotation));
        }

        private IEnumerator RotateCoroutine(Quaternion targetRotation)
        {
            Quaternion startRotation = transform.rotation;
            float elapsed = 0f;

            while (elapsed < ROTATION_DURATION)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / ROTATION_DURATION);

                // Ease-in-out curve
                float easedT = t * t * (3f - 2f * t);
                transform.rotation = Quaternion.Slerp(startRotation, targetRotation, easedT);

                yield return null;
            }

            // Snap to exact final rotation
            transform.rotation = targetRotation;
            rotationCoroutine = null;
        }

        // ===============================================================
        // Scan & Interact (RotateAndInteract behavior only)
        // ===============================================================

        /// <summary>
        /// Look in the facing direction, find the first occupant, and interact.
        /// Returns a coroutine yield if we need to wait for attack contact timing.
        /// </summary>
        private IEnumerator ScanAndInteract()
        {
            if (furnitureObject == null) yield break;

            GridManager gm = GridManager.Instance;
            if (gm == null) yield break;

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

                // Found something — check alliance, unlock, and health.
                GridEntityHealth targetHealth = occupant.GetComponent<GridEntityHealth>();

                if (targetHealth != null && !targetHealth.IsDestroyed)
                {
                    // Allied entities (workers, buildings) — skip entirely, treat as empty
                    if (targetHealth.IsAllied)
                        continue;

                    if (!targetHealth.WorkerCanInteract)
                    {
                        // Can't interact yet — weak bump animation
                        FaceTarget(checkX, checkY);
                        if (animator != null) animator.SetTrigger("interact_weak");
                        yield break;
                    }

                    // Valid target — strong interaction (attack)
                    ResetIdleCounter();
                    yield return PerformStrongInteraction(targetHealth, checkX, checkY);
                    yield break;
                }
                else
                {
                    // Occupant exists but has no health — weak interaction
                    ResetIdleCounter();
                    PerformWeakInteraction(checkX, checkY);
                    yield break;
                }
            }

            // Nothing found in range — play idle bounce and track idle ticks
            if (animator != null) animator.SetTrigger("idle_bounce");
            IncrementIdleCounter();
        }

        // ---------------------------------------------------------------
        // Interactions (RotateAndInteract behavior only)
        // ---------------------------------------------------------------

        /// <summary>
        /// Strong interaction: valid target. Play attack animation, wait for contact moment,
        /// then deal damage + trigger target feedback. This ensures the HP popup and jiggle
        /// line up with when the character visually makes contact.
        /// Also spawns loot particles if the target is a ResourceNode.
        /// If the target is killed, the worker advances into the target's cell ("lunge and stay").
        /// </summary>
        private IEnumerator PerformStrongInteraction(GridEntityHealth target, int targetX, int targetY)
        {
            // Face the target (in case of multi-cell objects or range > 1)
            FaceTarget(targetX, targetY);

            // Play interact_strong animation on the attacker
            if (animator != null)
            {
                animator.SetTrigger("interact_strong");
            }

            // SFX: strong attack hit
            if (GameSFXManager.Instance != null)
                GameSFXManager.Instance.PlayHitImpact();

            // Wait until the animation reaches the contact point
            yield return new WaitForSeconds(ATTACK_CONTACT_DELAY);

            bool targetKilled = false;

            // NOW deal damage (triggers HP popup + target jiggle via GridEntityHealth)
            if (target != null && !target.IsDestroyed)
            {
                int attackPower = health != null ? health.AttackPower : 1;
                int damageDealt = target.TakeDamage(attackPower);

                // Spawn loot particles if target is a resource node
                // Uses HP-to-loot conversion: AccumulateDamage tracks how much damage
                // has been dealt and returns how many loot particles to spawn.
                var resourceNode = target.GetComponent<ClockworkCraft.ResourceNode>();
                if (resourceNode != null && resourceNode.resourceType != ClockworkCraft.ResourceType.None)
                {
                    int lootCount = resourceNode.AccumulateDamage(damageDealt);
                    if (lootCount > 0)
                    {
                        float topY = GridEntityHPBar.GetTopOfObject(target.transform, 0.5f);
                        Vector3 hitPos = target.transform.position + Vector3.up * topY;

                        var lootFX = ClockworkCraft.ResourceLootFX.Instance;
                        if (lootFX != null)
                        {
                            lootFX.SpawnLoot(hitPos, resourceNode.resourceType, lootCount);

                            // SFX: loot burst
                            if (GameSFXManager.Instance != null)
                                GameSFXManager.Instance.PlayLootBurst();
                        }
                        else
                        {
                            // Fallback: add resource directly if FX system isn't available
                            ClockworkCraft.ResourceManager.Instance?.AddResource(resourceNode.resourceType, lootCount);
                        }
                    }
                }

                // Grant meal buff if target is a MealBuffSource
                MealBuffSource mealSource = target.GetComponent<MealBuffSource>();
                if (mealSource != null && !hasMealBuff)
                {
                    GrantMealBuff(ConvertDurationToTicks());

                    // Visual: food icon arcs from feast to this worker
                    if (mealSource.icon != null)
                        ClockworkCraft.IconFlyFX.Instance?.SpawnArc(mealSource.icon, target.transform.position, transform.position);

                    // Visual: restart existing aura if expiring, otherwise add fresh one
                    MealBuffVisual existingVisual = GetComponent<MealBuffVisual>();
                    if (existingVisual != null)
                        existingVisual.Restart();
                    else
                        gameObject.AddComponent<MealBuffVisual>();

                    if (verboseLogging)
                        Debug.Log($"[GridEntityActor] {gameObject.name} received meal buff ({mealBuffTicksRemaining} ticks)");
                }

                // Allied units (workers) reveal fog around their targets
                if (health != null && health.IsAllied && FogManager.Instance != null)
                {
                    FogManager.Instance.RevealCell(targetX, targetY);
                    FogManager.Instance.RevealCell(targetX + 1, targetY);
                    FogManager.Instance.RevealCell(targetX - 1, targetY);
                    FogManager.Instance.RevealCell(targetX, targetY + 1);
                    FogManager.Instance.RevealCell(targetX, targetY - 1);
                }

                targetKilled = target.IsDestroyed;
                if (verboseLogging)
                    Debug.Log($"[GridEntityActor] {gameObject.name} → STRONG interact → {target.gameObject.name} for {damageDealt} damage (target HP: {target.CurrentHP}/{target.MaxHP}){(targetKilled ? " [KILLED]" : "")}");
            }

            // If the target was killed, advance into its cell — but only if the
            // target is slot-takeable (static environment like trees, rocks).
            // Mobile units (dinos, monsters) are NOT slot-takeable by default —
            // they vacate their cell on death and the worker should stay put.
            if (targetKilled && furnitureObject != null)
            {
                bool canTakeSlot = target == null || target.IsSlotTakeable;
                if (canTakeSlot)
                {
                    yield return StartCoroutine(AdvanceIntoCell(targetX, targetY));
                }
            }
        }

        /// <summary>
        /// After killing a target, the worker advances into the target's cell.
        /// Waits for the lunge animation to finish, then smoothly moves to the new cell.
        /// The worker keeps its attack facing and resumes normal tick behavior next tick.
        /// </summary>
        private IEnumerator AdvanceIntoCell(int targetX, int targetY)
        {
            GridManager gm = GridManager.Instance;
            if (gm == null) yield break;

            // Wait for the rest of the interact_strong animation to play out
            // (total duration 0.5s, contact at 0.2s, so 0.3s remaining)
            yield return new WaitForSeconds(0.3f);

            // Check the cell is actually free now (the target's destruction may take a frame)
            // Wait a short extra buffer for GridEntityManager.HandleEntityDestroyed to process
            yield return null;

            // Only advance if the cell is free (or occupied by a dying object)
            GameObject occupant = gm.GetCellOccupant(targetX, targetY);
            if (occupant != null && occupant != gameObject)
            {
                // Check if the occupant is dead/dying — if so, force-free the cell
                GridEntityHealth occupantHealth = occupant.GetComponent<GridEntityHealth>();
                if (occupantHealth != null && occupantHealth.IsDestroyed)
                {
                    gm.RemoveUnit(targetX, targetY);
                    if (verboseLogging)
                        Debug.Log($"[GridEntityActor] Force-freed cell ({targetX},{targetY}) — occupant was already dead");
                }
                else
                {
                    if (verboseLogging)
                        Debug.Log($"[GridEntityActor] {gameObject.name} can't advance to ({targetX},{targetY}) — cell still occupied by {occupant.name}");
                    yield break;
                }
            }

            int oldX = furnitureObject.GridX;
            int oldY = furnitureObject.GridY;

            // Determine our cell state
            CellState myState = gm.GetCellState(oldX, oldY);

            // Claim new cell
            gm.PlaceUnit(targetX, targetY, gameObject, myState);

            // Free old cell
            gm.RemoveUnit(oldX, oldY);

            // Update FurnitureObject grid position
            furnitureObject.GridX = targetX;
            furnitureObject.GridY = targetY;

            // Reveal fog around new position
            if (FogManager.Instance != null)
            {
                FogManager.Instance.RevealCell(targetX, targetY);
                FogManager.Instance.RevealCell(targetX + 1, targetY);
                FogManager.Instance.RevealCell(targetX - 1, targetY);
                FogManager.Instance.RevealCell(targetX, targetY + 1);
                FogManager.Instance.RevealCell(targetX, targetY - 1);
            }

            // Smoothly slide to the new cell position
            Vector3 startPos = transform.position;
            Vector3 endPos = gm.GridToWorldPosition(targetX, targetY);
            endPos.y += 0.01f; // Shadow clipping offset

            float elapsed = 0f;
            while (elapsed < MOVE_DURATION)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / MOVE_DURATION);
                float easedT = t * t * (3f - 2f * t); // Ease-in-out
                transform.position = Vector3.Lerp(startPos, endPos, easedT);
                yield return null;
            }
            transform.position = endPos;

            if (verboseLogging)
                Debug.Log($"[GridEntityActor] {gameObject.name} advanced into killed target's cell ({targetX},{targetY})");
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

            if (verboseLogging)
                Debug.Log($"[GridEntityActor] {gameObject.name} → WEAK interact → cell ({targetX},{targetY})");
        }

        // ---------------------------------------------------------------
        // Starvation System (RotateAndInteract only)
        // ---------------------------------------------------------------

        // ---------------------------------------------------------------
        // Meal Buff
        // ---------------------------------------------------------------

        private int ConvertDurationToTicks()
        {
            if (IntervalTimer.Instance == null)
            {
                // IntervalTimer not ready — should not happen in normal play.
                // FallbackBarDuration must match IntervalTimer.baseIntervalDuration inspector default (2.0f).
                // If that default changes, update this constant to match.
                const float FallbackBarDuration = 2f;
                Debug.LogWarning("[GridEntityActor] IntervalTimer.Instance is null — using fallback bar duration");
                return Mathf.Max(1, Mathf.RoundToInt(mealBuffDurationSeconds / FallbackBarDuration));
            }
            float barDuration = Mathf.Max(float.Epsilon, IntervalTimer.Instance.IntervalDuration);
            return Mathf.Max(1, Mathf.RoundToInt(mealBuffDurationSeconds / barDuration));
        }

        /// <summary>
        /// Grant a meal buff lasting the specified number of interval ticks.
        /// While active, the worker subscribes to OnHalfBar for double-speed movement
        /// and skips MealBuffSource targets during scan.
        /// </summary>
        public void GrantMealBuff(int durationTicks)
        {
            if (hasMealBuff) return; // already buffed — prevents double-subscription

            hasMealBuff = true;
            mealBuffTicksRemaining = durationTicks;

            if (IntervalTimer.Instance != null)
                IntervalTimer.Instance.OnHalfBar += OnHalfBarTick;
        }

        private void ExpireMealBuff()
        {
            hasMealBuff = false;
            mealBuffTicksRemaining = 0;

            if (IntervalTimer.Instance != null)
                IntervalTimer.Instance.OnHalfBar -= OnHalfBarTick;

            if (verboseLogging)
                Debug.Log($"[GridEntityActor] {gameObject.name} meal buff expired");
        }

        /// <summary>
        /// Only subscribed while the meal buff is active (see GrantMealBuff/ExpireMealBuff).
        /// Fires at beats 1 and 3, doubling the worker's action rate.
        /// </summary>
        private void OnHalfBarTick(int bar)
        {
            if (!isInitialized) return;
            if (health != null && health.IsDestroyed) return;

            // Respect interval multiplier — same barNumber check as OnBarTick.
            // Both beat-1 and beat-3 of a given bar share the same barNumber,
            // so both fire or both skip together on multiplier workers.
            if (attackIntervalMultiplier > 1 && bar % attackIntervalMultiplier != 0)
                return;

            if (interactionCoroutine != null)
                StopCoroutine(interactionCoroutine);

            switch (behaviorType)
            {
                case BehaviorType.RotateAndInteract:
                    interactionCoroutine = StartCoroutine(ClockworkTickInteract());
                    break;
                case BehaviorType.RotateAndMove:
                    interactionCoroutine = StartCoroutine(ClockworkTickMove());
                    break;
                case BehaviorType.RotateRotateMove:
                    interactionCoroutine = StartCoroutine(ClockworkTickRotateRotateMove());
                    break;
                default:
                    interactionCoroutine = StartCoroutine(ClockworkTickInteract());
                    break;
            }
        }

        // ---------------------------------------------------------------
        // Starvation
        // ---------------------------------------------------------------

        /// <summary>
        /// Reset the idle counter completely. Called on any successful interaction.
        /// </summary>
        private void ResetIdleCounter()
        {
            if (idleTickCount > 0 && verboseLogging)
            {
                Debug.Log($"[GridEntityActor] {gameObject.name} idle counter reset (was {idleTickCount})");
            }
            idleTickCount = 0;
        }

        /// <summary>
        /// Increment the idle counter and check for starvation death.
        /// Called when ScanAndInteract finds nothing.
        /// </summary>
        private void IncrementIdleCounter()
        {
            // Only workers (RotateAndInteract) starve
            if (behaviorType != BehaviorType.RotateAndInteract) return;

            idleTickCount++;

            int totalThreshold = graceThreshold + countdownThreshold;

            if (idleTickCount > graceThreshold && idleTickCount <= totalThreshold)
            {
                // Phase 2: Countdown — show red number
                int remaining = totalThreshold - idleTickCount;
                // remaining goes from (countdownThreshold-1) down to 0
                // We display (remaining + 0) but we want to show countdownThreshold, countdownThreshold-1, ... 1
                // When idleTickCount == graceThreshold+1, remaining = countdownThreshold-1, display = countdownThreshold-1+1 = countdownThreshold...
                // Actually: remaining = totalThreshold - idleTickCount
                // At idleTickCount = graceThreshold+1: remaining = countdownThreshold - 1 → display countdownThreshold
                // Wait, that's not right. Let me recalculate:
                // display number = totalThreshold - idleTickCount + 1... no.
                // We want: first countdown tick shows countdownThreshold, last shows 1, then death.
                // idleTickCount = graceThreshold + 1 → show countdownThreshold
                // idleTickCount = graceThreshold + 2 → show countdownThreshold - 1
                // idleTickCount = graceThreshold + countdownThreshold → show 1
                // idleTickCount = graceThreshold + countdownThreshold + 1 → death
                // So: displayNumber = graceThreshold + countdownThreshold - idleTickCount + 1
                int displayNumber = totalThreshold - idleTickCount + 1;

                if (verboseLogging)
                    Debug.Log($"[GridEntityActor] {gameObject.name} starving! Countdown: {displayNumber} (idle ticks: {idleTickCount}/{totalThreshold})");
                SpawnCountdownPopup(displayNumber);

                // SFX: warning tick
                if (GameSFXManager.Instance != null)
                    GameSFXManager.Instance.PlayClockTick();
            }
            else if (idleTickCount > totalThreshold)
            {
                // Death by starvation
                if (verboseLogging)
                    Debug.Log($"[GridEntityActor] {gameObject.name} STARVED TO DEATH after {idleTickCount} idle ticks!");
                isStarving = true;

                // Kill through normal death pipeline
                if (health != null && !health.IsDestroyed)
                {
                    health.TakeDamage(health.CurrentHP);
                }
            }
            else
            {
                // Phase 1: Grace period — silent
                if (verboseLogging)
                    Debug.Log($"[GridEntityActor] {gameObject.name} idle tick {idleTickCount}/{graceThreshold} (grace period)");
            }
        }

        /// <summary>
        /// Spawn a floating red countdown number that pops in, floats up, and fades out
        /// — like the damage numbers. Each tick spawns a NEW popup, so the numbers feel
        /// like an urgent countdown rather than a static label changing its text.
        /// </summary>
        private void SpawnCountdownPopup(int number)
        {
            // Use RefHeight system so the popup appears above the actual model
            float spawnHeight = GridEntityHPBar.GetTopOfObject(transform, 2.2f) + 0.3f;
            float spreadX = Random.Range(-0.3f, 0.3f);
            Vector3 spawnPos = transform.position + new Vector3(spreadX, spawnHeight, 0f);

            GameObject popupObj = new GameObject("StarvationCountdown");
            popupObj.transform.position = spawnPos;

            TextMeshPro tmp = popupObj.AddComponent<TextMeshPro>();
            tmp.text = number.ToString();
            tmp.fontSize = 7f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.9f, 0.15f, 0.15f, 1f); // Red
            tmp.fontStyle = FontStyles.Bold;
            tmp.sortingOrder = 100;
            tmp.enableWordWrapping = false;
            tmp.richText = false; // Prevent underline glyph lookup on bitmap fonts

            // Try GUI Pro Kit MuseoModerno font first, then fall back to TMP default
            TMP_FontAsset font = null;
            GUIProKitAssets guiKit = GUIProKitAssets.Instance;
            if (guiKit != null && guiKit.criticalNumberFont != null)
                font = guiKit.criticalNumberFont;
            if (font == null)
                font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font == null && TMP_Settings.defaultFontAsset != null)
                font = TMP_Settings.defaultFontAsset;
            if (font != null)
                tmp.font = font;

            // Dark outline for readability (skip if font doesn't support it, e.g. bitmap atlas)
            bool hasOutline = font != null && font.material != null &&
                font.material.HasProperty("_OutlineColor");
            if (hasOutline)
            {
                tmp.outlineWidth = 0.25f;
                tmp.outlineColor = new Color32(40, 10, 0, 220);
            }

            RectTransform rect = popupObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(3f, 2f);

            // Attach the popup animator — same style as damage numbers
            StarvationCountdownPopup animator = popupObj.AddComponent<StarvationCountdownPopup>();
            animator.Initialize(1.0f, 1.4f); // floatDistance, duration (slower than damage — it's a countdown)
        }

        private void OnDestroy()
        {
            // Countdown popups are self-destructing — no cleanup needed
        }

        /// <summary>
        /// Rotate the ROOT transform to face a specific grid cell.
        /// Used before playing interaction animations (all objects face along local Z).
        /// We rotate root instead of AnimatorHolder so the Animator doesn't override it.
        /// </summary>
        private void FaceTarget(int targetX, int targetY)
        {
            if (furnitureObject == null) return;

            GridManager gm = GridManager.Instance;
            if (gm == null) return;

            Vector3 myWorldPos = gm.GridToWorldPosition(furnitureObject.GridX, furnitureObject.GridY);
            Vector3 targetWorldPos = gm.GridToWorldPosition(targetX, targetY);

            Vector3 direction = (targetWorldPos - myWorldPos).normalized;
            if (direction.sqrMagnitude > 0.001f)
            {
                // Snap root rotation to face target (the interaction animation handles the visual movement)
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
        }
    }

    /// <summary>
    /// Animates a starvation countdown popup: pop-scale entrance, float upward,
    /// fade out, self-destruct. Slower and larger than damage numbers to feel
    /// like a dramatic countdown. Billboard in LateUpdate.
    /// </summary>
    public class StarvationCountdownPopup : MonoBehaviour
    {
        private float floatDistance;
        private float duration;
        private float elapsed = 0f;
        private Vector3 startPos;
        private TextMeshPro tmp;
        private Color startColor;
        private Color32 startOutlineColor;
        private bool hasOutline;

        public void Initialize(float distance, float totalDuration)
        {
            floatDistance = distance;
            duration = totalDuration;
            startPos = transform.position;
            tmp = GetComponent<TextMeshPro>();
            if (tmp != null)
            {
                startColor = tmp.color;
                hasOutline = tmp.font != null && tmp.font.material != null &&
                    tmp.font.material.HasProperty("_OutlineColor");
                if (hasOutline)
                    startOutlineColor = tmp.outlineColor;
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Float upward with ease-out
            float easedT = 1f - (1f - t) * (1f - t);
            transform.position = startPos + new Vector3(0f, floatDistance * easedT, 0f);

            // Scale: big pop in, then settle
            float scale = 1f;
            if (t < 0.1f)
            {
                float popT = t / 0.1f;
                scale = Mathf.Lerp(0f, 1.8f, popT);
            }
            else if (t < 0.25f)
            {
                float settleT = (t - 0.1f) / 0.15f;
                scale = Mathf.Lerp(1.8f, 1f, settleT);
            }
            transform.localScale = Vector3.one * scale;

            // Fade out in the last 40%
            if (tmp != null && t > 0.6f)
            {
                float fadeT = (t - 0.6f) / 0.4f;
                Color c = startColor;
                c.a = 1f - fadeT;
                tmp.color = c;

                if (hasOutline)
                {
                    byte outlineAlpha = (byte)(startOutlineColor.a * (1f - fadeT));
                    tmp.outlineColor = new Color32(
                        startOutlineColor.r, startOutlineColor.g,
                        startOutlineColor.b, outlineAlpha);
                }
            }

            if (elapsed >= duration)
                Destroy(gameObject);
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam != null)
                transform.forward = cam.transform.forward;
        }
    }
}
