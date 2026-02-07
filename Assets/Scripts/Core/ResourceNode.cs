using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// A harvestable resource node on the grid.
    /// Does not rotate. Grants tokens when attacked by player units.
    /// </summary>
    public class ResourceNode : MonoBehaviour, IDamageable
    {
        [Header("Resource Stats")]
        [SerializeField] private int maxHP = 10;
        [SerializeField] private int level = 1;
        [SerializeField] private int tokensPerHit = 1;
        [SerializeField] private int bonusTokens = 3;

        private int currentHP;
        private bool isDestroyed = false;

        // Grid position
        public int GridX { get; set; }
        public int GridY { get; set; }
        public int Level => level;

        // IDamageable implementation
        public int CurrentHP => currentHP;
        public int MaxHP => maxHP;
        public bool IsDestroyed => isDestroyed;

        // HP bar references (found by name in children)
        private Transform hpBarFill;

        // Hit flash state
        private Renderer[] renderers;
        private Color[] originalColors;
        private float flashTimer;
        private const float FlashDuration = 0.15f;

        private void Awake()
        {
            currentHP = maxHP;
        }

        private void Start()
        {
            FindHPBar();
            CacheRenderers();
            UpdateHPBar();
        }

        private void FindHPBar()
        {
            // Find HP bar fill by name in children hierarchy
            Transform[] allChildren = GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allChildren)
            {
                if (t.name == "HPBarFill")
                {
                    hpBarFill = t;
                    return;
                }
            }
        }

        private void Update()
        {
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                if (flashTimer <= 0f)
                {
                    RestoreColors();
                }
            }
        }

        private void CacheRenderers()
        {
            renderers = GetComponentsInChildren<Renderer>();
            originalColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                originalColors[i] = renderers[i].material.color;
            }
        }

        /// <summary>
        /// Called when a unit attacks this resource node.
        /// Returns the number of tokens earned.
        /// </summary>
        public int TakeDamage(int damage)
        {
            if (currentHP <= 0) return 0;

            int actualDamage = Mathf.Min(damage, currentHP);
            currentHP -= actualDamage;

            // Tokens earned = tokensPerHit per HP lost
            int tokensEarned = actualDamage * tokensPerHit;

            // Visual feedback
            FlashWhite();
            UpdateHPBar();

            if (currentHP <= 0)
            {
                // Grant bonus tokens on final hit
                tokensEarned += bonusTokens;
                OnDestroyed();
            }

            return tokensEarned;
        }

        private void OnDestroyed()
        {
            if (isDestroyed) return;

            isDestroyed = true;

            // Remove from grid
            if (GridManager.Instance != null)
            {
                GridManager.Instance.RemoveUnit(GridX, GridY);
            }

            // Spawn destruction particles
            SpawnDestructionEffect();

            Destroy(gameObject);
        }

        private void SpawnDestructionEffect()
        {
            // Simple particle burst
            GameObject particleObj = new GameObject("ResourceDestroyVFX");
            particleObj.transform.position = transform.position + Vector3.up * 0.5f;

            ParticleSystem ps = particleObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.5f;
            main.startLifetime = 0.6f;
            main.startSpeed = 3f;
            main.startSize = 0.15f;
            main.startColor = new Color(0.2f, 0.9f, 0.3f);
            main.maxParticles = 20;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, 20)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            // Auto-destroy after particles finish
            Destroy(particleObj, 1.5f);
        }

        private void FlashWhite()
        {
            if (renderers == null) return;

            foreach (Renderer r in renderers)
            {
                if (r != null)
                    r.material.color = Color.white;
            }
            flashTimer = FlashDuration;
        }

        private void RestoreColors()
        {
            if (renderers == null || originalColors == null) return;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].material.color = originalColors[i];
            }
        }

        private void UpdateHPBar()
        {
            if (hpBarFill == null) return;

            float ratio = (float)currentHP / maxHP;
            Vector3 scale = hpBarFill.localScale;
            scale.x = ratio;
            hpBarFill.localScale = scale;

            // Shift color from green to red as HP decreases
            Renderer fillRenderer = hpBarFill.GetComponent<Renderer>();
            if (fillRenderer != null)
            {
                fillRenderer.material.color = Color.Lerp(Color.red, new Color(0.2f, 0.9f, 0.3f), ratio);
            }
        }
    }
}
