using System.Collections;
using UnityEngine;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// Continuous particle aura attached to a worker while they have a meal buff active.
    /// Spawns golden-white spheres that drift upward. Flickers in the last 3 ticks.
    /// Self-destructs (component only) when the buff expires.
    ///
    /// Added at runtime by GridEntityActor when GrantMealBuff fires.
    /// Reads buff state directly from the sibling GridEntityActor component.
    /// </summary>
    public class MealBuffVisual : MonoBehaviour
    {
        // ── Tuning constants ───────────────────────────────────────────────
        private const float NORMAL_INTERVAL   = 0.18f;  // seconds between spawns (~5-6/sec)
        private const float FLICKER_INTERVAL  = 0.08f;  // seconds between spawns (~12/sec)
        private const float NORMAL_LIFETIME   = 1.4f;   // particle lifetime in normal mode
        private const float FLICKER_LIFETIME  = 0.7f;   // particle lifetime in flicker mode
        private const float PARTICLE_RADIUS   = 1.1f;   // ring radius around worker
        private const float SPAWN_HEIGHT_MIN  = 0.1f;   // lowest spawn Y offset (feet)
        private const float SPAWN_HEIGHT_MAX  = 1.2f;   // highest spawn Y offset (above head)
        private static readonly Color BUFF_COLOR = new Color(1f, 0.92f, 0.45f);

        // ── State ──────────────────────────────────────────────────────────
        private GridEntityActor actor;
        private bool isFlickering = false;  // one-way latch; set when ticksRemaining <= 3
        private bool isExpiring   = false;  // set when HasMealBuff becomes false
        private float timeSinceLastSpawn = 0f;
        private Material buffMaterial;

        // ── Lifecycle ──────────────────────────────────────────────────────

        void Start()
        {
            actor = GetComponent<GridEntityActor>();
            if (actor == null)
            {
                Debug.LogWarning("[MealBuffVisual] No GridEntityActor on same GameObject — removing self.");
                Destroy(this);
                return;
            }

            if (IntervalTimer.Instance == null)
            {
                Debug.LogWarning("[MealBuffVisual] IntervalTimer.Instance is null — aura will not respond to ticks.");
                return;
            }

            IntervalTimer.Instance.OnIntervalTick += OnTick;
            Shader buffShader = Shader.Find("Unlit/Color");
            if (buffShader == null)
            {
                Debug.LogError("[MealBuffVisual] Shader 'Unlit/Color' not found. Add it to Always Included Shaders in Project Settings > Graphics.");
                buffMaterial = new Material(Shader.Find("Sprites/Default")) { color = BUFF_COLOR };
            }
            else
            {
                buffMaterial = new Material(buffShader) { color = BUFF_COLOR };
            }
        }

        void OnDestroy()
        {
            if (buffMaterial != null) Destroy(buffMaterial);
            if (IntervalTimer.Instance != null)
                IntervalTimer.Instance.OnIntervalTick -= OnTick;
        }

        void Update()
        {
            if (actor == null) return;
            if (isExpiring) return;

            // Detect buff expiry every frame — avoids tick ordering race with GridEntityActor
            if (!actor.HasMealBuff)
            {
                isExpiring = true;
                StartCoroutine(ExpireAfterDelay());
                return;
            }

            float interval = isFlickering ? FLICKER_INTERVAL : NORMAL_INTERVAL;
            timeSinceLastSpawn += Time.deltaTime;

            if (timeSinceLastSpawn >= interval)
            {
                timeSinceLastSpawn = 0f;
                SpawnParticle(isFlickering ? FLICKER_LIFETIME : NORMAL_LIFETIME);
            }
        }

        // ── Tick handler ───────────────────────────────────────────────────

        private void OnTick(int intervalCount)
        {
            if (actor == null || isExpiring) return;

            // Flicker: one-way latch when ticks remaining drops to 3
            if (actor.MealBuffTicksRemaining <= 3 && !isFlickering)
            {
                isFlickering = true;
                timeSinceLastSpawn = 0f; // reset so full FLICKER_INTERVAL elapses before first flicker particle
            }
        }

        /// <summary>
        /// Called by GridEntityActor when the buff is re-granted while this component
        /// is still alive but expiring. Cancels the expire coroutine and resets state.
        /// </summary>
        public void Restart()
        {
            StopAllCoroutines();
            isExpiring   = false;
            isFlickering = false;
            timeSinceLastSpawn = 0f;
        }

        private IEnumerator ExpireAfterDelay()
        {
            // Wait for in-flight particles to finish their longest possible lifetime
            yield return new WaitForSeconds(NORMAL_LIFETIME);
            Destroy(this); // component only — worker GameObject is unaffected
        }

        // ── Particle spawn ─────────────────────────────────────────────────

        private void SpawnParticle(float lifetime)
        {
            // Spawn in a ring around the worker at a random height — actually surrounds the model
            float angle   = Random.Range(0f, Mathf.PI * 2f);
            float radius  = Random.Range(PARTICLE_RADIUS * 0.5f, PARTICLE_RADIUS);
            float offsetX = Mathf.Cos(angle) * radius;
            float offsetZ = Mathf.Sin(angle) * radius;
            float offsetY = Random.Range(SPAWN_HEIGHT_MIN, SPAWN_HEIGHT_MAX);
            Vector3 spawnPos = transform.position + new Vector3(offsetX, offsetY, offsetZ);

            float size = Random.Range(0.12f, 0.22f);

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "MealBuffParticle";
            sphere.transform.position = spawnPos;
            sphere.transform.localScale = Vector3.one * size;

            // Remove collider — we don't need physics
            Collider col = sphere.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Unlit material in buff color
            Renderer rend = sphere.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = buffMaterial;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
            }

            // Attach the particle animator
            MealBuffParticle particle = sphere.AddComponent<MealBuffParticle>();
            particle.Initialize(lifetime, size);
        }
    }

    /// <summary>
    /// Animates a single meal buff aura sphere: drifts upward, shrinks to zero, self-destructs.
    /// Parented to scene root (not the worker) so it persists through worker destruction.
    /// </summary>
    public class MealBuffParticle : MonoBehaviour
    {
        private float lifetime;
        private float startSize;
        private float elapsed;

        private const float DRIFT_SPEED = 0.6f; // units/sec upward

        public void Initialize(float lifetime, float startSize)
        {
            this.lifetime  = lifetime;
            this.startSize = startSize;
            this.elapsed   = 0f;
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / lifetime);

            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            // Drift upward
            transform.position += Vector3.up * DRIFT_SPEED * Time.deltaTime;

            // Quadratic ease-in shrink: holds size briefly then shrinks fast
            float scaleT = t * t;
            float currentSize = startSize * (1f - scaleT);
            transform.localScale = Vector3.one * Mathf.Max(currentSize, 0f);
        }
    }
}
