#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;

/// <summary>
/// Editor tool: generates the production timer + popup prefabs and their
/// texture assets (circle, ring) so designers can edit appearance in the
/// Inspector and Scene view.
///
/// Menu: Tools > ClockworkCraft > Generate Production UI Prefabs
///
/// Creates:
///   Assets/Prefabs/UI/ProductionTimer.prefab
///   Assets/Prefabs/UI/ProductionPopup.prefab
///   Assets/Prefabs/UI/Textures/Circle.png
///   Assets/Prefabs/UI/Textures/Ring.png
///
/// After generating, auto-assigns both prefabs to BuildingProductionManager
/// if it exists in the scene.
/// </summary>
public class ProductionUIPrefabGenerator : Editor
{
    private const string UI_DIR      = "Assets/Prefabs/UI";
    private const string TEX_DIR     = "Assets/Prefabs/UI/Textures";
    private const string TIMER_PATH  = "Assets/Prefabs/UI/ProductionTimer.prefab";
    private const string POPUP_PATH  = "Assets/Prefabs/UI/ProductionPopup.prefab";
    private const string CIRCLE_PATH = "Assets/Prefabs/UI/Textures/Circle.png";
    private const string RING_PATH   = "Assets/Prefabs/UI/Textures/Ring.png";

    [MenuItem("Tools/ClockworkCraft/Generate Production UI Prefabs")]
    static void Generate()
    {
        EnsureFolder("Assets/Prefabs", "UI");
        EnsureFolder(UI_DIR, "Textures");

        // 1. Generate texture assets
        Sprite circleSprite = GenerateCircleTexture();
        Sprite ringSprite   = GenerateRingTexture();

        // 2. Build prefabs
        GenerateTimerPrefab(ringSprite);
        GeneratePopupPrefab(circleSprite);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 3. Try auto-assign to scene manager
        AutoAssignToManager();

        Debug.Log("[ProductionUIPrefabGenerator] Done — created timer + popup prefabs in Assets/Prefabs/UI/");
    }

    // ─────────────────────────────────────────────────────────────────
    // Texture Generation
    // ─────────────────────────────────────────────────────────────────

    static Sprite GenerateCircleTexture()
    {
        const int size = 256;
        float center = (size - 1) * 0.5f;
        float outerRadius = center;

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[size * size];
        Color32 white = new Color32(255, 255, 255, 255);
        Color32 clear = new Color32(0, 0, 0, 0);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // 1.5px anti-aliased feather
                float alpha = Mathf.Clamp01((outerRadius - dist) / 1.5f);
                pixels[y * size + x] = alpha >= 1f ? white :
                    alpha > 0f ? new Color32(255, 255, 255, (byte)(alpha * 255)) : clear;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        // Save as PNG
        byte[] png = tex.EncodeToPNG();
        DestroyImmediate(tex);
        File.WriteAllBytes(CIRCLE_PATH, png);
        AssetDatabase.ImportAsset(CIRCLE_PATH);

        // Configure import settings for UI sprite
        ConfigureSpriteImport(CIRCLE_PATH);

        return AssetDatabase.LoadAssetAtPath<Sprite>(CIRCLE_PATH);
    }

    static Sprite GenerateRingTexture()
    {
        const int size = 256;
        float center = (size - 1) * 0.5f;
        float outerRadius = center;
        float innerRadius = center * 0.55f; // 45% ring thickness

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[size * size];
        Color32 white = new Color32(255, 255, 255, 255);
        Color32 clear = new Color32(0, 0, 0, 0);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float outerAlpha = Mathf.Clamp01((outerRadius - dist) / 1.5f);
                float innerAlpha = Mathf.Clamp01((dist - innerRadius) / 1.5f);
                float alpha = Mathf.Min(outerAlpha, innerAlpha);

                pixels[y * size + x] = alpha >= 1f ? white :
                    alpha > 0f ? new Color32(255, 255, 255, (byte)(alpha * 255)) : clear;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        byte[] png = tex.EncodeToPNG();
        DestroyImmediate(tex);
        File.WriteAllBytes(RING_PATH, png);
        AssetDatabase.ImportAsset(RING_PATH);

        ConfigureSpriteImport(RING_PATH);

        return AssetDatabase.LoadAssetAtPath<Sprite>(RING_PATH);
    }

    static void ConfigureSpriteImport(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 256;
        importer.SaveAndReimport();
    }

    // ─────────────────────────────────────────────────────────────────
    // Timer Prefab
    // ─────────────────────────────────────────────────────────────────

