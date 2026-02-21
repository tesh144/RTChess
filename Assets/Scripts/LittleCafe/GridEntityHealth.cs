using UnityEngine;
using System;
using System.Collections;
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

        private int currentHP;
        private bool isDestroyed = false;

        // Damage flash
        private Renderer[] renderers;
        private Color[] originalColors;
        private Coroutine flashCoroutine;
        private const float FLASH_DURATION = 0.15f;

        // Animator reference (cached)
        private Animator animator;

        // --- IDamageable ---
        public int CurrentHP => currentHP;
        public int MaxHP => maxHP;
        public bool IsDestroyed => isDestroyed;

        // --- Public accessors ---
        public int AttackPower => attackPower;

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
        public void Initialize(int hp, int atkPower)
        {
            maxHP = hp;
            attackPower = atkPower;
            currentHP = maxHP;
            isDestroyed = false;

            CacheRenderers();
            CacheAnimator();

            Debug.Log($"[GridEntityHealth] {gameObject.name} initialized: HP={maxHP}, ATK={attackPower}");
        }

        private void CacheRenderers()
        {
            renderers = GetComponentsInChildren<Renderer>();
            originalColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].material != null)
                    originalColors[i] = renderers[i].material.color;
            }
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

            // Visual feedback: damage flash
            if (flashCoroutine != null)
                StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(DamageFlashCoroutine());

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

            // Play remove animation if we have an animator
            if (animator != null)
            {
                animator.SetTrigger("remove");
            }

            // Fire events — listeners decide what happens
            OnEntityDestroyed?.Invoke(this);
            OnAnyEntityDestroyed?.Invoke(this);
        }

        // ---------------------------------------------------------------
        // Damage Flash
        // ---------------------------------------------------------------

        private IEnumerator DamageFlashCoroutine()
        {
            // Flash red
            SetRenderersColor(Color.red);
            yield return new WaitForSeconds(FLASH_DURATION);
            RestoreRendererColors();
            flashCoroutine = null;
        }

        private void SetRenderersColor(Color color)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].material != null)
                    renderers[i].material.color = color;
            }
        }

        private void RestoreRendererColors()
        {
            if (renderers == null || originalColors == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].material != null)
                    renderers[i].material.color = originalColors[i];
            }
        }

        // ---------------------------------------------------------------
        // Cleanup
        // ---------------------------------------------------------------

        private void OnDisable()
        {
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
                flashCoroutine = null;
            }
        }
    }
}
