using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// A harvestable resource node on the grid.
    /// Does not rotate. Grants tokens when attacked by player units.
    /// Iteration 8: Supports multi-cell occupation (Level 2: 2 cells, Level 3: 4 cells)
    /// </summary>
    public class ResourceNode : MonoBehaviour, IDamageable
    {
        [Header("Resource Stats")]
        [SerializeField] private int maxHP = 10;
        [SerializeField] private int level = 1;
        [SerializeField] private int tokensPerHit = 1;
        [SerializeField] private int bonusTokens = 3;

        [Header("Multi-Cell Occupation - Iteration 8")]
        [SerializeField] private Vector2Int gridSize = new Vector2Int(1, 1); // Level 1: 1x1, Level 2: 2x1, Level 3: 2x2

        private int currentHP;
        private bool isDestroyed = false;

        // Grid position (top-left cell for multi-cell resources)
        public int GridX { get; set; }
        public int GridY { get; set; }
        public int Level => level;
        public Vector2Int GridSize => gridSize;

        // IDamageable implementation
        public int CurrentHP => currentHP;
        public int MaxHP => maxHP;
        public bool IsDestroyed => isDestroyed;

        // HP text references (found by name in children)
        private TextMesh hpTextMesh;
        private TextMesh hpTextShadow;

        // Hit flash state
        private Renderer[] renderers;
        private Color[] originalColors;
        private float flashTimer;
        private const float FlashDuration = 0.15f;

        // Audio
        private AudioSource audioSource;

        private void Awake()
        {
            currentHP = maxHP;
        }

        private void Start()
        {
            FindHPText();
            CacheRenderers();
            UpdateHPText();

            // Setup audio
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.7f;
            audioSource.volume = 0.4f;

            // Generate mine destroyed sound if none assigned in MusicSystem
            if (MusicSystem.instance != null && MusicSystem.instance.mine_destroyed_sfx == null)
            {
                MusicSystem.instance.mine_destroyed_sfx = GenerateMineDestroyedSound(1);
            }

            // Note: Registration removed - WaveManager handles spawning now (Iteration 10)
        }

        /// <summary>
        /// Initialize multi-cell resource (Iteration 8).
        /// Called by WaveManager when spawning.
        /// </summary>
        public void Initialize(Vector2Int size)
        {
            gridSize = size;
        }

        private void FindHPText()
        {
            // Find HP text components by name in children hierarchy
            TextMesh[] allTextMeshes = GetComponentsInChildren<TextMesh>(true);
            foreach (TextMesh tm in allTextMeshes)
            {
                if (tm.name == "HPText")
                {
                    hpTextMesh = tm;
                }
                else if (tm.name == "HPTextShadow")
                {
                    hpTextShadow = tm;
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

            // Fixed 1 token per hit regardless of damage
            int tokensEarned = 1;

            // Visual feedback
            FlashWhite();
            UpdateHPText();

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

            // Note: Unregistration removed - WaveManager handles spawning now (Iteration 10)

            // Remove from grid (Iteration 8: Free all occupied cells)
            if (GridManager.Instance != null)
            {
                for (int dx = 0; dx < gridSize.x; dx++)
                {
                    for (int dy = 0; dy < gridSize.y; dy++)
                    {
                        int cellX = GridX + dx;
                        int cellY = GridY + dy;
                        GridManager.Instance.RemoveUnit(cellX, cellY);
                    }
                }
            }

            // Notify WaveManager for wave completion tracking
            if (WaveManager.Instance != null)
                WaveManager.Instance.OnResourceNodeDestroyed();

            // Play celebratory destruction sound (regenerate with current level)
            if (audioSource != null && MusicSystem.instance != null)
            {
                AudioClip destroySound = GenerateMineDestroyedSound(level);
                audioSource.PlayOneShot(destroySound);
            }

            // Spawn destruction particles
            SpawnDestructionEffect();

            Destroy(gameObject, 0.5f); // Delay destruction so sound can play
        }

        private void SpawnDestructionEffect()
        {
            GameObject particleObj = new GameObject("ResourceDestroyVFX");
            particleObj.transform.position = transform.position + Vector3.up * 0.5f;

            ParticleSystem ps = particleObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.5f;
            main.startLifetime = 0.8f;
            main.startSpeed = 2.5f;
            main.startSize = 0.12f;
            main.startColor = new Color(1f, 0.85f, 0.2f); // Gold color
            main.maxParticles = 20;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.8f; // Particles arc and fall (bounce feel)

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, 20)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            // Assign material so particles don't render magenta
            var renderer = particleObj.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
            renderer.material.color = new Color(1f, 0.85f, 0.2f); // Gold

            // Color fades from bright gold to darker gold over lifetime
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1f, 0.9f, 0.3f), 0f),
                    new GradientColorKey(new Color(0.9f, 0.7f, 0.1f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = grad;

            Destroy(particleObj, 2f);
        }

        /// <summary>
        /// Generate celebratory mine destroyed sound.
        /// More exciting for higher level mines.
        /// </summary>
        private AudioClip GenerateMineDestroyedSound(int mineLevel)
        {
            int sampleRate = 44100;
            float duration = 0.4f + (mineLevel - 1) * 0.1f; // Longer for higher levels
            int sampleCount = Mathf.FloorToInt(sampleRate * duration);

            AudioClip clip = AudioClip.Create("MineDestroyedSound", sampleCount, 1, sampleRate, false);
            float[] samples = new float[sampleCount];

            // More chimes for higher levels (1-3 chimes)
            int numChimes = Mathf.Min(mineLevel, 3);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float sample = 0f;

                // Ascending chime sequence
                for (int c = 0; c < numChimes; c++)
                {
                    float chimeStart = c * 0.15f;
                    float chimeEnd = chimeStart + 0.25f;

                    if (t >= chimeStart && t <= chimeEnd)
                    {
                        float chimeT = (t - chimeStart) / (chimeEnd - chimeStart);

                        // Higher pitch for each chime
                        float baseFreq = 440f + (c * 220f); // A4, then higher notes
                        float chime = Mathf.Sin(2f * Mathf.PI * baseFreq * chimeT);

                        // Add harmonics for richer sound
                        chime += Mathf.Sin(2f * Mathf.PI * baseFreq * 2f * chimeT) * 0.4f;
                        chime += Mathf.Sin(2f * Mathf.PI * baseFreq * 3f * chimeT) * 0.2f;

                        // Bell-like envelope
                        float chimeEnvelope = Mathf.Exp(-6f * chimeT);

                        sample += chime * chimeEnvelope * 0.5f;
                    }
                }

                // Add sparkle (high frequency texture)
                if (t < 0.3f)
                {
                    float sparkle = (Random.value * 2f - 1f) * 0.15f;
                    sparkle *= Mathf.Exp(-10f * t);
                    sample += sparkle;
                }

                samples[i] = Mathf.Clamp(sample, -1f, 1f);
            }

            clip.SetData(samples, 0);
            return clip;
        }

        private void FlashWhite()
        {
            if (renderers == null) return;

            foreach (Renderer r in renderers)
            {
                if (r != null && r.name != "HPText" && r.name != "HPTextShadow")
                    r.material.color = Color.white;
            }
            flashTimer = FlashDuration;
        }

        private void RestoreColors()
        {
            if (renderers == null || originalColors == null) return;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].name != "HPText" && renderers[i].name != "HPTextShadow")
                    renderers[i].material.color = originalColors[i];
            }
        }

        private void UpdateHPText()
        {
            if (hpTextMesh == null) return;

            // Update both text meshes with current HP value
            string hpString = currentHP.ToString();
            hpTextMesh.text = hpString;

            if (hpTextShadow != null)
            {
                hpTextShadow.text = hpString;
            }

            // Color from green to red based on HP ratio
            float ratio = (float)currentHP / maxHP;
            hpTextMesh.color = Color.Lerp(Color.red, new Color(0.2f, 0.9f, 0.3f), ratio);
        }
    }
}
