using UnityEditor;
using UnityEngine;
using LittleCafe;
using System.IO;

/// <summary>
/// Creates EquipmentCardData ScriptableObjects for cafe furniture and assigns them to CafeDockBar.
/// This bridges the FurnitureDatabase with the cafe equipment system.
/// Run via: Tools/PEPO/Create Equipment Cards from Furniture
/// </summary>
public class CreateEquipmentCardsFromFurniture : EditorWindow
{
    [MenuItem("Tools/PEPO/Create Equipment Cards from Furniture")]
    public static void CreateEquipmentCards()
    {
        // Find or create folder for equipment cards
        string equipmentCardsFolder = "Assets/ScriptableObjects/Equipment Cards";
        if (!AssetDatabase.IsValidFolder(equipmentCardsFolder))
        {
            string parentFolder = "Assets/ScriptableObjects";
            if (!AssetDatabase.IsValidFolder(parentFolder))
            {
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            }
            AssetDatabase.CreateFolder(parentFolder, "Equipment Cards");
        }

        // Define 6 furniture pieces with their equipment type mappings
        var equipmentConfigs = new (string name, EquipmentType type, Color color, string prefabPath)[]
        {
            ("Wall", EquipmentType.Wall, EquipmentData.GetColor(EquipmentType.Wall),
                "Assets/Prefabs/PEPO/MainFurniture/Wall Variant.prefab"),
            ("Table", EquipmentType.Table, EquipmentData.GetColor(EquipmentType.Table),
                "Assets/Prefabs/PEPO/MainFurniture/DiningTable Variant.prefab"),
            ("Chair", EquipmentType.Chair, EquipmentData.GetColor(EquipmentType.Chair),
                "Assets/Prefabs/PEPO/MainFurniture/Chair_1 Variant.prefab"),
            ("Cooking Station", EquipmentType.CookingStation, EquipmentData.GetColor(EquipmentType.CookingStation),
                "Assets/Prefabs/PEPO/MainFurniture/Furnace Variant.prefab"),
            ("Washing Station", EquipmentType.WashingStation, EquipmentData.GetColor(EquipmentType.WashingStation),
                "Assets/Prefabs/PEPO/MainFurniture/Sink_2 Variant.prefab"),
            ("Decoration", EquipmentType.PlateRack, EquipmentData.GetColor(EquipmentType.PlateRack),
                "Assets/Prefabs/PEPO/MainFurniture/PineTree Variant.prefab"),
        };

        int created = 0;

        foreach (var config in equipmentConfigs)
        {
            // Create the ScriptableObject
            EquipmentCardData card = ScriptableObject.CreateInstance<EquipmentCardData>();

            card.equipmentType = config.type;
            card.displayName = config.name;
            card.cardColor = config.color;

            // Load prefab
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(config.prefabPath);
            if (prefab != null)
            {
                card.equipmentPrefab = prefab;
            }

            // Find icon sprite if available (try common locations)
            Sprite icon = FindIconSprite(config.name, config.type);
            if (icon != null)
            {
                card.iconSprite = icon;
            }

            // Save the asset
            string assetPath = Path.Combine(equipmentCardsFolder, $"{config.name} Card.asset");
            AssetDatabase.CreateAsset(card, assetPath);
            created++;

            Debug.Log($"✓ Created {config.name} equipment card at {assetPath}");
        }

        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog(
            "Equipment Cards Created",
            $"✓ Created {created} equipment cards\n\n" +
            "Now assign them to CafeDockBar:\n" +
            "1. Find CafeDockBar in the scene\n" +
            "2. Open Inspector\n" +
            "3. Expand 'Available Cards'\n" +
            "4. Add size = 6\n" +
            "5. Drag the created cards from Assets/ScriptableObjects/Equipment Cards",
            "OK");
    }

    private static Sprite FindIconSprite(string name, EquipmentType type)
    {
        // Try to find icon in Assets/Icons or Assets/Sprites
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
