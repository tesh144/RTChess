#pragma warning disable CS0414, CS0219, CS0618
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace ClockworkGrid
{
    /// <summary>
    /// Self-scanning card component (replaces UnitIcon). Hierarchy scanning,
    /// drag-and-drop via DragDropHandler, hover magnification, visual states
    /// (Normal, New, Locked). Animation methods in GameCardUIAnimations.cs.
    /// </summary>
    public partial class GameCardUI : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
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

        private Dictionary<string, Transform> elementsByPath;
        private Dictionary<string, Transform> elementsByName;
        private Dictionary<string, Image> imageCache = new Dictionary<string, Image>();
        private Dictionary<string, TextMeshProUGUI> textCache = new Dictionary<string, TextMeshProUGUI>();
        private bool dictionariesBuilt;

        private Image _iconImage;
        private TextMeshProUGUI _nameText;
        private GameObject _greenBadge;
        private TextMeshProUGUI _greenBadgeText;
        private GameObject _redBadge;
        private GameObject _lockOverlay;
        private Button _cardButton;
        private Image _cardBackground;

        private GameObject unitPrefab;
        private UnitStats unitStats;
        private DockBarManager dockManager;
        private RectTransform rectTransform;
        private Vector3 originalScale;
        private Vector2 originalPosition;
        private bool isDragging = false;

        private bool isNew = false;
        private bool isLocked = false;
        private Color normalTextColor;
        private Color normalIconColor = Color.white;
        private bool hasStoredColors = false;

        private Coroutine badgeWobbleCoroutine;
        private Coroutine cardIdleCoroutine;
        private Coroutine appearCoroutine;
        private float idlePhaseOffset; // Random offset so cards don't all breathe in sync

        public UnitStats UnitStats => unitStats;
        public GameObject UnitPrefab => unitPrefab;
        public bool IsNew => isNew;
        public bool IsLocked => isLocked;
        public Image IconImage => _iconImage;
        public TextMeshProUGUI NameText => _nameText;

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

            // Name text (with tier prefix for tier > 0)
            string displayName = !string.IsNullOrEmpty(stats.unitName) ? stats.unitName : stats.unitType.ToString();
            if (_nameText != null)
                _nameText.text = FormatCardName(displayName, stats.tier);

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
                _nameText.text = FormatCardName(stats.unitName, stats.tier);

            SetNew(markAsNew);
            if (_greenBadge != null) _greenBadge.SetActive(false);
            SetLocked(false);
        }

        /// <summary>Builds fast lookup dictionaries from the serialized element list.</summary>
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

        /// <summary>Resolves known card elements from scanned dictionary, falls back to transform.Find().</summary>
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

        /// <summary>Editor-only: scans the live hierarchy and rebuilds the serialized element list.</summary>
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

        /// <summary>Format a card display name with an optional tier prefix.</summary>
        private static string FormatCardName(string baseName, int tier)
        {
            if (string.IsNullOrEmpty(baseName)) return baseName;
            if (tier <= 0) return baseName;
            return $"<size=70%><color=#999999>T{tier}</color></size> {baseName}";
        }

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

        /// <summary>Lock or unlock the card. Locked = greyed out, lock icon, text muted, not draggable.</summary>
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

            if (unitPrefab == null)
            {
                Debug.LogWarning($"[GameCardUI] Cannot drag card '{unitStats?.unitName}' — no prefab assigned");
                isDragging = false;
                return;
            }

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

        private void OnTokensChanged(int newTotal)
        {
            // Future: update cost affordability display if needed
        }

    }
}
