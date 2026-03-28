using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ClockworkGrid;

namespace ClockworkCraft
{
    /// <summary>
    /// World-space bubble with variant toggling and animations.
    /// Used for both POI markers (fog-edge hints) and building popups (Insert/Collect/Alert).
    ///
    /// Lifecycle: Setup() → RisingIn → [DrawingTether] → Bobbing → Dismiss() → Inactive
    ///
    /// Variants are children named after BubbleType enum values. Setup() hides all,
    /// then enables the matching one. UIPanel tracks them by name.
    /// </summary>
    [RequireComponent(typeof(UIPanel))]
    public class POIBubble : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Rise Animation")]
        [Tooltip("How far below the target position the bubble starts.")]
        [SerializeField] private float riseDistance = 2.5f;
        [Tooltip("Duration of the rise-in phase (seconds).")]
        [SerializeField] private float riseInDuration = 0.7f;

        [Header("Tether")]
        [Tooltip("Duration of the tether draw phase after rise completes (seconds). POI only.")]
        [SerializeField] private float tetherDrawDuration = 0.45f;
        [Tooltip("Width at the bubble end.")]
        [SerializeField] private float tetherWidthTop = 0.25f;
        [Tooltip("Width at the ground end.")]
        [SerializeField] private float tetherWidthBottom = 0.02f;

        [Header("Bob")]
        [Tooltip("Vertical bob amplitude (world units).")]
        [SerializeField] private float bobHeight = 0.15f;
        [Tooltip("Full bob cycle duration (seconds).")]
        [SerializeField] private float bobDuration = 1.4f;

        [Header("Dismiss")]
        [Tooltip("Duration of the fade-out/shrink on dismiss (seconds).")]
        [SerializeField] private float fadeOutDuration = 0.4f;

        // ── Internal State ───────────────────────────────────────────────

        private UIPanel panel;
        private BubbleType currentType;
        private GameObject activeChild;
        private Vector3 targetScale = Vector3.one;

        private enum State { Inactive, RisingIn, DrawingTether, Bobbing, Dismissing }
        private State state = State.Inactive;
        private float timer;
        private Vector3 basePosition;
        private float bobTimer;

        // Tether
        private LineRenderer tether;
        private Vector3 groundPosition;

        // Cached enum names for variant lookup
        private static readonly string[] bubbleTypeNames;
        static POIBubble()
        {
            var values = System.Enum.GetValues(typeof(BubbleType));
            bubbleTypeNames = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                bubbleTypeNames[i] = values.GetValue(i).ToString();
        }

        // ── Lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            panel = GetComponent<UIPanel>();
            HideAllVariants();
        }

        private void OnDestroy()
        {
            if (tether != null)
                Destroy(tether.gameObject);
        }

        // ── Properties ───────────────────────────────────────────────────

        public bool IsActive => state != State.Inactive;
        public BubbleType CurrentType => currentType;
        public GameObject ActiveChild => activeChild;
        public UIPanel Panel => panel;

        /// <summary>Fired when the bubble is tapped/clicked. Subscribe to handle collect/interact.</summary>
        public event Action OnTapped;

        /// <summary>Set the world-space scale for this bubble. Animations scale relative to this.</summary>
        public void SetTargetScale(Vector3 scale) => targetScale = scale;

        /// <summary>Override animation params. Used by BuildingProductionManager for Insert/Collect bubbles.</summary>
        public void SetAnimParams(float riseIn, float bob, float bobDur, float fadeOut)
        {
            riseInDuration = riseIn;
            bobHeight = bob;
            bobDuration = bobDur;
            fadeOutDuration = fadeOut;
        }

        /// <summary>Find the Image named "Icon" within the active variant.</summary>
        public Image GetIconImage()
        {
            if (activeChild == null) return null;
            foreach (var img in activeChild.GetComponentsInChildren<Image>(true))
                if (img.gameObject.name == "Icon") return img;
            return null;
        }

        /// <summary>Find the Image named "Fill" within the active variant (for HoldToFill progress bar).</summary>
        public Image GetFillImage()
        {
            if (activeChild == null) return null;
            foreach (var img in activeChild.GetComponentsInChildren<Image>(true))
                if (img.gameObject.name == "Fill") return img;
            return null;
        }

        /// <summary>Returns true if UIPanel has a child for this BubbleType.</summary>
        public bool HasVariant(BubbleType type)
        {
            return panel != null && panel.Get(type.ToString()) != null;
        }

        // ── Setup / Dismiss ──────────────────────────────────────────────

        /// <summary>
        /// Activate this bubble. Shows the matching variant, starts the rise animation.
        /// POI types (Gold/Grey/Red) get a tether line; building types don't.
        /// </summary>
        public void Setup(BubbleType bubbleType, string text, Vector3 worldPos, Sprite icon = null)
        {
            basePosition = worldPos;
            currentType = bubbleType;

            // Show the correct variant
            HideAllVariants();
            if (panel != null)
            {
                var obj = panel.GetObject(bubbleType.ToString());
                if (obj != null)
                {
                    EnableWithParents(obj);
                    activeChild = obj;

                    var tmp = obj.GetComponentInChildren<TextMeshProUGUI>();
                    if (tmp != null) tmp.text = text;

                    if (icon != null)
                    {
                        var iconImg = GetIconImage();
                        if (iconImg != null)
                        {
                            iconImg.sprite = icon;
                            iconImg.enabled = true;
                        }
                    }

                    // Make the variant tappable via Unity UI
                    WireUpButton(obj);
                }
            }

            // Alpha starts at zero — rise animation fades in
            if (canvasGroup != null) canvasGroup.alpha = 0f;

            // Tether only for POI bubbles
            bool isPOI = bubbleType == BubbleType.POI_Gold ||
                         bubbleType == BubbleType.POI_Grey ||
                         bubbleType == BubbleType.POI_Red;
            if (isPOI)
            {
                SetupTether(bubbleType, worldPos);
                if (tether != null) tether.gameObject.SetActive(false);
            }

            // Start below target
            transform.position = worldPos + Vector3.down * riseDistance;
            transform.localScale = Vector3.zero;
            gameObject.SetActive(true);

            state = State.RisingIn;
            timer = 0f;
            bobTimer = 0f;
        }

        /// <summary>Add a Button to the variant so UI taps fire OnTapped.</summary>
        private void WireUpButton(GameObject variant)
        {
            // Need a raycast-target Image for the Button to work.
            // Use the first Image found, or add a transparent one.
            var img = variant.GetComponent<Image>();
            if (img != null)
                img.raycastTarget = true;

            var btn = variant.GetComponent<Button>();
            if (btn == null) btn = variant.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnTapped?.Invoke());

            // Make the button fully transparent (no visual change on press)
            var colors = btn.colors;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = Color.white;
            btn.colors = colors;
        }

        /// <summary>Start fade-out, then deactivate.</summary>
        public void Dismiss()
        {
            if (state == State.Inactive || state == State.Dismissing) return;
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            transform.localScale = targetScale;
            OnTapped = null; // Clear subscribers so pooled bubbles don't fire stale callbacks
            state = State.Dismissing;
            timer = 0f;
        }

        /// <summary>Set the label text on the active variant.</summary>
        public void SetLabel(string text)
        {
            if (activeChild == null) return;
            var tmp = activeChild.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.text = text;
        }

        // ── Variant Toggling ─────────────────────────────────────────────

        private void HideAllVariants()
        {
            // Hide EVERY descendant — not just direct children.
            // This ensures nested variants (inside AllBubbles etc.) are all off.
            var all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == transform) continue;
                all[i].gameObject.SetActive(false);
            }
            activeChild = null;
        }

        /// <summary>Enable obj, all its descendants, and every parent up to root.</summary>
        private void EnableWithParents(GameObject obj)
        {
            // Enable the variant and ALL its children (Icon, Fill, backgrounds, etc.)
            foreach (var child in obj.GetComponentsInChildren<Transform>(true))
                child.gameObject.SetActive(true);

            // Enable parent chain so the variant is actually visible
            Transform t = obj.transform.parent;
            while (t != null && t != transform)
            {
                t.gameObject.SetActive(true);
                t = t.parent;
            }
        }

        // ── Tether ───────────────────────────────────────────────────────

        private static readonly Color fogGrey = new Color(0.78f, 0.78f, 0.78f, 0f);

        private static Color GetTetherColor(BubbleType type)
        {
            switch (type)
            {
                case BubbleType.POI_Gold: return new Color(0.95f, 0.75f, 0.25f, 0.9f);
                case BubbleType.POI_Grey: return new Color(0.55f, 0.60f, 0.72f, 0.9f);
                case BubbleType.POI_Red:  return new Color(0.90f, 0.25f, 0.25f, 0.9f);
                default:                  return new Color(0.55f, 0.60f, 0.72f, 0.9f);
            }
        }

        private void SetupTether(BubbleType bubbleType, Vector3 bubblePos)
        {
            groundPosition = new Vector3(bubblePos.x, -0.5f, bubblePos.z);

            if (tether == null)
            {
                var tetherObj = new GameObject("Tether");
                tetherObj.transform.SetParent(transform.parent ?? transform, false);
                tether = tetherObj.AddComponent<LineRenderer>();
                tether.material = new Material(Shader.Find("Sprites/Default"))
                    { renderQueue = 3100 };
                tether.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tether.receiveShadows = false;
                tether.allowOcclusionWhenDynamic = false;
                tether.positionCount = 2;
                tether.useWorldSpace = true;
                tether.sortingOrder = 5;
                tether.numCapVertices = 2;
            }

            tether.startWidth = tetherWidthTop;
            tether.endWidth = tetherWidthBottom;

            Color top = GetTetherColor(bubbleType);
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(top, 0f), new GradientColorKey(fogGrey, 1f) },
                new[] { new GradientAlphaKey(top.a, 0f), new GradientAlphaKey(0.15f, 0.6f), new GradientAlphaKey(0f, 1f) }
            );
            tether.colorGradient = gradient;
        }

        // ── Animation ────────────────────────────────────────────────────

        private void Update()
        {
            switch (state)
            {
                case State.RisingIn:      UpdateRiseIn();      break;
                case State.DrawingTether: UpdateDrawTether();  break;
                case State.Bobbing:       UpdateBob();         break;
                case State.Dismissing:    UpdateDismiss();     break;
            }
        }

        private void LateUpdate()
        {
            // Billboard: face camera
            if (state != State.Inactive && Camera.main != null)
                transform.rotation = Camera.main.transform.rotation;
        }

        private void UpdateRiseIn()
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / riseInDuration);

            // Position: EaseOutQuad
            float posEased = t * (2f - t);
            transform.position = Vector3.Lerp(basePosition + Vector3.down * riseDistance, basePosition, posEased);

            // Scale: OutBack
            float s = 1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f);
            transform.localScale = targetScale * s;

            // Alpha: fade in over first 60%
            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.Clamp01(t / 0.6f);

            if (t >= 1f)
            {
                transform.position = basePosition;
                transform.localScale = targetScale;
                if (canvasGroup != null) canvasGroup.alpha = 1f;

                if (tether != null)
                {
                    // Start tether draw phase
                    state = State.DrawingTether;
                    timer = 0f;
                    tether.gameObject.SetActive(true);
                    tether.SetPosition(0, basePosition);
                    tether.SetPosition(1, basePosition);
                }
                else
                {
                    state = State.Bobbing;
                    bobTimer = 0f;
                }
            }
        }

        private void UpdateDrawTether()
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / tetherDrawDuration);

            // EaseOutCubic: sweep bottom from bubble to ground
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            if (tether != null)
            {
                tether.SetPosition(0, basePosition);
                tether.SetPosition(1, Vector3.Lerp(basePosition, groundPosition, eased));
            }

            if (t >= 1f)
            {
                state = State.Bobbing;
                bobTimer = 0f;
            }
        }

        private void UpdateBob()
        {
            bobTimer += Time.deltaTime;
            float t = Mathf.PingPong(bobTimer / bobDuration, 1f);
            float eased = -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;
            Vector3 pos = basePosition + Vector3.up * (eased * bobHeight);
            transform.position = pos;

            if (tether != null && tether.gameObject.activeSelf)
            {
                tether.SetPosition(0, pos);
                tether.SetPosition(1, groundPosition);
            }
        }

        private void UpdateDismiss()
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / fadeOutDuration);

            transform.localScale = targetScale * (1f - t);
            if (canvasGroup != null) canvasGroup.alpha = 1f - t;

            if (tether != null && tether.gameObject.activeSelf)
                tether.gameObject.SetActive(false);

            if (t >= 1f)
            {
                state = State.Inactive;
                HideAllVariants();
                gameObject.SetActive(false);
            }
        }
    }
}
