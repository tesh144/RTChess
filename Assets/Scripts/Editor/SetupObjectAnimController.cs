using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Sets up the ObjectAnimController with animation clips and state machine.
/// </summary>
public class SetupObjectAnimController : EditorWindow
{
    [MenuItem("Tools/PEPO/Setup Object Animator Controller")]
    public static void SetupController()
    {
        string controllerPath = "Assets/Animations/ObjectAnimController.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

        if (controller == null)
        {
            Debug.LogError($"[SetupObjectAnimController] Controller not found at {controllerPath}");
            return;
        }

        // Load animation clips
        AnimationClip appearClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Furniture_Appear.anim");
        AnimationClip removeClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Furniture_Remove.anim");
        AnimationClip weakInteract = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Furniture_Interact_Weak.anim");
        AnimationClip strongInteract = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Furniture_Interact_Strong.anim");

        if (appearClip == null)
        {
            Debug.LogError("[SetupObjectAnimController] Furniture_Appear.anim not found!");
            return;
        }

        // Get the base layer
        AnimatorControllerLayer layer = controller.layers[0];
        AnimatorStateMachine stateMachine = layer.stateMachine;

        // Remove old states if they exist
        foreach (ChildAnimatorState state in stateMachine.states)
        {
            stateMachine.RemoveState(state.state);
        }

        // Create Entry state (default)
        AnimatorState entryState = stateMachine.AddState("Idle");
        stateMachine.defaultState = entryState;

        // Create Appear state
        AnimatorState appearState = stateMachine.AddState("Appear");
        appearState.motion = appearClip;
        appearState.tag = "appear";

        // Add parameters
        if (controller.parameters.Length == 0)
        {
            controller.AddParameter("appear", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("remove", AnimatorControllerParameterType.Trigger);
        }

        // Create transition from Idle to Appear
        AnimatorStateTransition idleToAppear = entryState.AddTransition(appearState);
        idleToAppear.AddCondition(AnimatorConditionMode.If, 0, "appear");
        idleToAppear.duration = 0;
        idleToAppear.hasExitTime = false;

        // Transition back to Idle when appear finishes
        AnimatorStateTransition appearToIdle = appearState.AddTransition(entryState);
        appearToIdle.duration = 0;
        appearToIdle.hasExitTime = true;
        appearToIdle.exitTime = 1f;

        Debug.Log("[SetupObjectAnimController] ✓ Setup complete!");
        Debug.Log("[SetupObjectAnimController] - Created 'Idle' and 'Appear' states");
        Debug.Log("[SetupObjectAnimController] - Added 'appear' trigger parameter");
        Debug.Log("[SetupObjectAnimController] - Wired animations to states");

        EditorUtility.DisplayDialog(
            "Animator Controller Setup",
            "ObjectAnimController has been configured:\n\n" +
            "✓ Idle state (default)\n" +
            "✓ Appear state with Furniture_Appear clip\n" +
            "✓ Appear trigger parameter\n" +
            "✓ Transitions properly configured",
            "OK");
    }
}
