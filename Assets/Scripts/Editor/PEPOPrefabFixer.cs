#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using UnityEditor;
using System.IO;
using ClockworkGrid;

/// <summary>
/// Fixes furniture prefabs by adding animations and converting materials to Unlit shader.
/// </summary>
public class PEPOPrefabFixer : EditorWindow
{
    private string prefabFolder = "Assets/Prefabs/PEPO";
    private bool fixAnimations = true;
    private bool fixMaterials = true;
    private int processedCount = 0;
    private int fixedCount = 0;

    [MenuItem("Tools/PEPO/Fix Furniture Prefabs")]
    public static void ShowWindow()
    {
        GetWindow<PEPOPrefabFixer>("PEPO Prefab Fixer");
    }

    private void OnGUI()
    {
        GUILayout.Label("PEPO Furniture Prefab Fixer", EditorStyles.boldLabel);
        GUILayout.Label("Fixes animations and materials for all furniture prefabs");
        GUILayout.Space(10);

        prefabFolder = EditorGUILayout.TextField("Prefab Folder", prefabFolder);
        fixAnimations = EditorGUILayout.Toggle("Fix Animations", fixAnimations);
        fixMaterials = EditorGUILayout.Toggle("Convert to Unlit Materials", fixMaterials);

        GUILayout.Space(20);

        if (GUILayout.Button("Fix All Prefabs", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog(
                "Fix PEPO Prefabs",
                $"This will fix all prefabs in:\n{prefabFolder}\n\n" +
                $"Fix animations: {fixAnimations}\n" +
                $"Convert materials: {fixMaterials}\n\n" +
                "Continue?",
                "Fix", "Cancel"))
            {
                FixAllPrefabs();
            }
        }

        if (processedCount > 0)
        {
            GUILayout.Space(20);
            GUILayout.Label($"Processed: {processedCount}", EditorStyles.boldLabel);
            GUILayout.Label($"✓ Fixed: {fixedCount}");
        }
    }

