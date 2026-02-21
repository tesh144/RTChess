using UnityEditor;
using UnityEngine;
using System.IO;

/// <summary>
/// Removes embedded Camera and Light components from PEPO furniture prefabs.
/// These shouldn't exist in furniture objects and override scene settings.
/// </summary>
public class RemoveEmbeddedCameras : EditorWindow
{
    private string prefabFolder = "Assets/Prefabs/PEPO";
    private int processedCount = 0;
    private int fixedCount = 0;

    [MenuItem("Tools/PEPO/Remove Embedded Cameras & Lights")]
    public static void ShowWindow()
    {
        GetWindow<RemoveEmbeddedCameras>("Remove Embedded Cameras");
    }

    private void OnGUI()
    {
        GUILayout.Label("Remove Embedded Cameras & Lights", EditorStyles.boldLabel);
        GUILayout.Label("Removes Camera and Light components from furniture prefabs");
        GUILayout.Space(10);

        prefabFolder = EditorGUILayout.TextField("Prefab Folder", prefabFolder);

        GUILayout.Space(20);

        if (GUILayout.Button("Remove Embedded Components", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog(
                "Remove Embedded Components",
                $"This will remove Camera and Light components from all prefabs in:\n{prefabFolder}\n\nContinue?",
                "Remove", "Cancel"))
            {
                RemoveAllEmbeddedComponents();
            }
        }

        if (processedCount > 0)
        {
            GUILayout.Space(20);
            GUILayout.Label($"Processed: {processedCount}", EditorStyles.boldLabel);
            GUILayout.Label($"✓ Fixed: {fixedCount}");
        }
    }

    private void RemoveAllEmbeddedComponents()
    {
        processedCount = 0;
        fixedCount = 0;

        if (!Directory.Exists(prefabFolder))
        {
            Debug.LogError($"[RemoveEmbeddedCameras] Prefab folder not found: {prefabFolder}");
            return;
        }

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabFolder });
        Debug.Log($"[RemoveEmbeddedCameras] Found {prefabGuids.Length} prefabs to check");

        foreach (string guid in prefabGuids)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
            bool wasFixed = RemoveEmbeddedFromPrefab(prefabPath);

            processedCount++;
            if (wasFixed) fixedCount++;

            EditorUtility.DisplayProgressBar(
                "Removing Embedded Components",
                $"Processing {Path.GetFileName(prefabPath)}... ({processedCount}/{prefabGuids.Length})",
                (float)processedCount / prefabGuids.Length);

            Repaint();
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[RemoveEmbeddedCameras] Complete! Fixed {fixedCount} / {processedCount} prefabs");
        EditorUtility.DisplayDialog(
            "Embedded Components Removed",
            $"Removed embedded components from {fixedCount} prefabs.",
            "OK");
    }

    private bool RemoveEmbeddedFromPrefab(string prefabPath)
    {
        try
        {
            GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                bool madeChanges = false;

                // Remove Camera components
                Camera[] cameras = prefabContents.GetComponentsInChildren<Camera>(true);
                foreach (Camera cam in cameras)
                {
                    Debug.Log($"[RemoveEmbeddedCameras] Removing Camera from {prefabContents.name}/{cam.gameObject.name}");
                    DestroyImmediate(cam);
                    madeChanges = true;
                }

                // Remove Light components
                Light[] lights = prefabContents.GetComponentsInChildren<Light>(true);
                foreach (Light light in lights)
                {
                    Debug.Log($"[RemoveEmbeddedCameras] Removing Light from {prefabContents.name}/{light.gameObject.name}");
                    DestroyImmediate(light);
                    madeChanges = true;
                }

                if (madeChanges)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
                    Debug.Log($"[RemoveEmbeddedCameras] ✓ Fixed: {Path.GetFileName(prefabPath)}");
                    return true;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RemoveEmbeddedCameras] ✗ Failed to process {prefabPath}: {e.Message}");
        }

        return false;
    }
}
