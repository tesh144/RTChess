#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// Shows placement costs as world-space currency icons stacked in a vertical column
    /// above the drop location. Icons appear one-by-one from bottom to top with a pop animation.
    ///
    /// Uses the FloatingCurrencyWorldUI prefab (a world-space Canvas with Icon Image + Quantity TMP).
    /// Red tint + alpha pulse on the quantity text when the player can't afford a resource.
    ///
    /// Prefab is auto-discovered from Assets/Prefabs/ if not assigned in Inspector.
    ///
    /// Activated by DragDropHandler.StartDrag(), updated by UpdateDrag(),
    /// hidden by CleanupDragVisuals().
    /// </summary>
    public class PlacementCostDisplay : MonoBehaviour
    {
        public static PlacementCostDisplay Instance { get; private set; }

        [Header("Prefab")]
        [Tooltip("FloatingCurrencyWorldUI prefab — a world-space Canvas with Icon + Quantity. Auto-found if null.")]
        [SerializeField] private GameObject currencyIconPrefab;

        [Header("Column Settings")]
        [SerializeField] private float baseHeight = 0.7f;         // Height of first (bottom) icon above ground
        [SerializeField] private float stackSpacing = 0.45f;      // Vertical spacing between icons
        [SerializeField] private float iconWorldScale = 0.008f;   // World-space scale for the Canvas

        [Header("Can't Afford")]
        [SerializeField] private float pulseSpeed = 4f;
        [SerializeField] private float pulseMin = 0.4f;

        [Header("Appear Animation")]
        [SerializeField] private float perEntryDelay = 0.07f;     // Stagger between each icon appearing
        [SerializeField] private float popDuration = 0.2f;        // Duration of each icon's pop-in

        // Runtime
        private readonly List<ColumnEntry> entries = new List<ColumnEntry>();
        private Vector3 columnBase;
        private bool isVisible;
        private Coroutine appearCoroutine;

        private struct ColumnEntry
        {
            public GameObject root;
            public Image iconImage;
            public TextMeshProUGUI quantityText;
            public bool canAfford;
            public int stackIndex;           // 0 = bottom, 1 = next up, etc.
            public Color baseIconColor;
            public Color baseTextColor;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            FindPrefab();
        }

        private void Update()
        {
            if (!isVisible || entries.Count == 0) return;

            float pulseT = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            float pulseAlpha = Mathf.Lerp(pulseMin, 1f, pulseT);

            Camera cam = Camera.main;

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.root == null) continue;

                // Column position: stack vertically above the base point
                Vector3 pos = columnBase + new Vector3(0f, baseHeight + stackSpacing * e.stackIndex, 0f);
                e.root.transform.position = pos;

                // Billboard — match camera rotation so icons always face the viewer
                if (cam != null)
                    e.root.transform.rotation = cam.transform.rotation;

                // Pulse only the text when unaffordable — icon keeps its normal look
                if (!e.canAfford)
                {
                    Color textC = e.baseTextColor;
                    textC.a = pulseAlpha;
                    if (e.quantityText != null) e.quantityText.color = textC;
                }
            }
        }

        // ── Public API ────────────────────────────────────────────────

        /// <summary>
        /// Show placement costs for the given unit stats.
        /// </summary>
        public void Show(UnitStats stats)
        {
            if (stats == null) { Hide(); return; }

            string itemName = stats.unitName ?? "(null)";

            // Priority 1: EconomyManager
            if (EconomyManager.Instance != null
                && EconomyManager.Instance.balanceConfig != null
                && EconomyManager.Instance.HasConfiguredCost(itemName))
            {
                var costs = EconomyManager.Instance.GetPlacementCosts(itemName);
                if (costs != null && costs.Count > 0) { ShowCosts(costs); return; }
            }

            // Priority 2: UnitStats.placementCosts
            if (stats.placementCosts != null && stats.placementCosts.Count > 0)
            {
                ShowCosts(stats.placementCosts);
                return;
            }

            // Priority 3: Legacy resourceCost → Gold
            if (stats.resourceCost > 0)
            {
                ShowCosts(new List<PlacementCost> {
                    new PlacementCost {
                        resourceType = ClockworkCraft.ResourceType.Gold,
                        amount = stats.resourceCost
                    }
                });
                return;
            }

            Hide();
        }

        /// <summary>
        /// Show a list of placement costs as a vertical column of icons.
        /// </summary>
        public void ShowCosts(List<PlacementCost> costs)
        {
            if (currencyIconPrefab == null) FindPrefab();
            if (currencyIconPrefab == null)
            {
                Debug.LogWarning("[PlacementCostDisplay] No FloatingCurrencyWorldUI prefab found");
                return;
            }
            if (costs == null || costs.Count == 0) { Hide(); return; }

            ClearEntries();

            int idx = 0;
            foreach (var cost in costs)
            {
                if (cost.amount <= 0) continue;
                CreateEntry(cost, idx);
                idx++;
            }

            isVisible = idx > 0;
            if (isVisible)
            {
                if (appearCoroutine != null) StopCoroutine(appearCoroutine);
                appearCoroutine = StartCoroutine(AppearAnimation());
            }
        }

        /// <summary>
        /// Hide all icons.
        /// </summary>
        public void Hide()
        {
            if (appearCoroutine != null) { StopCoroutine(appearCoroutine); appearCoroutine = null; }
            ClearEntries();
            isVisible = false;
        }

        /// <summary>
        /// Kept for API compat — screen-space position (unused now).
        /// </summary>
        public void UpdatePosition(Vector3 screenPosition) { }

        /// <summary>
        /// Set the column base position in world space.
        /// Called by DragDropHandler each frame during drag.
        /// </summary>
        public void SetWorldCenter(Vector3 worldCenter)
        {
            columnBase = worldCenter;
        }

        /// <summary>
        /// Show/hide existing entries without destroying them.
        /// Used by DragDropHandler to toggle visibility based on cell validity.
        /// </summary>
        public void SetEntriesVisible(bool visible)
        {
            foreach (var e in entries)
            {
                if (e.root != null)
                    e.root.SetActive(visible);
            }
            isVisible = visible && entries.Count > 0;
        }

        /// <summary>
        /// Reset state for a fresh drag session.
        /// Called before Show() to avoid stale position from previous drag.
        /// </summary>
        public void ResetOrbit()
        {
            // Name kept for API compat — just resets internal state
        }

        // ── Entry Creation ────────────────────────────────────────────

        private void CreateEntry(PlacementCost cost, int stackIndex)
        {
            bool canAfford = CanAfford(cost);

            // Instantiate the prefab
            GameObject instance = Instantiate(currencyIconPrefab);
            instance.name = $"CostColumn_{cost.resourceType}";
            instance.transform.localScale = Vector3.one * iconWorldScale;

            // Find Icon (Image) and Quantity (TMP) inside the prefab hierarchy
            Image iconImage = null;
            TextMeshProUGUI quantityText = null;

            var images = instance.GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img.gameObject.name == "Icon" || img.sprite != null)
                {
                    iconImage = img;
                    break;
                }
            }
            if (iconImage == null && images.Length > 0)
                iconImage = images[0];

            quantityText = instance.GetComponentInChildren<TextMeshProUGUI>(true);

            // Set the icon sprite from CurrencyDatabase
            Sprite sprite = cost.icon;
            if (sprite == null)
                sprite = ClockworkCraft.ResourceDisplayUI.GetIconForResource(cost.resourceType);

            if (iconImage != null && sprite != null)
                iconImage.sprite = sprite;

            // Set the quantity text
            if (quantityText != null)
                quantityText.text = cost.amount.ToString();

            // Icon always stays normal color; text goes red if can't afford
            Color iconColor = Color.white;
            Color textColor = canAfford ? Color.white : new Color(1f, 0.3f, 0.3f);

            if (iconImage != null) iconImage.color = iconColor;
            if (quantityText != null) quantityText.color = textColor;

            entries.Add(new ColumnEntry
            {
                root = instance,
                iconImage = iconImage,
                quantityText = quantityText,
                canAfford = canAfford,
                stackIndex = stackIndex,
                baseIconColor = iconColor,
                baseTextColor = textColor
            });
        }

        // ── Animation ─────────────────────────────────────────────────

        /// <summary>
        /// Staggered appear animation: each icon pops in one-by-one from bottom to top.
        /// Each entry scales 0 → 1.2× → 1× with a slight delay between entries.
        /// </summary>
        private IEnumerator AppearAnimation()
        {
            float targetScale = iconWorldScale;

            // Start all entries at zero scale
            foreach (var e in entries)
            {
                if (e.root != null)
                    e.root.transform.localScale = Vector3.zero;
            }

            // Pop in each entry from bottom (index 0) to top
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.root == null) continue;

                // Start this entry's pop animation as a nested coroutine
                StartCoroutine(PopEntry(entry.root, targetScale));

                // Wait before starting the next entry
                if (i < entries.Count - 1)
                    yield return new WaitForSeconds(perEntryDelay);
            }

            // Wait for the last entry to finish
            yield return new WaitForSeconds(popDuration);

            // Ensure final scales
            foreach (var e in entries)
            {
                if (e.root != null)
                    e.root.transform.localScale = Vector3.one * targetScale;
            }

            appearCoroutine = null;
        }

        /// <summary>
        /// Pop a single entry: 0 → 1.2× → 1× over popDuration.
        /// </summary>
        private IEnumerator PopEntry(GameObject root, float targetScale)
        {
            float elapsed = 0f;

            while (elapsed < popDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / popDuration);

                float scale;
                if (t < 0.6f)
                {
                    float subT = t / 0.6f;
                    subT = 1f - (1f - subT) * (1f - subT); // ease-out quad
                    scale = Mathf.Lerp(0f, 1.2f, subT);
                }
                else
                {
                    float subT = (t - 0.6f) / 0.4f;
                    scale = Mathf.Lerp(1.2f, 1f, subT);
                }

                if (root != null)
                    root.transform.localScale = Vector3.one * (targetScale * scale);

                yield return null;
            }

            if (root != null)
                root.transform.localScale = Vector3.one * targetScale;
        }

        // ── Helpers ───────────────────────────────────────────────────

        private bool CanAfford(PlacementCost cost)
        {
            if (ClockworkCraft.ResourceManager.Instance == null) return true;
            return ClockworkCraft.ResourceManager.Instance.GetResource(cost.resourceType) >= cost.amount;
        }

        private void ClearEntries()
        {
            foreach (var e in entries)
            {
                if (e.root != null)
                    Destroy(e.root);
            }
            entries.Clear();
        }

        /// <summary>
        /// Auto-find the FloatingCurrencyWorldUI prefab if not assigned.
        /// </summary>
        private void FindPrefab()
        {
            if (currencyIconPrefab != null) return;

#if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets("FloatingCurrencyWorldUI t:Prefab");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                currencyIconPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (currencyIconPrefab != null)
                    Debug.Log($"[PlacementCostDisplay] Auto-found prefab at {path}");
            }
#endif
            if (currencyIconPrefab == null)
            {
                // Try Resources fallback
                currencyIconPrefab = Resources.Load<GameObject>("FloatingCurrencyWorldUI");
            }

            if (currencyIconPrefab == null)
                Debug.LogWarning("[PlacementCostDisplay] Could not find FloatingCurrencyWorldUI prefab — assign it in Inspector");
        }
    }

    /// <summary>
    /// A single resource cost for placing an object.
    /// </summary>
    [System.Serializable]
    public struct PlacementCost
    {
        [Tooltip("Which resource type is required (e.g. Gold, Wood, Stone).")]
        public ClockworkCraft.ResourceType resourceType;

        [Tooltip("Amount required.")]
        public int amount;

        [Tooltip("Icon sprite for this currency (auto-resolved from CurrencyDatabase if null).")]
        public Sprite icon;
    }
}
