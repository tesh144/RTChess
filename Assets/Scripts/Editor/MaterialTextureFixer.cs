#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Editor tool that fixes extracted FBX materials with missing texture references.
/// Scans all .mat files under a folder, finds ones with empty _MainTex, and
/// auto-assigns textures by matching material name to nearby texture files.
///
/// Also swaps all materials to Custom/UnlitSaturation shader.
///
/// Usage: ClockworkCraft → Fix Material Textures
/// </summary>
public class MaterialTextureFixer : EditorWindow
{
    private string targetFolder = "Assets/PEPO";
    private bool swapShader = true;
    private string targetShaderName = "Custom/UnlitSaturation";
    private Vector2 scrollPos;
    private List<string> log = new List<string>();

    [MenuItem("ClockworkCraft/Fix Material Textures")]
    static void ShowWindow()
    {
        GetWindow<MaterialTextureFixer>("Material Texture Fixer");
    }

    void OnGUI()
    {
        GUILayout.Label("Material Texture Fixer", EditorStyles.boldLabel);
        GUILayout.Space(5);

        targetFolder = EditorGUILayout.TextField("Target Folder", targetFolder);
        swapShader = EditorGUILayout.Toggle("Swap to UnlitSaturation", swapShader);

        GUILayout.Space(10);

        if (GUILayout.Button("Scan (Dry Run)", GUILayout.Height(30)))
        {
            log.Clear();
            RunFix(dryRun: true);
        }

        if (GUILayout.Button("Fix All", GUILayout.Height(30)))
        {
            log.Clear();
            RunFix(dryRun: false);
        }

        GUILayout.Space(10);
        GUILayout.Label($"Log ({log.Count} entries):");
        scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Height(300));
        foreach (var line in log)
            GUILayout.Label(line);
        GUILayout.EndScrollView();
    }

    void RunFix(bool dryRun)
    {
        // Build texture lookup: name (lowercase, no extension) → asset path
        var textureLookup = new Dictionary<string, string>();
        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { targetFolder });
        foreach (string guid in textureGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            // Prefer textures closer to root (shorter path) if duplicates exist
            if (!textureLookup.ContainsKey(name))
                textureLookup[name] = path;
        }
        log.Add($"Found {textureLookup.Count} textures in {targetFolder}");

        // Find target shader
        Shader targetShader = swapShader ? Shader.Find(targetShaderName) : null;
        if (swapShader && targetShader == null)
        {
            log.Add($"ERROR: Shader '{targetShaderName}' not found!");
            return;
        }

        // Process all materials
        string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { targetFolder });
        int fixedTextures = 0;
        int swappedShaders = 0;
        int alreadyOk = 0;

        foreach (string guid in matGuids)
        {
            string matPath = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) continue;

            bool changed = false;

            // Fix missing _MainTex
            if (mat.HasProperty("_MainTex"))
            {
                Texture currentTex = mat.GetTexture("_MainTex");
                if (currentTex == null)
                {
                    // Try to find a matching texture by material name
                    Texture2D foundTex = FindMatchingTexture(mat.name, matPath, textureLookup);
                    if (foundTex != null)
                    {
                        if (!dryRun)
                            mat.SetTexture("_MainTex", foundTex);
                        log.Add($"FIX: {mat.name} ← {AssetDatabase.GetAssetPath(foundTex)}");
                        fixedTextures++;
                        changed = true;
                    }
                    else
                    {
                        log.Add($"MISS: {mat.name} — no matching texture found");
                    }
                }
                else
                {
                    alreadyOk++;
                }
            }

            // Swap shader
            if (swapShader && targetShader != null && mat.shader != targetShader)
            {
                if (!dryRun)
                    mat.shader = targetShader;
                swappedShaders++;
                changed = true;
            }

            if (changed && !dryRun)
                EditorUtility.SetDirty(mat);
        }

        if (!dryRun)
            AssetDatabase.SaveAssets();

        log.Add("---");
        log.Add($"Total materials: {matGuids.Length}");
        log.Add($"Textures fixed: {fixedTextures}");
        log.Add($"Shaders swapped: {swappedShaders}");
        log.Add($"Already OK: {alreadyOk}");
        log.Add(dryRun ? "(DRY RUN — no changes made)" : "DONE — changes saved");
    }

    // Strip underscores, hyphens, spaces for fuzzy comparison
    static string Normalize(string s) => s.ToLowerInvariant().Replace("_", "").Replace("-", "").Replace(" ", "");

    Texture2D FindMatchingTexture(string matName, string matPath, Dictionary<string, string> textureLookup)
    {
        string matNameLower = matName.ToLowerInvariant();
        string matNameNorm = Normalize(matName);

        // Strategy 1: Exact match on material name
        if (textureLookup.TryGetValue(matNameLower, out string exactPath))
            return AssetDatabase.LoadAssetAtPath<Texture2D>(exactPath);

        // Strategy 2: Match ignoring underscores/hyphens (WheatL04 matches Wheat_L04)
        foreach (var kvp in textureLookup)
        {
            if (Normalize(kvp.Key) == matNameNorm)
                return AssetDatabase.LoadAssetAtPath<Texture2D>(kvp.Value);
        }

        // Strategy 3: Strip common suffixes and try again
        string[] suffixes = { "_base_color", "_basecolor", "_diffuse", "_albedo", "_color", "_texture", "_2d_view", "_texture_2d_view", "_texutre" };
        foreach (string suffix in suffixes)
        {
            if (matNameLower.EndsWith(suffix))
            {
                string stripped = matNameLower.Substring(0, matNameLower.Length - suffix.Length);
                if (textureLookup.TryGetValue(stripped, out string strippedPath))
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(strippedPath);
                // Also try normalized
                string strippedNorm = Normalize(stripped);
                foreach (var kvp in textureLookup)
                {
                    if (Normalize(kvp.Key) == strippedNorm)
                        return AssetDatabase.LoadAssetAtPath<Texture2D>(kvp.Value);
                }
            }
        }

        // Strategy 4: Search nearby — go up TWO levels from Materials/ subfolder
        string matDir = Path.GetDirectoryName(matPath);
        string parentDir = Path.GetDirectoryName(matDir);
        string grandparentDir = parentDir != null ? Path.GetDirectoryName(parentDir) : null;

        string[] searchDirs = new[] { parentDir, grandparentDir }.Where(d => d != null).ToArray();
        foreach (string searchDir in searchDirs)
        {
            string[] nearbyTextures = AssetDatabase.FindAssets("t:Texture2D", new[] { searchDir });
            foreach (string texGuid in nearbyTextures)
            {
                string texPath = AssetDatabase.GUIDToAssetPath(texGuid);
                string texName = Path.GetFileNameWithoutExtension(texPath).ToLowerInvariant();
                string texNameNorm = Normalize(texName);

                // Normalized fuzzy match
                if (matNameNorm.Contains(texNameNorm) || texNameNorm.Contains(matNameNorm))
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

                // Also try with suffixes stripped
                foreach (string suffix in suffixes)
                {
                    if (matNameLower.EndsWith(suffix))
                    {
                        string strippedNorm = Normalize(matNameLower.Substring(0, matNameLower.Length - suffix.Length));
                        if (texNameNorm.Contains(strippedNorm) || strippedNorm.Contains(texNameNorm))
                            return AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                    }
                }
            }
        }

        return null;
    }
}
