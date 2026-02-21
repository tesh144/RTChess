using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

/// <summary>
/// Creates high-quality animations for ObjectPrefabHolder and sets up Animator Controller
/// Run via: Tools → Create Object Animations
/// </summary>
public class ObjectAnimationCreator
{
    [MenuItem("Tools/Create Object Animations")]
    public static void CreateAnimations()
    {
        Debug.Log("=== Creating Object Animations ===");

        string animFolder = "Assets/Animations/ObjectAnimations";
        string controllerPath = "Assets/Prefabs/ObjectAnimatorController.controller";

        // Create folder if needed
        if (!AssetDatabase.IsValidFolder(animFolder))
        {
            Directory.CreateDirectory(animFolder.Replace("Assets/", Application.dataPath + "/"));
            AssetDatabase.Refresh();
        }

        // Create animation clips
        AnimationClip appearClip = CreateAppearAnimation(animFolder);
        AnimationClip destroyClip = CreateDestroyAnimation(animFolder);
        AnimationClip interactClip = CreateInteractAnimation(animFolder);
        AnimationClip idleClip = CreateIdleAnimation(animFolder);

        // Setup Animator Controller
        SetupAnimatorController(controllerPath, appearClip, destroyClip, interactClip, idleClip);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("✓ Animations created successfully!");
        EditorUtility.DisplayDialog("Success",
            "Created 4 animations:\n• Appear (fall & wobble)\n• Destroy (shrink & fade)\n• Interact (bounce)\n• Idle (subtle breathing)\n\nAnimator Controller updated!",
            "OK");
    }

    static AnimationClip CreateAppearAnimation(string folder)
    {
        AnimationClip clip = new AnimationClip();
        clip.name = "Object_Appear";

        float duration = 0.8f;
        clip.frameRate = 60;

        // Target: AnimatorHolder (the animated wrapper)
        string path = "AnimatorHolder";

        // PHASE 1: Fall from above (0.0 - 0.4s)
        AnimationCurve posY = new AnimationCurve();
        posY.AddKey(new Keyframe(0f, 3f, 0f, -8f));      // Start high, falling fast
        posY.AddKey(new Keyframe(0.35f, 0.1f, -4f, 0f)); // Impact bounce up slightly
        posY.AddKey(new Keyframe(0.45f, 0f, 0f, 0f));    // Settle at ground
        posY.AddKey(new Keyframe(duration, 0f, 0f, 0f)); // Stay grounded

        // PHASE 2: Squash on impact (0.35 - 0.5s)
        AnimationCurve scaleY = new AnimationCurve();
        scaleY.AddKey(new Keyframe(0f, 1f));
        scaleY.AddKey(new Keyframe(0.35f, 0.7f));  // Squash on impact
        scaleY.AddKey(new Keyframe(0.45f, 1.15f)); // Stretch back up
        scaleY.AddKey(new Keyframe(0.55f, 0.95f)); // Settle
        scaleY.AddKey(new Keyframe(duration, 1f)); // Back to normal

        AnimationCurve scaleXZ = new AnimationCurve();
        scaleXZ.AddKey(new Keyframe(0f, 1f));
        scaleXZ.AddKey(new Keyframe(0.35f, 1.2f));  // Stretch on impact
        scaleXZ.AddKey(new Keyframe(0.45f, 0.9f));  // Compress
        scaleXZ.AddKey(new Keyframe(0.55f, 1.05f)); // Overshoot
        scaleXZ.AddKey(new Keyframe(duration, 1f)); // Settle

        // PHASE 3: Wobble rotation (0.4 - 0.8s)
        AnimationCurve rotZ = new AnimationCurve();
        rotZ.AddKey(new Keyframe(0f, 0f));
        rotZ.AddKey(new Keyframe(0.4f, -8f));   // Wobble left
        rotZ.AddKey(new Keyframe(0.55f, 5f));   // Wobble right
        rotZ.AddKey(new Keyframe(0.65f, -2f));  // Small left
        rotZ.AddKey(new Keyframe(duration, 0f)); // Settle

        // Apply curves
        clip.SetCurve(path, typeof(Transform), "localPosition.y", posY);
        clip.SetCurve(path, typeof(Transform), "localScale.x", scaleXZ);
        clip.SetCurve(path, typeof(Transform), "localScale.z", scaleXZ);
        clip.SetCurve(path, typeof(Transform), "localScale.y", scaleY);
        clip.SetCurve(path, typeof(Transform), "localEulerAngles.z", rotZ);

        // Smooth all curves
        SmoothAllCurves(clip);

        string assetPath = Path.Combine(folder, "Object_Appear.anim").Replace("\\", "/");
        AssetDatabase.CreateAsset(clip, assetPath);
        Debug.Log($"✓ Created: {assetPath}");
        return clip;
    }

