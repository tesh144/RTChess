#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor tool to export ground tile prefabs and their dependencies as a .unitypackage.
/// Menu: Tools > ClockworkCraft > Export Tile Package
/// </summary>
public class ExportTilePackage
{
    [MenuItem("Tools/ClockworkCraft/Export Tile Package")]
    public static void Export()
    {
        // All tile-related root assets — ExportPackage with IncludeDependencies
        // will automatically pull in materials, shaders, textures, and scripts.
        string[] assetPaths = new string[]
        {
            "Assets/Prefabs/Tile 1.prefab",
            "Assets/Prefabs/Tile 2.prefab",
            "Assets/Prefabs/GridTilePrefab.prefab",
            "Assets/VerticalFogUnlit.shader",
            "Assets/Materials/Materials/Tile1.mat",
            "Assets/Materials/Materials/Tile2.mat",
            // Include the TileFog script (runtime component added to tiles)
            "Assets/Scripts/Core/TileFog.cs",
            // Include GridVisualizer (creates tiles at runtime)
            "Assets/Scripts/Core/GridVisualizer.cs",
        };

        // Validate all paths exist
        int missing = 0;
        foreach (string path in assetPaths)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) == null)
            {
                Debug.LogWarning($"[ExportTilePackage] Asset not found: {path}");
                missing++;
            }
        }

        if (missing > 0)
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Export Tile Package",
                $"{missing} asset(s) could not be found. Export anyway with the assets that exist?",
                "Export Anyway", "Cancel");

            if (!proceed) return;
        }

        // Let user choose save location
        string savePath = EditorUtility.SaveFilePanel(
            "Export Tile Package",
            "",
            "ClockworkCraft_Tiles",
            "unitypackage");

        if (string.IsNullOrEmpty(savePath))
        {
            Debug.Log("[ExportTilePackage] Export cancelled.");
            return;
        }

        AssetDatabase.ExportPackage(
            assetPaths,
            savePath,
            ExportPackageOptions.IncludeDependencies | ExportPackageOptions.Recurse);

        Debug.Log($"[ExportTilePackage] Exported tile package to: {savePath}");
        EditorUtility.DisplayDialog(
            "Export Complete",
            $"Tile package exported to:\n{savePath}",
            "OK");
    }
}
