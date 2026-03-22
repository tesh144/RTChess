#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using ClockworkGrid;

namespace ClockworkCraft
{
    /// <summary>
    /// Manages the currency display bar.
    ///
    /// How it works:
    /// 1. Finds the "Currency Bar" container in the scene hierarchy (child of the Lobby panel)
    /// 2. Removes all demo CurrencyHolder children placed in the scene (preserves Button_Add)
    /// 3. For each currency the player currently owns (amount > 0), clones the CurrencyHolder prefab
    /// 4. Each clone's TMP text is set to: <sprite name="Gold"> 20  (inline icon + amount)
    /// 5. Slots appear when a resource is first earned, and hide when it reaches 0
    ///
    /// The CurrencyHolder prefab already has font, color, size, and TMP_SpriteAsset baked in.
    /// We clone it exactly as-is and only change the text content.
    ///
    /// Singleton — lives on a scene GameObject (typically alongside MapGeneratorV2).
    /// </summary>
    public class ResourceDisplayUI : MonoBehaviour
    {
        public static ResourceDisplayUI Instance { get; private set; }

        [Header("Prefab")]
        [Tooltip("CurrencyHolder prefab — a TMP text object with sprite asset. Auto-found if not assigned.")]
        [SerializeField] private GameObject currencyHolderPrefab;

        // ── Runtime State ──────────────────────────────────────────────

        private Transform currencyBarTransform;
        private CurrencyDatabase currencyDB;
        private readonly Dictionary<ResourceType, CurrencySlotUI> activeSlots = new Dictionary<ResourceType, CurrencySlotUI>();
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

        // ── Lifecycle ──────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            currencyDB = FindCurrencyDatabase();
            if (currencyDB != null)
                Debug.Log($"[ResourceDisplayUI] Found CurrencyDatabase with {currencyDB.Count} entries");

            // Auto-find CurrencyHolder prefab if not assigned in Inspector
            if (currencyHolderPrefab == null)
                currencyHolderPrefab = FindCurrencyHolderPrefab();

            FindCurrencyBar();

