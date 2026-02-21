using UnityEditor;
using UnityEngine;
using ClockworkGrid;
using System.IO;

/// <summary>
/// Creates UnitStats ScriptableObjects for cafe furniture with custom names and costs.
/// These are used by DockBarManager to display cards with correct names, costs, and icons.
/// Run via: Tools/PEPO/Create Furniture Unit Stats
/// </summary>
public class CreateFurnitureUnitStats : EditorWindow
{
    [MenuItem("Tools/PEPO/Create Furniture Unit Stats")]
    public static void CreateFurnitureStats()
    {
        // Create folder for furniture stats
        string statsFolder = "Assets/ScriptableObjects/Furniture Stats";
        if (!AssetDatabase.IsValidFolder(statsFolder))
        {
            string parentFolder = "Assets/ScriptableObjects";
            if (!AssetDatabase.IsValidFolder(parentFolder))
            {
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            }
            AssetDatabase.CreateFolder(parentFolder, "Furniture Stats");
        }

        // Define 6 furniture pieces with their stats
        var furnitureConfigs = new (string name, UnitType type, int cost, string prefabPath)[]
        {
            ("Wall", UnitType.Wall, 5, "Assets/Prefabs/PEPO/MainFurniture/Wall Variant.prefab"),
            ("Table", UnitType.Table, 8, "Assets/Prefabs/PEPO/MainFurniture/DiningTable Variant.prefab"),
            ("Chair", UnitType.Chair, 3, "Assets/Prefabs/PEPO/MainFurniture/Chair_1 Variant.prefab"),
            ("Pine Tree", UnitType.Ninja, 0, "Assets/Prefabs/PEPO/MainFurniture/PineTree Variant.prefab"),  // Using Ninja as deco type
            ("Cooking Station", UnitType.CookingStation, 12, "Assets/Prefabs/PEPO/MainFurniture/Furnace Variant.prefab"),
            ("Sink", UnitType.WashingStation, 6, "Assets/Prefabs/PEPO/MainFurniture/Sink_2 Variant.prefab"),
        };

        int created = 0;

        foreach (var config in furnitureConfigs)
        {
            // Create UnitStats ScriptableObject
            UnitStats stats = ScriptableObject.CreateInstance<UnitStats>();

            stats.unitType = config.type;
            stats.unitName = config.name;  // ✓ This is what displays on the card!
            stats.resourceCost = config.cost;
            stats.rarity = Rarity.Common;

            // Load and assign prefab
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(config.prefabPath);
            if (prefab != null)
            {
                stats.unitPrefab = prefab;
            }

            // Try to find icon sprite
            Sprite icon = FindIconSprite(config.name);
            if (icon != null)
            {
                stats.iconSprite = icon;
            }

            // Save the asset
            string assetPath = Path.Combine(statsFolder, $"{config.name} Stats.asset");
            AssetDatabase.CreateAsset(stats, assetPath);
            created++;

            Debug.Log($"✓ Created {config.name} UnitStats with cost={config.cost}");
        }

        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog(
            "Furniture Unit Stats Created",
            $"✓ Created {created} UnitStats for furniture\n\n" +
            "Now assign them to DockBarManager:\n" +
            "1. Find DockBarManager prefab in scene\n" +
            "2. Find the script or look for AddUnitToDock calls\n" +
            "3. Or modify CafeSceneSetupV2 to use these stats\n\n" +
            "Stats are in: Assets/ScriptableObjects/Furniture Stats/",
            "OK");
    }

    private static Sprite FindIconSprite(string name)
    {
        string[] iconPaths = new[]
        {
            "Assets/Icons",
            "Assets/Sprites",
            "Assets/UI/Icons",
            "Assets/Images"
        };

        foreach (string iconPath in iconPaths)
        {
            if (AssetDatabase.IsValidFolder(iconPath))
            {
                string[] spriteGuids = AssetDatabase.FindAssets($"{name} t:Sprite", new[] { iconPath });
                if (spriteGuids.Length > 0)
                {
                    string spritePath = AssetDatabase.GUIDToAssetPath(spriteGuids[0]);
                    return AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                }
            }
        }

        return null;
    }
}
