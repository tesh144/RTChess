#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using UnityEditor;
using ClockworkCraft;

/// <summary>
/// One-shot editor script: populates the CurrencyDatabase asset with all currency entries
/// and auto-assigns icon sprites from the Icons folder.
/// Runs automatically on next domain reload, then deletes itself.
/// </summary>
[InitializeOnLoad]
public static class PopulateCurrencyDatabase
{
    static PopulateCurrencyDatabase()
    {
        // Delay to ensure everything is loaded
        EditorApplication.delayCall += Run;
    }

    static void Run()
    {
        // Find the CurrencyDatabase asset
        string[] guids = AssetDatabase.FindAssets("t:CurrencyDatabase");
        if (guids.Length == 0)
        {
            Debug.LogError("[PopulateCurrencyDatabase] No CurrencyDatabase asset found!");
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        CurrencyDatabase db = AssetDatabase.LoadAssetAtPath<CurrencyDatabase>(path);
        if (db == null)
        {
            Debug.LogError($"[PopulateCurrencyDatabase] Failed to load CurrencyDatabase at {path}");
            return;
        }

        // Clear existing entries
        db.Clear();

        // All currency definitions: (ResourceType, name, emoji, iconPath, startingAmount, unlockedAtStart)
        var entries = new (ResourceType type, string name, string emoji, string iconPath, int starting, bool unlocked)[]
        {
            // ── Core Resources ──────────────────
            (ResourceType.Gold,        "Gold",         "\U0001F4B0", "Assets/Icons/Icon_30px/World/Gold - World0.png",                20, true),
            (ResourceType.Wood,        "Wood",         "\U0001F332", "Assets/Icons/Icon_30px/Currency/Wood - Currency.png",             0, true),
            (ResourceType.Food,        "Food",         "\U0001F344", "Assets/Icons/Icon_30px/Currency/Food - Currency.png",             0, false),
            (ResourceType.Stone,       "Stone",        "\U0001FAA8", "Assets/Icons/Icon_30px/Currency/Rock - Currency.png",             0, false),
            (ResourceType.Water,       "Water",        "\U0001F4A7", "Assets/Icons/Icon_30px/World/PanelAsset_007.png",                 0, false),
            (ResourceType.Clay,        "Clay",         "\U0001F9F1", "Assets/Icons/Icon_30px/Currency/Clay - Currency.png",             0, false),
            (ResourceType.Flowers,     "Flowers",      "\U0001F33B", "Assets/Icons/Icon_30px/World/Petal - World.png",                  0, false),

            // ── Gathered Resources ──────────────
            (ResourceType.Gem,         "Gem",          "\U0001F48E", "Assets/Icons/Icon_30px/Currency/Gem - Currency.png",              0, false),
            (ResourceType.Copper,      "Copper",       "\U0001FA99", "Assets/Icons/Icon_30px/Currency/Copper - Currency.png",           0, false),
            (ResourceType.Ore,         "Ore",          "\u2692",     "Assets/Icons/Icon_30px/World/ORE - World.png",                    0, false),
            (ResourceType.WhiteMarble, "White Marble", "\U0001F9CA", "Assets/Icons/Icon_30px/Currency/WhiteMarble - Currency.png",      0, false),
            (ResourceType.Moonstone,   "Moonstone",    "\U0001F319", "Assets/Icons/Icon_30px/Currency/Moonstone - Currency.png",        0, false),
            (ResourceType.Bark,        "Bark",         "\U0001FAB5", "Assets/Icons/Icon_30px/Currency/Bark - Currency.png",             0, false),
            (ResourceType.Twig,        "Twig",         "\U0001FAB9", "Assets/Icons/Icon_30px/Currency/TwighPile - Currency.png",        0, false),
            (ResourceType.Acorn,       "Acorn",        "\U0001F330", "Assets/Icons/Icon_30px/Currency/Acorn - Currency.png",            0, false),
            (ResourceType.Leaf,        "Leaf",         "\U0001F343", "Assets/Icons/Icon_30px/Currency/Leafe - Currency.png",            0, false),
            (ResourceType.Grass,       "Grass",        "\U0001F33F", "Assets/Icons/Icon_30px/World/Grass - World.png",                  0, false),
            (ResourceType.Petal,       "Petal",        "\U0001F338", "Assets/Icons/Icon_30px/World/Petal - World.png",                  0, false),
            (ResourceType.Rice,        "Rice",         "\U0001F33E", "Assets/Icons/Icon_30px/World/Rice - World.png",                   0, false),
            (ResourceType.Coconut,     "Coconut",      "\U0001F965", "Assets/Icons/Icon_25px/coconut.png",                              0, false),

            // ── Food & Animal Resources ─────────
            (ResourceType.Carrot,      "Carrot",       "\U0001F955", "Assets/Icons/Icon_30px/World/Carrot - World.png",                 0, false),
            (ResourceType.Tomato,      "Tomato",       "\U0001F345", "Assets/Icons/Icon_30px/World/Tomato  - World.png",                0, false),
            (ResourceType.Meat,        "Meat",         "\U0001F356", "Assets/Icons/Icon_30px/World/Meat - World.png",                   0, false),
            (ResourceType.Meat2,       "Meat II",      "\U0001F969", "Assets/Icons/Icon_30px/World/Meat - World (2).png",               0, false),
            (ResourceType.Meat3,       "Meat III",     "\U0001F357", "Assets/Icons/Icon_30px/Currency/Meat3 - Currency.png",            0, false),
            (ResourceType.Boar,        "Boar",         "\U0001F417", "Assets/Icons/Icon_20px/Boar Body.png",                            0, false),
            (ResourceType.Fish,        "Fish",         "\U0001F41F", "Assets/Icons/Icon_30px/Others/Fish.png",                          0, false),
            (ResourceType.Pumpkin,     "Pumpkin",      "\U0001F383", "Assets/Icons/Icon_30px/Currency/PumpKin - Currency.png",          0, false),

            // ── Special / Abstract ──────────────
            (ResourceType.GoldBag,     "Gold Bag",     "\U0001F4B0", "Assets/Icons/Icon_30px/World/Gold Bag - World.png",               0, false),
            (ResourceType.Approval,    "Approval",     "\U0001F44D", "Assets/Icons/Icon_25px/ThumbsUp - Bubble.png",                    0, false),
            (ResourceType.Heart,       "Heart",        "\U0001F49C", "Assets/Icons/Icon_25px/PinkHeart - Bubble.png",                   0, false),
        };

        int total = 0;
        int iconsAssigned = 0;

        foreach (var e in entries)
        {
            // Load the sprite
            Sprite icon = null;
            if (!string.IsNullOrEmpty(e.iconPath))
            {
                icon = AssetDatabase.LoadAssetAtPath<Sprite>(e.iconPath);
                if (icon == null)
                {
                    // Try loading as Texture2D (sprites might be sub-assets)
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(e.iconPath);
                    if (tex != null)
                    {
                        // Get first sprite sub-asset from this texture
                        var subAssets = AssetDatabase.LoadAllAssetsAtPath(e.iconPath);
                        foreach (var sub in subAssets)
                        {
                            if (sub is Sprite s)
                            {
                                icon = s;
                                break;
                            }
                        }
                    }

                    if (icon == null)
                        Debug.LogWarning($"[PopulateCurrencyDatabase] No sprite at: {e.iconPath} for {e.type}");
                }

                if (icon != null) iconsAssigned++;
            }

            db.AddCurrency(new CurrencyData
            {
                currencyName    = e.name,
                resourceType    = e.type,
                fallbackEmoji   = e.emoji,
                startingAmount  = e.starting,
                unlockedAtStart = e.unlocked,
                icon            = icon
            });
            total++;
        }

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[PopulateCurrencyDatabase] SUCCESS — Populated {total} currencies, {iconsAssigned} icons assigned. Database at: {path}");
        Debug.Log("[PopulateCurrencyDatabase] DONE — You can safely delete Assets/Scripts/Editor/PopulateCurrencyDatabase.cs");
    }
}
