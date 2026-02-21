using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using LittleCafe;
using ClockworkGrid;

/// <summary>
/// Editor tool to batch-generate all 114 PEPO furniture prefabs.
/// Creates prefabs with proper hierarchy, animations, and components.
///
/// USAGE:
/// 1. Tools → LittleCafe → Generate PEPO Prefabs
/// 2. Confirm in dialog
/// 3. Wait for generation (takes ~2-3 minutes for 114 assets)
/// 4. Check console for results
/// </summary>
public class PEPOPrefabGenerator : EditorWindow
{
    private FurnitureDatabase database;
    private string outputFolder = "Assets/Prefabs/PEPO";
    private bool deleteExistingPrefabs = true;
    private bool generateAnimations = true;

    private int processedCount = 0;
    private int successCount = 0;
    private int errorCount = 0;

    [MenuItem("Tools/LittleCafe/Generate PEPO Prefabs")]
    public static void ShowWindow()
    {
        GetWindow<PEPOPrefabGenerator>("PEPO Prefab Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("PEPO Prefab Generator", EditorStyles.boldLabel);
        GUILayout.Space(10);

        database = (FurnitureDatabase)EditorGUILayout.ObjectField(
            "Furniture Database", database, typeof(FurnitureDatabase), false);

        GUILayout.Space(10);
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
        deleteExistingPrefabs = EditorGUILayout.Toggle("Delete Existing Prefabs", deleteExistingPrefabs);
        generateAnimations = EditorGUILayout.Toggle("Generate Animations", generateAnimations);

        GUILayout.Space(20);

        GUI.enabled = database != null;
        if (GUILayout.Button("Generate All Prefabs", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog(
                "Generate PEPO Prefabs",
                $"This will generate {database.Count} prefabs.\n\n" +
                $"Output: {outputFolder}\n" +
                $"Delete existing: {deleteExistingPrefabs}\n\n" +
                "This may take 2-3 minutes. Continue?",
                "Generate", "Cancel"))
            {
                GenerateAllPrefabs();
            }
        }
        GUI.enabled = true;

        if (processedCount > 0)
        {
            GUILayout.Space(20);
            GUILayout.Label($"Progress: {processedCount} / {(database != null ? database.Count : 0)}", EditorStyles.boldLabel);
            GUILayout.Label($"✓ Success: {successCount}");
            GUILayout.Label($"✗ Errors: {errorCount}");
        }
    }

    private void GenerateAllPrefabs()
    {
        if (database == null)
        {
            Debug.LogError("[PEPOPrefabGenerator] No database assigned!");
            return;
        }

        processedCount = 0;
        successCount = 0;
        errorCount = 0;

        // Ensure output folder exists
        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
            AssetDatabase.Refresh();
        }

        // Delete existing prefabs if requested
        if (deleteExistingPrefabs)
        {
            DeleteExistingPrefabs();
        }

        // Generate animator controller with animations (once)
        RuntimeAnimatorController animController = null;
        if (generateAnimations)
        {
            animController = CreateAnimatorController();
        }

        // Generate each prefab
        foreach (FurnitureData data in database.AllFurniture)
        {
            bool success = GeneratePrefab(data, animController);

            processedCount++;
            if (success)
                successCount++;
            else
                errorCount++;

            // Update progress bar
            EditorUtility.DisplayProgressBar(
                "Generating PEPO Prefabs",
                $"Processing {data.assetName}... ({processedCount}/{database.Count})",
                (float)processedCount / database.Count);

            Repaint();
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[PEPOPrefabGenerator] Complete! Success: {successCount}, Errors: {errorCount}");
        EditorUtility.DisplayDialog(
            "Prefab Generation Complete",
            $"Generated {successCount} prefabs successfully.\n" +
            $"Errors: {errorCount}\n\n" +
            $"Check console for details.",
            "OK");
    }

    private bool GeneratePrefab(FurnitureData data, RuntimeAnimatorController animController)
    {
        try
        {
            // This tool is now deprecated since we use custom prefabs instead
            Debug.LogWarning($"[PEPOPrefabGenerator] This tool is deprecated - use custom prefabs instead of auto-generation");
            return false;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PEPOPrefabGenerator] ✗ Failed to generate {data.assetName}: {e.Message}");
            return false;
        }
    }

    private void AddComponents(GameObject root, FurnitureData data)
    {
        // Add GridObject component
        GridObject gridObj = root.AddComponent<GridObject>();
        gridObj.GridSize = data.gridSize;

        // Add FurnitureObject (or specialized variant)
        switch (data.type)
        {
            case FurnitureType.Chair:
                root.AddComponent<ChairObject>();
                break;
            case FurnitureType.Table:
                root.AddComponent<TableObject>();
                break;
            case FurnitureType.Wall:
                root.AddComponent<WallObject>();
                break;
            default:
                root.AddComponent<FurnitureObject>();
                break;
        }

        // Configure component properties via reflection (since they're serialized fields)
        var furniture = root.GetComponent<FurnitureObject>();
        if (furniture != null)
        {
            SerializedObject so = new SerializedObject(furniture);
            so.FindProperty("furnitureType").enumValueIndex = (int)data.type;
            so.FindProperty("isFunctional").boolValue = data.isFunctional;
            so.FindProperty("isWalkableDefault").boolValue = data.isWalkable;
            so.FindProperty("gridSize").vector2IntValue = data.gridSize;
            so.ApplyModifiedProperties();
        }
    }

    private RuntimeAnimatorController CreateAnimatorController()
    {
        // Check if ObjectAnimController already exists
        string controllerPath = "Assets/Animations/ObjectAnimController.controller";
        RuntimeAnimatorController existing = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);

        if (existing != null)
        {
            Debug.Log("[PEPOPrefabGenerator] Using existing ObjectAnimController");
            return existing;
        }

        // Create animations directory if it doesn't exist
        string animDir = "Assets/Animations";
        if (!Directory.Exists(animDir))
        {
            Directory.CreateDirectory(animDir);
        }

        // Create new AnimatorController
        UnityEditor.Animations.AnimatorController controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        // Create animation clips (placeholders - will be populated by ObjectAnimationCreator)
        CreatePlaceholderAnimation("Assets/Animations/Furniture_Appear.anim");
        CreatePlaceholderAnimation("Assets/Animations/Furniture_Remove.anim");
        CreatePlaceholderAnimation("Assets/Animations/Furniture_Interact_Weak.anim");
        CreatePlaceholderAnimation("Assets/Animations/Furniture_Interact_Strong.anim");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[PEPOPrefabGenerator] Created ObjectAnimController");
        return controller;
    }

    private void CreatePlaceholderAnimation(string path)
    {
        if (File.Exists(path)) return;

        AnimationClip clip = new AnimationClip();
        clip.legacy = false;
        AssetDatabase.CreateAsset(clip, path);
    }

    private void DeleteExistingPrefabs()
    {
        if (!Directory.Exists(outputFolder)) return;

        string[] existingPrefabs = Directory.GetFiles(outputFolder, "*.prefab");
        foreach (string prefabPath in existingPrefabs)
        {
            AssetDatabase.DeleteAsset(prefabPath);
        }

        Debug.Log($"[PEPOPrefabGenerator] Deleted {existingPrefabs.Length} existing prefabs");
    }
}
