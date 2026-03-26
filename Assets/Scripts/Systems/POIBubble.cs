using UnityEngine;
using TMPro;
using ClockworkGrid;

namespace ClockworkCraft
{
    /// <summary>
    /// World-space billboard bubble with variant toggling and animations.
    /// Pooled by POIManager — call Setup() to activate, Dismiss() to fade out and return to pool.
    ///
    /// Uses UIPanel (on the same GameObject) to access children by name.
    /// Children named after BubbleType enum values (POI_Gold, POI_Grey, etc.)
    /// are toggled on/off — only the active variant is visible.
    /// </summary>
    [RequireComponent(typeof(UIPanel))]
    public class POIBubble : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasGroup canvasGroup;

        private UIPanel panel;
        private BubbleType currentType;
        private GameObject activeChild;

        // All BubbleType names cached once
        private static readonly string[] bubbleTypeNames;

        static POIBubble()
        {
            var values = System.Enum.GetValues(typeof(BubbleType));
            bubbleTypeNames = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                bubbleTypeNames[i] = values.GetValue(i).ToString();
        }

        // Animation params — injected by POIManager via SetAnimParams()
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

        // ── Lifecycle ─────────────────────────────────────────────────────

        private void Awake()
        {
            panel = GetComponent<UIPanel>();
        }

        // ── Properties ────────────────────────────────────────────────────

        public bool IsActive => state != State.Inactive;
        public BubbleType CurrentType => currentType;
        public GameObject ActiveChild => activeChild;

        /// <summary>The underlying UIPanel for direct element access.</summary>
        public UIPanel Panel => panel;

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>Inject animation params from POIManager inspector fields.</summary>
        public void SetAnimParams(float popIn, float bob, float bobDur, float fadeOut)
        {
            popInDuration = popIn;
            bobHeight = bob;
            bobDuration = bobDur;
            fadeOutDuration = fadeOut;
        }

        /// <summary>
        /// Activate this bubble at the given world position.
        /// Toggles the matching BubbleType child on (all others off) and sets the label.
        /// </summary>
        public void Setup(BubbleType bubbleType, string text, Vector3 worldPos)
        {
            basePosition = worldPos;
            transform.position = worldPos;

            // Toggle the correct variant via UIPanel
            HideAllVariants();
            currentType = bubbleType;

            if (panel != null)
            {
                var obj = panel.GetObject(bubbleType.ToString());
                if (obj != null)
                {
                    obj.SetActive(true);
                    activeChild = obj;

                    var tmp = obj.GetComponentInChildren<TextMeshProUGUI>();
                    if (tmp != null) tmp.text = text;
                }
            }

            if (canvasGroup != null) canvasGroup.alpha = 1f;

            transform.localScale = Vector3.zero;
            gameObject.SetActive(true);

            state = State.PoppingIn;
            timer = 0f;
            bobTimer = 0f;
        }

        /// <summary>Legacy overload for backwards compatibility.</summary>
        public void Setup(string text, Color color, Vector3 worldPos)
        {
            Setup(BubbleType.POI_Grey, text, worldPos);
        }

        /// <summary>Start fade-out, then deactivate and return to pool.</summary>
        public void Dismiss()
        {
            if (state == State.Inactive || state == State.Dismissing) return;
            state = State.Dismissing;
            timer = 0f;
        }

        /// <summary>Set the label text on the currently active variant.</summary>
        public void SetLabel(string text)
        {
            if (activeChild == null) return;
            var tmp = activeChild.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.text = text;
        }

        /// <summary>Returns true if UIPanel has a child for this BubbleType.</summary>
        public bool HasVariant(BubbleType type)
        {
            return panel != null && panel.Get(type.ToString()) != null;
        }

        // ── Variant Toggling ──────────────────────────────────────────────

        private void HideAllVariants()
        {
            if (panel == null) return;

            for (int i = 0; i < bubbleTypeNames.Length; i++)
            {
                var obj = panel.GetObject(bubbleTypeNames[i]);
                if (obj != null) obj.SetActive(false);
            }

            activeChild = null;
        }

        // ── Animation ─────────────────────────────────────────────────────

        private void Update()
        {
            switch (state)
            {
                case State.PoppingIn:  UpdatePopIn();  break;
                case State.Bobbing:    UpdateBob();    break;
                case State.Dismissing: UpdateDismiss(); break;
            }
        }

        private void UpdatePopIn()
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / popInDuration);

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
                HideAllVariants();
                gameObject.SetActive(false);
            }
        }

        // ── Billboarding ──────────────────────────────────────────────────

        private void LateUpdate()
        {
            if (state == State.Inactive) return;
            if (Camera.main != null)
                transform.rotation = Camera.main.transform.rotation;
        }
    }
}
