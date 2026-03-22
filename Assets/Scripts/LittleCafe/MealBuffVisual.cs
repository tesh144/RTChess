using System.Collections;
using UnityEngine;

namespace LittleCafe
{
    /// <summary>
    /// Displays a small golden food icon above a worker while their meal buff is active.
    /// Added at runtime by GridEntityActor when GrantMealBuff fires.
    /// Reads buff state from the sibling GridEntityActor component.
    /// </summary>
    public class MealBuffVisual : MonoBehaviour
    {
        // ── Tuning constants ───────────────────────────────────────────────
        private const float ICON_HEIGHT      = 1.6f;   // units above worker root
        private const float ICON_SCALE       = 0.5f;   // world-space size of the icon
        private const float BOB_AMPLITUDE    = 0.06f;  // up/down bob range
        private const float BOB_SPEED        = 2.5f;   // bob cycles per second
        private const int   FLICKER_TICKS    = 3;      // ticks remaining when flicker starts
        private const float FLICKER_SPEED    = 10f;    // alpha pulses per second during flicker
        private const float FADE_DURATION    = 0.3f;   // seconds for expiry fade-out
        private static readonly Color GOLDEN = new Color(1f, 0.82f, 0.15f, 1f);

        // ── Public — set by GridEntityActor immediately after AddComponent ──
        public Sprite buffIcon;

        // ── Internal ───────────────────────────────────────────────────────
        private GridEntityActor actor;
        private GameObject      iconObject;
        private SpriteRenderer  spriteRenderer;
        private bool            isExpiring;

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

            // Build a child GameObject to hold the sprite
            iconObject = new GameObject("MealBuffIcon");
            iconObject.transform.SetParent(transform, worldPositionStays: false);
            iconObject.transform.localPosition = Vector3.up * ICON_HEIGHT;
            iconObject.transform.localScale    = Vector3.one * ICON_SCALE;

            spriteRenderer = iconObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite       = buffIcon;
            spriteRenderer.color        = GOLDEN;
            spriteRenderer.sortingOrder = 20;   // render on top of world geometry
        }

        void OnDestroy()
        {
            if (iconObject != null)
                Destroy(iconObject);
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

            // Gentle bob
            float bobOffset = Mathf.Sin(Time.time * BOB_SPEED) * BOB_AMPLITUDE;
            iconObject.transform.localPosition = new Vector3(0f, ICON_HEIGHT + bobOffset, 0f);

            // Flicker alpha in the last few ticks
            if (actor.MealBuffTicksRemaining <= FLICKER_TICKS)
            {
                float alpha = Mathf.Abs(Mathf.Sin(Time.time * FLICKER_SPEED));
                spriteRenderer.color = new Color(GOLDEN.r, GOLDEN.g, GOLDEN.b, alpha);
            }
            else
            {
                spriteRenderer.color = GOLDEN;
            }

            // Billboard: rotate to always face the camera
            if (Camera.main != null)
                iconObject.transform.rotation = Camera.main.transform.rotation;
        }

        // ── Public API ─────────────────────────────────────────────────────

        /// <summary>
        /// Called by GridEntityActor when the buff is re-granted while this component
        /// is still alive but expiring. Cancels the fade and resets state.
        /// </summary>
        public void Restart()
        {
            StopAllCoroutines();
            isExpiring = false;
            if (spriteRenderer != null)
                spriteRenderer.color = GOLDEN;
        }

        // ── Expiry ─────────────────────────────────────────────────────────

        private IEnumerator FadeOut()
        {
            float elapsed = 0f;
            while (elapsed < FADE_DURATION)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / FADE_DURATION);
                if (spriteRenderer != null)
                    spriteRenderer.color = new Color(GOLDEN.r, GOLDEN.g, GOLDEN.b, alpha);
                yield return null;
            }
            Destroy(this); // component only — worker GameObject is unaffected
        }
    }
}
