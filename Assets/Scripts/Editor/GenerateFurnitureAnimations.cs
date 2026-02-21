using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates juicy furniture animations with keyframes.
/// Creates Appear, Remove, Interact_Weak, and Interact_Strong animations.
/// </summary>
public class GenerateFurnitureAnimations : EditorWindow
{
    [MenuItem("Tools/PEPO/Generate Furniture Animations")]
    public static void ShowWindow()
    {
        GetWindow<GenerateFurnitureAnimations>("Generate Animations");
    }

    private void OnGUI()
    {
        GUILayout.Label("Furniture Animation Generator", EditorStyles.boldLabel);
        GUILayout.Label("Creates keyframe animations for furniture placement and interaction");
        GUILayout.Space(20);

        if (GUILayout.Button("Generate All Animations", GUILayout.Height(40)))
        {
            GenerateAllAnimations();
        }
    }

    private void GenerateAllAnimations()
    {
        Debug.Log("[GenerateFurnitureAnimations] Starting animation generation...");

        GenerateAppearAnimation();
        GenerateRemoveAnimation();
        GenerateInteractWeakAnimation();
        GenerateInteractStrongAnimation();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[GenerateFurnitureAnimations] ✓ All animations generated!");
        EditorUtility.DisplayDialog(
            "Animations Generated",
            "✓ Furniture_Appear\n" +
            "✓ Furniture_Remove\n" +
            "✓ Furniture_Interact_Weak\n" +
            "✓ Furniture_Interact_Strong",
            "OK");
    }

    private void GenerateAppearAnimation()
    {
        AnimationClip clip = new AnimationClip();
        clip.name = "Furniture_Appear";
        float duration = 0.8f;
        clip.frameRate = 60f;

        // --- POSITION: Falls from 4 units high with realistic drop physics ---
        AnimationCurve posY = AnimationCurve.EaseInOut(0, 4f, duration * 0.6f, 0f); // Longer fall from higher
        // Landing wobble - bounces after impact
        posY.AddKey(new Keyframe(duration * 0.65f, -0.15f)); // Squish down into ground
        posY.AddKey(new Keyframe(duration * 0.75f, 0.1f)); // Bounce up
        posY.AddKey(new Keyframe(duration * 0.85f, -0.03f)); // Small bounce down
        posY.AddKey(new Keyframe(duration, 0f)); // Settle

        var posBinding = EditorCurveBinding.FloatCurve("", typeof(Transform), "localPosition.y");
        AnimationUtility.SetEditorCurve(clip, posBinding, posY);

        // --- SCALE: Starts at 0.3x, grows to 1x during fall, heavy squish on landing ---
        AnimationCurve scaleXY = AnimationCurve.Linear(0, 0.3f, duration * 0.5f, 1f); // Fade in + grow
        scaleXY.AddKey(new Keyframe(duration * 0.65f, 0.85f)); // Heavy squish on landing
        scaleXY.AddKey(new Keyframe(duration * 0.75f, 1.15f)); // Big overshoot/bounce
        scaleXY.AddKey(new Keyframe(duration * 0.82f, 0.98f)); // Slight compression
        scaleXY.AddKey(new Keyframe(duration, 1f)); // Settle to natural size

        var scaleXBinding = EditorCurveBinding.FloatCurve("", typeof(Transform), "localScale.x");
        var scaleYBinding = EditorCurveBinding.FloatCurve("", typeof(Transform), "localScale.y");
        var scaleZBinding = EditorCurveBinding.FloatCurve("", typeof(Transform), "localScale.z");
        AnimationUtility.SetEditorCurve(clip, scaleXBinding, scaleXY);
        AnimationUtility.SetEditorCurve(clip, scaleYBinding, scaleXY);
        AnimationUtility.SetEditorCurve(clip, scaleZBinding, scaleXY);

        // --- ROTATION: Spin during fall, stops on landing ---
        AnimationCurve rotZ = AnimationCurve.Linear(0, 0, duration * 0.6f, 360f); // Full rotation during fall
        rotZ.AddKey(new Keyframe(duration, 360f)); // Stays at final rotation

        var rotZBinding = EditorCurveBinding.FloatCurve("", typeof(Transform), "localEulerAngles.z");
        AnimationUtility.SetEditorCurve(clip, rotZBinding, rotZ);

        AssetDatabase.CreateAsset(clip, "Assets/Animations/Furniture_Appear.anim");
        Debug.Log("[GenerateFurnitureAnimations] ✓ Created Furniture_Appear (4 unit drop with landing wobble)");
    }

