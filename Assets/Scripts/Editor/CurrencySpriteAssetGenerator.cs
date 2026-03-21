#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using ClockworkCraft;

/// <summary>
/// Editor tool that generates a TMP_SpriteAsset from CurrencyDatabase icons.
/// This allows using inline sprites in TextMeshPro: <sprite name="Gold"> 20
///
/// Run via: Tools > ClockworkCraft > Generate Currency Sprite Asset
///
/// The generated asset is saved to Assets/Fonts/CurrencySpriteAsset.asset
/// and should be assigned to the CurrencyHolder prefab's TMP spriteAsset field.
/// </summary>
public class CurrencySpriteAssetGenerator : Editor
{
    private const string OUTPUT_PATH = "Assets/Fonts/CurrencySpriteAsset.asset";
    private const int SPRITE_SIZE = 64; // Uniform size in atlas
    private const int ATLAS_PADDING = 2;

    [MenuItem("Tools/ClockworkCraft/Generate Currency Sprite Asset")]
    public static void Generate()
    {
        // 1. Find the CurrencyDatabase
        string[] guids = AssetDatabase.FindAssets("t:CurrencyDatabase");
        if (guids.Length == 0)
        {
            Debug.LogError("[CurrencySpriteAssetGenerator] No CurrencyDatabase found in project.");
            return;
        }

        CurrencyDatabase db = AssetDatabase.LoadAssetAtPath<CurrencyDatabase>(
            AssetDatabase.GUIDToAssetPath(guids[0]));

        if (db == null || db.Count == 0)
        {
            Debug.LogError("[CurrencySpriteAssetGenerator] CurrencyDatabase is empty.");
            return;
        }

        // 2. Collect all currencies that have icons
        var entries = new List<(string name, Sprite icon)>();
        foreach (var currency in db.AllCurrencies)
        {
            if (currency.icon != null)
            {
                // Use the ResourceType enum name as the sprite name (e.g., "Gold", "Wood")
                entries.Add((currency.resourceType.ToString(), currency.icon));
            }
        }

        if (entries.Count == 0)
        {
            Debug.LogError("[CurrencySpriteAssetGenerator] No currencies have icons assigned. Run 'Auto-Assign Icon Sprites' on CurrencyDatabase first.");
            return;
        }

        Debug.Log($"[CurrencySpriteAssetGenerator] Packing {entries.Count} currency icons into sprite asset...");

        // 3. Calculate atlas dimensions (square-ish grid)
        int cols = Mathf.CeilToInt(Mathf.Sqrt(entries.Count));
        int rows = Mathf.CeilToInt((float)entries.Count / cols);
        int atlasWidth = cols * (SPRITE_SIZE + ATLAS_PADDING);
        int atlasHeight = rows * (SPRITE_SIZE + ATLAS_PADDING);

        // Round up to power of 2 for GPU friendliness
        atlasWidth = Mathf.NextPowerOfTwo(atlasWidth);
        atlasHeight = Mathf.NextPowerOfTwo(atlasHeight);

        // 4. Create atlas texture
        Texture2D atlas = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, false);
        atlas.name = "CurrencyAtlas";
        atlas.filterMode = FilterMode.Bilinear;

        // Fill with transparent
        Color32[] clearPixels = new Color32[atlasWidth * atlasHeight];
        for (int i = 0; i < clearPixels.Length; i++)
            clearPixels[i] = new Color32(0, 0, 0, 0);
        atlas.SetPixels32(clearPixels);

        // 5. Blit each icon into the atlas and build sprite/glyph lists
        var spriteGlyphs = new List<TMP_SpriteGlyph>();
        var spriteChars = new List<TMP_SpriteCharacter>();