    static AnimationClip CreateDestroyAnimation(string folder)
    {
        AnimationClip clip = new AnimationClip();
        clip.name = "Object_Destroy";

        float duration = 0.5f;
        clip.frameRate = 60;

        string path = "AnimatorHolder";

        // Implode effect - shrink and spin
        AnimationCurve scale = AnimationCurve.EaseInOut(0f, 1f, duration, 0f);
        AnimationCurve rotY = new AnimationCurve();
        rotY.AddKey(new Keyframe(0f, 0f, 0f, 720f)); // Spin fast
        rotY.AddKey(new Keyframe(duration, 360f));

        // Slight upward motion before disappearing
        AnimationCurve posY = new AnimationCurve();
        posY.AddKey(new Keyframe(0f, 0f));
        posY.AddKey(new Keyframe(0.3f, 0.5f)); // Rise slightly
        posY.AddKey(new Keyframe(duration, 0.3f)); // Float down while shrinking

        clip.SetCurve(path, typeof(Transform), "localScale.x", scale);
        clip.SetCurve(path, typeof(Transform), "localScale.y", scale);
        clip.SetCurve(path, typeof(Transform), "localScale.z", scale);
        clip.SetCurve(path, typeof(Transform), "localEulerAngles.y", rotY);
        clip.SetCurve(path, typeof(Transform), "localPosition.y", posY);

        SmoothAllCurves(clip);

        string assetPath = Path.Combine(folder, "Object_Destroy.anim").Replace("\\", "/");
        AssetDatabase.CreateAsset(clip, assetPath);
        Debug.Log($"✓ Created: {assetPath}");
        return clip;
    }

    static AnimationClip CreateInteractAnimation(string folder)
    {
        AnimationClip clip = new AnimationClip();
        clip.name = "Object_Interact";

        float duration = 0.3f; // Quick feedback
        clip.frameRate = 60;

        string path = "AnimatorHolder";

        // Quick swell and bounce (Overcooked-style)
        AnimationCurve scale = new AnimationCurve();
        scale.AddKey(new Keyframe(0f, 1f, 0f, 3f));      // Start normal
        scale.AddKey(new Keyframe(0.1f, 1.15f, 0f, 0f)); // Swell quickly
        scale.AddKey(new Keyframe(0.2f, 0.95f, 0f, 0f)); // Compress
        scale.AddKey(new Keyframe(duration, 1f, 0f, 0f)); // Return to normal

        // Slight rotation wobble
        AnimationCurve rotZ = new AnimationCurve();
        rotZ.AddKey(new Keyframe(0f, 0f));
        rotZ.AddKey(new Keyframe(0.1f, -3f));
        rotZ.AddKey(new Keyframe(0.2f, 2f));
        rotZ.AddKey(new Keyframe(duration, 0f));

        // Tiny hop
        AnimationCurve posY = new AnimationCurve();
        posY.AddKey(new Keyframe(0f, 0f));
        posY.AddKey(new Keyframe(0.1f, 0.1f)); // Small hop
        posY.AddKey(new Keyframe(duration, 0f)); // Land

        clip.SetCurve(path, typeof(Transform), "localScale.x", scale);
        clip.SetCurve(path, typeof(Transform), "localScale.y", scale);
        clip.SetCurve(path, typeof(Transform), "localScale.z", scale);
        clip.SetCurve(path, typeof(Transform), "localEulerAngles.z", rotZ);
        clip.SetCurve(path, typeof(Transform), "localPosition.y", posY);

        SmoothAllCurves(clip);

        string assetPath = Path.Combine(folder, "Object_Interact.anim").Replace("\\", "/");
        AssetDatabase.CreateAsset(clip, assetPath);
        Debug.Log($"✓ Created: {assetPath}");
        return clip;
    }

