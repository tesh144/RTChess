#pragma warning disable CS0414, CS0219, CS0618
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace ClockworkGrid
{
    /// <summary>
    /// Orchestrates the title screen experience with deliberate pacing.
    /// Execution order -50 ensures this runs before PanelControl (default 0)
    /// but after CafeSceneSetupV2 (-100) and UIThemeManager (Awake).
    ///
    ///   Scene loads → black
    ///   → Background fades in (slow, cinematic)
    ///   → Title logo fades up
    ///   → Hold — let the player breathe
    ///   → Buttons slide up from below, staggered
    ///   → Player taps Play
    ///   → Buttons slide down, title fades out
    ///   → Cinematic pause on background alone
    ///   → Background fades out → game world revealed
    ///   → Game systems start (dock bar, clock, map gen)
    ///
    /// All references are serialized and wired via the editor tool
    /// (Tools > ClockworkCraft > Setup Title Screen).
    /// Button onClick events are persistent/serialized — visible in Inspector.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class TitleScreenController : MonoBehaviour
    {
        [Header("Panel")]
        [Tooltip("The Title_common UIPanel. Assigned by editor tool.")]
        public UIPanel titlePanel;

        [Header("Element References (assigned by editor tool)")]
        [Tooltip("Background Image — fades in/out")]
        public Image background;

        [Tooltip("Flat black overlay behind the background illustration. " +
                 "Deactivated after the illustration fades in so it doesn't " +
                 "interfere with the fade-out to game world.")]
        public GameObject backgroundColorOverlay;

        [Tooltip("Title logo — fades in/out")]
        public RectTransform titleLogo;

        [Tooltip("Play button — slides in, triggers game start")]
        public Button playButton;

        [Tooltip("Guest button — slides in")]
        public Button guestButton;

        [Tooltip("Facebook button — slides in")]
        public Button facebookButton;

        [Header("Timing — Entrance")]
        public float initialDelay = 0.4f;
        public float bgFadeInDuration = 1.5f;
        public float preTitlePause = 0.3f;
        public float titleFadeInDuration = 1.0f;
        public float postTitleHold = 0.8f;
        public float buttonSlideInDuration = 0.6f;
        public float buttonStaggerDelay = 0.15f;

        [Header("Timing — Exit")]
        public float buttonSlideOutDuration = 0.45f;
        public float titleFadeOutDuration = 0.7f;
        public float cinematicPause = 0.8f;
        public float bgFadeOutDuration = 1.2f;
        public float preGamePause = 0.5f;

        [Header("Animation")]
        [Tooltip("How far below screen buttons start (in canvas units)")]
        public float buttonSlideDistance = 300f;

        [Header("Events")]
        [Tooltip("Fired when the title screen exit completes and the game should start. " +
                 "Wire CafeSceneSetupV2.OnGameStarted and MapGeneratorV2 here.")]
        public UnityEvent onGameStart = new UnityEvent();

        // ── Internal state ────────────────────────────────────────────

        private CanvasGroup titleLogoGroup;
        private RectTransform playRT;
        private RectTransform guestRT;
        private RectTransform facebookRT;

        private Vector2 playOrigPos;
        private Vector2 guestOrigPos;
        private Vector2 facebookOrigPos;

        private bool isExiting = false;
        private bool isInitialized = false;

        // Record toggle (repurposed guest button)
        private TextMeshProUGUI guestLabel;
        private Image guestBg;
        private bool isRecordingEnabled = false;

        // ── Setup ─────────────────────────────────────────────────────

        private void Awake()
        {
            Debug.Log("[TitleScreen] Awake — initializing");
            CacheReferences();
        }

        private void Start()
        {
            // Initialize() may have already been called externally (runtime creation path)
            if (!isInitialized)
                Initialize();
        }

        /// <summary>
        /// Caches RectTransforms and ensures CanvasGroups exist.
        /// Safe to call multiple times.
        /// </summary>
        private void CacheReferences()
        {
            if (playButton != null) playRT = playButton.GetComponent<RectTransform>();
            if (guestButton != null) guestRT = guestButton.GetComponent<RectTransform>();
            if (facebookButton != null) facebookRT = facebookButton.GetComponent<RectTransform>();

            if (titleLogo != null)
            {
                titleLogoGroup = titleLogo.GetComponent<CanvasGroup>();
                if (titleLogoGroup == null)
                    titleLogoGroup = titleLogo.gameObject.AddComponent<CanvasGroup>();
            }
        }

        /// <summary>
        /// Public initialization — sets up the title screen and starts the entrance animation.
        /// Called automatically in Start(), or can be called externally for runtime-created controllers.
        /// Safe to call multiple times (only runs once).
        /// </summary>
        public void Initialize()
        {
            if (isInitialized) return;
            isInitialized = true;

            if (titlePanel == null)
            {
                Debug.LogError("[TitleScreen] Title panel not assigned! Triggering game start immediately.");
                StartCoroutine(FallbackStart());
                return;
            }

            // Ensure references are cached (in case Awake hasn't run yet, e.g., runtime creation)
            CacheReferences();

            // Show the panel and make everything invisible for the entrance animation
            titlePanel.Show();
            Debug.Log("[TitleScreen] Title_common panel activated — starting entrance sequence");

            SetInitialState();
            SetupRecordToggle();
            StartCoroutine(EntranceSequence());
        }

        private void SetInitialState()
        {
            // Panel is already shown by Start() above.
            // Make all visual elements invisible — the entrance coroutine fades them in.

            // Background starts fully transparent
            if (background != null)
            {
                Color c = background.color;
                c.a = 0f;
                background.color = c;
            }

            // Title logo starts invisible
            if (titleLogoGroup != null)
                titleLogoGroup.alpha = 0f;

            // Buttons start invisible and offset below
            if (playRT != null)
            {
                playOrigPos = playRT.anchoredPosition;
                playRT.anchoredPosition = playOrigPos + Vector2.down * buttonSlideDistance;
                SetCanvasGroupAlpha(playRT, 0f);
            }

            if (guestRT != null)
            {
                guestOrigPos = guestRT.anchoredPosition;
                guestRT.anchoredPosition = guestOrigPos + Vector2.down * buttonSlideDistance;
                SetCanvasGroupAlpha(guestRT, 0f);
            }

            if (facebookRT != null)
            {
                facebookOrigPos = facebookRT.anchoredPosition;
                facebookRT.anchoredPosition = facebookOrigPos + Vector2.down * buttonSlideDistance;
                SetCanvasGroupAlpha(facebookRT, 0f);
            }
        }

        // ── Entrance Sequence ─────────────────────────────────────────

        private IEnumerator EntranceSequence()
        {
            // Ensure the black overlay is active at start (fade-in target)
            if (backgroundColorOverlay != null)
                backgroundColorOverlay.SetActive(true);

            yield return new WaitForSeconds(initialDelay);
            yield return StartCoroutine(FadeImage(background, 0f, 1f, bgFadeInDuration));

            // Background illustration is now fully visible on top of the black overlay.
            // Deactivate the overlay so it won't be there during the exit fade-out
            // (we want the game world to show through, not black).
            if (backgroundColorOverlay != null)
                backgroundColorOverlay.SetActive(false);

            yield return new WaitForSeconds(preTitlePause);
            yield return StartCoroutine(FadeCanvasGroup(titleLogoGroup, 0f, 1f, titleFadeInDuration));
            yield return new WaitForSeconds(postTitleHold);

            StartCoroutine(SlideAndFadeIn(playRT, playOrigPos, buttonSlideInDuration));
            yield return new WaitForSeconds(buttonStaggerDelay);

            StartCoroutine(SlideAndFadeIn(guestRT, guestOrigPos, buttonSlideInDuration));
            yield return new WaitForSeconds(buttonStaggerDelay);

            StartCoroutine(SlideAndFadeIn(facebookRT, facebookOrigPos, buttonSlideInDuration));
        }

        // ── Play Button Pressed ───────────────────────────────────────
        // PUBLIC so it can be wired as a persistent onClick listener in the Inspector.

        /// <summary>
        /// Called when the Play button is pressed.
        /// Wire this to Button_Play's OnClick() in the Inspector.
        /// </summary>
        public void OnPlayButtonClicked()
        {
            Debug.Log("[TitleScreen] Play button pressed!");
            if (isExiting) return;
            isExiting = true;

            // Play button click SFX
            if (GameSFXManager.Instance != null)
                GameSFXManager.Instance.PlayButtonClick();

            if (playButton != null) playButton.interactable = false;
            if (guestButton != null) guestButton.interactable = false;
            if (facebookButton != null) facebookButton.interactable = false;

            StartCoroutine(ExitSequence());
        }

        // ── Exit Sequence ─────────────────────────────────────────────

        private IEnumerator ExitSequence()
        {
            // Buttons slide down, reverse order
            if (facebookRT != null)
                StartCoroutine(SlideAndFadeOut(facebookRT, buttonSlideDistance, buttonSlideOutDuration));
            yield return new WaitForSeconds(buttonStaggerDelay * 0.5f);

            if (guestRT != null)
                StartCoroutine(SlideAndFadeOut(guestRT, buttonSlideDistance, buttonSlideOutDuration));
            yield return new WaitForSeconds(buttonStaggerDelay * 0.5f);

            if (playRT != null)
                StartCoroutine(SlideAndFadeOut(playRT, buttonSlideDistance, buttonSlideOutDuration));

            yield return new WaitForSeconds(buttonSlideOutDuration);

            // Title fades out
            yield return StartCoroutine(FadeCanvasGroup(titleLogoGroup, 1f, 0f, titleFadeOutDuration));

            // Cinematic pause
            yield return new WaitForSeconds(cinematicPause);

            // Background fades out
            yield return StartCoroutine(FadeImage(background, 1f, 0f, bgFadeOutDuration));

            // Final pause
            yield return new WaitForSeconds(preGamePause);

            // Hide the panel
            titlePanel.Hide();

            // Fire the serialized game start event
            Debug.Log("[TitleScreen] Game starting!");
            onGameStart?.Invoke();
        }

        // ── Fallback ──────────────────────────────────────────────────

        private IEnumerator FallbackStart()
        {
            yield return new WaitForSeconds(1f);
            onGameStart?.Invoke();
        }

        // ── Animation Helpers ─────────────────────────────────────────

        private IEnumerator FadeImage(Image img, float from, float to, float duration)
        {
            if (img == null) yield break;
            float elapsed = 0f;
            Color c = img.color;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseInOutCubic(Mathf.Clamp01(elapsed / duration));
                c.a = Mathf.Lerp(from, to, t);
                img.color = c;
                yield return null;
            }
            c.a = to;
            img.color = c;
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
        {
            if (group == null) yield break;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseInOutCubic(Mathf.Clamp01(elapsed / duration));
                group.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }
            group.alpha = to;
        }

        private IEnumerator SlideAndFadeIn(RectTransform rt, Vector2 targetPos, float duration)
        {
            if (rt == null) yield break;
            CanvasGroup cg = EnsureCanvasGroup(rt);
            Vector2 startPos = rt.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOutCubic(Mathf.Clamp01(elapsed / duration));
                rt.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                cg.alpha = t;
                yield return null;
            }
            rt.anchoredPosition = targetPos;
            cg.alpha = 1f;
        }

        private IEnumerator SlideAndFadeOut(RectTransform rt, float distance, float duration)
        {
            if (rt == null) yield break;
            CanvasGroup cg = EnsureCanvasGroup(rt);
            Vector2 startPos = rt.anchoredPosition;
            Vector2 endPos = startPos + Vector2.down * distance;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseInCubic(Mathf.Clamp01(elapsed / duration));
                rt.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                cg.alpha = 1f - t;
                yield return null;
            }
            rt.anchoredPosition = endPos;
            cg.alpha = 0f;
        }

        // ── Easing ────────────────────────────────────────────────────

        private float EaseInOutCubic(float t)
        {
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }

        private float EaseOutCubic(float t)
        {
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        private float EaseInCubic(float t)
        {
            return t * t * t;
        }

        // ── Record Toggle (repurposed guest button) ────────────────

        /// <summary>
        /// Repurposes the guest button as a Record toggle.
        /// Swaps its label to "REC" and wires onClick to toggle recording on/off.
        /// Visual: gray when off, red with "● REC" when on.
        /// The flag is read by GameplayRecorder on game start.
        /// </summary>
        private void SetupRecordToggle()
        {
            if (guestButton == null) return;

            // Cache existing visuals
            guestBg = guestButton.GetComponent<Image>();
            guestLabel = guestButton.GetComponentInChildren<TextMeshProUGUI>();

            // Set initial "off" state text
            if (guestLabel != null)
                guestLabel.text = "REC";

            // Clear any existing persistent onClick listeners (e.g. "Guest Login")
            guestButton.onClick.RemoveAllListeners();

            // Wire up record toggle behavior
            guestButton.onClick.AddListener(() =>
            {
                isRecordingEnabled = !isRecordingEnabled;
                LittleCafe.GameplayRecorder.RecordNextSession = isRecordingEnabled;
                UpdateRecordButtonVisual();

                if (GameSFXManager.Instance != null)
                    GameSFXManager.Instance.PlayButtonClick();
            });
        }

        private void UpdateRecordButtonVisual()
        {
            if (isRecordingEnabled)
            {
                if (guestBg != null) guestBg.color = new Color(0.8f, 0.15f, 0.15f, 0.9f);
                if (guestLabel != null) { guestLabel.text = "\u25cf REC"; guestLabel.color = Color.white; }
            }
            else
            {
                if (guestBg != null) guestBg.color = new Color(0.3f, 0.3f, 0.3f, 0.7f);
                if (guestLabel != null) { guestLabel.text = "REC"; guestLabel.color = new Color(0.6f, 0.6f, 0.6f); }
            }
        }

        // ── Utility ───────────────────────────────────────────────────

        private CanvasGroup EnsureCanvasGroup(RectTransform rt)
        {
            CanvasGroup cg = rt.GetComponent<CanvasGroup>();
            if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();
            return cg;
        }

        private void SetCanvasGroupAlpha(RectTransform rt, float alpha)
        {
            if (rt == null) return;
            CanvasGroup cg = EnsureCanvasGroup(rt);
            cg.alpha = alpha;
        }
    }
}