        for (int i = 0; i < entries.Count; i++)
        {
            int col = i % cols;
            int row = i / cols;
            int x = col * (SPRITE_SIZE + ATLAS_PADDING);
            int y = (rows - 1 - row) * (SPRITE_SIZE + ATLAS_PADDING); // Top-to-bottom

            // Read the source sprite's pixels
            Texture2D readableTex = MakeReadable(entries[i].icon);
            if (readableTex == null) continue;

            // Scale to SPRITE_SIZE x SPRITE_SIZE
            Texture2D scaled = ScaleTexture(readableTex, SPRITE_SIZE, SPRITE_SIZE);
            Color32[] pixels = scaled.GetPixels32();

            // Blit into atlas
            for (int py = 0; py < SPRITE_SIZE; py++)
            {
                for (int px = 0; px < SPRITE_SIZE; px++)
                {
                    int atlasX = x + px;
                    int atlasY = y + py;
                    if (atlasX < atlasWidth && atlasY < atlasHeight)
                        atlas.SetPixel(atlasX, atlasY, pixels[py * SPRITE_SIZE + px]);
                }
            }

            // Create glyph
            var glyph = new TMP_SpriteGlyph();
            glyph.index = (uint)i;
            glyph.metrics = new UnityEngine.TextCore.GlyphMetrics(
                SPRITE_SIZE, SPRITE_SIZE, 0, SPRITE_SIZE * 0.8f, SPRITE_SIZE);
            glyph.glyphRect = new UnityEngine.TextCore.GlyphRect(x, y, SPRITE_SIZE, SPRITE_SIZE);
            glyph.scale = 1.0f;
            spriteGlyphs.Add(glyph);

            // Create character
            var character = new TMP_SpriteCharacter();
            character.name = entries[i].name;
            character.glyphIndex = (uint)i;
            character.scale = 1.0f;
            spriteChars.Add(character);

            if (readableTex != entries[i].icon.texture)
                DestroyImmediate(readableTex);
            DestroyImmediate(scaled);
        }

        atlas.Apply(false, false);