    private void GenerateRemoveAnimation()
    {
        AnimationClip clip = new AnimationClip();
        clip.name = "Furniture_Remove";
        float duration = 0.5f;
        clip.frameRate = 60f;

        // --- POSITION: Rise upward ---
        AnimationCurve posY = AnimationCurve.Linear(0, 0, duration, 1f);
        var posBinding = EditorCurveBinding.FloatCurve("", typeof(Transform), "localPosition.y");
        AnimationUtility.SetEditorCurve(clip, posBinding, posY);

        // --- SCALE: Pulse up then shrink to zero (playful disappear) ---
        AnimationCurve scale = AnimationCurve.Linear(0, 1f, duration * 0.3f, 1.15f); // Pulse up
        scale.AddKey(new Keyframe(duration * 0.5f, 1f)); // Reset
        scale.AddKey(new Keyframe(duration, 0f)); // Vanish

        var scaleXBinding = EditorCurveBinding.FloatCurve("", typeof(Transform), "localScale.x");
        var scaleYBinding = EditorCurveBinding.FloatCurve("", typeof(Transform), "localScale.y");
        var scaleZBinding = EditorCurveBinding.FloatCurve("", typeof(Transform), "localScale.z");
        AnimationUtility.SetEditorCurve(clip, scaleXBinding, scale);
        AnimationUtility.SetEditorCurve(clip, scaleYBinding, scale);
        AnimationUtility.SetEditorCurve(clip, scaleZBinding, scale);

        AssetDatabase.CreateAsset(clip, "Assets/Animations/Furniture_Remove.anim");
        Debug.Log("[GenerateFurnitureAnimations] ✓ Created Furniture_Remove");
    }

    private void GenerateInteractWeakAnimation()
    {
        AnimationClip clip = new AnimationClip();
        clip.name = "Furniture_Interact_Weak";
        float duration = 0.3f;
        clip.frameRate = 60f;

        // --- POSITION: Subtle upward pop ---
        AnimationCurve posY = AnimationCurve.Linear(0, 0, duration * 0.4f, 0.1f); // Pop up
        posY.AddKey(new Keyframe(duration, 0f)); // Land back

        var posBinding = EditorCurveBinding.FloatCurve("", typeof(Transform), "localPosition.y");
        AnimationUtility.SetEditorCurve(clip, posBinding, posY);

        // --- SCALE: Subtle squish (5% variation) ---
        AnimationCurve scale = AnimationCurve.Linear(0, 1f, duration * 0.2f, 0.95f); // Compress
        scale.AddKey(new Keyframe(duration * 0.35f, 1.02f)); // Slight overshoot
        scale.AddKey(new Keyframe(duration, 1f)); // Settle

        var scaleXBinding = EditorCurveBinding.FloatCurve("", typeof(Transform), "localScale.x");
        var scaleYBinding = EditorCurveBinding.FloatCurve("", typeof(Transform), "localScale.y");
        var scaleZBinding = EditorCurveBinding.FloatCurve("", typeof(Transform), "localScale.z");
        AnimationUtility.SetEditorCurve(clip, scaleXBinding, scale);
        AnimationUtility.SetEditorCurve(clip, scaleYBinding, scale);
        AnimationUtility.SetEditorCurve(clip, scaleZBinding, scale);

        // --- ROTATION: Slight wiggle (±5 degrees) ---
        AnimationCurve rotZ = AnimationCurve.Linear(0, 0, duration * 0.25f, 5f);
        rotZ.AddKey(new Keyframe(duration * 0.5f, -3f));
        rotZ.AddKey(new Keyframe(duration, 0f));

        var rotZBinding = EditorCurveBinding.FloatCurve("", typeof(Transform), "localEulerAngles.z");
        AnimationUtility.SetEditorCurve(clip, rotZBinding, rotZ);

        AssetDatabase.CreateAsset(clip, "Assets/Animations/Furniture_Interact_Weak.anim");
        Debug.Log("[GenerateFurnitureAnimations] ✓ Created Furniture_Interact_Weak");
    }

