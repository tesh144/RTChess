#pragma warning disable CS0414, CS0219, CS0618
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEditor.SceneManagement;
using ClockworkGrid;

/// <summary>
/// Automatically ensures TitleScreenController exists in the scene with all
/// serialized references and persistent onClick/onGameStart listeners.
///
/// Runs on:
///   1. Domain reload (after scripts compile) — wires everything immediately
///   2. Before entering play mode — safety check in case scene was changed
///   3. After scene opens — in case user switches scenes
///
/// This replaces ANY runtime creation or AddListener() calls.
/// Everything is serialized and visible in the Inspector.
/// </summary>
[InitializeOnLoad]
public static class TitleScreenAutoSetup
{
    static TitleScreenAutoSetup()
    {
        // Run after domain reload (compile) with a slight delay so all objects are ready
        EditorApplication.delayCall += RunSetupIfNeeded;

        // Run before entering play mode as a safety net
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        // Run when a scene is opened
        EditorSceneManager.sceneOpened += (scene, mode) =>
        {
            EditorApplication.delayCall += RunSetupIfNeeded;
        };
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode) return;
        RunSetupIfNeeded();
    }

    private static void RunSetupIfNeeded()
    {
        // Don't run in play mode
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;

        var controller = Object.FindFirstObjectByType<TitleScreenController>();

        if (controller == null)
        {
            // No TitleScreenController at all — check if Title_common exists first
            // (if no Title_common panel, this scene doesn't need a title screen)
            bool hasTitlePanel = false;
            foreach (var panel in Object.FindObjectsOfType<UIPanel>(true))
            {
                if (panel.pageId == "Title_common" && !panel.isDarkMode)
                {
                    hasTitlePanel = true;
                    break;
                }
            }

            if (!hasTitlePanel)
            {
                // No title panel in this scene — nothing to set up
                return;
            }

            Debug.Log("[TitleScreenAutoSetup] No TitleScreenController found — creating with serialized references...");
            SetupTitleScreen.Run();
            SaveScene();
            return;
        }

        // Controller exists — check if onClick and onGameStart are properly wired
        bool needsRewire = false;

        if (controller.playButton != null)
        {
            if (!HasPersistentListener(controller.playButton.onClick, controller, "OnPlayButtonClicked"))
            {
                Debug.Log("[TitleScreenAutoSetup] Button_Play onClick empty — wiring persistent listener...");
                needsRewire = true;
            }
        }

        if (controller.onGameStart == null || controller.onGameStart.GetPersistentEventCount() == 0)
        {
            Debug.Log("[TitleScreenAutoSetup] onGameStart empty — wiring persistent listeners...");
            needsRewire = true;
        }

        if (needsRewire)
        {
            SetupTitleScreen.Run();
            SaveScene();
        }
    }

    private static bool HasPersistentListener(UnityEventBase evt, Object target, string methodName)
    {
        int count = evt.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
        {
            if (evt.GetPersistentTarget(i) == target &&
                evt.GetPersistentMethodName(i) == methodName)
            {
                return true;
            }
        }
        return false;
    }

    private static void SaveScene()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.isDirty)
        {
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[TitleScreenAutoSetup] Scene saved with serialized references.");
        }
    }
}
#endif
