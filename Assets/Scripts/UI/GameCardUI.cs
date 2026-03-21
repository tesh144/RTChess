using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace ClockworkGrid
{
    /// <summary>
    /// Self-scanning card component that replaces UnitIcon.
    /// Combines the hierarchy scanning pattern from UIPanel with the
    /// drag-and-drop integration from UnitIcon.
    ///
    /// Hierarchy scanning:
    ///   On ScanHierarchy(), indexes all children (like UIPanel).
    ///   Known elements resolved by name: Icon, Text, Notify_Count_Green,
    ///   Notify_Count_Red, Icon_Lock.
    ///   Custom Inspector has Rescan Hierarchy + Log All Elements buttons.
    ///
    /// Drag integration:
    ///   Implements IBeginDragHandler/IDragHandler/IEndDragHandler.
    ///   Routes to DragDropHandler for placement.
    ///   Hover magnification (macOS dock style).
    ///
    /// Visual states:
    ///   Normal   — icon + name, draggable, hover magnification
    ///   New      — red badge shown (Notify_Count_Red)
    ///   Locked   — greyed out, lock icon (Icon_Lock), text muted, not draggable
    /// </summary>
    public class GameCardUI : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        // ── Serialized Element Registry (same pattern as UIPanel) ────

        [Serializable]
        public class CardElement
        {
            [Tooltip("Hierarchy path relative to this card")]
            public string path;

            [Tooltip("Short name (last path segment)")]
            public string name;

            [Tooltip("Reference to the Transform")]
            public Transform transform;

            [Tooltip("Component types found on this element")]
            public string components;
        }

        [Header("Discovered Elements")]
        [Tooltip("All child elements. Populated by Rescan Hierarchy button in Inspector.")]
        [SerializeField] private List<CardElement> elementList = new List<CardElement>();

        /// <summary>Total number of indexed child elements.</summary>
        public int ElementCount => elementList.Count;

        [Header("Drag Settings")]
        [SerializeField] private float hoverScale = 1.2f;
        [SerializeField] private Image characterSpriteImage;

        // ── Runtime Dictionaries ─────────────────────────────────────

        private Dictionary<string, Transform> elementsByPath;
        private Dictionary<string, Transform> elementsByName;
        private Dictionary<string, Image> imageCache = new Dictionary<string, Image>();
        private Dictionary<string, TextMeshProUGUI> textCache = new Dictionary<string, TextMeshProUGUI>();
        private bool dictionariesBuilt;

        // ── Known Element Shortcuts (resolved from hierarchy) ────────

        private Image _iconImage;
        private TextMeshProUGUI _nameText;
        private GameObject _greenBadge;
        private TextMeshProUGUI _greenBadgeText;
        private GameObject _redBadge;
        private GameObject _lockOverlay;
        private Button _cardButton;
        private Image _cardBackground;

        // ── Drag State (from UnitIcon) ───────────────────────────────

        private GameObject unitPrefab;
        private UnitStats unitStats;
        private DockBarManager dockManager;
        private RectTransform rectTransform;
        private Vector3 originalScale;
        private Vector2 originalPosition;
        private bool isDragging = false;

        // ── Visual State ─────────────────────────────────────────────

        private bool isNew = false;
        private bool isLocked = false;
        private Color normalTextColor;
        private Color normalIconColor = Color.white;
        private bool hasStoredColors = false;

        // ── Idle Animation State ──────────────────────────────────────
        private Coroutine badgeWobbleCoroutine;
        private Coroutine cardIdleCoroutine;
        private Coroutine appearCoroutine;
        private float idlePhaseOffset; // Random offset so cards don't all breathe in sync

        // ── Public Properties ────────────────────────────────────────

        public UnitStats UnitStats => unitStats;
        public GameObject UnitPrefab => unitPrefab;
        public bool IsNew => isNew;
        public bool IsLocked => isLocked;
        public Image IconImage => _iconImage;
        public TextMeshProUGUI NameText => _nameText;

        // ── Lifecycle ────────────────────────────────────────────────

        private void Awake()
        {
            EnsureRectTransform();
            BuildRuntimeDictionaries();
            ResolveKnownElements();
            idlePhaseOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        }

        private void OnDestroy()
        {
            if (ResourceTokenManager.Instance != null)
                ResourceTokenManager.Instance.OnTokensChanged -= OnTokensChanged;
        }

        private void EnsureRectTransform()
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
                if (rectTransform != null)
                    originalScale = rectTransform.localScale;
            }
        }

        // ── Initialization ───────────────────────────────────────────

        /// <summary>
        /// Initialize with UnitStats and DockBarManager (replaces UnitIcon.Initialize).
        /// </summary>
        public void Initialize(UnitStats stats, DockBarManager manager)
        {
            EnsureRectTransform();
            if (!dictionariesBuilt) { BuildRuntimeDictionaries(); ResolveKnownElements(); }

            unitStats = stats;
            unitPrefab = stats.unitPrefab;
            dockManager = manager;
            originalPosition = rectTransform.anchoredPosition;

            // Icon sprite
            if (_iconImage != null && stats.iconSprite != null)
                _iconImage.sprite = stats.iconSprite;

            // Also set legacy characterSpriteImage if assigned
            if (characterSpriteImage != null && stats.iconSprite != null)
                characterSpriteImage.sprite = stats.iconSprite;

            // Name text
            string displayName = !string.IsNullOrEmpty(stats.unitName) ? stats.unitName : stats.unitType.ToString();
            if (_nameText != null)
                _nameText.text = displayName;

            // Badges off by default
            SetNew(false);
            if (_greenBadge != null) _greenBadge.SetActive(false);
            SetLocked(false);

            // Subscribe to token changes for cost color updates
            if (ResourceTokenManager.Instance != null)
                ResourceTokenManager.Instance.OnTokensChanged += OnTokensChanged;
        }

        /// <summary>
        /// Quick setup with unit data (for simpler callers).
        /// </summary>
        public void Setup(UnitStats stats, bool markAsNew = false)
        {
            if (!dictionariesBuilt) { BuildRuntimeDictionaries(); ResolveKnownElements(); }

            unitStats = stats;
            unitPrefab = stats != null ? stats.unitPrefab : null;

            if (_iconImage != null && stats != null && stats.iconSprite != null)
                _iconImage.sprite = stats.iconSprite;

            if (_nameText != null && stats != null)
                _nameText.text = stats.unitName;

            SetNew(markAsNew);
            if (_greenBadge != null) _greenBadge.SetActive(false);
            SetLocked(false);
        }

        // ── Hierarchy Scanning ───────────────────────────────────────

        /// <summary>
        /// Builds fast lookup dictionaries from the serialized element list.
        /// </summary>
        public void BuildRuntimeDictionaries()
        {
            if (dictionariesBuilt) return;
            dictionariesBuilt = true;

            elementsByPath = new Dictionary<string, Transform>(elementList.Count);
            elementsByName = new Dictionary<string, Transform>(elementList.Count);
            imageCache.Clear();
            textCache.Clear();

            foreach (var elem in elementList)
            {
                if (elem.transform == null) continue;
                elementsByPath[elem.path] = elem.transform;
                if (!elementsByName.ContainsKey(elem.name))
                    elementsByName[elem.name] = elem.transform;
            }
        }

        /// <summary>
        /// Resolves known card elements. Tries the scanned element dictionary first,
        /// falls back to transform.Find() so cards work even without a pre-scanned hierarchy.
        /// </summary>
        private void ResolveKnownElements()
        {
            // Icon — the main image showing the worker/item sprite
            var iconT = Get("Icon") ?? transform.Find("Icon");
            if (iconT != null) _iconImage = iconT.GetComponent<Image>();

            // Text — the name label below/on the card
            var textT = Get("Text") ?? transform.Find("Text");
            if (textT != null) _nameText = textT.GetComponent<TextMeshProUGUI>();

            // Green badge (e.g. stack count)
            var greenT = Get("Notify_Count_Green") ?? transform.Find("Notify_Count_Green");
            if (greenT != null)
            {
                _greenBadge = greenT.gameObject;
                _greenBadgeText = greenT.GetComponentInChildren<TextMeshProUGUI>();
            }

            // Red badge (e.g. "new" indicator)
            var redT = Get("Notify_Count_Red") ?? transform.Find("Notify_Count_Red");
            if (redT != null) _redBadge = redT.gameObject;

            // Lock overlay
            var lockT = Get("Icon_Lock") ?? transform.Find("Icon_Lock");
            if (lockT != null) _lockOverlay = lockT.gameObject;

            _cardButton = GetComponent<Button>();
            _cardBackground = GetComponent<Image>();

            if (!hasStoredColors)
            {
                if (_nameText != null) normalTextColor = _nameText.color;
                if (_iconImage != null) normalIconColor = _iconImage.color;
                hasStoredColors = true;
            }

            // Log resolution results for debugging
            if (_iconImage == null)
                Debug.LogWarning($"[GameCardUI] Could not find 'Icon' child with Image on {name}", this);
            if (_nameText == null)
                Debug.LogWarning($"[GameCardUI] Could not find 'Text' child with TMP on {name}", this);
        }

        /// <summary>
        /// Editor-only: scans the live hierarchy and rebuilds the serialized element list.
        /// </summary>
        public void ScanHierarchy()
        {
            elementList.Clear();
            ScanChildren(transform, "");
        }

        private void ScanChildren(Transform parent, string parentPath)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                string childPath = string.IsNullOrEmpty(parentPath)
                    ? child.name
                    : parentPath + "/" + child.name;

                string comps = "";
                if (child.GetComponent<Image>() != null) comps += "[Image] ";
                if (child.GetComponent<TextMeshProUGUI>() != null) comps += "[TMP] ";
                if (child.GetComponent<Button>() != null) comps += "[Button] ";
                if (child.GetComponent<Shadow>() != null) comps += "[Shadow] ";
                if (child.GetComponent<CanvasGroup>() != null) comps += "[CanvasGroup] ";
                if (child.GetComponent<LayoutGroup>() != null) comps += "[Layout] ";
                if (child.GetComponent<LayoutElement>() != null) comps += "[LayoutElement] ";
                comps = comps.TrimEnd();

                elementList.Add(new CardElement
                {
                    path = childPath,
                    name = child.name,
                    transform = child,
                    components = comps
                });

                ScanChildren(child, childPath);
            }
        }

        // ── Element Access ───────────────────────────────────────────

        /// <summary>Get a Transform by full path or short name.</summary>
        public Transform Get(string pathOrName)
        {
            BuildRuntimeDictionaries();
            if (elementsByPath != null && elementsByPath.TryGetValue(pathOrName, out var byPath))
                return byPath;
            if (elementsByName != null && elementsByName.TryGetValue(pathOrName, out var byName))
                return byName;
            return null;
        }

        /// <summary>Get a child GameObject by path or name.</summary>
        public GameObject GetObject(string pathOrName)
        {
            var t = Get(pathOrName);
            return t != null ? t.gameObject : null;
        }

        /// <summary>Get an Image from a child element.</summary>
        public Image GetImage(string pathOrName)
        {
            if (imageCache.TryGetValue(pathOrName, out var c)) return c;
            var t = Get(pathOrName);
            if (t == null) return null;
            var img = t.GetComponent<Image>();
            if (img != null) imageCache[pathOrName] = img;
            return img;
        }

        /// <summary>Get a TextMeshProUGUI from a child element.</summary>
        public TextMeshProUGUI GetText(string pathOrName)
        {
            if (textCache.TryGetValue(pathOrName, out var c)) return c;
            var t = Get(pathOrName);
            if (t == null) return null;
            var tmp = t.GetComponent<TextMeshProUGUI>();
            if (tmp != null) textCache[pathOrName] = tmp;
            return tmp;
        }

        /// <summary>All registered element paths.</summary>
        public IEnumerable<string> AllPaths
        {
            get { foreach (var e in elementList) yield return e.path; }
        }

        /// <summary>Set a child element's active state.</summary>
        public void SetActive(string pathOrName, bool active)
        {
            var o = GetObject(pathOrName);
            if (o != null) o.SetActive(active);
        }

        /// <summary>Set text on a TMP child.</summary>
        public void SetText(string pathOrName, string text)
        {
            var t = GetText(pathOrName);
            if (t != null) t.text = text;
        }

        /// <summary>Set sprite on an Image child.</summary>
        public void SetSprite(string pathOrName, Sprite sprite)
        {
            var i = GetImage(pathOrName);
            if (i != null) i.sprite = sprite;
        }

        // ── Visual State: New ────────────────────────────────────────

        /// <summary>Show/hide red notification badge. Starts wobble animation when shown.</summary>
        public void SetNew(bool value)
        {
            isNew = value;
            if (_redBadge != null)
            {
                _redBadge.SetActive(value);

                // Start/stop badge wobble animation
                if (value && gameObject.activeInHierarchy)
                {
                    if (badgeWobbleCoroutine != null) StopCoroutine(badgeWobbleCoroutine);
                    badgeWobbleCoroutine = StartCoroutine(BadgeWobbleLoop());
                }
                else if (!value && badgeWobbleCoroutine != null)
                {
                    StopCoroutine(badgeWobbleCoroutine);
                    badgeWobbleCoroutine = null;
                    // Reset badge transform
                    _redBadge.transform.localScale = Vector3.one;
                    _redBadge.transform.localRotation = Quaternion.identity;
                }
            }
        }

        // ── Visual State: Locked ─────────────────────────────────────

        /// <summary>
        /// Lock or unlock the card. Locked = greyed out, lock icon shown,
        /// text muted, not draggable. Matches Clan button locked visual.
        /// </summary>
        public void SetLocked(bool value)
        {
            isLocked = value;

            if (_lockOverlay != null) _lockOverlay.SetActive(value);
            if (_cardButton != null) _cardButton.interactable = !value;

            if (_cardBackground != null)
            {
                Color c = _cardBackground.color;
                c.a = value ? 0.4f : 1f;
                _cardBackground.color = c;
            }

            if (_iconImage != null)
                _iconImage.color = value ? new Color(0.5f, 0.5f, 0.5f, 0.6f) : normalIconColor;

            if (_nameText != null)
            {
                if (value)
                {
                    Color muted = normalTextColor;
                    muted.r = Mathf.Lerp(muted.r, 0.5f, 0.6f);
                    muted.g = Mathf.Lerp(muted.g, 0.5f, 0.6f);
                    muted.b = Mathf.Lerp(muted.b, 0.5f, 0.6f);
                    muted.a *= 0.5f;
                    _nameText.color = muted;
                }
                else
                {
                    _nameText.color = normalTextColor;
                }
            }
        }

        // ── Visual State: Green Badge ────────────────────────────────

        /// <summary>Show green badge with a value (quantity, level, etc.).</summary>
        public void SetGreenBadge(int value)
        {
            if (_greenBadge != null)
            {
                _greenBadge.SetActive(value > 0);
                if (_greenBadgeText != null)
                    _greenBadgeText.text = value.ToString();
            }
        }

        // ── Drag Handlers (from UnitIcon) ────────────────────────────

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isDragging && !isLocked)
                rectTransform.localScale = originalScale * hoverScale;

            // Clear "New" badge on first interaction (hover)
            if (isNew) SetNew(false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isDragging)
                rectTransform.localScale = originalScale;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (isLocked) return;

            // Stop idle animation during drag
            StopIdleAnimation();

            // Reset to original scale (don't derive from current — it may be hover-scaled or mid-bounce)
            rectTransform.localScale = originalScale;
            originalPosition = rectTransform.anchoredPosition;

            if (isNew) SetNew(false);

            if (DragDropHandler.Instance != null && DragDropHandler.Instance.StartDrag(this, unitPrefab))
            {
                isDragging = true;
            }
            else
            {
                isDragging = false;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (DragDropHandler.Instance != null)
                DragDropHandler.Instance.UpdateDrag(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            isDragging = false;
            if (DragDropHandler.Instance != null)
                DragDropHandler.Instance.EndDrag();
        }

        /// <summary>Snap card back to its dock position after cancelled drag.</summary>
        public void SnapBackToOriginalPosition()
        {
            rectTransform.anchoredPosition = originalPosition;
            rectTransform.localScale = originalScale;
            // Resume idle breathing after snap back
            StartIdleAnimation();
        }

        // ── Token Cost Updates ───────────────────────────────────────

        private void OnTokensChanged(int newTotal)
        {
            // Future: update cost affordability display if needed
        }

        // ── Card Idle Animation ────────────────────────────────────

        /// <summary>
        /// Starts the subtle idle breathing animation on this card.
        /// Called after the card has settled into the dock.
        /// Snapshots the current position as the idle base so it doesn't fight the layout.
        /// </summary>
        public void StartIdleAnimation()
        {
            if (cardIdleCoroutine != null) StopCoroutine(cardIdleCoroutine);
            cardIdleCoroutine = StartCoroutine(CardIdleLoop());
        }

        /// <summary>
        /// Stops the idle animation and resets scale to original.
        /// </summary>
        public void StopIdleAnimation()
        {
            if (cardIdleCoroutine != null)
            {
                StopCoroutine(cardIdleCoroutine);
                cardIdleCoroutine = null;
            }
            if (rectTransform != null)
                rectTransform.localScale = originalScale;
        }

        /// <summary>
        /// Plays a pop-in appear animation when a card is added to the dock.
        /// Scale from 0 → overshoot → settle, with a slight upward bounce.
        /// </summary>
        public void PlayAppearAnimation()
        {
            // Ensure the GameObject is active — StartCoroutine fails on inactive objects
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            if (appearCoroutine != null) StopCoroutine(appearCoroutine);
            appearCoroutine = StartCoroutine(AppearAnimationRoutine());
        }

        // ── Animation Coroutines ──────────────────────────────────────

        /// <summary>
        /// Badge wobble: subtle scale pulse + gentle rotation oscillation.
        /// Runs continuously while the "new" badge is visible.
        /// </summary>
        private IEnumerator BadgeWobbleLoop()
        {
            float time = 0f;
            while (true)
            {
                time += Time.deltaTime;

                // Scale pulse: 1.0 → 1.12 → 1.0 over ~1.2s
                float scaleT = Mathf.Sin(time * 5.2f) * 0.5f + 0.5f; // 0..1
                float scale = 1f + scaleT * 0.12f;

                // Gentle rotation: ±6 degrees
                float rotation = Mathf.Sin(time * 3.7f) * 6f;

                if (_redBadge != null)
                {
                    _redBadge.transform.localScale = new Vector3(scale, scale, 1f);
                    _redBadge.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
                }

                yield return null;
            }
        }

        /// <summary>
        /// Card idle: periodic little bounce every 2-3 seconds.
        /// Quick scale pop (1.0 → 1.08 → 1.0) then waits.
        /// Each card has a random phase offset so they don't all bounce at once.
        /// </summary>
        private IEnumerator CardIdleLoop()
        {
            // Initial delay based on phase offset so cards don't sync
            yield return new WaitForSeconds(idlePhaseOffset / (Mathf.PI * 2f) * 2f);

            while (true)
            {
                if (!isDragging && rectTransform != null)
                {
                    // Quick bounce: 0.15s up, 0.15s down
                    float bounceDuration = 0.15f;
                    float bounceScale = 1.08f;

                    // Scale up
                    float elapsed = 0f;
                    while (elapsed < bounceDuration)
                    {
                        elapsed += Time.deltaTime;
                        float t = Mathf.Clamp01(elapsed / bounceDuration);
                        float ease = t * (2f - t); // ease-out quad
                        rectTransform.localScale = originalScale * Mathf.Lerp(1f, bounceScale, ease);
                        yield return null;
                    }

                    // Scale back down
                    elapsed = 0f;
                    while (elapsed < bounceDuration)
                    {
                        elapsed += Time.deltaTime;
                        float t = Mathf.Clamp01(elapsed / bounceDuration);
                        float ease = t * t; // ease-in quad
                        rectTransform.localScale = originalScale * Mathf.Lerp(bounceScale, 1f, ease);
                        yield return null;
                    }

                    rectTransform.localScale = originalScale;
                }

                // Wait 2-3 seconds before next bounce
                yield return new WaitForSeconds(UnityEngine.Random.Range(2f, 3f));
            }
        }

        /// <summary>
        /// Appear animation: scale pop from small to overshoot to settle.
        /// Runs once when the card first appears in the dock.
        /// </summary>
        private IEnumerator AppearAnimationRoutine()
        {
            float duration = 0.4f;
            float elapsed = 0f;

            Vector3 startScale = originalScale * 0.3f;
            Vector3 overshootScale = originalScale * 1.12f;
            Vector3 targetScale = originalScale;

            if (rectTransform != null)
                rectTransform.localScale = startScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Overshoot ease: quick ramp up with a bounce
                float scale;
                if (t < 0.6f)
                {
                    // Fast grow to overshoot
                    float subT = t / 0.6f;
                    subT = 1f - (1f - subT) * (1f - subT); // ease-out quad
                    scale = Mathf.Lerp(0.3f, 1.12f, subT);
                }
                else
                {
                    // Settle from overshoot to target
                    float subT = (t - 0.6f) / 0.4f;
                    subT = subT * subT * (3f - 2f * subT); // ease-in-out
                    scale = Mathf.Lerp(1.12f, 1f, subT);
                }

                if (rectTransform != null)
                    rectTransform.localScale = originalScale * scale;

                yield return null;
            }

            if (rectTransform != null)
                rectTransform.localScale = originalScale;

            // Start idle animation after appear settles
            StartIdleAnimation();
        }

        // ── Debug ────────────────────────────────────────────────────

        /// <summary>Logs the full element tree to the console.</summary>
        public void DebugLogHierarchy()
        {
            Debug.Log($"[GameCardUI] {elementList.Count} elements indexed:");
            foreach (var elem in elementList)
                Debug.Log($"  {elem.path} {elem.components}");
        }
    }
}