        // 6. Ensure output directory exists
        string dir = System.IO.Path.GetDirectoryName(OUTPUT_PATH);
        if (!AssetDatabase.IsValidFolder(dir))
        {
            string[] parts = dir.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        // 7. Save atlas texture as asset
        string texPath = OUTPUT_PATH.Replace(".asset", "_Atlas.png");
        System.IO.File.WriteAllBytes(
            System.IO.Path.Combine(Application.dataPath, "..", texPath),
            atlas.EncodeToPNG());
        AssetDatabase.ImportAsset(texPath);

        // Set texture import settings for TMP sprite atlas (must be Default, NOT Sprite type)
        TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = Mathf.Max(atlasWidth, atlasHeight);
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        Texture2D savedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

        // 8. Create or update the TMP_SpriteAsset
        TMP_SpriteAsset spriteAsset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(OUTPUT_PATH);
        if (spriteAsset == null)
        {
            spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            AssetDatabase.CreateAsset(spriteAsset, OUTPUT_PATH);
        }

        spriteAsset.spriteSheet = savedTex;

        // Create or update the material (TMP needs a material with "TextMeshPro/Sprite" shader)
        string matPath = OUTPUT_PATH.Replace(".asset", "_Material.mat");
        Material spriteMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (spriteMat == null)
        {
            Shader spriteShader = Shader.Find("TextMeshPro/Sprite");
            if (spriteShader == null)
                spriteShader = Shader.Find("TextMesh Pro/Sprite"); // alternate name
            if (spriteShader != null)
            {
                spriteMat = new Material(spriteShader);
                spriteMat.name = "CurrencySpriteAsset_Material";
                AssetDatabase.CreateAsset(spriteMat, matPath);
            }
            else
            {
                Debug.LogWarning("[CurrencySpriteAssetGenerator] Could not find TMP sprite shader. Sprites may not render.");
            }
        }

        if (spriteMat != null)
        {
            spriteMat.mainTexture = savedTex;
            EditorUtility.SetDirty(spriteMat);
        }

        // Assign material to sprite asset via SerializedObject
        {
            var matSO = new SerializedObject(spriteAsset);
            matSO.Update();
            var materialProp = matSO.FindProperty("material");
            if (materialProp != null)
            {
                materialProp.objectReferenceValue = spriteMat;
                matSO.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // spriteGlyphTable and spriteCharacterTable are read-only properties in TMP.
        // Use SerializedObject to write them via the backing fields.
        var so = new SerializedObject(spriteAsset);
        so.Update();

        // Set glyph table
        var glyphTableProp = so.FindProperty("m_SpriteGlyphTable");
        glyphTableProp.ClearArray();
        for (int i = 0; i < spriteGlyphs.Count; i++)
        {
            glyphTableProp.InsertArrayElementAtIndex(i);
            var elem = glyphTableProp.GetArrayElementAtIndex(i);

            elem.FindPropertyRelative("m_Index").intValue = (int)spriteGlyphs[i].index;
            elem.FindPropertyRelative("m_Scale").floatValue = spriteGlyphs[i].scale;

            var metricsP = elem.FindPropertyRelative("m_Metrics");
            metricsP.FindPropertyRelative("m_Width").floatValue = spriteGlyphs[i].metrics.width;
            metricsP.FindPropertyRelative("m_Height").floatValue = spriteGlyphs[i].metrics.height;
            metricsP.FindPropertyRelative("m_HorizontalBearingX").floatValue = spriteGlyphs[i].metrics.horizontalBearingX;
            metricsP.FindPropertyRelative("m_HorizontalBearingY").floatValue = spriteGlyphs[i].metrics.horizontalBearingY;
            metricsP.FindPropertyRelative("m_HorizontalAdvance").floatValue = spriteGlyphs[i].metrics.horizontalAdvance;

            var rectP = elem.FindPropertyRelative("m_GlyphRect");
            rectP.FindPropertyRelative("m_X").intValue = spriteGlyphs[i].glyphRect.x;
            rectP.FindPropertyRelative("m_Y").intValue = spriteGlyphs[i].glyphRect.y;
            rectP.FindPropertyRelative("m_Width").intValue = spriteGlyphs[i].glyphRect.width;
            rectP.FindPropertyRelative("m_Height").intValue = spriteGlyphs[i].glyphRect.height;
        }

        // Set character table
        var charTableProp = so.FindProperty("m_SpriteCharacterTable");
        charTableProp.ClearArray();
        for (int i = 0; i < spriteChars.Count; i++)
        {
            charTableProp.InsertArrayElementAtIndex(i);
            var elem = charTableProp.GetArrayElementAtIndex(i);

            elem.FindPropertyRelative("m_Name").stringValue = spriteChars[i].name;
            elem.FindPropertyRelative("m_GlyphIndex").intValue = (int)spriteChars[i].glyphIndex;
            elem.FindPropertyRelative("m_Scale").floatValue = spriteChars[i].scale;
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        // Update lookup tables
        spriteAsset.UpdateLookupTables();

        EditorUtility.SetDirty(spriteAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 9. Auto-assign to CurrencyHolder prefab's TMP component
        string[] prefabGuids = AssetDatabase.FindAssets("CurrencyHolder t:Prefab");
        foreach (var guid in prefabGuids)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) continue;

            var tmp = prefab.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = prefab.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null && tmp.spriteAsset != spriteAsset)
            {
                // Use SerializedObject to modify the prefab's TMP sprite asset reference
                var tmpSO = new SerializedObject(tmp);
                tmpSO.Update();
                var spriteAssetProp = tmpSO.FindProperty("m_spriteAsset");
                if (spriteAssetProp != null)
                {
                    spriteAssetProp.objectReferenceValue = spriteAsset;
                    tmpSO.ApplyModifiedProperties();
                    EditorUtility.SetDirty(prefab);
                    Debug.Log($"[CurrencySpriteAssetGenerator] Auto-assigned sprite asset to {prefabPath}");
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[CurrencySpriteAssetGenerator] DONE — Created sprite asset at {OUTPUT_PATH} with {entries.Count} sprites. " +
                  $"Atlas: {atlasWidth}x{atlasHeight}.");
    }

    /// <summary>
    /// Makes a texture readable by creating a temporary RenderTexture copy.
    /// Required because source icon textures are usually marked as non-readable.
    /// </summary>
    private static Texture2D MakeReadable(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null) return null;

        Texture2D srcTex = sprite.texture;
        Rect spriteRect = sprite.textureRect;

        RenderTexture rt = RenderTexture.GetTemporary(srcTex.width, srcTex.height, 0);
        Graphics.Blit(srcTex, rt);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D readable = new Texture2D((int)spriteRect.width, (int)spriteRect.height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(spriteRect.x, spriteRect.y, spriteRect.width, spriteRect.height), 0, 0);
        readable.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        return readable;
    }

    /// <summary>
    /// Bilinear-scale a texture to the target size.
    /// </summary>
    private static Texture2D ScaleTexture(Texture2D source, int targetWidth, int targetHeight)
    {
        RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0);
        rt.filterMode = FilterMode.Bilinear;
        Graphics.Blit(source, rt);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
        result.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        return result;
    }
}
#endif