    static AnimationClip CreateIdleAnimation(string folder)
    {
        AnimationClip clip = new AnimationClip();
        clip.name = "Object_Idle";
        clip.wrapMode = WrapMode.Loop;

        float duration = 2f; // Slow breathing
        clip.frameRate = 60;

        string path = "AnimatorHolder";

        // Subtle breathing scale (very gentle)
        AnimationCurve breathe = new AnimationCurve();
        breathe.AddKey(new Keyframe(0f, 1f));
        breathe.AddKey(new Keyframe(duration * 0.5f, 1.02f)); // Inhale
        breathe.AddKey(new Keyframe(duration, 1f)); // Exhale
        breathe.postWrapMode = WrapMode.Loop;

        clip.SetCurve(path, typeof(Transform), "localScale.y", breathe);

        SmoothAllCurves(clip);

        string assetPath = Path.Combine(folder, "Object_Idle.anim").Replace("\\", "/");
        AssetDatabase.CreateAsset(clip, assetPath);
        Debug.Log($"✓ Created: {assetPath}");
        return clip;
    }

    static void SetupAnimatorController(string controllerPath, AnimationClip appear, AnimationClip destroy, AnimationClip interact, AnimationClip idle)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            Debug.Log($"✓ Created new Animator Controller: {controllerPath}");
        }

        // Clear existing states
        var layer = controller.layers[0];
        var stateMachine = layer.stateMachine;

        // Clear old states
        foreach (var state in stateMachine.states)
        {
            stateMachine.RemoveState(state.state);
        }

        // Create states
        var idleState = stateMachine.AddState("Idle", new Vector3(300, 0, 0));
        idleState.motion = idle;

        var appearState = stateMachine.AddState("Appear", new Vector3(300, 100, 0));
        appearState.motion = appear;

        var interactState = stateMachine.AddState("Interact", new Vector3(300, 200, 0));
        interactState.motion = interact;

        var destroyState = stateMachine.AddState("Destroy", new Vector3(300, 300, 0));
        destroyState.motion = destroy;

        // Set default state
        stateMachine.defaultState = appearState;

        // Add parameters
        controller.AddParameter("Interact", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Destroy", AnimatorControllerParameterType.Trigger);

        // Create transitions
        // Appear -> Idle (when appear finishes)
        var appearToIdle = appearState.AddTransition(idleState);
        appearToIdle.hasExitTime = true;
        appearToIdle.exitTime = 1f;
        appearToIdle.duration = 0.1f;

        // Idle -> Interact (on trigger)
        var idleToInteract = idleState.AddTransition(interactState);
        idleToInteract.AddCondition(AnimatorConditionMode.If, 0, "Interact");
        idleToInteract.hasExitTime = false;
        idleToInteract.duration = 0.05f;

        // Interact -> Idle (when interact finishes)
        var interactToIdle = interactState.AddTransition(idleState);
        interactToIdle.hasExitTime = true;
        interactToIdle.exitTime = 1f;
        interactToIdle.duration = 0.05f;

        // Any -> Destroy (on trigger)
        var anyToDestroy = stateMachine.AddAnyStateTransition(destroyState);
        anyToDestroy.AddCondition(AnimatorConditionMode.If, 0, "Destroy");
        anyToDestroy.hasExitTime = false;
        anyToDestroy.duration = 0.1f;

        EditorUtility.SetDirty(controller);
        Debug.Log($"✓ Animator Controller configured: {controllerPath}");
    }

    static void SmoothAllCurves(AnimationClip clip)
    {
        var bindings = AnimationUtility.GetCurveBindings(clip);
        foreach (var binding in bindings)
        {
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            for (int i = 0; i < curve.keys.Length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }
    }
}
