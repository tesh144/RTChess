#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using TMPro;

/// <summary>
/// Holds references to GUI Pro Kit assets (sprites, fonts) that runtime code needs.
/// Lives in Assets/Resources/ so it can be loaded via Resources.Load at runtime.
/// An editor script auto-creates and auto-assigns the references.
/// </summary>
[CreateAssetMenu(fileName = "GUIProKitAssets", menuName = "ClockworkCraft/GUI Pro Kit Assets")]
public class GUIProKitAssets : ScriptableObject
{
    private static GUIProKitAssets _instance;

    public static GUIProKitAssets Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<GUIProKitAssets>("GUIProKitAssets");
            return _instance;
        }
    }

    [Header("Slider/Progress Bar Sprites")]
    [Tooltip("Background frame sprite for horizontal progress bars.")]
    public Sprite sliderFrame;

    [Tooltip("Fill sprite for horizontal progress bars.")]
    public Sprite sliderFill;

    [Header("Fonts")]
    [Tooltip("MuseoModerno Red SDF — pre-baked red bitmap font for damage popups.")]
    public TMP_FontAsset criticalNumberFont;

    [Tooltip("MuseoModerno Transparent SDF — outline-only overlay font (NOT for standalone use).")]
    public TMP_FontAsset neutralNumberFont;

    [Tooltip("Quicksand Bold SDF — proper SDF text font for HP labels and UI numbers. Fully tintable via tmp.color.")]
    public TMP_FontAsset uiNumberFont;

    [Tooltip("Rubik Medium SDF — proper SDF text font for UI labels and body text.")]
    public TMP_FontAsset uiLabelFont;

    [Header("Circle Frame Sprites")]
    [Tooltip("Circle frame sprite for dock bar card backgrounds.")]
    public Sprite circleFrame116;

    [Tooltip("Larger circle frame for highlighted/selected cards.")]
    public Sprite circleFrame154;
}
