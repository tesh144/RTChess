using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using ClockworkGrid;
using LittleCafe;

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
        private float animScaleMultiplier = 1f;
        private Image cachedIconImage;
        private Image cachedFillImage;

        private enum State { Inactive, RisingIn, DrawingTether, Bobbing, Dismissing }
        private State state = State.Inactive;
        private float timer;
        private Vector3 basePosition;
        private float bobTimer;

        // Tether
        private LineRenderer tether;
        private Vector3 groundPosition;

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

        /// <summary>Fired on quick tap (pointer down + up within holdThreshold).</summary>
        public event Action OnTapped;
        /// <summary>Fired when pointer is held down beyond holdThreshold.</summary>
        public event Action OnHoldStarted;
        /// <summary>Fired when pointer is released after a hold.</summary>
        public event Action OnHoldEnded;

        internal void FireTapped() => OnTapped?.Invoke();
        internal void FireHoldStarted() => OnHoldStarted?.Invoke();
        internal void FireHoldEnded() => OnHoldEnded?.Invoke();

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

        /// <summary>Find the Image named "Icon" within the active variant. Cached after first lookup.</summary>
        public Image GetIconImage()
        {
            if (cachedIconImage != null) return cachedIconImage;
            if (activeChild == null) return null;
            foreach (var img in activeChild.GetComponentsInChildren<Image>(true))
                if (img.gameObject.name == "Icon") { cachedIconImage = img; return img; }
            return null;
        }

        /// <summary>Find the Image named "Fill" within the active variant. Cached after first lookup.</summary>
        public Image GetFillImage()
        {
            if (cachedFillImage != null) return cachedFillImage;
            if (activeChild == null) return null;
            foreach (var img in activeChild.GetComponentsInChildren<Image>(true))
                if (img.gameObject.name == "Fill") { cachedFillImage = img; return img; }
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

                    // Set icon if provided, otherwise leave as-is (caller sets after Setup)
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

        /// <summary>Wire up pointer events on the variant for tap/hold detection.</summary>
        private void WireUpButton(GameObject variant)
        {
            var img = variant.GetComponent<Image>();
            if (img != null)
                img.raycastTarget = true;

            // Remove old Button if present (we use BubbleTapHandler instead)
            var oldBtn = variant.GetComponent<Button>();
            if (oldBtn != null) Destroy(oldBtn);

            var handler = variant.GetComponent<BubbleTapHandler>();
            if (handler == null) handler = variant.AddComponent<BubbleTapHandler>();
            handler.Setup(this);
        }

        /// <summary>Start fade-out, then deactivate.</summary>
        public void Dismiss()
        {
            if (state == State.Inactive || state == State.Dismissing) return;
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            animScaleMultiplier = 1f;
            OnTapped = null;
            OnHoldStarted = null;
            OnHoldEnded = null;
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
            for (int i = 0; i < transform.childCount; i++)
                SetActiveRecursive(transform.GetChild(i), false);
            activeChild = null;
            cachedIconImage = null;
            cachedFillImage = null;
        }

        /// <summary>Enable obj, all its descendants, and every parent up to root.</summary>
        private void EnableWithParents(GameObject obj)
        {
            // Zero-allocation: recursively enable the variant and all children
            SetActiveRecursive(obj.transform, true);

            // Enable parent chain so the variant is actually visible
            Transform t = obj.transform.parent;
            while (t != null && t != transform)
            {
                t.gameObject.SetActive(true);
                t = t.parent;
            }
        }

        private static void SetActiveRecursive(Transform t, bool active)
        {
            t.gameObject.SetActive(active);
            for (int i = 0; i < t.childCount; i++)
                SetActiveRecursive(t.GetChild(i), active);
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
            if (state == State.Inactive) return;

            // Billboard: face camera
            if (Camera.main != null)
                transform.rotation = Camera.main.transform.rotation;

            // Combine animation scale with zoom compensation
            float zoomMul = 1f;
            if (GridCamera.Instance != null)
            {
                float currentZoom = GridCamera.Instance.CurrentDistance;
                float zoomT = Mathf.Clamp01((currentZoom - 5f) / (40f - 5f));
                zoomMul = 1f + zoomT * 0.3f;
            }
            transform.localScale = targetScale * (animScaleMultiplier * zoomMul);
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
            animScaleMultiplier = s;

            // Alpha: fade in over first 60%
            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.Clamp01(t / 0.6f);

            if (t >= 1f)
            {
                transform.position = basePosition;
                animScaleMultiplier = 1f;
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

            animScaleMultiplier = 1f - t;
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

    /// <summary>
    /// Handles pointer down/up on a bubble variant to distinguish tap vs hold.
    /// Tap (< 0.3s) fires OnTapped. Hold (>= 0.3s) fires OnHoldStarted/OnHoldEnded.
    /// </summary>
    public class BubbleTapHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private POIBubble bubble;
        private float pointerDownTime;
        private bool isDown;
        private bool holdFired;
        private const float HOLD_THRESHOLD = 0.3f;

        public void Setup(POIBubble owner) => bubble = owner;

        public void OnPointerDown(PointerEventData eventData)
        {
            pointerDownTime = Time.time;
            isDown = true;
            holdFired = false;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!isDown) return;
            isDown = false;

            if (holdFired)
            {
                bubble?.FireHoldEnded();
            }
            else
            {
                bubble?.FireTapped();
            }
        }

        private void Update()
        {
            if (!isDown || holdFired) return;
            if (Time.time - pointerDownTime >= HOLD_THRESHOLD)
            {
                holdFired = true;
                bubble?.FireHoldStarted();
            }
        }
    }
}