    private void GenerateInteractStrongAnimation()
    {
        AnimationClip clip = new AnimationClip();
        clip.name = "Furniture_Interact_Strong";
        float duration = 0.6f;
        clip.frameRate = 60f;

        // --- POSITION: Big bouncy pop ---
        AnimationCurve posY = AnimationCurve.Linear(0, 0, duration * 0.3f, 0.25f); // Pop up high
        posY.AddKey(new Keyframe(duration * 0.5f, 0f)); // Come back down
        posY.AddKey(new Keyframe(duration * 0.65f, -0.05f)); // Slight overshoot down
        posY.AddKey(new Keyframe(duration, 0f)); // Settle

        var posBinding = EditorCurveBinding.FloatCurve("", typeof(Transform), "localPosition.y");
        AnimationUtility.SetEditorCurve(clip, posBinding, posY);

        // --- SCALE: Exaggerated squish and swell (±20%) ---
        AnimationCurve scale = AnimationCurve.Linear(0, 1f, duration * 0.2f, 0.8f); // Big squish
        scale.AddKey(new Keyframe(duration * 0.35f, 1.2f)); // Big swell
        scale.AddKey(new Keyframe(duration * 0.5f, 1.1f)); // Settle to 1.1
        scale.AddKey(new Keyframe(duration * 0.7f, 0.95f)); // Slight squish
        scale.AddKey(new Keyframe(duration, 1f)); // Final settle

        var scaleXBinding = EditorCurveBinding.FloatCurve("", typeof(Transform), "localScale.x");
        var scaleYBinding = EditorCurveBinding.FloatCurve("", typeof(Transform), "localScale.y");
        var scaleZBinding = EditorCurveBinding.FloatCurve("", typeof(Transform), "localScale.z");
        AnimationUtility.SetEditorCurve(clip, scaleXBinding, scale);
        AnimationUtility.SetEditorCurve(clip, scaleYBinding, scale);
        AnimationUtility.SetEditorCurve(clip, scaleZBinding, scale);

        // --- ROTATION: Energetic jiggle (±15 degrees, multiple oscillations) ---
        AnimationCurve rotZ = AnimationCurve.Linear(0, 0, duration * 0.15f, 15f);
        rotZ.AddKey(new Keyframe(duration * 0.25f, -12f));
        rotZ.AddKey(new Keyframe(duration * 0.35f, 10f));
        rotZ.AddKey(new Keyframe(duration * 0.45f, -8f));
        rotZ.AddKey(new Keyframe(duration * 0.55f, 5f));
        rotZ.AddKey(new Keyframe(duration, 0f));

        var rotZBinding = EditorCurveBinding.FloatCurve("", typeof(Transform), "localEulerAngles.z");
        AnimationUtility.SetEditorCurve(clip, rotZBinding, rotZ);

        AssetDatabase.CreateAsset(clip, "Assets/Animations/Furniture_Interact_Strong.anim");
        Debug.Log("[GenerateFurnitureAnimations] ✓ Created Furniture_Interact_Strong");
    }
}
