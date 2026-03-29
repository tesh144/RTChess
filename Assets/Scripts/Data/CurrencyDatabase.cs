#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace ClockworkCraft
{
    /// <summary>
    /// ScriptableObject database containing all currency definitions.
    /// Create via: Right-click → Create → ClockworkCraft → Currency Database
    ///
    /// This is the single source of truth for what currencies exist in the game.
    /// EnvironmentData references entries by ResourceType to define what each
    /// environment node drops when hit.
    ///
    /// The ResourceDisplayUI reads this database to know which emoji/sprite
    /// to show for each resource type.
    /// </summary>
    [CreateAssetMenu(fileName = "CurrencyDatabase", menuName = "ClockworkCraft/Currency Database")]
    public class CurrencyDatabase : ScriptableObject
    {
        [Header("All Currencies")]
        [SerializeField] private List<CurrencyData> currencyList = new List<CurrencyData>();

        public List<CurrencyData> AllCurrencies => currencyList;

        /// <summary>
        /// Get currency data by ResourceType enum.
        /// </summary>
        public CurrencyData GetByType(ResourceType type)
        {
            return currencyList.FirstOrDefault(c => c.resourceType == type);
        }

        /// <summary>
        /// Get currency data by display name.
        /// </summary>
        public CurrencyData GetByName(string name)
        {
            return currencyList.FirstOrDefault(c => c.currencyName == name);
        }

        /// <summary>
        /// Get the emoji string for a resource type.
        /// Falls back to ✨ if the type isn't in the database.
        /// </summary>
        public string GetEmoji(ResourceType type)
        {
            var data = GetByType(type);
            return data != null ? data.fallbackEmoji : "\u2728";
        }

        /// <summary>
        /// Get the icon sprite for a resource type, or null if none assigned.
        /// </summary>
        public Sprite GetIcon(ResourceType type)
        {
            var data = GetByType(type);
            return data?.icon;
        }

        /// <summary>
        /// Get all currencies that are unlocked at the start.
        /// </summary>
        public List<CurrencyData> GetUnlockedAtStart()
        {
            return currencyList.Where(c => c.unlockedAtStart).ToList();
        }

        /// <summary>
        /// Add a currency entry.
        /// </summary>
        public void AddCurrency(CurrencyData data)
        {
            if (!currencyList.Contains(data))
                currencyList.Add(data);
        }

        /// <summary>
        /// Clear all currency data.
        /// </summary>
        public void Clear()
        {
            currencyList.Clear();
        }

        /// <summary>
        /// Number of currency entries.
        /// </summary>
        public int Count => currencyList.Count;

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only: Populate with default entries for all ResourceType enum values.
        /// Skips ResourceType.None. Won't overwrite existing entries.
        /// </summary>
        [ContextMenu("Populate Default Currencies")]
        public void PopulateDefaults()
        {
            // (type, displayName, emoji, startingAmount, unlockedAtStart)
            var defaults = new (ResourceType type, string name, string emoji, int starting, bool unlocked)[]
            {
                // ── Core Resources ──────────────────
                (ResourceType.Gold,        "Gold",         "\U0001F4B0", 0, true),   // 💰
                (ResourceType.Wood,        "Wood",         "\U0001F332", 0, true),   // 🌲
                (ResourceType.Food,        "Food",         "\U0001F344", 0, false),  // 🍄
                (ResourceType.Stone,       "Stone",        "\U0001FAA8",  0, false),  // 🪨
                (ResourceType.Water,       "Water",        "\U0001F4A7",  0, false),  // 💧
                (ResourceType.Clay,        "Clay",         "\U0001F9F1",  0, false),  // 🧱
                (ResourceType.Flowers,     "Flowers",      "\U0001F33B",  0, false),  // 🌻
                // ── Gathered Resources ──────────────
                (ResourceType.Gem,         "Gem",          "\U0001F48E",  0, false),  // 💎
                (ResourceType.Copper,      "Copper",       "\U0001FA99",  0, false),  // 🪙
                (ResourceType.Ore,         "Ore",          "\u2692",      0, false),  // ⚒
                (ResourceType.WhiteMarble, "White Marble", "\U0001F9CA",  0, false),  // 🧊
                (ResourceType.Moonstone,   "Moonstone",    "\U0001F319",  0, false),  // 🌙
                (ResourceType.Bark,        "Bark",         "\U0001FAB5",  0, false),  // 🪵
                (ResourceType.Twig,        "Twig",         "\U0001FAB9",  0, false),  // 🪹
                (ResourceType.Acorn,       "Acorn",        "\U0001F330",  0, false),  // 🌰
                (ResourceType.Leaf,        "Leaf",         "\U0001F343",  0, false),  // 🍃
                (ResourceType.Grass,       "Grass",        "\U0001F33F",  0, false),  // 🌿
                (ResourceType.Petal,       "Petal",        "\U0001F338",  0, false),  // 🌸
                (ResourceType.Rice,        "Rice",         "\U0001F33E",  0, false),  // 🌾
                (ResourceType.Coconut,     "Coconut",      "\U0001F965",  0, false),  // 🥥
                // ── Food & Animal Resources ─────────
                (ResourceType.Carrot,      "Carrot",       "\U0001F955",  0, false),  // 🥕
                (ResourceType.Tomato,      "Tomato",       "\U0001F345",  0, false),  // 🍅
                (ResourceType.Meat,        "Meat",         "\U0001F356",  0, false),  // 🍖
                (ResourceType.Meat2,       "Meat II",      "\U0001F969",  0, false),  // 🥩
                (ResourceType.Meat3,       "Meat III",     "\U0001F357",  0, false),  // 🍗
                (ResourceType.Boar,        "Boar",         "\U0001F417",  0, false),  // 🐗
                (ResourceType.Fish,        "Fish",         "\U0001F41F",  0, false),  // 🐟
                (ResourceType.Pumpkin,     "Pumpkin",      "\U0001F383",  0, false),  // 🎃
                // ── Special / Abstract ──────────────
                (ResourceType.GoldBag,     "Gold Bag",     "\U0001F4B0",  0, false),  // 💰
                (ResourceType.Approval,    "Approval",     "\U0001F44D",  0, false),  // 👍
                (ResourceType.Heart,       "Heart",        "\U0001F49C",  0, false),  // 💜
                // ── Building Production Resources ────
                (ResourceType.Scrap,       "Scrap",        "\U0001F527",  0, false),  // 🔧 — produced by Scrapper
                (ResourceType.Reed,        "Reed",         "\U0001F33E",  0, false),  // 🌾 — harvested from reed tiles
            };

            foreach (var d in defaults)
            {
                if (GetByType(d.type) != null) continue;

                AddCurrency(new CurrencyData
                {
                    currencyName    = d.name,
                    resourceType    = d.type,
                    fallbackEmoji   = d.emoji,
                    startingAmount  = d.starting,
                    unlockedAtStart = d.unlocked,
                    icon            = null
                });
            }

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[CurrencyDatabase] Populated with {Count} currencies");
        }

        /// <summary>
        /// Editor-only: Auto-assign icon sprites by searching the Icons folder.
        /// Matches currency names to sprite filenames in Icon_30px/Currency and Icon_30px/World.
        /// Run after PopulateDefaults to fill in all the icons.
        /// </summary>
        [ContextMenu("Auto-Assign Icon Sprites")]
        public void AutoAssignIcons()
        {
            // Map ResourceType → search path (relative to Assets/)
            // Prefers 30px Currency, falls back to 30px World, then 25px Bubble, then 20px Panel
            var iconMap = new (ResourceType type, string assetPath)[]
            {
                (ResourceType.Gold,        "Assets/Icons/Icon_30px/World/Gold - World0.png"),
                (ResourceType.Wood,        "Assets/Icons/Icon_30px/Currency/Wood - Currency.png"),
                (ResourceType.Food,        "Assets/Icons/Icon_30px/Currency/Food - Currency.png"),
                (ResourceType.Stone,       "Assets/Icons/Icon_30px/Currency/Rock - Currency.png"),
                (ResourceType.Water,       "Assets/Icons/Icon_30px/World/PanelAsset_007.png"),
                (ResourceType.Clay,        "Assets/Icons/Icon_30px/Currency/Clay - Currency.png"),
                (ResourceType.Flowers,     "Assets/Icons/Icon_30px/World/Petal - World.png"),
                (ResourceType.Gem,         "Assets/Icons/Icon_30px/Currency/Gem - Currency.png"),
                (ResourceType.Copper,      "Assets/Icons/Icon_30px/Currency/Copper - Currency.png"),
                (ResourceType.Ore,         "Assets/Icons/Icon_30px/World/ORE - World.png"),
                (ResourceType.WhiteMarble, "Assets/Icons/Icon_30px/Currency/WhiteMarble - Currency.png"),
                (ResourceType.Moonstone,   "Assets/Icons/Icon_30px/Currency/Moonstone - Currency.png"),
                (ResourceType.Bark,        "Assets/Icons/Icon_30px/Currency/Bark - Currency.png"),
                (ResourceType.Twig,        "Assets/Icons/Icon_30px/Currency/TwighPile - Currency.png"),
                (ResourceType.Acorn,       "Assets/Icons/Icon_30px/Currency/Acorn - Currency.png"),
                (ResourceType.Leaf,        "Assets/Icons/Icon_30px/Currency/Leafe - Currency.png"),
                (ResourceType.Grass,       "Assets/Icons/Icon_30px/World/Grass - World.png"),
                (ResourceType.Petal,       "Assets/Icons/Icon_30px/World/Petal - World.png"),
                (ResourceType.Rice,        "Assets/Icons/Icon_30px/World/Rice - World.png"),
                (ResourceType.Coconut,     "Assets/Icons/Icon_25px/coconut.png"),
                (ResourceType.Carrot,      "Assets/Icons/Icon_30px/World/Carrot - World.png"),
                (ResourceType.Tomato,      "Assets/Icons/Icon_30px/World/Tomato  - World.png"),
                (ResourceType.Meat,        "Assets/Icons/Icon_30px/World/Meat - World.png"),
                (ResourceType.Meat2,       "Assets/Icons/Icon_30px/World/Meat - World (2).png"),
                (ResourceType.Meat3,       "Assets/Icons/Icon_30px/Currency/Meat3 - Currency.png"),
                (ResourceType.Boar,        "Assets/Icons/Icon_20px/Boar Body.png"),
                (ResourceType.Fish,        "Assets/Icons/Icon_30px/Others/Fish.png"),
                (ResourceType.Pumpkin,     "Assets/Icons/Icon_30px/Currency/PumpKin - Currency.png"),
                (ResourceType.GoldBag,     "Assets/Icons/Icon_30px/World/Gold Bag - World.png"),
                (ResourceType.Approval,    "Assets/Icons/Icon_25px/ThumbsUp - Bubble.png"),
                (ResourceType.Heart,       "Assets/Icons/Icon_25px/PinkHeart - Bubble.png"),
                // ── Building Production Resources ────
                // TODO: add art and update these paths once Scrap/Reed icons are created
                (ResourceType.Scrap,       "Assets/Icons/Icon_30px/Currency/Scrap - Currency.png"),
                (ResourceType.Reed,        "Assets/Icons/Icon_30px/Currency/Reed - Currency.png"),
            };

            int assigned = 0;
            int skipped = 0;
            foreach (var entry in iconMap)
            {
                var data = GetByType(entry.type);
                if (data == null) continue;

                // Never overwrite a manually assigned icon — designer input takes priority
                if (data.icon != null)
                {
                    skipped++;
                    continue;
                }

                var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(entry.assetPath);
                if (sprite != null)
                {
                    data.icon = sprite;
                    assigned++;
                }
                else
                {
                    Debug.LogWarning($"[CurrencyDatabase] Sprite not found at: {entry.assetPath} for {entry.type}");
                }
            }

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[CurrencyDatabase] Auto-assigned {assigned}/{iconMap.Length} icons ({skipped} skipped — already set)");
        }
#endif
    }
}
