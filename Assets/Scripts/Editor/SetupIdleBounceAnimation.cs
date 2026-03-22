#pragma warning disable CS0414, CS0219, CS0618
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

/// <summary>
/// One-time editor script that adds the idle_bounce trigger and animation state
/// to the ObjectAnimController. Creates a subtle Y-scale bounce animation clip.
///
/// Auto-runs on domain reload. Also available via menu.
/// DELETE THIS SCRIPT after confirming it worked.
/// </summary>
[InitializeOnLoad]
public class SetupIdleBounceAnimation
{
    static SetupIdleBounceAnimation()
    {
        // Auto-run on domain reload (after compilation)
        EditorApplication.delayCall += () => Run();
    }

    [MenuItem("Tools/ClockworkCraft/Setup Idle Bounce Animation")]
    public static void Run()
    {
        // ── Find the ObjectAnimController ─────────────────────────────
        string[] guids = AssetDatabase.FindAssets("ObjectAnimController t:AnimatorController");
        if (guids.Length == 0)
        {
            Debug.LogError("[SetupIdleBounce] ObjectAnimController not found!");
            return;
        }

        string controllerPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            Debug.LogError("[SetupIdleBounce] Failed to load ObjectAnimController!");
            return;
        }

        Debug.Log($"[SetupIdleBounce] Found controller at: {controllerPath}");

        // ── Check if idle_bounce parameter already exists ─────────────
        bool hasParam = false;
        foreach (var param in controller.parameters)
        {
            if (param.name == "idle_bounce")
            {
                hasParam = true;
                break;
            }
        }

        if (!hasParam)
        {
            controller.AddParameter("idle_bounce", AnimatorControllerParameterType.Trigger);
            Debug.Log("[SetupIdleBounce] Added 'idle_bounce' trigger parameter");
        }
        else
        {
            Debug.Log("[SetupIdleBounce] 'idle_bounce' parameter already exists");
        }

        // ── Create the animation clip ─────────────────────────────────
        string clipPath = "Assets/Animations/IdleBounce.anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);

        if (clip == null)
        {
            clip = CreateIdleBounceClip();
            AssetDatabase.CreateAsset(clip, clipPath);
            Debug.Log($"[SetupIdleBounce] Created IdleBounce.anim at {clipPath}");
        }
        else
        {
            Debug.Log("[SetupIdleBounce] IdleBounce.anim already exists — updating");
            // Recreate keyframes on existing clip
            UpdateIdleBounceClip(clip);
            EditorUtility.SetDirty(clip);
        }

        // ── Add state to animator controller ──────────────────────────
        AnimatorStateMachine rootSM = controller.layers[0].stateMachine;

        // Check if IdleBounce state already exists
        AnimatorState existingState = null;
        foreach (var state in rootSM.states)
        {
            if (state.state.name == "IdleBounce")
            {
                existingState = state.state;
                break;
            }
        }

        AnimatorState idleBounceState;
        if (existingState != null)
        {
            idleBounceState = existingState;
            idleBounceState.motion = clip;
            Debug.Log("[SetupIdleBounce] Updated existing IdleBounce state");
        }
        else
        {
            idleBounceState = rootSM.AddState("IdleBounce", new Vector3(400, 200, 0));
            idleBounceState.motion = clip;
            Debug.Log("[SetupIdleBounce] Added IdleBounce state to controller");
        }

        // ── Add AnyState → IdleBounce transition ─────────────────────
        // Check if transition already exists
        bool hasTransition = false;
        foreach (var transition in rootSM.anyStateTransitions)
        {
            if (transition.destinationState == idleBounceState)
            {
                hasTransition = true;
                break;
            }
        }

        if (!hasTransition)
        {
            AnimatorStateTransition anyToIdle = rootSM.AddAnyStateTransition(idleBounceState);
            anyToIdle.AddCondition(AnimatorConditionMode.If, 0, "idle_bounce");
            anyToIdle.duration = 0.05f;
            anyToIdle.hasExitTime = false;
            anyToIdle.canTransitionToSelf = true;
            Debug.Log("[SetupIdleBounce] Added AnyState → IdleBounce transition");
        }

        // ── Add IdleBounce → Idle transition (auto return) ───────────
        // Find the Idle state
        AnimatorState idleState = null;
        foreach (var state in rootSM.states)
        {
            if (state.state.name == "Idle")
            {
                idleState = state.state;
                break;
            }
        }

