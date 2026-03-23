#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// One-shot tool that creates the placeholder corruption assets:
///   - A 32x32 magenta circle sprite (PNG)
///   - A CorruptionHeartIndicator prefab using that sprite
///   - A CorruptionHeart prefab with CorruptionHeart component, wired to the indicator
///
/// Run via: Tools > Corruption > Create Corruption Prefabs
///
/// After running, assign the CorruptionHeart prefab to MapGeneratorV2 > Corruption Hearts > Heart Prefab.
/// Replace the placeholder sprite with final art when ready.
/// </summary>
public static class CreateCorruptionPrefabs
{
    private const string ArtFolder        = "Assets/Art/VFX/Corruption";
    private const string PrefabFolder     = "Assets/Prefabs/Corruption";
    private const string SpritePath       = ArtFolder  + "/CorruptionHeartIndicator_Placeholder.png";
    private const string IndicatorPrefabPath = PrefabFolder + "/CorruptionHeartIndicator.prefab";
    private const string HeartPrefabPath     = PrefabFolder + "/CorruptionHeart.prefab";

    [MenuItem("Tools/Corruption/Create Corruption Prefabs")]
    public static void Create()
    {
        // ── 1. Folder setup ──────────────────────────────────────────────
        EnsureFolder("Assets/Art",      "VFX");
        EnsureFolder("Assets/Art/VFX",  "Corruption");
        EnsureFolder("Assets",          "Prefabs");
        EnsureFolder("Assets/Prefabs",  "Corruption");

        // ── 2. Generate placeholder sprite texture ────────────────────────
        Sprite indicatorSprite = GetOrCreateSprite();
        if (indicatorSprite == null)
        {
            Debug.LogError("[CreateCorruptionPrefabs] Failed to create/load sprite. Aborting.");
            return;
        }

        // ── 3. Create CorruptionHeartIndicator prefab ─────────────────────
        GameObject indicatorPrefab = CreateIndicatorPrefab(indicatorSprite);
        if (indicatorPrefab == null)
        {
            Debug.LogError("[CreateCorruptionPrefabs] Failed to save indicator prefab. Aborting.");
            return;
        }

        // ── 4. Create CorruptionHeart prefab ──────────────────────────────
        CreateHeartPrefab(indicatorPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[CreateCorruptionPrefabs] Done! Assets created:\n" +
                  $"  Sprite:    {SpritePath}\n" +
                  $"  Indicator: {IndicatorPrefabPath}\n" +
                  $"  Heart:     {HeartPrefabPath}\n\n" +
                  "Next step: assign the CorruptionHeart prefab to MapGeneratorV2 > Corruption Hearts > Heart Prefab.");

        EditorUtility.DisplayDialog(
            "Corruption Prefabs Created",
            "Assets created successfully.\n\n" +
            "Next step:\nDrag 'Assets/Prefabs/Corruption/CorruptionHeart.prefab' onto the " +
            "'Heart Prefab' field on your MapGeneratorV2 component.\n\n" +
            "Replace the placeholder sprite with final art when ready.",
            "OK");
    }

    // ── Sprite ──────────────────────────────────────────────────────────────

    static Sprite GetOrCreateSprite()
    {
        // If already exists, just reload it
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (existing != null) return existing;

        // Generate a 32x32 magenta circle PNG
        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color fill   = new Color(0.85f, 0.1f, 1f, 0.95f);
        Color clear  = new Color(0f, 0f, 0f, 0f);
        float center = (size - 1) * 0.5f;
        float radius = center - 1f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - center;
            float dy = y - center;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            // Soft anti-aliased edge
            float alpha = Mathf.Clamp01(1f - (dist - radius + 1f));
            tex.SetPixel(x, y, dist <= radius + 1f ? new Color(fill.r, fill.g, fill.b, fill.a * alpha) : clear);
        }
        tex.Apply();

        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        File.WriteAllBytes(Path.Combine(Application.dataPath, SpritePath.Replace("Assets/", "")), png);
        AssetDatabase.Refresh();

        // Configure the imported texture as a Sprite
        TextureImporter importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType      = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode       = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
    }

    // ── Indicator prefab ────────────────────────────────────────────────────

    static GameObject CreateIndicatorPrefab(Sprite sprite)
    {
        // Reuse existing prefab if present
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(IndicatorPrefabPath);
        if (existing != null)
        {
            Debug.Log($"[CreateCorruptionPrefabs] Indicator prefab already exists at {IndicatorPrefabPath} — skipping.");
            return existing;
        }

        // Build GO in memory
        var go = new GameObject("CorruptionHeartIndicator");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = sprite;
        sr.sortingOrder = 200;  // Renders above fog
        // Billboard-style: world-space, faces camera via SpriteRenderer default behaviour

        // Save as prefab asset
        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(go, IndicatorPrefabPath);
        Object.DestroyImmediate(go);
        return prefabAsset;
    }

    // ── Heart prefab ────────────────────────────────────────────────────────

    static void CreateHeartPrefab(GameObject indicatorPrefab)
    {
        // Reuse existing prefab if present
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(HeartPrefabPath);
        if (existing != null)
        {
            Debug.Log($"[CreateCorruptionPrefabs] Heart prefab already exists at {HeartPrefabPath} — skipping.");
            return;
        }

        var go = new GameObject("CorruptionHeart");
        var heart = go.AddComponent<LittleCafe.CorruptionHeart>();

        // Wire the indicator prefab into the heart via SerializedObject so the
        // private [SerializeField] field is written correctly
        var so = new SerializedObject(heart);
        SerializedProperty indicatorProp = so.FindProperty("floatingIndicatorPrefab");
        if (indicatorProp != null)
        {
            indicatorProp.objectReferenceValue = indicatorPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning("[CreateCorruptionPrefabs] Could not find 'floatingIndicatorPrefab' field on CorruptionHeart. Assign it manually in the prefab.");
        }

        PrefabUtility.SaveAsPrefabAsset(go, HeartPrefabPath);
        Object.DestroyImmediate(go);
    }

    // ── Utility ─────────────────────────────────────────────────────────────

    static void EnsureFolder(string parent, string folderName)
    {
        string full = parent + "/" + folderName;
        if (!AssetDatabase.IsValidFolder(full))
        {
            AssetDatabase.CreateFolder(parent, folderName);
            Debug.Log($"[CreateCorruptionPrefabs] Created folder: {full}");
        }
    }
}
#endif
