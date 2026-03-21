using UnityEngine;

namespace LittleCafe
{
    /// <summary>
    /// Wind Waker–style poof effect: a burst of solid white unlit spheres that
    /// pop outward and scale down to zero. No transparency, no fading — purely
    /// geometric. Stylized and clean.
    ///
    /// Usage: PoofEffect.Spawn(worldPosition)
    ///        PoofEffect.Spawn(worldPosition, count: 12, color: Color.yellow)
    ///
    /// Each sphere is a runtime-generated mesh with an unlit material.
    /// The entire effect self-destructs after all spheres have vanished.
    /// </summary>
    public class PoofEffect : MonoBehaviour
    {
        // ─── Defaults ──────────────────────────────────────────────
        private const int DEFAULT_COUNT = 10;
        private const float DEFAULT_MIN_SIZE = 0.08f;
        private const float DEFAULT_MAX_SIZE = 0.22f;
        private const float DEFAULT_BURST_SPEED = 2.5f;
        private const float DEFAULT_LIFETIME = 0.45f;
        private const float DEFAULT_GRAVITY = 3.0f;

        // ─── Shared mesh (one sphere mesh for all particles) ───────
        private static Mesh sharedSphereMesh;
        private static Material sharedWhiteMaterial;

        // ─── Public spawn methods ──────────────────────────────────

        /// <summary>
        /// Spawn a poof at the given world position with default white spheres.
        /// </summary>
        public static void Spawn(Vector3 position)
        {
            Spawn(position, DEFAULT_COUNT, Color.white, DEFAULT_MIN_SIZE, DEFAULT_MAX_SIZE);
        }

        /// <summary>
        /// Spawn a poof with custom settings.
        /// </summary>
        public static void Spawn(Vector3 position, int count = DEFAULT_COUNT,
            Color? color = null, float minSize = DEFAULT_MIN_SIZE, float maxSize = DEFAULT_MAX_SIZE)
        {
            Color sphereColor = color ?? Color.white;

            GameObject root = new GameObject("PoofEffect");
            root.transform.position = position;
            PoofEffect poof = root.AddComponent<PoofEffect>();
            poof.SpawnSpheres(count, sphereColor, minSize, maxSize);
        }

        // ─── Internal ──────────────────────────────────────────────

        private PoofSphere[] spheres;
        private float elapsed = 0f;
        private float maxLifetime;

        private void SpawnSpheres(int count, Color color, float minSize, float maxSize)
        {
            EnsureSharedResources(color);

            spheres = new PoofSphere[count];
            maxLifetime = 0f;

            for (int i = 0; i < count; i++)
            {
                // Random direction: mostly outward-upward hemisphere
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.8f + 0.2f; // Bias upward
                dir = dir.normalized;

                float startSize = Random.Range(minSize, maxSize);
                float speed = DEFAULT_BURST_SPEED * Random.Range(0.6f, 1.4f);
                float lifetime = DEFAULT_LIFETIME * Random.Range(0.7f, 1.3f);

                // Create sphere GameObject
                GameObject sphereObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphereObj.name = "PoofSphere";
                sphereObj.transform.SetParent(transform, false);
                sphereObj.transform.localPosition = Vector3.zero;
                sphereObj.transform.localScale = Vector3.one * startSize;

                // Remove collider (we don't need physics)
                Collider col = sphereObj.GetComponent<Collider>();
                if (col != null) Destroy(col);

                // Use unlit material with the given color
                Renderer rend = sphereObj.GetComponent<Renderer>();
                if (rend != null)
                {
                    // Each sphere gets its own material instance for potential per-sphere coloring
                    Material mat = new Material(Shader.Find("Unlit/Color"));
                    mat.color = color;
                    rend.material = mat;
                    rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    rend.receiveShadows = false;
                }

                spheres[i] = new PoofSphere
                {
                    transform = sphereObj.transform,
                    direction = dir,
                    speed = speed,
                    startSize = startSize,
                    lifetime = lifetime,
                    velocity = dir * speed
                };

                if (lifetime > maxLifetime) maxLifetime = lifetime;
            }
        }

        private void Update()
        {
            if (spheres == null) { Destroy(gameObject); return; }

            elapsed += Time.deltaTime;

            bool allDone = true;

            for (int i = 0; i < spheres.Length; i++)
            {
                ref PoofSphere s = ref spheres[i];
                if (s.transform == null) continue;

                float t = Mathf.Clamp01(elapsed / s.lifetime);

                if (t >= 1f)
                {
                    // Done — destroy this sphere
                    Destroy(s.transform.gameObject);
                    s.transform = null;
                    continue;
                }

                allDone = false;

                // Apply gravity to velocity
                s.velocity.y -= DEFAULT_GRAVITY * Time.deltaTime;

                // Move
                s.transform.localPosition += s.velocity * Time.deltaTime;

                // Scale down: starts at full size, shrinks to zero
                // Use an ease-in curve so it holds its size briefly then shrinks fast
                float scaleT = t * t; // Quadratic ease-in
                float currentScale = s.startSize * (1f - scaleT);
                s.transform.localScale = Vector3.one * Mathf.Max(currentScale, 0f);
            }

            if (allDone)
            {
                Destroy(gameObject);
            }
        }

        // ─── Shared resources ──────────────────────────────────────

        private static void EnsureSharedResources(Color color)
        {
            // Sphere mesh is auto-created by CreatePrimitive, so no need to cache
            // Just ensure the unlit shader exists
            if (Shader.Find("Unlit/Color") == null)
            {
                Debug.LogWarning("[PoofEffect] Unlit/Color shader not found — spheres may render pink");
            }
        }

        // ─── Data ──────────────────────────────────────────────────

        private struct PoofSphere
        {
            public Transform transform;
            public Vector3 direction;
            public float speed;
            public float startSize;
            public float lifetime;
            public Vector3 velocity;
        }
    }
}
