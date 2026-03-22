#pragma warning disable CS0414, CS0219, CS0618
using UnityEditor;
using UnityEngine;

/// <summary>
/// Debug tool to inspect animator state and verify animations are set up correctly.
/// </summary>
public class AnimationDebugger : EditorWindow
{
    [MenuItem("Tools/PEPO/Debug Animations")]
    public static void ShowWindow()
    {
        GetWindow<AnimationDebugger>("Animation Debugger");
    }

    private void OnGUI()
    {
        GUILayout.Label("Animation Debugger", EditorStyles.boldLabel);
        GUILayout.Space(10);

        if (GUILayout.Button("Check ObjectAnimController", GUILayout.Height(40)))
        {
            CheckController();
        }

        if (GUILayout.Button("Check Animation Clips", GUILayout.Height(40)))
        {
            CheckAnimationClips();
        }

        if (GUILayout.Button("Check Placed Objects", GUILayout.Height(40)))
        {
            CheckPlacedObjects();
        }
    }

    private void CheckController()
    {
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/ObjectAnimController.controller");
        if (controller == null)
        {
            Debug.LogError("[AnimationDebugger] ObjectAnimController not found!");
            return;
        }

        Debug.Log($"[AnimationDebugger] Controller: {controller.name}");
        Debug.Log($"[AnimationDebugger] Controller type: {controller.GetType()}");
    }

    private void CheckAnimationClips()
    {
        string[] clips = new[] {
            "Assets/Animations/Furniture_Appear.anim",
            "Assets/Animations/Furniture_Remove.anim",
            "Assets/Animations/Furniture_Interact_Weak.anim",
            "Assets/Animations/Furniture_Interact_Strong.anim"
        };

        foreach (string clipPath in clips)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                Debug.LogError($"[AnimationDebugger] Not found: {clipPath}");
                continue;
            }

            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            Debug.Log($"[AnimationDebugger] {clip.name}: {bindings.Length} animated properties, duration: {clip.length}s");

            foreach (var binding in bindings)
            {
                Debug.Log($"  - {binding.path} / {binding.type.Name} / {binding.propertyName}");
            }
        }
    }

    private void CheckPlacedObjects()
    {
        Animator[] animators = FindObjectsOfType<Animator>();
        Debug.Log($"[AnimationDebugger] Found {animators.Length} Animator components in scene");

        foreach (Animator anim in animators)
        {
            Debug.Log($"[AnimationDebugger] Animator on {anim.gameObject.name}");
            Debug.Log($"  - Controller: {anim.runtimeAnimatorController?.name ?? "NONE"}");
            Debug.Log($"  - Enabled: {anim.enabled}");
        }
    }
}