            if (currencyHolderPrefab == null)
                Debug.LogError("[ResourceDisplayUI] CurrencyHolder prefab not found! Assign it in Inspector or place it at Assets/Prefabs/UI/CurrencyHolder.");

            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.OnResourceChanged += OnResourceChanged;
                SyncAllFromManager();
            }
            else
            {
                StartCoroutine(WaitForResourceManager());
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

        // ── Find Currency Bar ──────────────────────────────────────────

        /// <summary>
        /// Finds the "Currency Bar" Transform in the scene.
        /// Searches under any Canvas for a GameObject named "Currency Bar".
        /// </summary>
        private void FindCurrencyBar()
        {
            // Search all transforms for "Currency Bar"
            var allTransforms = FindObjectsOfType<Transform>(true);
            foreach (var t in allTransforms)
            {
                if (t.name == "Currency Bar")
                {
                    currencyBarTransform = t;
                    Debug.Log($"[ResourceDisplayUI] Found 'Currency Bar' under '{t.parent?.name ?? "root"}'");

                    // Remove ALL demo CurrencyHolder children and any previously
                    // dynamically-created slots. Scene has: "CurrencyHolder",
                    // "CurrencyHolder (1)", "CurrencyHolder (2)", "CurrencyHolder (3)".
                    // Preserve Button_Add and anything else that isn't a currency slot.
                    for (int i = currencyBarTransform.childCount - 1; i >= 0; i--)
                    {
                        Transform child = currencyBarTransform.GetChild(i);
                        if (child.name.StartsWith("Currency"))
                            Destroy(child.gameObject);
                    }
                    return;
                }
            }

            Debug.LogWarning("[ResourceDisplayUI] 'Currency Bar' not found in scene — creating fallback");
            BuildFallbackCurrencyBar();
        }

        private void BuildFallbackCurrencyBar()
        {
            Canvas targetCanvas = null;
            Canvas[] allCanvases = FindObjectsOfType<Canvas>(true);
            foreach (var c in allCanvases)
            {
                if (c.gameObject.activeInHierarchy && c.isRootCanvas)
                {
                    targetCanvas = c;
                    break;
                }
            }
            if (targetCanvas == null && allCanvases.Length > 0)
                targetCanvas = allCanvases[0];
            if (targetCanvas == null) return;

            GameObject bar = new GameObject("Currency Bar");
            bar.transform.SetParent(targetCanvas.transform, false);

            RectTransform barRect = bar.AddComponent<RectTransform>();
            barRect.anchorMin = new Vector2(1f, 1f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(1f, 1f);
            barRect.anchoredPosition = new Vector2(-20f, -20f);
            barRect.sizeDelta = new Vector2(0f, 50f);

            HorizontalLayoutGroup hlg = bar.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.spacing = 12f;
            hlg.padding = new RectOffset(8, 8, 4, 4);
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            ContentSizeFitter fitter = bar.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            currencyBarTransform = bar.transform;
            Debug.Log("[ResourceDisplayUI] Created fallback 'Currency Bar' on Canvas");
        }

        // ── Slot Management ─────────────────────────────────────────────

        /// <summary>
        /// Creates a slot for a resource type by cloning the CurrencyHolder prefab.
        /// Inserts at the correct position based on DisplayOrder.
        /// </summary>
        private CurrencySlotUI CreateSlot(ResourceType type)
        {
            if (currencyBarTransform == null || currencyHolderPrefab == null)
            {
                if (currencyHolderPrefab == null)
                    Debug.LogError($"[ResourceDisplayUI] Cannot create slot for {type} — no CurrencyHolder prefab!");
                return null;
            }

            // Clone the CurrencyHolder prefab exactly as-is.
            // The prefab has the correct font, color, size, and TMP_SpriteAsset.
            GameObject slotObj = Instantiate(currencyHolderPrefab, currencyBarTransform, false);
            slotObj.name = $"Currency_{type}";

            CurrencySlotUI slot = slotObj.AddComponent<CurrencySlotUI>();
            slot.Initialize(type);

            // Set sibling index based on display order
            int targetIndex = GetDisplayOrderIndex(type);
            int siblingIndex = 0;
            for (int i = 0; i < currencyBarTransform.childCount; i++)
            {
                var existingSlot = currencyBarTransform.GetChild(i).GetComponent<CurrencySlotUI>();
                if (existingSlot != null && GetDisplayOrderIndex(existingSlot.ResourceType) < targetIndex)
                    siblingIndex = i + 1;
            }
            slotObj.transform.SetSiblingIndex(siblingIndex);

            activeSlots[type] = slot;
            Debug.Log($"[ResourceDisplayUI] Created slot for {type} at index {siblingIndex}");
            return slot;
        }

        private int GetDisplayOrderIndex(ResourceType type)
        {
            for (int i = 0; i < DisplayOrder.Length; i++)
                if (DisplayOrder[i] == type) return i;
            return DisplayOrder.Length;
        }

        // ── Resource Change Handling ────────────────────────────────────

        private void OnResourceChanged(ResourceType type, int newTotal)
        {
            isDirty = true;
        }

        private void SyncAllFromManager()
        {
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
                SyncAllFromManager();
            }
            else
            {
                Debug.LogWarning("[ResourceDisplayUI] ResourceManager not found after timeout");
            }
        }

        /// <summary>
        /// Rebuilds the visible state of the currency bar.
        /// Creates new slots for newly-earned currencies, updates amounts, hides empty ones.
        /// </summary>
        private void RefreshDisplay()
        {
            if (ResourceManager.Instance == null) return;

            foreach (var type in DisplayOrder)
            {
                int amount = ResourceManager.Instance.GetResource(type);

                if (amount > 0)
                {
                    // Ensure slot exists
                    if (!activeSlots.TryGetValue(type, out CurrencySlotUI slot) || slot == null)
                    {
                        slot = CreateSlot(type);
                        if (slot == null) continue;
                    }

                    slot.UpdateAmount(amount);
                    slot.gameObject.SetActive(true);
                }
                else
                {
                    // Hide slot if it exists
                    if (activeSlots.TryGetValue(type, out CurrencySlotUI slot) && slot != null)
                    {
                        slot.gameObject.SetActive(false);
                    }
                }
            }
        }

        // ── Public API ─────────────────────────────────────────────────

        /// <summary>
        /// Returns the RectTransform of the Currency Bar (for loot flyout targeting).
        /// </summary>
        public RectTransform GetContainerRect()
        {
            return currencyBarTransform != null ? currencyBarTransform.GetComponent<RectTransform>() : null;
        }

        /// <summary>
        /// Get the RectTransform of a specific currency slot (for loot fly targeting per-resource).
        /// </summary>
        public RectTransform GetSlotRect(ResourceType type)
        {
            if (activeSlots.TryGetValue(type, out CurrencySlotUI slot) && slot != null)
                return slot.GetComponent<RectTransform>();
            return GetContainerRect();
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

        /// <summary>
        /// Get the emoji string for a resource type.
        /// </summary>
        public static string GetEmojiForResource(ResourceType type)
        {
            if (Instance != null && Instance.currencyDB != null)
            {
                var data = Instance.currencyDB.GetByType(type);
                if (data != null && !string.IsNullOrEmpty(data.fallbackEmoji))
                    return data.fallbackEmoji;
            }
            return "\u2728";
        }

        // ── Prefab Resolution ─────────────────────────────────────────

        /// <summary>
        /// Auto-finds the CurrencyHolder prefab if not assigned in Inspector.
        /// Searches loaded assets for a GameObject named "CurrencyHolder".
        /// </summary>
        private static GameObject FindCurrencyHolderPrefab()
        {
            // Search all loaded GameObjects for the prefab
            var allGOs = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in allGOs)
            {
                if (go.name == "CurrencyHolder" && go.GetComponent<TextMeshProUGUI>() != null)
                {
                    // Verify it's a prefab (not a scene instance) by checking hideFlags
                    // Prefab assets typically have HideInHierarchy set
                    if (go.scene.name == null || !go.scene.isLoaded)
                    {
                        Debug.Log($"[ResourceDisplayUI] Auto-found CurrencyHolder prefab: {go.name}");
                        return go;
                    }
                }
            }

            // Try Resources.Load
            var loaded = Resources.Load<GameObject>("CurrencyHolder");
            if (loaded != null)
            {
                Debug.Log("[ResourceDisplayUI] Found CurrencyHolder via Resources.Load");
                return loaded;
            }

            return null;
        }

        // ── Database Resolution ────────────────────────────────────────

        private CurrencyDatabase FindCurrencyDatabase()
        {
            var mapGen = FindFirstObjectByType<MapGeneratorV2>();
            if (mapGen != null && mapGen.currencyDatabase != null)
                return mapGen.currencyDatabase;

            if (ResourceManager.Instance != null && ResourceManager.Instance.currencyDatabase != null)
                return ResourceManager.Instance.currencyDatabase;

            var dbs = Resources.FindObjectsOfTypeAll<CurrencyDatabase>();
            if (dbs.Length > 0) return dbs[0];

            return null;
        }
    }
}
