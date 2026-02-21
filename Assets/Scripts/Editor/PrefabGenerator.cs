using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Automates the creation of properly structured animated object prefabs
/// Place this script in an "Editor" folder in your Unity project
/// </summary>
public class PrefabGenerator : EditorWindow
{
    // ===== CONFIGURATION =====
    [Header("Source Assets")]
    [Tooltip("Folder containing your FBX/object assets")]
    private string sourceFolder = "Assets/Models/";

    [Header("Materials")]
    [Tooltip("Path to your unlit material")]
    private string unlitMaterialPath = "Assets/Materials/UnlitMaterial.mat";

    [Header("Animation")]
    [Tooltip("Path to your Animator Controller")]
    private string animatorControllerPath = "Assets/Animations/ObjectAnimatorController.controller";

    [Header("Output")]
    [Tooltip("Where to save the generated prefabs")]
    private string outputFolder = "Assets/Prefabs/Generated/";

    [Header("Options")]
    [Tooltip("Delete existing prefabs in output folder before generating")]
    private bool cleanOutputFolder = true;

    private Material unlitMaterial;
    private RuntimeAnimatorController animatorController;
    private List<GameObject> objectsToProcess = new List<GameObject>();

    [MenuItem("Tools/Prefab Generator")]
    public static void ShowWindow()
    {
        GetWindow<PrefabGenerator>("Prefab Generator");
    }

    void OnGUI()
    {
        GUILayout.Label("Animated Object Prefab Generator", EditorStyles.boldLabel);
        GUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "This tool creates properly structured prefabs with:\n" +
            "• ObjectPrefabHolder (root)\n" +
            "• CharacterHold (animated)\n" +
            "• CharacterRe:Zero (re-centers object)\n" +
            "• Your object (complete asset)",
            MessageType.Info);

        GUILayout.Space(10);

