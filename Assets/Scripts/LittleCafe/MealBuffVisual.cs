using System.Collections;
using UnityEngine;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// Displays a small golden food icon above a worker while their meal buff is active,
    /// and tints the worker's body renderers with a warm golden glow that pulses with
    /// the interval timer and fades as the buff runs down.
    ///
    /// Added at runtime by GridEntityActor on buff grant.
    /// Removed automatically when the buff expires (via FadeOut coroutine).
    /// </summary>
    public class MealBuffVisual : MonoBehaviour
    {
        // ── Icon tuning ────────────────────────────────────────────────────
        private const float ICON_HEIGHT    = 1.6f;   // units above worker root
        private const float ICON_SCALE     = 0.375f; // world-space size at full buff
        private const float BOB_AMPLITUDE  = 0.06f;  // up/down bob range
        private const float BOB_SPEED      = 2.5f;   // bob cycles per second
        private const float PULSE_PEAK     = 1.4f;   // scale/alpha multiplier at pulse peak
        private const float PULSE_SETTLE   = 12f;    // speed (units/s) at which pulse settles back
        private const float FADE_DURATION  = 0.3f;   // seconds for expiry fade-out
        private static readonly Color GOLDEN    = new Color(1f, 0.82f, 0.15f, 1f);

        // ── Body tint tuning ───────────────────────────────────────────────
        private const int   FAST_FADE_TICKS = 3;    // last N ticks use accelerated decay
        private static readonly Color BODY_TINT = new Color(1f, 0.85f, 0.4f, 1f); // warm gold

        // ── Public — set by GridEntityActor immediately after AddComponent ──
        public Sprite buffIcon;

        // ── Icon state ─────────────────────────────────────────────────────
        private GridEntityActor actor;
        private GameObject      iconObject;
        private SpriteRenderer  iconSpriteRenderer;
        private bool            isExpiring;
        private int             initialTicks;       // buff ticks at the moment of activation
        private float           currentBaseScale;   // shrinks each tick toward 0
        private float           pulseLerp = 1f;     // 0 = at peak pulse, 1 = settled

        // ── Body tint state ────────────────────────────────────────────────
        private Renderer[]  bodyRenderers;   // captured before icon child is created
        private Material[]  sharedMats;      // original shared materials — restored in OnDestroy
        private Material[]  bodyMaterials;   // instantiated copies modified for tint
        private Color[]     originalColors;  // per-renderer original colour
        private float       tintAlpha = 1f;  // current tint intensity, decays per tick

        // ── Lifecycle ──────────────────────────────────────────────────────

        void Start()
        {
            actor = GetComponent<GridEntityActor>();

            if (actor == null)
            {
                Debug.LogWarning("[MealBuffVisual] No GridEntityActor found — removing self.");
                Destroy(this);
                return;
            }

            if (buffIcon == null)
            {
                Debug.LogWarning("[MealBuffVisual] buffIcon not assigned — removing self.");
                Destroy(this);
                return;
            }

            initialTicks     = Mathf.Max(1, actor.MealBuffTicksRemaining);
            currentBaseScale = ICON_SCALE;
            pulseLerp        = 0f; // fire initial pop-in pulse
            tintAlpha        = 1f;

            // ── Body tint — MUST happen before icon child is created ───────
            // GetComponentsInChildren would otherwise capture the icon's SpriteRenderer too.
            // Only include renderers whose shared material supports the _Color property —
            // shaders like Unlit/Texture don't have _Color and would throw errors on .color access.
            var allRenderers = GetComponentsInChildren<Renderer>();
            var tintable = new System.Collections.Generic.List<Renderer>();
            foreach (var r in allRenderers)
                if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Color"))
                    tintable.Add(r);

            bodyRenderers  = tintable.ToArray();
            sharedMats     = new Material[bodyRenderers.Length];
            bodyMaterials  = new Material[bodyRenderers.Length];
            originalColors = new Color[bodyRenderers.Length];
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                sharedMats[i]     = bodyRenderers[i].sharedMaterial;
                bodyMaterials[i]  = bodyRenderers[i].material; // auto-instantiates per-instance copy
                originalColors[i] = sharedMats[i].HasProperty("_Color") ? sharedMats[i].color : Color.white;
            }
            ApplyTint(tintAlpha);

            // ── Icon child ─────────────────────────────────────────────────
            iconObject = new GameObject("MealBuffIcon");
            iconObject.transform.SetParent(transform, worldPositionStays: false);
            iconObject.transform.localPosition = Vector3.up * ICON_HEIGHT;
            iconObject.transform.localScale    = Vector3.one * ICON_SCALE;

            iconSpriteRenderer = iconObject.AddComponent<SpriteRenderer>();
            iconSpriteRenderer.sprite       = buffIcon;
            iconSpriteRenderer.color        = GOLDEN;
            iconSpriteRenderer.sortingOrder = 20;

            if (IntervalTimer.Instance != null)
                IntervalTimer.Instance.OnIntervalTick += OnTick;
        }

        void OnDestroy()
        {
            if (iconObject != null)
                Destroy(iconObject);

            if (IntervalTimer.Instance != null)
                IntervalTimer.Instance.OnIntervalTick -= OnTick;

            // Restore each renderer to its shared material, then destroy our instances
            if (bodyRenderers != null)
            {
                for (int i = 0; i < bodyRenderers.Length; i++)
                {
                    if (bodyRenderers[i] != null && sharedMats[i] != null)
                        bodyRenderers[i].sharedMaterial = sharedMats[i];
                    if (bodyMaterials[i] != null)
                        Destroy(bodyMaterials[i]);
                }
            }
        }

        void Update()
        {
            if (actor == null || iconObject == null) return;
            if (isExpiring) return;

            // Detect buff expiry every frame — avoids tick-ordering race with GridEntityActor
            if (!actor.HasMealBuff)
            {
                isExpiring = true;
                StartCoroutine(FadeOut());
                return;
            }

            // Settle pulse back toward current base scale/tint
            pulseLerp = Mathf.MoveTowards(pulseLerp, 1f, PULSE_SETTLE * Time.deltaTime);

            // ── Icon ──────────────────────────────────────────────────────
            float displayScale = Mathf.Lerp(currentBaseScale * PULSE_PEAK, currentBaseScale, pulseLerp);
            iconObject.transform.localScale = Vector3.one * Mathf.Max(displayScale, 0f);

            float bobOffset = Mathf.Sin(Time.time * BOB_SPEED) * BOB_AMPLITUDE;
            iconObject.transform.localPosition = new Vector3(0f, ICON_HEIGHT + bobOffset, 0f);

            if (Camera.main != null)
                iconObject.transform.rotation = Camera.main.transform.rotation;

            // ── Body tint pulse ───────────────────────────────────────────
            if (bodyMaterials != null && bodyMaterials.Length > 0)
            {
                float displayAlpha = Mathf.Lerp(Mathf.Min(tintAlpha * PULSE_PEAK, 1f), tintAlpha, pulseLerp);
                ApplyTint(displayAlpha);
            }
        }

        // ── Tick handler ───────────────────────────────────────────────────

        private void OnTick(int intervalCount)
        {
            if (actor == null || isExpiring) return;

            // ── Icon: shrink proportionally ───────────────────────────────
            float ticksLeft      = Mathf.Max(0, actor.MealBuffTicksRemaining);
            currentBaseScale     = ICON_SCALE * (ticksLeft / (float)initialTicks);

            // ── Body tint: linear decay with quadratic acceleration in last FAST_FADE_TICKS ──
            float linearDecay    = initialTicks > 0 ? ticksLeft / (float)initialTicks : 0f;
            float fastMultiplier = ticksLeft <= FAST_FADE_TICKS
                ? (ticksLeft / (float)FAST_FADE_TICKS)
                : 1f;
            tintAlpha = linearDecay * fastMultiplier;

            // Fire pulse (both icon and tint settle from peak this frame)
            pulseLerp = 0f;
        }

        // ── Public API ─────────────────────────────────────────────────────

        /// <summary>
        /// Called by GridEntityActor when the buff is re-granted while this component
        /// is still alive. Resets size, tint, and pulse cycle to full.
        /// </summary>
        public void Restart()
        {
            StopAllCoroutines();
            isExpiring       = false;
            initialTicks     = actor != null ? Mathf.Max(1, actor.MealBuffTicksRemaining) : 8;
            currentBaseScale = ICON_SCALE;
            tintAlpha        = 1f;
            pulseLerp        = 0f; // immediate pop on both icon and tint

            if (iconSpriteRenderer != null)
                iconSpriteRenderer.color = GOLDEN;

            ApplyTint(1f);
        }

        // ── Expiry ─────────────────────────────────────────────────────────

        private IEnumerator FadeOut()
        {
            float startScale     = iconObject != null ? iconObject.transform.localScale.x : 0f;
            float startTintAlpha = tintAlpha;
            float elapsed        = 0f;

            while (elapsed < FADE_DURATION)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / FADE_DURATION;

                // Icon
                float alpha = Mathf.Lerp(1f, 0f, t);
                float scale = Mathf.Lerp(startScale, 0f, t);
                if (iconSpriteRenderer != null)
                    iconSpriteRenderer.color = new Color(GOLDEN.r, GOLDEN.g, GOLDEN.b, alpha);
                if (iconObject != null)
                    iconObject.transform.localScale = Vector3.one * Mathf.Max(scale, 0f);

                // Body tint
                ApplyTint(Mathf.Lerp(startTintAlpha, 0f, t));

                yield return null;
            }

            // Ensure fully restored before component is destroyed
            ApplyTint(0f);
            Destroy(this); // component only — worker GameObject is unaffected
        }

        // ── Private helpers ────────────────────────────────────────────────

        private void ApplyTint(float alpha)
        {
            if (bodyMaterials == null) return;
            for (int i = 0; i < bodyMaterials.Length; i++)
            {
                if (bodyMaterials[i] == null) continue;
                if (!bodyMaterials[i].HasProperty("_Color")) continue;
                bodyMaterials[i].color = Color.Lerp(originalColors[i], BODY_TINT, alpha);
            }
        }
    }
}