        if (idleState != null)
        {
            // Check if return transition exists
            bool hasReturn = false;
            foreach (var transition in idleBounceState.transitions)
            {
                if (transition.destinationState == idleState)
                {
                    hasReturn = true;
                    break;
                }
            }

            if (!hasReturn)
            {
                AnimatorStateTransition returnToIdle = idleBounceState.AddTransition(idleState);
                returnToIdle.hasExitTime = true;
                returnToIdle.exitTime = 1f;
                returnToIdle.duration = 0.1f;
                returnToIdle.hasFixedDuration = true;
                Debug.Log("[SetupIdleBounce] Added IdleBounce → Idle return transition");
            }
        }

        // ── Save ──────────────────────────────────────────────────────
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[SetupIdleBounce] DONE! You can delete this script now.\n" +
                  "  Controller: " + controllerPath + "\n" +
                  "  Animation: " + clipPath + "\n" +
                  "  Trigger: idle_bounce");
    }

    /// <summary>
    /// Create a very subtle Y-scale bounce animation.
    /// The AnimatorHolder (child of root) gets a gentle scale pulse:
    ///   0.0s → scale Y 1.0 (start)
    ///   0.1s → scale Y 1.06 (peak — very subtle up-stretch)
    ///   0.2s → scale Y 0.97 (slight squash on return)
    ///   0.3s → scale Y 1.0 (settle back)
    /// Total duration: 0.3 seconds. Very light, almost a breathing pulse.
    /// </summary>
    private static AnimationClip CreateIdleBounceClip()
    {
        AnimationClip clip = new AnimationClip();
        clip.name = "IdleBounce";

        // We animate the AnimatorHolder's localScale.y
        // The AnimatorHolder is the direct child that has the Animator
        // Since the Animator is ON the AnimatorHolder, we animate "" (self) localScale

        // Scale Y keyframes — very gentle bounce
        AnimationCurve scaleY = new AnimationCurve();
        scaleY.AddKey(new Keyframe(0f, 1f));       // Start normal
        scaleY.AddKey(new Keyframe(0.1f, 1.06f));  // Gentle stretch up
        scaleY.AddKey(new Keyframe(0.2f, 0.97f));  // Tiny squash
        scaleY.AddKey(new Keyframe(0.3f, 1f));     // Back to normal

        // Make curves smooth
        for (int i = 0; i < scaleY.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(scaleY, i, AnimationUtility.TangentMode.Auto);
            AnimationUtility.SetKeyRightTangentMode(scaleY, i, AnimationUtility.TangentMode.Auto);
        }

        // Scale X — subtle complementary squash/stretch
        AnimationCurve scaleX = new AnimationCurve();
        scaleX.AddKey(new Keyframe(0f, 1f));
        scaleX.AddKey(new Keyframe(0.1f, 0.97f));  // Slight narrow during stretch
        scaleX.AddKey(new Keyframe(0.2f, 1.02f));  // Slight wide during squash
        scaleX.AddKey(new Keyframe(0.3f, 1f));

        for (int i = 0; i < scaleX.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(scaleX, i, AnimationUtility.TangentMode.Auto);
            AnimationUtility.SetKeyRightTangentMode(scaleX, i, AnimationUtility.TangentMode.Auto);
        }

        // Scale Z — same as X for uniform horizontal
        AnimationCurve scaleZ = new AnimationCurve();
        scaleZ.AddKey(new Keyframe(0f, 1f));
        scaleZ.AddKey(new Keyframe(0.1f, 0.97f));
        scaleZ.AddKey(new Keyframe(0.2f, 1.02f));
        scaleZ.AddKey(new Keyframe(0.3f, 1f));

        for (int i = 0; i < scaleZ.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(scaleZ, i, AnimationUtility.TangentMode.Auto);
            AnimationUtility.SetKeyRightTangentMode(scaleZ, i, AnimationUtility.TangentMode.Auto);
        }

        // Set curves on the clip — "" path means the GameObject the Animator is on
        clip.SetCurve("", typeof(Transform), "localScale.x", scaleX);
        clip.SetCurve("", typeof(Transform), "localScale.y", scaleY);
        clip.SetCurve("", typeof(Transform), "localScale.z", scaleZ);

        // Settings
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        return clip;
    }

    private static void UpdateIdleBounceClip(AnimationClip clip)
    {
        // Clear existing curves
        clip.ClearCurves();

        // Re-create with same logic
        AnimationClip temp = CreateIdleBounceClip();

        // Copy curves from temp
        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(temp);
        foreach (var binding in bindings)
        {
            AnimationCurve curve = AnimationUtility.GetEditorCurve(temp, binding);
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(temp);
        AnimationUtility.SetAnimationClipSettings(clip, settings);
    }
}
#endif
