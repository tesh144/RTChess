using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ClockworkGrid
{
    /// <summary>
    /// Auto-discovers and indexes every child element in a UI panel hierarchy.
    /// All element references are serialized and visible in the Inspector.
    ///
    /// The editor tool (Tools > ClockworkCraft > Setup UI Panels) scans the
    /// hierarchy and populates the elements list. At runtime, dictionaries
    /// are built from those serialized references for fast O(1) lookup.
    ///
    /// Usage:
    ///   var panel = GetComponent&lt;UIPanel&gt;();
    ///   panel.GetImage("UserInfo/PictureFrame/Image");
    ///   panel.GetText("Text_Stage");
    ///   panel.SetActive("Notify_Count_Red", false);
    /// </summary>
    public class UIPanel : MonoBehaviour
    {
        [Tooltip("Human-readable page ID (e.g., 'Title_common', 'Lobby'). Matches between light and dark variants.")]
        public string pageId;

        [Tooltip("Whether this is a dark mode variant.")]
        public bool isDarkMode;

        [Tooltip("The paired panel (light <-> dark counterpart). Set by Setup UI Panels tool.")]
        public UIPanel pairedPanel;

        // ── Serialized Element Registry ───────────────────────────────
        // Visible in Inspector — populated by the editor tool

        [Serializable]
        public class UIElement
        {
            [Tooltip("Hierarchy path relative to this panel (e.g., 'UserInfo/Text_Stage')")]
            public string path;

            [Tooltip("Short name (last path segment, e.g., 'Text_Stage')")]
            public string name;

            [Tooltip("Reference to the Transform")]
            public Transform transform;

            [Tooltip("Component types found on this element")]
            public string components;
        }

        [Header("Discovered Elements")]
        [Tooltip("All child elements in this panel's hierarchy. Populated by Tools > ClockworkCraft > Setup UI Panels.")]
        [SerializeField] private List<UIElement> elementList = new List<UIElement>();

        /// <summary>Total number of indexed child elements.</summary>
        public int ElementCount => elementList.Count;

        // ── Runtime Dictionaries (built from serialized data) ─────────

        private Dictionary<string, Transform> elementsByPath;
        private Dictionary<string, Transform> elementsByName;

        // Cached component lookups
        private Dictionary<string, Image> imageCache = new Dictionary<string, Image>();
        private Dictionary<string, TextMeshProUGUI> textCache = new Dictionary<string, TextMeshProUGUI>();
        private Dictionary<string, Button> buttonCache = new Dictionary<string, Button>();
        private Dictionary<string, Slider> sliderCache = new Dictionary<string, Slider>();

        private bool initialized;

        // ── Initialization ────────────────────────────────────────────

        private void Awake()
        {
            BuildRuntimeDictionaries();
        }

        /// <summary>
        /// Builds fast lookup dictionaries from the serialized element list.
        /// Called automatically in Awake, but safe to call again if needed.
        /// </summary>
        public void BuildRuntimeDictionaries()
        {
            if (initialized) return;
            initialized = true;

            elementsByPath = new Dictionary<string, Transform>(elementList.Count);
            elementsByName = new Dictionary<string, Transform>(elementList.Count);
            imageCache.Clear();
            textCache.Clear();
            buttonCache.Clear();
            sliderCache.Clear();

            foreach (var elem in elementList)
            {
                if (elem.transform == null) continue;

                // Full path — always unique
                elementsByPath[elem.path] = elem.transform;

                // Short name — first-come wins for duplicates
                if (!elementsByName.ContainsKey(elem.name))
                    elementsByName[elem.name] = elem.transform;
            }
        }

        /// <summary>
        /// Editor-only: Scans the live hierarchy and rebuilds the serialized element list.
        /// Called by the SetupUIPanels editor tool.
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

                // Build component tag string
                string comps = "";
                if (child.GetComponent<Image>() != null) comps += "[Image] ";
                if (child.GetComponent<TextMeshProUGUI>() != null) comps += "[TMP] ";
                if (child.GetComponent<Button>() != null) comps += "[Button] ";
                if (child.GetComponent<Slider>() != null) comps += "[Slider] ";
                if (child.GetComponent<Mask>() != null) comps += "[Mask] ";
                if (child.GetComponent<CanvasGroup>() != null) comps += "[CanvasGroup] ";
                if (child.GetComponent<LayoutGroup>() != null) comps += "[Layout] ";
                comps = comps.TrimEnd();

                elementList.Add(new UIElement
                {
                    path = childPath,
                    name = child.name,
                    transform = child,
                    components = comps
                });

                // Recurse
                ScanChildren(child, childPath);
            }
        }

        // ── Element Access ────────────────────────────────────────────

        /// <summary>
        /// Get a Transform by full path (e.g., "UserInfo/PictureFrame/Image")
        /// or by short name (e.g., "Text_Stage") if unique.
        /// </summary>
        public Transform Get(string pathOrName)
        {
            BuildRuntimeDictionaries();

            if (elementsByPath != null && elementsByPath.TryGetValue(pathOrName, out var byPath))
                return byPath;
            if (elementsByName != null && elementsByName.TryGetValue(pathOrName, out var byName))
                return byName;

            Debug.LogWarning($"[UIPanel:{pageId}] Element not found: '{pathOrName}'", this);
            return null;
        }

        /// <summary>Get a child GameObject by path or name.</summary>
        public GameObject GetObject(string pathOrName)
        {
            var t = Get(pathOrName);
            return t != null ? t.gameObject : null;
        }

        /// <summary>Get an Image component from a child element.</summary>
        public Image GetImage(string pathOrName)
        {
            if (imageCache.TryGetValue(pathOrName, out var cached))
                return cached;

            var t = Get(pathOrName);
            if (t == null) return null;

            var img = t.GetComponent<Image>();
            if (img != null) imageCache[pathOrName] = img;
            return img;
        }

        /// <summary>Get a TextMeshProUGUI component from a child element.</summary>
        public TextMeshProUGUI GetText(string pathOrName)
        {
            if (textCache.TryGetValue(pathOrName, out var cached))
                return cached;

            var t = Get(pathOrName);
            if (t == null) return null;

            var tmp = t.GetComponent<TextMeshProUGUI>();
            if (tmp != null) textCache[pathOrName] = tmp;
            return tmp;
        }

        /// <summary>Get a Button component from a child element.</summary>
        public Button GetButton(string pathOrName)
        {
            if (buttonCache.TryGetValue(pathOrName, out var cached))
                return cached;

            var t = Get(pathOrName);
            if (t == null) return null;

            var btn = t.GetComponent<Button>();
            if (btn != null) buttonCache[pathOrName] = btn;
            return btn;
        }

        /// <summary>Get a Slider component from a child element.</summary>
        public Slider GetSlider(string pathOrName)
        {
            if (sliderCache.TryGetValue(pathOrName, out var cached))
                return cached;

            var t = Get(pathOrName);
            if (t == null) return null;

            var slider = t.GetComponent<Slider>();
            if (slider != null) sliderCache[pathOrName] = slider;
            return slider;
        }

        /// <summary>Get any component from a child element.</summary>
        public T GetComponent<T>(string pathOrName) where T : Component
        {
            var t = Get(pathOrName);
            return t != null ? t.GetComponent<T>() : null;
        }

        /// <summary>All registered element paths.</summary>
        public IEnumerable<string> AllPaths
        {
            get
            {
                foreach (var elem in elementList)
                    yield return elem.path;
            }
        }

        // ── Convenience Methods ───────────────────────────────────────

        /// <summary>Set a child element's active state.</summary>
        public void SetActive(string pathOrName, bool active)
        {
            var obj = GetObject(pathOrName);
            if (obj != null) obj.SetActive(active);
        }

        /// <summary>Set text content on a TextMeshProUGUI child.</summary>
        public void SetText(string pathOrName, string text)
        {
            var tmp = GetText(pathOrName);
            if (tmp != null) tmp.text = text;
        }

        /// <summary>Set sprite on an Image child.</summary>
        public void SetSprite(string pathOrName, Sprite sprite)
        {
            var img = GetImage(pathOrName);
            if (img != null) img.sprite = sprite;
        }

        /// <summary>Set slider value on a Slider child.</summary>
        public void SetSliderValue(string pathOrName, float value)
        {
            var slider = GetSlider(pathOrName);
            if (slider != null) slider.value = value;
        }

        /// <summary>Set fill amount on an Image child (for radial/filled images).</summary>
        public void SetFillAmount(string pathOrName, float amount)
        {
            var img = GetImage(pathOrName);
            if (img != null) img.fillAmount = amount;
        }

        /// <summary>Set text color on a TextMeshProUGUI child.</summary>
        public void SetTextColor(string pathOrName, Color color)
        {
            var tmp = GetText(pathOrName);
            if (tmp != null) tmp.color = color;
        }

        /// <summary>Set image color/tint on an Image child.</summary>
        public void SetImageColor(string pathOrName, Color color)
        {
            var img = GetImage(pathOrName);
            if (img != null) img.color = color;
        }

        /// <summary>Register a button click listener on a child element.</summary>
        public void OnClick(string pathOrName, UnityEngine.Events.UnityAction action)
        {
            var btn = GetButton(pathOrName);
            if (btn != null) btn.onClick.AddListener(action);
        }

        /// <summary>Set alpha on a CanvasGroup (adds one if missing).</summary>
        public void SetAlpha(float alpha)
        {
            var cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            cg.alpha = alpha;
            cg.interactable = alpha > 0f;
            cg.blocksRaycasts = alpha > 0f;
        }

        // ── Show / Hide ──────────────────────────────────────────────

        /// <summary>Show this panel.</summary>
        public void Show()
        {
            gameObject.SetActive(true);
        }

        /// <summary>Hide this panel.</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // ── Debug ─────────────────────────────────────────────────────

        /// <summary>Logs the full element tree to the console.</summary>
        public void DebugLogHierarchy()
        {
            Debug.Log($"[UIPanel:{pageId}] {elementList.Count} elements indexed:");
            foreach (var elem in elementList)
            {
                Debug.Log($"  {elem.path} {elem.components}");
            }
        }
    }
}
