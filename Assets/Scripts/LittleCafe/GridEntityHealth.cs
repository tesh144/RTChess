using UnityEngine;
using System;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// Gives any placed grid object hit points and makes it damageable.
    /// Implements IDamageable for compatibility with existing combat systems.
    /// HP reaching zero fires an event — the listener decides what happens
    /// (destruction, completion, resource drop, etc.).
    /// </summary>
    public class GridEntityHealth : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField] private int maxHP = 3;
        [SerializeField] private int attackPower = 1;

        [Header("Interaction")]
        [Tooltip("Can workers interact with / attack this entity? Set at spawn time from InteractionRegistry.")]
        [SerializeField] private bool workerCanInteract = true;

        [Tooltip("Allied entities (workers, buildings) are never attacked by the player's own workers.")]
        [SerializeField] private bool isAllied = false;

        [Tooltip("When this entity is killed, can the attacker advance into its cell? True for static objects (trees, rocks), false for mobile units (dinos, monsters).")]
        [SerializeField] private bool isSlotTakeable = true;

        private int currentHP;
        private bool isDestroyed = false;

        // Animator reference (cached)
        private Animator animator;

        // --- IDamageable ---
        public int CurrentHP => currentHP;
        public int MaxHP => maxHP;
        public bool IsDestroyed => isDestroyed;

        // --- Public accessors ---
        public int AttackPower => attackPower;
        public bool WorkerCanInteract => workerCanInteract;
        public bool IsAllied => isAllied;
        public bool IsSlotTakeable => isSlotTakeable;

        // --- Events ---
        /// <summary>
        /// Fired when this entity takes damage. Passes (damageDealt, currentHP, maxHP).
        /// </summary>
        public event Action<int, int, int> OnDamaged;

        /// <summary>
        /// Fired when HP reaches zero. The listener decides what happens next.
        /// Passes this GridEntityHealth so the listener can identify the object.
        /// </summary>
        public event Action<GridEntityHealth> OnEntityDestroyed;

        /// <summary>
        /// Static event so the GridEntityManager can listen globally.
        /// </summary>
        public static event Action<GridEntityHealth> OnAnyEntityDestroyed;

        // ---------------------------------------------------------------
        // Initialization
        // ---------------------------------------------------------------

        /// <summary>
        /// Configure health from database values. Called by GridEntityManager after attaching.
        /// </summary>
        public void Initialize(int hp, int atkPower, bool canInteract = true, bool allied = false, bool slotTakeable = true)
        {
            maxHP = hp;
            attackPower = atkPower;
            currentHP = maxHP;
            isDestroyed = false;
            workerCanInteract = canInteract;
            isAllied = allied;
            isSlotTakeable = slotTakeable;

            CacheAnimator();

            Debug.Log($"[GridEntityHealth] {gameObject.name} initialized: HP={maxHP}, ATK={attackPower}, workerCanInteract={workerCanInteract}");
        }

        private void CacheAnimator()
        {
            // Find the AnimatorHolder child (PEPO prefab convention)
            Transform animatorHolder = transform.Find("AnimatorHolder");
            if (animatorHolder != null)
                animator = animatorHolder.GetComponent<Animator>();
        }

        // ---------------------------------------------------------------
        // IDamageable Implementation
        // ---------------------------------------------------------------

        public int TakeDamage(int damage)
        {
            if (isDestroyed) return 0;

            int actualDamage = Mathf.Min(damage, currentHP);
            currentHP -= actualDamage;

            Debug.Log($"[GridEntityHealth] {gameObject.name} took {actualDamage} damage. HP: {currentHP}/{maxHP}");

            // Play interact_weak animation on the target being hit (jiggle feedback)
            if (animator != null && currentHP > 0)
            {
                animator.SetTrigger("interact_weak");
            }

            // SFX: damage impact feedback
            if (GameSFXManager.Instance != null)
                GameSFXManager.Instance.PlayDamageImpact();

            // Mini poof on each hit (small burst of 4-5 spheres at contact point)
            Vector3 hitPos = transform.position + Vector3.up * 0.5f;
            PoofEffect.Spawn(hitPos, count: 4, color: Color.white, minSize: 0.04f, maxSize: 0.1f);

            // Notify listeners
            OnDamaged?.Invoke(actualDamage, currentHP, maxHP);

            // Check for death
            if (currentHP <= 0)
            {
                HandleDestroyed();
            }

            return actualDamage;
        }

        // ---------------------------------------------------------------
        // Death Handling
        // ---------------------------------------------------------------

        private void HandleDestroyed()
        {
            if (isDestroyed) return;
            isDestroyed = true;

            Debug.Log($"[GridEntityHealth] {gameObject.name} destroyed (HP reached 0)");

            // SFX: entity death
            if (GameSFXManager.Instance != null)
                GameSFXManager.Instance.PlayEntityDeath();

            // Play remove animation if we have an animator
            if (animator != null)
            {
                animator.SetTrigger("remove");
            }

            // Fire events — listeners decide what happens
            OnEntityDestroyed?.Invoke(this);
            OnAnyEntityDestroyed?.Invoke(this);
        }

    }
}