    static void GenerateTimerPrefab(Sprite ringSprite)
    {
        // Root — world-space canvas
        GameObject root = new GameObject("ProductionTimer");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;

        RectTransform canvasRect = root.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(100f, 100f);
        canvasRect.localScale = Vector3.one * 0.006f; // 0.6 world units / 100 canvas units

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        GraphicRaycaster gr = root.AddComponent<GraphicRaycaster>();
        gr.enabled = false;

        // ─── Background ring (full, dark) ───
        GameObject bgObj = CreateChild("Background", canvasRect,
            Vector2.zero, Vector2.one, Vector2.zero);

        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.sprite = ringSprite;
        bgImage.color = new Color(0.15f, 0.15f, 0.15f, 0.7f);
        bgImage.type = Image.Type.Filled;
        bgImage.fillMethod = Image.FillMethod.Radial360;
        bgImage.fillAmount = 1f;
        bgImage.raycastTarget = false;

        // ─── Fill ring (radial progress) ───
        GameObject fillObj = CreateChild("Fill", canvasRect,
            Vector2.zero, Vector2.one, Vector2.zero);

        Image fillImage = fillObj.AddComponent<Image>();
        fillImage.sprite = ringSprite;
        fillImage.color = new Color(0.3f, 0.85f, 0.4f, 1f);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Radial360;
        fillImage.fillOrigin = (int)Image.Origin360.Top;
        fillImage.fillClockwise = true;
        fillImage.fillAmount = 0.65f; // Preview amount so designer can see it
        fillImage.raycastTarget = false;

        // Save prefab
        PrefabUtility.SaveAsPrefabAsset(root, TIMER_PATH);
        DestroyImmediate(root);

        Debug.Log($"[ProductionUIPrefabGenerator] Created timer prefab: {TIMER_PATH}");
    }

    // ─────────────────────────────────────────────────────────────────
    // Popup Prefab
    // ─────────────────────────────────────────────────────────────────

    static void GeneratePopupPrefab(Sprite circleSprite)
    {
        // Root — world-space canvas
        GameObject root = new GameObject("ProductionPopup");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 60;

        RectTransform canvasRect = root.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(100f, 100f);
        canvasRect.localScale = Vector3.one * 0.01f; // 1.0 world units / 100 canvas units

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        GraphicRaycaster gr = root.AddComponent<GraphicRaycaster>();
        gr.enabled = false;

        // ─── Drop shadow ───
        GameObject shadowObj = CreateChild("Shadow", canvasRect,
            new Vector2(-0.05f, -0.08f), new Vector2(1.05f, 0.97f), Vector2.zero);

        Image shadowImage = shadowObj.AddComponent<Image>();
        shadowImage.sprite = circleSprite;
        shadowImage.color = new Color(0f, 0f, 0f, 0.18f);
        shadowImage.raycastTarget = false;

        // ─── Green rim ring ───
        GameObject rimObj = CreateChild("Rim", canvasRect,
            new Vector2(-0.04f, -0.04f), new Vector2(1.04f, 1.04f), Vector2.zero);

        Image rimImage = rimObj.AddComponent<Image>();
        rimImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RING_PATH);
        rimImage.color = new Color(0.3f, 0.8f, 0.4f, 0.85f);
        rimImage.raycastTarget = false;

        // ─── Clean white base circle ───
        GameObject bgObj = CreateChild("Background", canvasRect,
            Vector2.zero, Vector2.one, Vector2.zero);

        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.sprite = circleSprite;
        bgImage.color = Color.white;
        bgImage.raycastTarget = false;

        // ─── Icon (worker sprite / reward, set at runtime) ───
        GameObject iconObj = CreateChild("Icon", canvasRect,
            new Vector2(0.15f, 0.15f), new Vector2(0.85f, 0.85f), Vector2.zero);

        Image iconImage = iconObj.AddComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        iconImage.enabled = false; // Enabled at runtime when sprite is assigned

        // Save prefab
        PrefabUtility.SaveAsPrefabAsset(root, POPUP_PATH);
        DestroyImmediate(root);

        Debug.Log($"[ProductionUIPrefabGenerator] Created popup prefab: {POPUP_PATH}");
    }

    // ─────────────────────────────────────────────────────────────────
    // Auto-Assign to Scene Manager
    // ─────────────────────────────────────────────────────────────────

    static void AutoAssignToManager()
    {
        // Find BuildingProductionManager in scene (or in prefabs)
        var manager = FindFirstObjectByType<LittleCafe.BuildingProductionManager>();
        if (manager == null)
        {
            Debug.Log("[ProductionUIPrefabGenerator] No BuildingProductionManager in scene — prefabs saved but not auto-assigned. Assign timerPrefab and popupPrefab manually in Inspector.");
            return;
        }

        SerializedObject so = new SerializedObject(manager);

        var timerProp = so.FindProperty("timerPrefab");
        var popupProp = so.FindProperty("popupPrefab");

        GameObject timerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TIMER_PATH);
        GameObject popupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(POPUP_PATH);

        if (timerProp != null && timerPrefab != null && timerProp.objectReferenceValue == null)
            timerProp.objectReferenceValue = timerPrefab;

        if (popupProp != null && popupPrefab != null && popupProp.objectReferenceValue == null)
            popupProp.objectReferenceValue = popupPrefab;

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(manager);

        Debug.Log("[ProductionUIPrefabGenerator] Auto-assigned timer + popup prefabs to BuildingProductionManager.");
    }

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    static void EnsureFolder(string parent, string name)
    {
        string full = $"{parent}/{name}";
        if (!AssetDatabase.IsValidFolder(full))
            AssetDatabase.CreateFolder(parent, name);
    }

    static GameObject CreateChild(string name, RectTransform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta)
    {
        GameObject obj = new GameObject(name);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.sizeDelta = sizeDelta;
        return obj;
    }
}
