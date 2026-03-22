#pragma warning disable CS0414, CS0219, CS0618
using UnityEditor;
using UnityEngine;

/// <summary>
/// Removes unwanted components from furniture prefabs that interfere with gameplay.
/// Removes: Camera, Light, AudioListener, and other non-essential components.
/// Run via: Tools/PEPO/Cleanup Furniture Prefabs
/// </summary>
public class CleanupFurniturePrefabs : EditorWindow
{
    [MenuItem("Tools/PEPO/Cleanup Furniture Prefabs")]
    public static void CleanupPrefabs()
    {
        var furniturePrefabs = new[]
        {
            "Assets/Prefabs/PEPO/MainFurniture/Wall Variant.prefab",
            "Assets/Prefabs/PEPO/MainFurniture/DiningTable Variant.prefab",
            "Assets/Prefabs/PEPO/MainFurniture/Chair_1 Variant.prefab",
            "Assets/Prefabs/PEPO/MainFurniture/PineTree Variant.prefab",
            "Assets/Prefabs/PEPO/MainFurniture/Furnace Variant.prefab",
            "Assets/Prefabs/PEPO/MainFurniture/Sink_2 Variant.prefab",
        };

        int cleaned = 0;
        foreach (string prefabPath in furniturePrefabs)
        {
            if (CleanupPrefab(prefabPath))
            {
                cleaned++;
            }
        }

        EditorUtility.DisplayDialog(
            "Cleanup Complete",
            $"✓ Cleaned {cleaned} furniture prefabs\n\n" +
            "Removed:\n" +
            "• Cameras\n" +
            "• Lights\n" +
            "• AudioListeners\n" +
            "• Other conflicting components",
            "OK");
    }

    private static bool CleanupPrefab(string prefabPath)
    {
        var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
        bool modified = false;

        try
        {
            // Remove Camera components
            var cameras = prefab.GetComponentsInChildren<Camera>();
            foreach (var cam in cameras)
            {
                DestroyImmediate(cam);
                modified = true;
            }

            // Remove Light components (except if needed for visual feedback)
            var lights = prefab.GetComponentsInChildren<Light>();
            foreach (var light in lights)
            {
                DestroyImmediate(light);
                modified = true;
            }

            // Remove AudioListener
            var audioListeners = prefab.GetComponentsInChildren<AudioListener>();
            foreach (var al in audioListeners)
            {
                DestroyImmediate(al);
                modified = true;
            }

            // Remove Animator if it's not the FurnitureObject's animator
            var animators = prefab.GetComponentsInChildren<Animator>();
            foreach (var animator in animators)
            {
                // Check if this is a stray animator (not an AnimatorHolder)
                if (animator.gameObject.name != "AnimatorHolder")
                {
                    // Only remove if it's not the root animator
                    if (animator.gameObject != prefab)
                    {
                        DestroyImmediate(animator.gameObject);
                        modified = true;
                    }
                }
            }

            // Ensure FurnitureObject component exists
            var furnitureObj = prefab.GetComponent<LittleCafe.FurnitureObject>();
            if (furnitureObj == null)
            {
                prefab.AddComponent<LittleCafe.FurnitureObject>();
                modified = true;
            }

            if (modified)
            {
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
                Debug.Log($"✓ Cleaned {System.IO.Path.GetFileNameWithoutExtension(prefabPath)}");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefab);
        }

        return modified;
    }
}
