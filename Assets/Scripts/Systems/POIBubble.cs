using UnityEngine;
using UnityEngine.UI;
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

        [Header("Tether Line")]
        [Tooltip("Width of the tether line at the bubble end.")]
        [SerializeField] private float tetherWidthTop = 0.06f;
        [Tooltip("Width of the tether line at the ground end.")]
        [SerializeField] private float tetherWidthBottom = 0.02f;

        private UIPanel panel;
        private BubbleType currentType;
        private GameObject activeChild;

        // Tether — LineRenderer connecting the bubble to the ground tile it represents
        private LineRenderer tether;
        private Vector3 groundPosition;  // world pos of the fog tile (y = ground level)

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

        // Rise-in animation params
        private float riseDistance = 0.8f;
        private float riseInDuration = 0.3f;
        private float tetherDrawDuration = 0.2f;

        // Target scale — set by POIManager to control world-space size.
        // Pop-in and dismiss animations scale relative to this, not Vector3.one.
        private Vector3 targetScale = Vector3.one;

        // State
        private enum State { Inactive, RisingIn, DrawingTether, Bobbing, Dismissing }
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

        /// <summary>
        /// Find the Image component named "Icon" within the active variant.
        /// Used by BuildingProductionManager to set reward/input icons on Bubble_Collect/Bubble_Insert.
        /// </summary>
        public Image GetIconImage()
        {
            if (activeChild == null) return null;
            foreach (var img in activeChild.GetComponentsInChildren<Image>(true))
            {
                if (img.gameObject.name == "Icon") return img;
            }
            return null;
        }

        // ── Tether Line ──────────────────────────────────────────────────

        /// <summary>Returns the accent color for a BubbleType (used for the tether gradient top).</summary>
        private static Color GetBubbleColor(BubbleType type)
        {
            switch (type)
            {
                case BubbleType.POI_Gold:       return new Color(0.95f, 0.75f, 0.25f, 0.9f);   // warm gold
                case BubbleType.POI_Grey:       return new Color(0.55f, 0.60f, 0.72f, 0.9f);   // cool blue-grey
                case BubbleType.POI_Red:        return new Color(0.90f, 0.25f, 0.25f, 0.9f);   // danger red
                case BubbleType.Bubble_Insert:  return new Color(0.40f, 0.75f, 0.95f, 0.9f);   // soft blue
                case BubbleType.Bubble_Collect: return new Color(0.45f, 0.85f, 0.45f, 0.9f);   // green
                case BubbleType.Bubble_Alert:   return new Color(1.00f, 0.60f, 0.20f, 0.9f);   // alert orange
                default:                        return new Color(0.55f, 0.60f, 0.72f, 0.9f);
            }
        }

        /// <summary>Fog grey — the color the tether fades into at ground level.</summary>
        private static readonly Color fogGrey = new Color(0.78f, 0.78f, 0.78f, 0.0f);

        private void SetupTether(BubbleType bubbleType, Vector3 bubblePos)
        {
            groundPosition = new Vector3(bubblePos.x, -0.5f, bubblePos.z);

            if (tether == null)
            {
                // Create a child GameObject so the LineRenderer isn't affected by Canvas scaling
                var tetherObj = new GameObject("Tether");
                tetherObj.transform.SetParent(transform.parent ?? transform, false);
                tether = tetherObj.AddComponent<LineRenderer>();

                // Use the default sprite material (unlit, supports vertex colors)
                tether.material = new Material(Shader.Find("Sprites/Default"));
                tether.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tether.receiveShadows = false;
                tether.allowOcclusionWhenDynamic = false;
                tether.positionCount = 2;
                tether.useWorldSpace = true;
                tether.sortingOrder = -1; // behind the bubble
                tether.numCapVertices = 2;
            }

            // Width: thicker at bubble, tapers toward ground
            tether.startWidth = tetherWidthTop;
            tether.endWidth = tetherWidthBottom;

            // Gradient: bubble color at top → fog grey (transparent) at bottom
            Color topColor = GetBubbleColor(bubbleType);
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(topColor, 0f),
                    new GradientColorKey(fogGrey, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(topColor.a, 0f),
                    new GradientAlphaKey(0.15f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            tether.colorGradient = gradient;

            UpdateTetherPositions(bubblePos);
            tether.gameObject.SetActive(true);
        }

        private void UpdateTetherPositions(Vector3 bubblePos)
        {
            if (tether == null) return;
            tether.SetPosition(0, bubblePos);
            tether.SetPosition(1, groundPosition);
        }

        private void HideTether()
        {
            if (tether != null)
                tether.gameObject.SetActive(false);
        }

        private void DestroyTether()
        {
            if (tether != null)
            {
                Destroy(tether.gameObject);
                tether = null;
            }
        }

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
        /// Set the target world-space scale for this bubble.
        /// Animations scale relative to this value instead of Vector3.one.
        /// </summary>
        public void SetTargetScale(Vector3 scale)
        {
            targetScale = scale;
        }

        /// <summary>
        /// Activate this bubble at the given world position.
        /// Toggles the matching BubbleType child on (all others off) and sets the label.
        /// </summary>
        public void Setup(BubbleType bubbleType, string text, Vector3 worldPos, Sprite icon = null)
        {
            basePosition = worldPos;
            transform.position = worldPos;

            // Toggle the correct variant via UIPanel
            HideAllVariants();
            currentType = bubbleType;

            if (panel != null)
            {
                string variantName = bubbleType.ToString();
                var obj = panel.GetObject(variantName);
                if (obj != null)
                {
                    obj.SetActive(true);
                    activeChild = obj;

                    var tmp = obj.GetComponentInChildren<TextMeshProUGUI>();
                    if (tmp != null) tmp.text = text;

                    // Set the icon if provided
                    if (icon != null)
                    {
                        var iconImg = GetIconImage();
                        if (iconImg != null)
                        {
                            iconImg.sprite = icon;
                            iconImg.enabled = true;
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"[POIBubble] Variant '{variantName}' not found in UIPanel ({panel.ElementCount} elements). Check that Setup UI Panels has been run on the prefab.");
                }
            }
            else
            {
                Debug.LogWarning("[POIBubble] No UIPanel component found — bubble will be blank.");
            }

            if (canvasGroup != null) canvasGroup.alpha = 0f;

            // Prepare tether but hide until DrawingTether phase
            SetupTether(bubbleType, worldPos);
            HideTether();

            // Start below target position
            transform.position = worldPos + Vector3.down * riseDistance;
            transform.localScale = Vector3.zero;
            gameObject.SetActive(true);

            state = State.RisingIn;
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
            // Ensure fully visible before starting dismiss (in case dismissed during rise)
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            transform.localScale = targetScale;
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
                case State.RisingIn:     UpdateRiseIn();     break;
                case State.DrawingTether: UpdateDrawTether(); break;
                case State.Bobbing:      UpdateBob();        break;
                case State.Dismissing:   UpdateDismiss();    break;
            }
        }

        private void UpdateRiseIn()
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / riseInDuration);

            // Position: EaseOutQuad rise from below to basePosition
            float posEased = t * (2f - t); // EaseOutQuad
            Vector3 startPos = basePosition + Vector3.down * riseDistance;
            transform.position = Vector3.Lerp(startPos, basePosition, posEased);

            // Scale: OutBack from zero to targetScale
            float scaleEased = 1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f);
            transform.localScale = targetScale * scaleEased;

            // Alpha: fade in over first 60%
            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.Clamp01(t / 0.6f);

            if (t >= 1f)
            {
                transform.position = basePosition;
                transform.localScale = targetScale;
                if (canvasGroup != null) canvasGroup.alpha = 1f;

                // Transition to tether draw
                state = State.DrawingTether;
                timer = 0f;

                // Show tether — start with bottom at bubble position
                if (tether != null)
                {
                    tether.gameObject.SetActive(true);
                    tether.SetPosition(0, basePosition);
                    tether.SetPosition(1, basePosition); // starts collapsed
                }
            }
        }

        private void UpdateDrawTether()
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / tetherDrawDuration);

            // EaseOutCubic: bottom endpoint sweeps from bubble down to ground
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            Vector3 currentBottom = Vector3.Lerp(basePosition, groundPosition, eased);

            if (tether != null)
            {
                tether.SetPosition(0, basePosition);
                tether.SetPosition(1, currentBottom);
            }

            if (t >= 1f)
            {
                if (tether != null)
                    UpdateTetherPositions(basePosition);

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
            UpdateTetherPositions(pos);
        }

        private void UpdateDismiss()
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / fadeOutDuration);

            transform.localScale = targetScale * (1f - t);
            if (canvasGroup != null) canvasGroup.alpha = 1f - t;

            // Fade tether alpha during dismiss
            if (tether != null)
            {
                Color topCol = GetBubbleColor(currentType);
                topCol.a *= (1f - t);
                var gradient = tether.colorGradient;
                var colorKeys = gradient.colorKeys;
                var alphaKeys = new GradientAlphaKey[] {
                    new GradientAlphaKey(topCol.a, 0f),
                    new GradientAlphaKey(0.15f * (1f - t), 0.6f),
                    new GradientAlphaKey(0f, 1f)
                };
                gradient.SetKeys(colorKeys, alphaKeys);
                tether.colorGradient = gradient;
                UpdateTetherPositions(transform.position);
            }

            if (t >= 1f)
            {
                state = State.Inactive;
                HideAllVariants();
                HideTether();
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

        private void OnDestroy()
        {
            DestroyTether();
        }
    }
}
