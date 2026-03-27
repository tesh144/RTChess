#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;

namespace ClockworkCraft
{
    /// <summary>
    /// Celebratory starburst effect when a POI is discovered (fog reveals the tile).
    /// Spawns 12–16 sparkle sprites that burst radially outward from the bubble
    /// position, colored per BubbleType. Fire-and-forget — self-destructs after
    /// all sparkles have faded.
    ///
    /// Usage:  POIDiscoveryFX.Play(worldPosition, bubbleType);
    ///
    /// Uses SpriteRenderers for reliable world-space rendering with proper
    /// sorting above all 3D geometry.
    /// </summary>
    public class POIDiscoveryFX : MonoBehaviour
    {
        // ─── Tuning ─────────────────────────────────────────────────
        private const int SPARKLE_COUNT_MIN = 12;
        private const int SPARKLE_COUNT_MAX = 16;
        private const float SPARKLE_LIFETIME = 0.55f;
        private const float BURST_SPEED_MIN = 1.8f;
        private const float BURST_SPEED_MAX = 3.5f;
        private const float UPWARD_BIAS = 0.5f;
        private const float SPARKLE_SIZE_START = 0.08f;
        private const float SPARKLE_SIZE_PEAK = 0.22f;
        private const float SPARKLE_SIZE_END = 0.0f;
        private const float ALPHA_FADE_START = 0.6f;
        private const float SPIN_SPEED_MIN = 180f;
        private const float SPIN_SPEED_MAX = 540f;
        private const float GRAVITY = 2.5f;
        private const float CAMERA_SHAKE_INTENSITY = 0.06f;
        private const float CAMERA_SHAKE_DURATION = 0.12f;

        // ─── Shared sprite (white diamond) ──────────────────────────
        private static Sprite diamondSprite;

        // ─── Internal ───────────────────────────────────────────────
        private Sparkle[] sparkles;

        private struct Sparkle
        {
            public Transform transform;
            public SpriteRenderer spriteRenderer;
            public Vector3 velocity;
            public float spinSpeed;
            public Color color;
            public float lifetime;
            public float age;
        }

        // ─── Public API ─────────────────────────────────────────────

        public static void Play(Vector3 worldPos, BubbleType bubbleType)
        {
            EnsureSharedResources();

            Debug.Log($"[POIDiscoveryFX] Playing celebration at {worldPos} for {bubbleType}");

            GameObject fxRoot = new GameObject("POIDiscoveryFX");
            fxRoot.transform.position = worldPos;
            var fx = fxRoot.AddComponent<POIDiscoveryFX>();
            fx.SpawnSparkles(worldPos, bubbleType);

            // Camera shake — skip for low-value grey POIs
            if (bubbleType != BubbleType.POI_Grey)
            {
                ClockworkGrid.CameraSystemLocator.Current?.Shake(CAMERA_SHAKE_INTENSITY, CAMERA_SHAKE_DURATION);
            }

            // SFX
            if (ClockworkGrid.GameSFXManager.Instance != null)
                ClockworkGrid.GameSFXManager.Instance.PlayPOIDiscovery();
        }

        // ─── Shared Resources ───────────────────────────────────────

        private static void EnsureSharedResources()
        {
            if (diamondSprite != null) return;

            // Create a small white diamond texture procedurally.
            // 16x16 is plenty — it'll be scaled by the SpriteRenderer transform.
            int size = 16;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            Color clear = new Color(1f, 1f, 1f, 0f);
            Color white = Color.white;
            Vector2 center = new Vector2(size / 2f, size / 2f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Diamond shape: |x - center| / halfW + |y - center| / halfH <= 1
                    float dx = Mathf.Abs(x - center.x + 0.5f) / (size * 0.35f);  // narrower horizontally
                    float dy = Mathf.Abs(y - center.y + 0.5f) / (size * 0.5f);
                    float d = dx + dy;

                    if (d <= 1f)
                    {
                        // Soft edge — fade alpha near the border
                        float edge = Mathf.Clamp01((1f - d) * 4f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, edge));
                    }
                    else
                    {
                        tex.SetPixel(x, y, clear);
                    }
                }
            }
            tex.Apply();

