using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Linq;
using LittleCafe;
using ClockworkGrid;

/// <summary>
/// One-click automation for Cafe Builder setup.
/// Replaces manual Steps 1, 4, and 5 from SETUP_INSTRUCTIONS.md.
///
/// USAGE:
/// Tools → LittleCafe → Auto-Setup Cafe Builder
/// </summary>
public class CafeBuilderAutoSetup
{
    private const string DATABASE_PATH = "Assets/Scripts/Data/FurnitureDatabase.asset";
    private const string PEPO_FOLDER = "Assets/PEPO";
    private const string DATA_FOLDER = "Assets/Scripts/Data";

    [MenuItem("Tools/LittleCafe/Auto-Setup Cafe Builder")]
    public static void AutoSetup()
    {
        if (!EditorUtility.DisplayDialog(
            "Cafe Builder Auto-Setup",
            "This will automatically:\n\n" +
            "✓ Create FurnitureDatabase\n" +
            "✓ Scan and populate 114 PEPO assets\n" +
            "✓ Auto-assign furniture types (Table/Chair/Wall/Decoration)\n" +
            "✓ Configure all properties\n\n" +
            "This replaces manual Steps 1 and 4.\n\n" +
            "Continue?",
            "Yes, Auto-Setup",
            "Cancel"))
        {
            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar("Auto-Setup", "Starting...", 0f);

            // Step 1: Create database
            FurnitureDatabase database = CreateOrLoadDatabase();

            // Step 2: Populate with PEPO assets
            PopulateDatabase(database);

            // Step 3: Auto-configure types
            AutoConfigureTypes(database);

            // Step 4: Save and refresh
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.ClearProgressBar();

            // Success dialog
            EditorUtility.DisplayDialog(
                "Auto-Setup Complete!",
                $"✓ Created FurnitureDatabase\n" +
                $"✓ Added {database.Count} furniture entries\n" +
                $"✓ Auto-configured types\n\n" +
                "NEXT STEPS:\n" +
                "1. Tools → Create Object Animations\n" +
                "2. Tools → LittleCafe → Generate PEPO Prefabs\n" +
                "3. Test in Play mode!\n\n" +
                "Database location: {DATABASE_PATH}",
                "OK");

            // Ping the database to highlight it
            EditorGUIUtility.PingObject(database);
            Selection.activeObject = database;
        }
        catch (System.Exception e)
        {
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Error", $"Auto-setup failed:\n\n{e.Message}", "OK");
            Debug.LogError($"[CafeBuilderAutoSetup] Error: {e}");
        }
    }

    private static FurnitureDatabase CreateOrLoadDatabase()
    {
        EditorUtility.DisplayProgressBar("Auto-Setup", "Creating database...", 0.1f);

        // Ensure Data folder exists
        if (!AssetDatabase.IsValidFolder(DATA_FOLDER))
        {
            string parentFolder = Path.GetDirectoryName(DATA_FOLDER);
            string folderName = Path.GetFileName(DATA_FOLDER);
            AssetDatabase.CreateFolder(parentFolder, folderName);
            Debug.Log($"[AutoSetup] Created folder: {DATA_FOLDER}");
        }

        // Check if database already exists
        FurnitureDatabase existing = AssetDatabase.LoadAssetAtPath<FurnitureDatabase>(DATABASE_PATH);
        if (existing != null)
        {
            Debug.Log("[AutoSetup] Using existing FurnitureDatabase");
            return existing;
        }

        // Create new database
        FurnitureDatabase database = ScriptableObject.CreateInstance<FurnitureDatabase>();
        AssetDatabase.CreateAsset(database, DATABASE_PATH);
        Debug.Log($"[AutoSetup] ✓ Created FurnitureDatabase at {DATABASE_PATH}");

        return database;
    }

