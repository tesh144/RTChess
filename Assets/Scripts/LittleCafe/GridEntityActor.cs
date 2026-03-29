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
    ///
    /// Split into three partial files:
    ///   GridEntityActor.cs          — Core: state, lifecycle, tick dispatch, meal buff, corruption
    ///   GridEntityActor.Movement.cs — Movement, rotation, behavior coroutines
    ///   GridEntityActor.Interaction.cs — Scanning, combat, starvation
    /// </summary>
    public partial class GridEntityActor : MonoBehaviour
    {
        [Header("Clockwork Settings")]
        [SerializeField] private Facing currentFacing = Facing.North;
        [SerializeField] private int attackRange = 1;
        [SerializeField] private int attackIntervalMultiplier = 1;

        [Header("Behavior")]
        [SerializeField] private BehaviorType behaviorType = BehaviorType.RotateAndInteract;
        private string walkableSurfaces = "None"; // Surface types this unit can walk on

        [Header("Rotation")]
        [SerializeField] private bool rotateClockwise = true;

        [Header("Starvation")]
        [Tooltip("Number of idle ticks before countdown begins (silent grace period).")]
        [SerializeField] private int graceThreshold = 4;
        [Tooltip("Number of countdown ticks after grace before death (visible red numbers).")]
        [SerializeField] private int countdownThreshold = 4;

        [Header("Meal Buff")]
        [Tooltip("How long the meal buff lasts in real seconds. Converted to bar ticks on grant.")]
        [SerializeField] private float mealBuffDurationSeconds = 20f;

        [Header("Corruption")]
        private bool isCorruptionPaused = false;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;

        // Cached references
        private PlacedObject _furnitureObject;
        private bool _furnitureObjectCached;
        private PlacedObject furnitureObject
        {
            get
            {
                if (!_furnitureObjectCached)
                {
                    _furnitureObject = GetComponent<PlacedObject>();
                    if (_furnitureObject != null)
                        _furnitureObjectCached = true;
                }
                return _furnitureObject;
            }
        }
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
        private int placedOnBar = -1; // Bar number when this actor was placed (-1 = not set)
        private bool isMoving = false;   // Prevent overlapping moves
        private bool rrm_isRotateTick = true; // RotateRotateMove: alternates between rotate-only and rotate+move ticks

        // Starvation state
        private int idleTickCount = 0;
        private bool isStarving = false;

        // Meal buff state
        private bool hasMealBuff = false;
        private int mealBuffTicksRemaining = 0;

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
            BehaviorType behavior = BehaviorType.RotateAndInteract, string walkable = "None")
        {
            rotateClockwise = clockwise;
            attackRange = range;
            attackIntervalMultiplier = intervalMultiplier;
            behaviorType = behavior;
            walkableSurfaces = walkable ?? "None";

            // Record which bar we were placed on so we skip only that same-tick call
            placedOnBar = IntervalTimer.Instance != null ? IntervalTimer.Instance.CurrentBar : -1;

            CacheReferences();

            // Face nearest valid target for interact behavior, random for move behaviors
            if (behaviorType == BehaviorType.RotateAndInteract)
            {
                currentFacing = FindBestInitialFacing();
            }
            else
            {
                // Random initial facing for wandering entities (RotateAndMove, RotateRotateMove, RotateAndMoveCorrupted)
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

                    // Corruption priority — corrupted tiles are valid targets even if unoccupied
                    CorruptionOverlay scanCorruption = CorruptionManager.Instance != null
                        ? CorruptionManager.Instance.GetOverlay(checkX, checkY) : null;
                    if (scanCorruption != null && scanCorruption.Health != null && !scanCorruption.Health.IsDestroyed)
                    {
                        if (step < bestDist)
                        {
                            bestDist = step;
                            bestFacing = facing;
                            found = true;
                        }
                        break;
                    }

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
            // furnitureObject is now lazy-loaded via property (may be added after Initialize)
            health = GetComponent<GridEntityHealth>();

            // Find the AnimatorHolder child (PEPO prefab convention)
            animatorHolder = transform.Find("AnimatorHolder");
            if (animatorHolder != null)
            {
                animator = animatorHolder.GetComponent<Animator>();
            }

            if (furnitureObject == null)
                Debug.LogWarning($"[GridEntityActor] No PlacedObject on {gameObject.name} — grid position unknown");
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
        // Clockwork Tick Dispatch
        // ---------------------------------------------------------------

        private void OnBarTick(int bar)
        {
            if (!isInitialized) return;
            if (health != null && health.IsDestroyed) return;
            if (isCorruptionPaused) return;

            // Grace period: skip only if this tick is the same bar we were placed on.
            // Prevents instant action on placement without adding a full extra bar delay.
            if (placedOnBar >= 0 && bar == placedOnBar)
            {
                placedOnBar = -1; // Clear so future ticks are not affected
                return;
            }

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
                case BehaviorType.RotateAndMoveCorrupted:
                    interactionCoroutine = StartCoroutine(ClockworkTickMoveCorrupted());
                    break;
                case BehaviorType.RotateRotateMove:
                    interactionCoroutine = StartCoroutine(ClockworkTickRotateRotateMove());
                    break;
                default:
                    interactionCoroutine = StartCoroutine(ClockworkTickInteract());
                    break;
            }
        }

    }
}