            // Create sprite: 16 pixels per unit → the sprite is 1x1 world unit before scaling
            diamondSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            diamondSprite.name = "SparkleeDiamond";
        }

        // ─── Color Palettes ─────────────────────────────────────────

        private static Color GetSparkleColor(BubbleType type)
        {
            switch (type)
            {
                case BubbleType.POI_Gold:
                    return Color.Lerp(
                        new Color(0.95f, 0.80f, 0.30f, 1f),
                        new Color(1.0f, 0.95f, 0.6f, 1f),
                        Random.value);

                case BubbleType.POI_Red:
                    return Color.Lerp(
                        new Color(0.95f, 0.30f, 0.20f, 1f),
                        new Color(1.0f, 0.60f, 0.25f, 1f),
                        Random.value);

                case BubbleType.POI_Grey:
                default:
                    return Color.Lerp(
                        new Color(0.65f, 0.72f, 0.85f, 1f),
                        new Color(0.85f, 0.90f, 1.0f, 1f),
                        Random.value);
            }
        }

        // ─── Spawning ───────────────────────────────────────────────

        private void SpawnSparkles(Vector3 center, BubbleType bubbleType)
        {
            int count = Random.Range(SPARKLE_COUNT_MIN, SPARKLE_COUNT_MAX + 1);
            sparkles = new Sparkle[count];

            for (int i = 0; i < count; i++)
            {
                // Direction: radial with upward bias
                float angle = (i / (float)count) * 360f + Random.Range(-15f, 15f);
                float rad = angle * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(
                    Mathf.Cos(rad),
                    UPWARD_BIAS + Random.Range(0f, 0.4f),
                    Mathf.Sin(rad)
                ).normalized;

                float speed = Random.Range(BURST_SPEED_MIN, BURST_SPEED_MAX);
                Color col = GetSparkleColor(bubbleType);

                // Create sparkle with SpriteRenderer
                GameObject obj = new GameObject($"Sparkle_{i}");
                obj.transform.SetParent(transform, false);
                obj.transform.position = center;
                obj.transform.localScale = Vector3.one * SPARKLE_SIZE_START;

                SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
                sr.sprite = diamondSprite;
                sr.color = col;
                sr.sortingOrder = 100;   // well above all world geometry

                sparkles[i] = new Sparkle
                {
                    transform = obj.transform,
                    spriteRenderer = sr,
                    velocity = dir * speed,
                    spinSpeed = Random.Range(SPIN_SPEED_MIN, SPIN_SPEED_MAX) * (Random.value > 0.5f ? 1f : -1f),
                    color = col,
                    lifetime = SPARKLE_LIFETIME * Random.Range(0.8f, 1.2f),
                    age = 0f
                };
            }
        }

        // ─── Update ─────────────────────────────────────────────────

        private void Update()
        {
            bool allDone = true;
            float dt = Time.deltaTime;

            // Billboard: face camera
            Camera cam = Camera.main;
            Quaternion camRot = cam != null ? cam.transform.rotation : Quaternion.identity;

            for (int i = 0; i < sparkles.Length; i++)
            {
                ref Sparkle s = ref sparkles[i];
                if (s.transform == null) continue;

                s.age += dt;
                float t = Mathf.Clamp01(s.age / s.lifetime);

                if (t >= 1f)
                {
                    Destroy(s.transform.gameObject);
                    s.transform = null;
                    continue;
                }

                allDone = false;

                // Movement: burst outward with gentle gravity
                s.velocity.y -= GRAVITY * dt;
                s.transform.position += s.velocity * dt;

                // Billboard + spin
                s.transform.rotation = camRot * Quaternion.Euler(0f, 0f, s.spinSpeed * s.age);

                // Scale: quick ramp up then shrink
                float scaleCurve;
                if (t < 0.2f)
                {
                    scaleCurve = Mathf.Lerp(SPARKLE_SIZE_START, SPARKLE_SIZE_PEAK, t / 0.2f);
                }
                else
                {
                    float shrinkT = (t - 0.2f) / 0.8f;
                    scaleCurve = Mathf.Lerp(SPARKLE_SIZE_PEAK, SPARKLE_SIZE_END, shrinkT * shrinkT);
                }
                s.transform.localScale = Vector3.one * scaleCurve;

                // Alpha: full until ALPHA_FADE_START, then fade to 0
                float alpha = 1f;
                if (t > ALPHA_FADE_START)
                {
                    alpha = 1f - ((t - ALPHA_FADE_START) / (1f - ALPHA_FADE_START));
                }
                Color c = s.color;
                c.a = alpha;
                s.spriteRenderer.color = c;
            }

            if (allDone)
            {
                Destroy(gameObject);
            }
        }
    }
}