        // Configuration
        GUILayout.Label("Configuration", EditorStyles.boldLabel);
        sourceFolder = EditorGUILayout.TextField("Source Folder", sourceFolder);
        unlitMaterialPath = EditorGUILayout.TextField("Unlit Material", unlitMaterialPath);
        animatorControllerPath = EditorGUILayout.TextField("Animator Controller", animatorControllerPath);
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);

        GUILayout.Space(10);
        cleanOutputFolder = EditorGUILayout.Toggle("Clean Output Folder", cleanOutputFolder);

        GUILayout.Space(20);

        // Buttons
        if (GUILayout.Button("Load Assets", GUILayout.Height(30)))
        {
            LoadAssets();
        }

        GUILayout.Space(10);

        // Show what will be processed
        if (objectsToProcess.Count > 0)
        {
            GUILayout.Label($"Found {objectsToProcess.Count} objects to process:", EditorStyles.boldLabel);
            foreach (var obj in objectsToProcess)
            {
                EditorGUILayout.LabelField("  • " + obj.name);
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Generate Prefabs", GUILayout.Height(40)))
            {
                GeneratePrefabs();
            }
        }

        GUILayout.Space(10);

        if (GUILayout.Button("Clean Up - Remove All Shadows from Scene"))
        {
            RemoveAllShadows();
        }
    }

    void LoadAssets()
    {
        objectsToProcess.Clear();

        // Load material
        unlitMaterial = AssetDatabase.LoadAssetAtPath<Material>(unlitMaterialPath);
        if (unlitMaterial == null)
        {
            EditorUtility.DisplayDialog("Error", $"Could not find unlit material at: {unlitMaterialPath}", "OK");
            return;
        }

        // Load animator controller
        animatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(animatorControllerPath);
        if (animatorController == null)
        {
            EditorUtility.DisplayDialog("Error", $"Could not find animator controller at: {animatorControllerPath}", "OK");
            return;
        }

        // Find all prefabs/FBX in source folder
        string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { sourceFolder });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject obj = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (obj != null)
            {
                objectsToProcess.Add(obj);
            }
        }

        Debug.Log($"Loaded {objectsToProcess.Count} objects from {sourceFolder}");
    }

    void GeneratePrefabs()
    {
        if (objectsToProcess.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "No objects to process. Click 'Load Assets' first.", "OK");
            return;
        }

        // Create output folder if it doesn't exist
        if (!AssetDatabase.IsValidFolder(outputFolder))
        {
            string parentFolder = Path.GetDirectoryName(outputFolder.TrimEnd('/'));
            string folderName = Path.GetFileName(outputFolder.TrimEnd('/'));
            AssetDatabase.CreateFolder(parentFolder, folderName);
        }

        // Clean output folder if requested
        if (cleanOutputFolder)
        {
            CleanOutputFolder();
        }

        int count = 0;
        foreach (var sourceObject in objectsToProcess)
        {
            EditorUtility.DisplayProgressBar("Generating Prefabs", $"Processing {sourceObject.name}...", (float)count / objectsToProcess.Count);

            CreatePrefabForObject(sourceObject);
            count++;
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Success", $"Generated {count} prefabs in {outputFolder}", "OK");
    }

    void CreatePrefabForObject(GameObject sourceObject)
    {
        // Create the structure in scene first
        GameObject root = new GameObject(sourceObject.name + "_Prefab");

        // Add Animator to root
        Animator animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = animatorController;

        // Create CharacterHold
        GameObject characterHold = new GameObject("CharacterHold");
        characterHold.transform.SetParent(root.transform);
        characterHold.transform.localPosition = Vector3.zero;
        characterHold.transform.localRotation = Quaternion.identity;
        characterHold.transform.localScale = Vector3.one;

        // Create CharacterRe:Zero
        GameObject characterReZero = new GameObject("CharacterRe:Zero");
        characterReZero.transform.SetParent(characterHold.transform);
        characterReZero.transform.localPosition = Vector3.zero;
        characterReZero.transform.localRotation = Quaternion.identity;
        characterReZero.transform.localScale = Vector3.one;

        // Instantiate the source object as a child of CharacterRe:Zero
        GameObject objectInstance = PrefabUtility.InstantiatePrefab(sourceObject) as GameObject;
        objectInstance.transform.SetParent(characterReZero.transform);
        objectInstance.transform.localPosition = Vector3.zero;
        objectInstance.transform.localRotation = Quaternion.identity;
        objectInstance.transform.localScale = Vector3.one;

        // Apply unlit material to all renderers in the object
        ApplyUnlitMaterial(objectInstance);

        // Remove any shadow components (in case they exist)
        RemoveShadowComponents(root);

        // Save as prefab
        string prefabPath = Path.Combine(outputFolder, sourceObject.name + "_Prefab.prefab");
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

        // Clean up scene
        DestroyImmediate(root);

        Debug.Log($"Created prefab: {prefabPath}");
    }

    void ApplyUnlitMaterial(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            Material[] materials = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = unlitMaterial;
            }
            renderer.sharedMaterials = materials;
        }

        Debug.Log($"Applied unlit material to {renderers.Length} renderers in {obj.name}");
    }

    void RemoveShadowComponents(GameObject root)
    {
        // Remove any GameObjects with "Shadow" in the name
        Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);
        List<GameObject> toDelete = new List<GameObject>();

        foreach (Transform t in allTransforms)
        {
            if (t.name.ToLower().Contains("shadow"))
            {
                toDelete.Add(t.gameObject);
            }
        }

        foreach (GameObject obj in toDelete)
        {
            DestroyImmediate(obj);
            Debug.Log($"Removed shadow object: {obj.name}");
        }
    }

    void RemoveAllShadows()
    {
        // Find all objects in the scene
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        int removedCount = 0;

        foreach (GameObject obj in allObjects)
        {
            if (obj.name.ToLower().Contains("shadow"))
            {
                DestroyImmediate(obj);
                removedCount++;
            }
        }

        Debug.Log($"Removed {removedCount} shadow objects from scene");
        EditorUtility.DisplayDialog("Clean Up Complete", $"Removed {removedCount} shadow objects from scene", "OK");
    }

    void CleanOutputFolder()
    {
        string[] existingPrefabs = AssetDatabase.FindAssets("t:Prefab", new[] { outputFolder });

        foreach (string guid in existingPrefabs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AssetDatabase.DeleteAsset(path);
        }

        Debug.Log($"Cleaned output folder: {outputFolder}");
    }
}
