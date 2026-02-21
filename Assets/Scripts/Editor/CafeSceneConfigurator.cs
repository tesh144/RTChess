using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using LittleCafe;
using ClockworkGrid;

/// <summary>
/// One-click configurator for existing cafe scenes.
/// Adds furniture system components and verifies setup.
/// </summary>
public class CafeSceneConfigurator
{
    [MenuItem("Tools/LittleCafe/Configure Current Scene for Furniture")]
    public static void ConfigureCurrentScene()
    {
        Scene currentScene = SceneManager.GetActiveScene();

        if (!EditorUtility.DisplayDialog(
            "Configure Scene for Furniture System",
            $"This will configure the current scene:\n\n" +
            $"Scene: {currentScene.name}\n\n" +
            $"Actions:\n" +
            $"✓ Add/Update LayoutLoader\n" +
            $"✓ Verify GridManager\n" +
            $"✓ Verify DockBarManager\n" +
            $"✓ Assign FurnitureDatabase\n" +
            $"✓ Clean up old components\n\n" +
            "Continue?",
            "Yes, Configure",
            "Cancel"))
        {
            return;
        }

        try
        {
            ConfigureScene();

            EditorUtility.DisplayDialog(
                "Scene Configured!",
                $"✅ {currentScene.name} is ready!\n\n" +
                "Next steps:\n" +
                "1. Save the scene (Ctrl+S)\n" +
                "2. Press Play to test\n" +
                "3. Draw furniture from dock bar\n" +
                "4. Place on grid\n\n" +
                "Everything should work now!",
                "OK");

            EditorSceneManager.MarkSceneDirty(currentScene);
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("Error", $"Configuration failed:\n\n{e.Message}", "OK");
            Debug.LogError($"[CafeSceneConfigurator] Error: {e}");
        }
    }

    private static void ConfigureScene()
    {
        Debug.Log("========================================");
        Debug.Log("[CafeSceneConfigurator] Starting scene configuration...");
        Debug.Log("========================================");

        // Load FurnitureDatabase
        FurnitureDatabase database = AssetDatabase.LoadAssetAtPath<FurnitureDatabase>(
            "Assets/Scripts/Data/FurnitureDatabase.asset");

        if (database == null)
        {
            throw new System.Exception("FurnitureDatabase not found! Run: Tools → LittleCafe → Auto-Setup Cafe Builder");
        }

        Debug.Log($"[CafeSceneConfigurator] ✓ Found FurnitureDatabase with {database.Count} entries");

        // Step 1: Verify GridManager
        VerifyGridManager();

        // Step 2: Verify DockBarManager
        VerifyDockBarManager();

        // Step 3: Add/Update LayoutLoader
        AddOrUpdateLayoutLoader(database);

        // Step 4: Clean up old components
        CleanupOldComponents();

        Debug.Log("========================================");
        Debug.Log("[CafeSceneConfigurator] ✅ Configuration complete!");
        Debug.Log("========================================");
    }

    private static void VerifyGridManager()
    {
        GridManager gridManager = Object.FindObjectOfType<GridManager>();

        if (gridManager == null)
        {
            Debug.LogWarning("[CafeSceneConfigurator] ⚠️ No GridManager found in scene!");
            Debug.LogWarning("  → You may need to add one manually");
        }
        else
        {
            Debug.Log($"[CafeSceneConfigurator] ✓ GridManager found: {gridManager.name}");
        }
    }

    private static void VerifyDockBarManager()
    {
        DockBarManager dockBar = Object.FindObjectOfType<DockBarManager>();

        if (dockBar == null)
        {
            Debug.LogWarning("[CafeSceneConfigurator] ⚠️ No DockBarManager found in scene!");
            Debug.LogWarning("  → You may need to add one manually");
        }
        else
        {
            Debug.Log($"[CafeSceneConfigurator] ✓ DockBarManager found: {dockBar.name}");

            // Check if it has required UI elements
            SerializedObject so = new SerializedObject(dockBar);
            var dockIconsContainer = so.FindProperty("dockIconsContainer");

            if (dockIconsContainer.objectReferenceValue == null)
            {
                Debug.LogWarning("[CafeSceneConfigurator] ⚠️ DockBarManager missing dockIconsContainer!");
                Debug.LogWarning("  → This is the Transform where furniture cards appear");
                Debug.LogWarning("  → Assign it manually in the Inspector");
            }
            else
            {
                Debug.Log($"[CafeSceneConfigurator] ✓ DockIconsContainer assigned");
            }
        }
    }

    private static void AddOrUpdateLayoutLoader(FurnitureDatabase database)
    {
        LayoutLoader loader = Object.FindObjectOfType<LayoutLoader>();

        if (loader == null)
        {
            // Create new LayoutLoader GameObject
            GameObject loaderObj = new GameObject("LayoutLoader");
            loader = loaderObj.AddComponent<LayoutLoader>();

            Debug.Log("[CafeSceneConfigurator] ✓ Created LayoutLoader GameObject");
        }
        else
        {
            Debug.Log($"[CafeSceneConfigurator] ✓ Found existing LayoutLoader: {loader.name}");
        }

        // Assign FurnitureDatabase using SerializedObject (private field)
        SerializedObject so = new SerializedObject(loader);
        SerializedProperty dbProp = so.FindProperty("furnitureDatabase");

        if (dbProp != null)
        {
            dbProp.objectReferenceValue = database;
            so.ApplyModifiedProperties();
            Debug.Log("[CafeSceneConfigurator] ✓ Assigned FurnitureDatabase to LayoutLoader");
        }
        else
        {
            Debug.LogWarning("[CafeSceneConfigurator] ⚠️ Could not find furnitureDatabase property");
        }
    }

    private static void CleanupOldComponents()
    {
        Debug.Log("[CafeSceneConfigurator] Checking for old components to clean up...");

        // Check for WaveManager (combat system - not needed for cafe)
        var waveManager = Object.FindObjectOfType<WaveManager>();
        if (waveManager != null)
        {
            Debug.Log($"[CafeSceneConfigurator] ℹ️ Found WaveManager - keeping it (you can delete manually if not needed)");
        }

        // Check for old CafeEquipment objects
        var oldEquipment = Object.FindObjectsOfType<CafeEquipment>();
        if (oldEquipment.Length > 0)
        {
            Debug.Log($"[CafeSceneConfigurator] ℹ️ Found {oldEquipment.Length} old CafeEquipment objects");
            Debug.Log("  → These use the OLD system - consider deleting them");
            Debug.Log("  → The new system uses FurnitureObject instead");
        }

        Debug.Log("[CafeSceneConfigurator] ✓ Cleanup check complete");
    }

    [MenuItem("Tools/LittleCafe/Configure Current Scene for Furniture", true)]
    public static bool ValidateConfigureScene()
    {
        // Only enable if a scene is loaded
        return SceneManager.GetActiveScene().IsValid();
    }
}