    private void FixAllPrefabs()
    {
        processedCount = 0;
        fixedCount = 0;

        if (!Directory.Exists(prefabFolder))
        {
            Debug.LogError($"[PEPOPrefabFixer] Prefab folder not found: {prefabFolder}");
            return;
        }

        // Get ObjectAnimController if fixing animations
        RuntimeAnimatorController animController = null;
        if (fixAnimations)
        {
            animController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/ObjectAnimController.controller");
            if (animController == null)
            {
                Debug.LogError("[PEPOPrefabFixer] ObjectAnimController.controller not found at Assets/Animations/ObjectAnimController.controller");
                Debug.LogError("Please run PEPO Prefab Generator with 'Generate Animations' checked first.");
                return;
            }
        }

        // Find all prefabs
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabFolder });
        Debug.Log($"[PEPOPrefabFixer] Found {prefabGuids.Length} prefabs to fix");

        foreach (string guid in prefabGuids)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
            bool wasFixed = FixPrefab(prefabPath, animController);

            processedCount++;
            if (wasFixed) fixedCount++;

            EditorUtility.DisplayProgressBar(
                "Fixing PEPO Prefabs",
                $"Processing {Path.GetFileName(prefabPath)}... ({processedCount}/{prefabGuids.Length})",
                (float)processedCount / prefabGuids.Length);

            Repaint();
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[PEPOPrefabFixer] Complete! Fixed {fixedCount} / {processedCount} prefabs");
        EditorUtility.DisplayDialog(
            "Prefab Fixing Complete",
            $"Fixed {fixedCount} prefabs successfully.\n" +
            $"Total processed: {processedCount}",
            "OK");
    }

    private bool FixPrefab(string prefabPath, RuntimeAnimatorController animController)
    {
        try
        {
            // Load prefab for editing
            GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                bool madeChanges = false;

                // Fix animations
                if (fixAnimations)
                {
                    madeChanges |= FixPrefabAnimations(prefabContents, animController);
                }

                // Fix materials
                if (fixMaterials)
                {
                    madeChanges |= FixPrefabMaterials(prefabContents);
                }

                if (madeChanges)
                {
                    // Save changes
                    PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
                    Debug.Log($"[PEPOPrefabFixer] ✓ Fixed: {Path.GetFileName(prefabPath)}");
                    return true;
                }
            }
            finally
            {
                // Always unload to prevent asset locking
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PEPOPrefabFixer] ✗ Failed to fix {prefabPath}: {e.Message}");
        }

        return false;
    }

    private bool FixPrefabAnimations(GameObject prefabRoot, RuntimeAnimatorController controller)
    {
        // Find AnimatorHolder child
        Transform animatorHolder = prefabRoot.transform.Find("AnimatorHolder");
        if (animatorHolder == null)
        {
            Debug.LogWarning($"[PEPOPrefabFixer] No AnimatorHolder found in {prefabRoot.name}");
            return false;
        }

        // Get or add Animator component
        Animator animator = animatorHolder.GetComponent<Animator>();
        if (animator == null)
        {
            animator = animatorHolder.gameObject.AddComponent<Animator>();
            Debug.Log($"[PEPOPrefabFixer] Added Animator to {prefabRoot.name}");
        }

        // Assign controller if not already assigned
        if (animator.runtimeAnimatorController != controller)
        {
            animator.runtimeAnimatorController = controller;
            Debug.Log($"[PEPOPrefabFixer] Assigned ObjectAnimController to {prefabRoot.name}");
            return true;
        }

        return false;
    }

    private bool FixPrefabMaterials(GameObject prefabRoot)
    {
        bool madeChanges = false;

        // Find all MeshRenderers in hierarchy
        MeshRenderer[] renderers = prefabRoot.GetComponentsInChildren<MeshRenderer>(true);

        foreach (MeshRenderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            bool rendererChanged = false;

            for (int i = 0; i < materials.Length; i++)
            {
                Material mat = materials[i];
                if (mat == null) continue;

                // Detect if this is a shadow material
                bool isShadowMaterial = mat.name.ToLower().Contains("shadow") ||
                                       renderer.gameObject.name.ToLower().Contains("shadow");

                if (isShadowMaterial)
                {
                    // Use custom UnlitTransparentColor shader (properly supports alpha!)
                    Shader shadowShader = Shader.Find("Unlit/TransparentColor");

                    if (shadowShader == null)
                    {
                        Debug.LogError("[PEPOPrefabFixer] Unlit/TransparentColor shader not found! Make sure Assets/Shaders/UnlitTransparentColor.shader exists.");
                        continue;
                    }

                    mat.shader = shadowShader;

                    // Set color to 40% opaque dark gray (visible shadow)
                    mat.color = new Color(0.1f, 0.1f, 0.1f, 0.4f);

                    Debug.Log($"[PEPOPrefabFixer] Shadow material: {mat.name} → Unlit/TransparentColor (40% dark gray)");
                    rendererChanged = true;
                    madeChanges = true;
                }
                else
                {
                    // Skip non-shadow materials if already using Unlit shader
                    if (mat.shader.name.StartsWith("Unlit/"))
                        continue;

                    // Regular materials use Unlit/Texture
                    Texture mainTex = mat.mainTexture;
                    Color color = mat.HasProperty("_Color") ? mat.color : Color.white;

                    Shader unlitShader = Shader.Find("Unlit/Texture");
                    if (unlitShader == null)
                    {
                        Debug.LogError("[PEPOPrefabFixer] Unlit/Texture shader not found!");
                        continue;
                    }

                    mat.shader = unlitShader;

                    // Restore properties
                    if (mainTex != null)
                    {
                        mat.mainTexture = mainTex;
                    }
                    mat.color = color;

                    rendererChanged = true;
                    madeChanges = true;
                }
            }

            if (rendererChanged)
            {
                // Reassign materials array to ensure changes are saved
                renderer.sharedMaterials = materials;
            }
        }

        return madeChanges;
    }
}
