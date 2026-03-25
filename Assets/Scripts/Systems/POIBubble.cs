using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace ClockworkCraft
{
    /// <summary>
    /// World-space billboard bubble shown above a POI.
    /// Pooled by POIManager — call Setup() to activate, Dismiss() to fade out and return to pool.
    ///
    /// Prefab structure:
    ///   POIBubble (root, this script)
    ///     └─ Canvas (World Space, sortingOrder=100)
    ///          ├─ Background (Image)
    ///          └─ Label (TextMeshProUGUI)
    /// </summary>
    public class POIBubble : MonoBehaviour
    {
        [Header("References (set on prefab)")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI label;

        // Animation params — injected by POIManager.Setup via SetAnimParams()
        private float popInDuration = 0.25f;
        private float bobHeight = 0.15f;
        private float bobDuration = 1.4f;
        private float fadeOutDuration = 0.4f;

        // State
        private enum State { Inactive, PoppingIn, Bobbing, Dismissing }
        private State state = State.Inactive;
        private float timer;
        private Vector3 basePosition;
        private float bobTimer;

        // ── Public API ──────────────────────────────────────────────────

        /// <summary>Inject animation params from POIManager inspector fields.</summary>
        public void SetAnimParams(float popIn, float bob, float bobDur, float fadeOut)
        {
            popInDuration = popIn;
            bobHeight = bob;
            bobDuration = bobDur;
            fadeOutDuration = fadeOut;
        }

        /// <summary>Activate this bubble at the given world position with label and color.</summary>
        public void Setup(string text, Color color, Vector3 worldPos)
        {
            basePosition = worldPos;
            transform.position = worldPos;

            if (label != null) label.text = text;
            if (background != null) background.color = color;
            if (canvasGroup != null) canvasGroup.alpha = 1f;

            transform.localScale = Vector3.zero;
            gameObject.SetActive(true);

            state = State.PoppingIn;
            timer = 0f;
            bobTimer = 0f;
        }

        /// <summary>Start fade-out, then deactivate and return to pool.</summary>
        public void Dismiss()
        {
            if (state == State.Inactive || state == State.Dismissing) return;
            state = State.Dismissing;
            timer = 0f;
        }

        public bool IsActive => state != State.Inactive;

        // ── Animation ───────────────────────────────────────────────────

        private void Update()
        {
            switch (state)
            {
                case State.PoppingIn:
                    UpdatePopIn();
                    break;
                case State.Bobbing:
                    UpdateBob();
                    break;
                case State.Dismissing:
                    UpdateDismiss();
                    break;
            }
        }

        private void UpdatePopIn()
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / popInDuration);

            // OutBack easing: overshoot then settle
            float eased = 1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f);
            transform.localScale = Vector3.one * eased;

            if (t >= 1f)
            {
                transform.localScale = Vector3.one;
                state = State.Bobbing;
                bobTimer = 0f;
            }
        }

        private void UpdateBob()
        {
            bobTimer += Time.deltaTime;
            // InOutSine yoyo
            float t = Mathf.PingPong(bobTimer / bobDuration, 1f);
            float eased = -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;
            transform.position = basePosition + Vector3.up * (eased * bobHeight);
        }

        private void UpdateDismiss()
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / fadeOutDuration);

            transform.localScale = Vector3.one * (1f - t);
            if (canvasGroup != null) canvasGroup.alpha = 1f - t;

            if (t >= 1f)
            {
                state = State.Inactive;
                gameObject.SetActive(false);
            }
        }

        // ── Billboarding ────────────────────────────────────────────────

        private void LateUpdate()
        {
            if (state == State.Inactive) return;
            if (Camera.main != null)
                transform.rotation = Camera.main.transform.rotation;
        }
    }
}
