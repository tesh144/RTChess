using System.Collections;
using UnityEngine;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// Displays a small golden food icon above a worker while their meal buff is active.
    /// Pulses on every global interval tick and shrinks progressively so it reaches zero
    /// exactly when the buff expires. Added at runtime by GridEntityActor on buff grant.
    /// </summary>
    public class MealBuffVisual : MonoBehaviour
    {
        // ── Tuning constants ───────────────────────────────────────────────
        private const float ICON_HEIGHT    = 1.6f;   // units above worker root
        private const float ICON_SCALE     = 0.375f; // world-space size at full buff
        private const float BOB_AMPLITUDE  = 0.06f;  // up/down bob range
        private const float BOB_SPEED      = 2.5f;   // bob cycles per second
        private const float PULSE_PEAK     = 1.4f;   // scale multiplier at pulse peak
        private const float PULSE_SETTLE   = 12f;    // speed (units/s) at which pulse settles back
        private const float FADE_DURATION  = 0.3f;   // seconds for expiry fade-out
        private static readonly Color GOLDEN = new Color(1f, 0.82f, 0.15f, 1f);

        // ── Public — set by GridEntityActor immediately after AddComponent ──
        public Sprite buffIcon;

        // ── Internal ───────────────────────────────────────────────────────
        private GridEntityActor actor;
        private GameObject      iconObject;
        private SpriteRenderer  spriteRenderer;
        private bool            isExpiring;

        private int   initialTicks;        // buff ticks at the moment the component was activated
        private float currentBaseScale;    // shrinks each tick toward 0
        private float pulseLerp = 1f;      // 0 = at peak pulse, 1 = settled at currentBaseScale

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
            pulseLerp        = 0f; // fire an initial pop-in pulse

            // Build the icon child object
            iconObject = new GameObject("MealBuffIcon");
            iconObject.transform.SetParent(transform, worldPositionStays: false);
            iconObject.transform.localPosition = Vector3.up * ICON_HEIGHT;
            iconObject.transform.localScale    = Vector3.one * ICON_SCALE;

            spriteRenderer = iconObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite       = buffIcon;
            spriteRenderer.color        = GOLDEN;
            spriteRenderer.sortingOrder = 20;

            if (IntervalTimer.Instance != null)
                IntervalTimer.Instance.OnIntervalTick += OnTick;
        }

        void OnDestroy()
        {
            if (iconObject != null)
                Destroy(iconObject);
            if (IntervalTimer.Instance != null)
                IntervalTimer.Instance.OnIntervalTick -= OnTick;
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

            // Settle the pulse back toward currentBaseScale
            pulseLerp = Mathf.MoveTowards(pulseLerp, 1f, PULSE_SETTLE * Time.deltaTime);
            float displayScale = Mathf.Lerp(currentBaseScale * PULSE_PEAK, currentBaseScale, pulseLerp);
            iconObject.transform.localScale = Vector3.one * Mathf.Max(displayScale, 0f);

            // Gentle bob
            float bobOffset = Mathf.Sin(Time.time * BOB_SPEED) * BOB_AMPLITUDE;
            iconObject.transform.localPosition = new Vector3(0f, ICON_HEIGHT + bobOffset, 0f);

            // Billboard: rotate to always face the camera
            if (Camera.main != null)
                iconObject.transform.rotation = Camera.main.transform.rotation;
        }

        // ── Tick handler ───────────────────────────────────────────────────

        private void OnTick(int intervalCount)
        {
            if (actor == null || isExpiring) return;

            // Shrink base scale proportionally — reaches 0 when ticks hit 0
            float ticksLeft   = Mathf.Max(0, actor.MealBuffTicksRemaining);
            currentBaseScale  = ICON_SCALE * (ticksLeft / (float)initialTicks);

            // Trigger pulse
            pulseLerp = 0f;
        }

        // ── Public API ─────────────────────────────────────────────────────

        /// <summary>
        /// Called by GridEntityActor when the buff is re-granted while this component
        /// is still alive but expiring. Resets size and re-arms the pulse cycle.
        /// </summary>
        public void Restart()
        {
            StopAllCoroutines();
            isExpiring       = false;
            initialTicks     = actor != null ? Mathf.Max(1, actor.MealBuffTicksRemaining) : 8;
            currentBaseScale = ICON_SCALE;
            pulseLerp        = 0f; // immediate pop
            if (spriteRenderer != null)
                spriteRenderer.color = GOLDEN;
        }

        // ── Expiry ─────────────────────────────────────────────────────────

        private IEnumerator FadeOut()
        {
            float startScale   = iconObject != null ? iconObject.transform.localScale.x : 0f;
            float elapsed      = 0f;
            while (elapsed < FADE_DURATION)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / FADE_DURATION;
                float alpha = Mathf.Lerp(1f, 0f, t);
                float scale = Mathf.Lerp(startScale, 0f, t);
                if (spriteRenderer != null)
                    spriteRenderer.color = new Color(GOLDEN.r, GOLDEN.g, GOLDEN.b, alpha);
                if (iconObject != null)
                    iconObject.transform.localScale = Vector3.one * Mathf.Max(scale, 0f);
                yield return null;
            }
            Destroy(this); // component only — worker GameObject is unaffected
        }
    }
}
