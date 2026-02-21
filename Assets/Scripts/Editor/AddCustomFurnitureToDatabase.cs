using UnityEditor;
using UnityEngine;
using LittleCafe;
using System.Collections.Generic;

/// <summary>
/// Populates the FurnitureDatabase with 6 custom furniture pieces.
/// Run via: Tools/PEPO/Add Custom Furniture to Database
/// </summary>
public class AddCustomFurnitureToDatabase : EditorWindow
{
    [MenuItem("Tools/PEPO/Add Custom Furniture to Database")]
    public static void AddFurnitureToDatabase()
    {
        // Find the FurnitureDatabase asset
        string[] dbGuids = AssetDatabase.FindAssets("FurnitureDatabase t:ScriptableObject");
        if (dbGuids.Length == 0)
        {
            EditorUtility.DisplayDialog("Error", "FurnitureDatabase not found! Please create one first.", "OK");
            return;
        }

        string dbPath = AssetDatabase.GUIDToAssetPath(dbGuids[0]);
        FurnitureDatabase database = AssetDatabase.LoadAssetAtPath<FurnitureDatabase>(dbPath);

        if (database == null)
        {
            EditorUtility.DisplayDialog("Error", "Could not load FurnitureDatabase.", "OK");
            return;
        }

        // Clear existing furniture (optional - comment out if you want to keep existing entries)
        // database.Clear();

        // Define the 6 custom furniture pieces with their properties
        var furnitureConfigs = new List<(string prefabPath, string assetName, FurnitureType type, bool isFunctional, Vector2Int gridSize, float visualScale)>
        {
            ("Assets/Prefabs/PEPO/MainFurniture/Wall Variant.prefab", "Wall", FurnitureType.Wall, true, new Vector2Int(3, 1), 1.0f),
            ("Assets/Prefabs/PEPO/MainFurniture/DiningTable Variant.prefab", "DiningTable", FurnitureType.Table, true, new Vector2Int(2, 2), 1.0f),
            ("Assets/Prefabs/PEPO/MainFurniture/Chair_1 Variant.prefab", "Chair", FurnitureType.Chair, true, new Vector2Int(1, 1), 1.0f),
            ("Assets/Prefabs/PEPO/MainFurniture/PineTree Variant.prefab", "PineTree", FurnitureType.Decoration, false, new Vector2Int(2, 2), 1.0f),
            ("Assets/Prefabs/PEPO/MainFurniture/Furnace Variant.prefab", "Furnace", FurnitureType.Cooker, true, new Vector2Int(2, 2), 1.0f),
            ("Assets/Prefabs/PEPO/MainFurniture/Sink_2 Variant.prefab", "Sink", FurnitureType.Sink, true, new Vector2Int(2, 1), 1.0f),
        };

        int added = 0;
        foreach (var config in furnitureConfigs)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(config.prefabPath);

            if (prefab == null)
            {
                Debug.LogWarning($"[AddCustomFurniture] Could not load prefab at {config.prefabPath}");
                continue;
            }

            FurnitureData data = new FurnitureData
            {
                assetName = config.assetName,
                type = config.type,
                isFunctional = config.isFunctional,
                isWalkable = false,
                gridSize = config.gridSize,
                visualScale = config.visualScale,
                prefab = prefab,
                icon = null
            };

            database.AddFurniture(data);
            added++;

            Debug.Log($"✓ Added {config.assetName} (Type: {config.type}, Grid: {config.gridSize.x}x{config.gridSize.y})");
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog(
            "Custom Furniture Added",
            $"✓ Successfully added {added} furniture pieces to FurnitureDatabase:\n\n" +
            "• Wall (3x1)\n" +
            "• DiningTable (2x2)\n" +
            "• Chair (1x1)\n" +
            "• PineTree (2x2)\n" +
            "• Furnace (2x2)\n" +
            "• Sink (2x1)",
            "OK");
    }
}
