using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace ClockworkCraft
{
    /// <summary>
    /// Sprite-icon-based resource display in the bottom-left corner.
    /// Shows only non-zero resources as [icon] [amount] pairs.
    /// Uses CurrencyDatabase icons when available, falls back to emoji text.
    ///
    /// Layout: horizontal row of (Image + TMP_Text) pairs, auto-hidden when empty.
    /// </summary>
    public class ResourceDisplayUI : MonoBehaviour
    {
        public static ResourceDisplayUI Instance { get; private set; }

        private RectTransform container;
        private Image backgroundImage;
        private CanvasGroup canvasGroup;
        private HorizontalLayoutGroup layoutGroup;
        private CurrencyDatabase currencyDB;

        // Per-resource UI elements
        private class ResourceSlot
        {
            public GameObject root;
            public Image icon;
            public TextMeshProUGUI label;
            public TextMeshProUGUI emojiLabel; // Fallback when no sprite
        }
        private Dictionary<ResourceType, ResourceSlot> slots = new Dictionary<ResourceType, ResourceSlot>();

        // Cached resource values
        private Dictionary<ResourceType, int> resourceValues = new Dictionary<ResourceType, int>();
        private bool isDirty = true;

        // Display order for resources (determines left-to-right ordering)
        private static readonly ResourceType[] DisplayOrder = {
            ResourceType.Gold, ResourceType.Wood, ResourceType.Food,
            ResourceType.Stone, ResourceType.Water, ResourceType.Clay, ResourceType.Flowers,
            ResourceType.Gem, ResourceType.Copper, ResourceType.Ore,
            ResourceType.WhiteMarble, ResourceType.Moonstone, ResourceType.Bark,
            ResourceType.Twig, ResourceType.Acorn, ResourceType.Leaf,
            ResourceType.Grass, ResourceType.Petal, ResourceType.Rice, ResourceType.Coconut,
            ResourceType.Carrot, ResourceType.Tomato, ResourceType.Meat,
            ResourceType.Meat2, ResourceType.Meat3, ResourceType.Boar,
            ResourceType.Fish, ResourceType.Pumpkin,
            ResourceType.GoldBag, ResourceType.Approval, ResourceType.Heart,
        };

        // Hardcoded emoji fallbacks (used when CurrencyDatabase icon isn't available)
        private static readonly Dictionary<ResourceType, string> FallbackEmojis = new Dictionary<ResourceType, string>
        {
            { ResourceType.Gold,        "\U0001F4B0" }, // 💰
            { ResourceType.Wood,        "\U0001F332" }, // 🌲
            { ResourceType.Food,        "\U0001F344" }, // 🍄
            { ResourceType.Stone,       "\U0001FAA8" }, // 🪨
            { ResourceType.Water,       "\U0001F4A7" }, // 💧
            { ResourceType.Clay,        "\U0001F9F1" }, // 🧱
            { ResourceType.Flowers,     "\U0001F33B" }, // 🌻
            { ResourceType.Gem,         "\U0001F48E" }, // 💎
            { ResourceType.Copper,      "\U0001FA99" }, // 🪙
            { ResourceType.Ore,         "\u2692"      }, // ⚒
            { ResourceType.WhiteMarble, "\U0001F9CA" }, // 🧊
            { ResourceType.Moonstone,   "\U0001F319" }, // 🌙
            { ResourceType.Bark,        "\U0001FAB5" }, // 🪵
            { ResourceType.Twig,        "\U0001FAB9" }, // 🪹
            { ResourceType.Acorn,       "\U0001F330" }, // 🌰
            { ResourceType.Leaf,        "\U0001F343" }, // 🍃
            { ResourceType.Grass,       "\U0001F33F" }, // 🌿
            { ResourceType.Petal,       "\U0001F338" }, // 🌸
            { ResourceType.Rice,        "\U0001F33E" }, // 🌾
            { ResourceType.Coconut,     "\U0001F965" }, // 🥥
            { ResourceType.Carrot,      "\U0001F955" }, // 🥕
            { ResourceType.Tomato,      "\U0001F345" }, // 🍅
            { ResourceType.Meat,        "\U0001F356" }, // 🍖
            { ResourceType.Meat2,       "\U0001F969" }, // 🥩
            { ResourceType.Meat3,       "\U0001F357" }, // 🍗
            { ResourceType.Boar,        "\U0001F417" }, // 🐗
            { ResourceType.Fish,        "\U0001F41F" }, // 🐟
            { ResourceType.Pumpkin,     "\U0001F383" }, // 🎃
            { ResourceType.GoldBag,     "\U0001F4B0" }, // 💰
            { ResourceType.Approval,    "\U0001F44D" }, // 👍
            { ResourceType.Heart,       "\U0001F49C" }, // 💜
        };

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>
        /// Search multiple sources to find the CurrencyDatabase.
        /// </summary>
        private CurrencyDatabase FindCurrencyDatabase()
        {
            // 1. Try via MapGeneratorV2
            var mapGen = FindObjectOfType<MapGeneratorV2>();
            if (mapGen != null && mapGen.currencyDatabase != null)
                return mapGen.currencyDatabase;

            // 2. Try via ResourceManager
            if (ResourceManager.Instance != null && ResourceManager.Instance.currencyDatabase != null)
                return ResourceManager.Instance.currencyDatabase;

            // 3. Try FindObjectOfType on all ScriptableObjects loaded in memory
            var dbs = Resources.FindObjectsOfTypeAll<CurrencyDatabase>();
            if (dbs.Length > 0) return dbs[0];

            return null;
        }

        private int CountIcons(CurrencyDatabase db)
        {
            int count = 0;
            foreach (var c in db.AllCurrencies)
                if (c.HasIcon) count++;
            return count;
        }

        void Start()
        {
            // Find CurrencyDatabase — try multiple sources
            currencyDB = FindCurrencyDatabase();
            if (currencyDB != null)
                Debug.Log($"[ResourceDisplayUI] Found CurrencyDatabase with {currencyDB.Count} entries, icons: {CountIcons(currencyDB)}");
            else
                Debug.LogWarning("[ResourceDisplayUI] No CurrencyDatabase found — icons will be blank");

            BuildUI();

            // Subscribe to resource changes
            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.OnResourceChanged += OnResourceChanged;
                SyncFromManager();
            }
            else
            {
                StartCoroutine(WaitForResourceManager());
            }
        }

        private void SyncFromManager()
        {
            var rm = ResourceManager.Instance;
            foreach (var type in DisplayOrder)
            {
                resourceValues[type] = rm.GetResource(type);
            }
            isDirty = true;
        }

        private System.Collections.IEnumerator WaitForResourceManager()
        {
            float timeout = 5f;
            float elapsed = 0f;
            while (ResourceManager.Instance == null && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.OnResourceChanged += OnResourceChanged;
                SyncFromManager();
            }
            else
            {
                Debug.LogWarning("[ResourceDisplayUI] ResourceManager not found after timeout");
            }
        }

        void OnDestroy()
        {
            if (ResourceManager.Instance != null)
                ResourceManager.Instance.OnResourceChanged -= OnResourceChanged;
        }

        void LateUpdate()
        {
            if (isDirty)
            {
                isDirty = false;
                RefreshDisplay();
            }
        }

        private void OnResourceChanged(ResourceType type, int newTotal)
        {
            resourceValues[type] = newTotal;
            isDirty = true;
        }

        private void RefreshDisplay()
        {
            bool hasAny = false;

            foreach (var type in DisplayOrder)
            {
                if (!slots.TryGetValue(type, out ResourceSlot slot)) continue;

                bool show = resourceValues.TryGetValue(type, out int amount) && amount > 0;
                slot.root.SetActive(show);

                if (show)
                {
                    slot.label.text = amount.ToString();
                    hasAny = true;
                }
            }

            if (canvasGroup != null)
                canvasGroup.alpha = hasAny ? 1f : 0f;

            // Auto-size background to fit visible content
            if (hasAny)
                LayoutRebuilder.ForceRebuildLayoutImmediate(container);
        }

        // ─────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the RectTransform of the container panel (for loot flyout targeting).
        /// </summary>
        public RectTransform GetContainerRect() => container;

        /// <summary>
        /// Get the emoji string for a resource type. Prefers CurrencyDatabase, falls back to hardcoded.
        /// (Kept for ResourceLootFX and other callers that need emoji text.)
        /// </summary>
        public static string GetEmojiForResource(ResourceType type)
        {
            // Try CurrencyDatabase first (via singleton instance)
            if (Instance != null && Instance.currencyDB != null)
            {
                var data = Instance.currencyDB.GetByType(type);
                if (data != null && !string.IsNullOrEmpty(data.fallbackEmoji))
                    return data.fallbackEmoji;
            }

            return FallbackEmojis.TryGetValue(type, out string emoji) ? emoji : "\u2728";
        }

        /// <summary>
        /// Get the icon sprite for a resource type from CurrencyDatabase, or null.
        /// </summary>
        public static Sprite GetIconForResource(ResourceType type)
        {
            if (Instance != null && Instance.currencyDB != null)
                return Instance.currencyDB.GetIcon(type);
            return null;
        }

        // ─────────────────────────────────────────────────────────────────
        // UI Construction
        // ─────────────────────────────────────────────────────────────────

        private void BuildUI()
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[ResourceDisplayUI] No Canvas found!");
                return;
            }

            // ── Container panel (bottom-left) ───────────────────────────
            GameObject panel = new GameObject("ResourceDisplayPanel");
            panel.transform.SetParent(canvas.transform, false);

            container = panel.AddComponent<RectTransform>();
            container.anchorMin = new Vector2(0f, 1f);
            container.anchorMax = new Vector2(0f, 1f);
            container.pivot = new Vector2(0f, 1f);
            container.anchoredPosition = new Vector2(16f, -16f);
            container.sizeDelta = new Vector2(0f, 44f); // Width auto-sized by layout

            backgroundImage = panel.AddComponent<Image>();
            backgroundImage.color = new Color(0f, 0f, 0f, 0.45f);

            canvasGroup = panel.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            // Horizontal layout for icon+amount pairs
            layoutGroup = panel.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.childAlignment = TextAnchor.MiddleLeft;
            layoutGroup.spacing = 12f;
            layoutGroup.padding = new RectOffset(10, 10, 4, 4);
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;

            // Content size fitter to auto-shrink container to content
            ContentSizeFitter fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ── Create a slot for each resource type ────────────────────
            foreach (var type in DisplayOrder)
            {
                var slot = CreateResourceSlot(panel.transform, type);
                slots[type] = slot;
                slot.root.SetActive(false); // Hidden until non-zero
            }

            Debug.Log("[ResourceDisplayUI] UI built — bottom-left sprite icon resource bar");
        }

        private ResourceSlot CreateResourceSlot(Transform parent, ResourceType type)
        {
            ResourceSlot slot = new ResourceSlot();

            // Root container for this resource pair
            GameObject root = new GameObject($"Res_{type}");
            root.transform.SetParent(parent, false);

            RectTransform rootRect = root.AddComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(0f, 36f);

            HorizontalLayoutGroup hlg = root.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.spacing = 4f;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            ContentSizeFitter rootFitter = root.AddComponent<ContentSizeFitter>();
            rootFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            rootFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            slot.root = root;

            // Try to get sprite from CurrencyDatabase
            Sprite iconSprite = null;
            if (currencyDB != null)
                iconSprite = currencyDB.GetIcon(type);

            if (iconSprite != null)
            {
                // ── Sprite icon ─────────────────────────────────────────
                GameObject iconObj = new GameObject("Icon");
                iconObj.transform.SetParent(root.transform, false);

                RectTransform iconRect = iconObj.AddComponent<RectTransform>();
                iconRect.sizeDelta = new Vector2(28f, 28f);

                LayoutElement iconLayout = iconObj.AddComponent<LayoutElement>();
                iconLayout.preferredWidth = 28f;
                iconLayout.preferredHeight = 28f;

                Image iconImage = iconObj.AddComponent<Image>();
                iconImage.sprite = iconSprite;
                iconImage.preserveAspect = true;

                slot.icon = iconImage;
            }
            else
            {
                // ── Emoji text fallback ─────────────────────────────────
                GameObject emojiObj = new GameObject("Emoji");
                emojiObj.transform.SetParent(root.transform, false);

                RectTransform emojiRect = emojiObj.AddComponent<RectTransform>();
                emojiRect.sizeDelta = new Vector2(28f, 28f);

                LayoutElement emojiLayout = emojiObj.AddComponent<LayoutElement>();
                emojiLayout.preferredWidth = 28f;
                emojiLayout.preferredHeight = 28f;

                TextMeshProUGUI emojiText = emojiObj.AddComponent<TextMeshProUGUI>();
                string emoji = FallbackEmojis.TryGetValue(type, out string e) ? e : "\u2728";
                emojiText.text = emoji;
                emojiText.fontSize = 20;
                emojiText.alignment = TextAlignmentOptions.Center;
                emojiText.enableAutoSizing = false;

                slot.emojiLabel = emojiText;
            }

            // ── Amount text ─────────────────────────────────────────
            GameObject textObj = new GameObject("Amount");
            textObj.transform.SetParent(root.transform, false);

            LayoutElement textLayout = textObj.AddComponent<LayoutElement>();
            textLayout.preferredHeight = 28f;

            TextMeshProUGUI amountText = textObj.AddComponent<TextMeshProUGUI>();
            amountText.text = "0";
            amountText.fontSize = 20;
            amountText.color = Color.white;
            amountText.alignment = TextAlignmentOptions.MidlineLeft;
            amountText.fontStyle = FontStyles.Bold;
            amountText.enableAutoSizing = false;
            amountText.overflowMode = TextOverflowModes.Overflow;

            amountText.outlineWidth = 0.15f;
            amountText.outlineColor = new Color(0f, 0f, 0f, 0.8f);

            slot.label = amountText;

            return slot;
        }
    }
}