    private static void PopulateDatabase(FurnitureDatabase database)
    {
        EditorUtility.DisplayProgressBar("Auto-Setup", "Scanning PEPO folder...", 0.3f);

        // Clear existing data
        database.Clear();

        // Find all FBX files in PEPO folder
        string[] fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { PEPO_FOLDER });

        int count = 0;
        foreach (string guid in fbxGuids)
        {
            string fbxPath = AssetDatabase.GUIDToAssetPath(guid);

            // Only include .fbx files
            if (fbxPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            {
                string fileName = System.IO.Path.GetFileNameWithoutExtension(fbxPath);
                FurnitureData data = new FurnitureData
                {
                    assetName = fileName,
                    type = FurnitureType.Decoration,
                    isFunctional = false,
                    isWalkable = false,
                    gridSize = new Vector2Int(1, 1),
                    visualScale = 1.0f,
                    prefab = null,
                    icon = null
                };
                database.AddFurniture(data);
                count++;
            }
        }

        Debug.Log($"[AutoSetup] ✓ Populated with {count} PEPO assets");
    }

    private static void AutoConfigureTypes(FurnitureDatabase database)
    {
        EditorUtility.DisplayProgressBar("Auto-Setup", "Auto-configuring types...", 0.6f);

        int tables = 0, chairs = 0, walls = 0, decorations = 0;

        foreach (FurnitureData data in database.AllFurniture)
        {
            string nameLower = data.assetName.ToLower();

            // Auto-assign based on name patterns
            if (nameLower.Contains("table"))
            {
                data.type = FurnitureType.Table;
                data.isFunctional = true;
                data.isWalkable = false;
                tables++;
            }
            else if (nameLower.Contains("chair") || nameLower.Contains("seat") || nameLower.Contains("stool"))
            {
                data.type = FurnitureType.Chair;
                data.isFunctional = true;
                data.isWalkable = false; // Dynamic at runtime
                chairs++;
            }
            else if (nameLower.Contains("wall") || nameLower.Contains("fence") || nameLower.Contains("barrier"))
            {
                data.type = FurnitureType.Wall;
                data.isFunctional = true;
                data.isWalkable = false;
                walls++;
            }
            else
            {
                // Everything else is decoration
                data.type = FurnitureType.Decoration;
                data.isFunctional = false;
                data.isWalkable = false; // Safe default: non-walkable
                decorations++;
            }

            // Default grid size (can be adjusted later)
            data.gridSize = Vector2Int.one;
            data.visualScale = 1.0f;
        }

        Debug.Log($"[AutoSetup] ✓ Auto-configured types:");
        Debug.Log($"  - Tables: {tables}");
        Debug.Log($"  - Chairs: {chairs}");
        Debug.Log($"  - Walls: {walls}");
        Debug.Log($"  - Decorations: {decorations}");
    }

    // Quick fix: Repopulate existing database
    [MenuItem("Tools/LittleCafe/Repopulate Database (Fix)")]
    public static void RepopulateDatabase()
    {
        FurnitureDatabase db = AssetDatabase.LoadAssetAtPath<FurnitureDatabase>(DATABASE_PATH);

        if (db == null)
        {
            EditorUtility.DisplayDialog("Error", "Database not found! Run Auto-Setup first.", "OK");
            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar("Repopulating", "Scanning PEPO folder...", 0.5f);

            PopulateDatabase(db);
            AutoConfigureTypes(db);

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.ClearProgressBar();

            EditorUtility.DisplayDialog("Success", $"✓ Populated database with {db.Count} entries!", "OK");
            EditorGUIUtility.PingObject(db);
        }
        catch (System.Exception e)
        {
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Error", $"Failed to populate:\n\n{e.Message}", "OK");
        }
    }

    // Optional: Validate that PEPO folder exists
    [MenuItem("Tools/LittleCafe/Auto-Setup Cafe Builder", true)]
    public static bool ValidateAutoSetup()
    {
        return AssetDatabase.IsValidFolder(PEPO_FOLDER);
    }
}
