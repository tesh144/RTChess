#pragma warning disable CS0414, CS0219, CS0618
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using TMPro;

/// <summary>
/// Editor tool: auto-creates and populates the GUIProKitAssets ScriptableObject
/// with references to GUI Pro Kit sprites and fonts.
///
/// Usage: Tools > ClockworkCraft > Setup GUI Pro Kit Assets
/// </summary>
public static class GUIProKitAutoAssign
{
    private const string ASSET_PATH = "Assets/Resources/GUIProKitAssets.asset";

    [MenuItem("Tools/ClockworkCraft/Setup GUI Pro Kit Assets")]
    public static void SetupAssets()
    {
        // Ensure Resources folder exists
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        // Load or create the ScriptableObject
        GUIProKitAssets assets = AssetDatabase.LoadAssetAtPath<GUIProKitAssets>(ASSET_PATH);
        if (assets == null)
        {
            assets = ScriptableObject.CreateInstance<GUIProKitAssets>();
            AssetDatabase.CreateAsset(assets, ASSET_PATH);
            Debug.Log("[GUIProKitAutoAssign] Created GUIProKitAssets at " + ASSET_PATH);
        }

        int assigned = 0;

        // ── Slider Sprites ──────────────────────────────────────────
        string sliderBase = "Assets/ThirdParty/GUIProKit/Sprite/Component/Slider/Slider_Custom/";

        Sprite frame = LoadSprite(sliderBase + "Slider01~06_White_Frame.png");
        if (frame != null) { assets.sliderFrame = frame; assigned++; }

        Sprite fill = LoadSprite(sliderBase + "Slider01~06_White_Fill.png");
        if (fill != null) { assets.sliderFill = fill; assigned++; }

        // ── Circle Frame Sprites ────────────────────────────────────
        string framesBase = "Assets/ThirdParty/GUIProKit/Sprite/Component/Frames/";

        // Try multiple possible paths for circle frame sprites
        Sprite circle116 = LoadSpriteFromPaths(new[]
        {
            framesBase + "BasicFrame_Circle_116_Common.png",
            framesBase + "Circle/BasicFrame_Circle_116_Common.png",
            framesBase + "Frame_Common/BasicFrame_Circle_116_Common_Blue.png",
        });
        if (circle116 != null) { assets.circleFrame116 = circle116; assigned++; }

        Sprite circle154 = LoadSpriteFromPaths(new[]
        {
            framesBase + "BasicFrame_Circle_154_Common.png",
            framesBase + "Circle/BasicFrame_Circle_154_Common.png",
            framesBase + "Frame_Common/BasicFrame_Circle_154_Common_Blue.png",
        });
        if (circle154 != null) { assets.circleFrame154 = circle154; assigned++; }

        // If exact paths didn't work, search by GUID patterns
        if (assets.circleFrame116 == null)
        {
            string[] guids = AssetDatabase.FindAssets("BasicFrame_Circle_116 t:Sprite");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                assets.circleFrame116 = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (assets.circleFrame116 != null) assigned++;
            }
        }

        if (assets.circleFrame154 == null)
        {
            string[] guids = AssetDatabase.FindAssets("BasicFrame_Circle_154 t:Sprite");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                assets.circleFrame154 = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (assets.circleFrame154 != null) assigned++;
            }
        }

        // ── Fonts ───────────────────────────────────────────────────
        string fontsBase = "Assets/ThirdParty/GUIProKit/Fonts/";

        // Red variant — pre-baked red, good for damage popups
        TMP_FontAsset redFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            fontsBase + "MuseoModerno-CriticalNum_Red_64_Dark SDF.asset");
        if (redFont == null)
            redFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                fontsBase + "MuseoModerno-CriticalNum_Red_64_Light SDF.asset");
        if (redFont != null) { assets.criticalNumberFont = redFont; assigned++; }

        // Transparent variant — outline-only overlay (NOT for standalone use)
        TMP_FontAsset neutralFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            fontsBase + "MuseoModerno-CriticalNum_Transpar_46 SDF.asset");
        if (neutralFont != null) { assets.neutralNumberFont = neutralFont; assigned++; }

        // Quicksand Bold — proper SDF font for HP labels and UI numbers (fully tintable)
        TMP_FontAsset quicksandBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            fontsBase + "Quicksand-Bold SDF.asset");
        if (quicksandBold != null) { assets.uiNumberFont = quicksandBold; assigned++; }

        // Rubik Medium — proper SDF font for UI labels and body text
        TMP_FontAsset rubikMedium = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            fontsBase + "Rubik-Medium SDF.asset");
        if (rubikMedium != null) { assets.uiLabelFont = rubikMedium; assigned++; }

        // Save
        EditorUtility.SetDirty(assets);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[GUIProKitAutoAssign] Done — assigned {assigned} assets to GUIProKitAssets");

        if (assets.sliderFrame == null) Debug.LogWarning("[GUIProKitAutoAssign] sliderFrame not found!");
        if (assets.sliderFill == null) Debug.LogWarning("[GUIProKitAutoAssign] sliderFill not found!");
        if (assets.criticalNumberFont == null) Debug.LogWarning("[GUIProKitAutoAssign] criticalNumberFont not found!");
        if (assets.neutralNumberFont == null) Debug.LogWarning("[GUIProKitAutoAssign] neutralNumberFont not found!");

        Selection.activeObject = assets;
    }

    private static Sprite LoadSprite(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Sprite LoadSpriteFromPaths(string[] paths)
    {
        foreach (string path in paths)
        {
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s != null) return s;
        }
        return null;
    }
}
#endif
