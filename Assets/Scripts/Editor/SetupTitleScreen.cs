#pragma warning disable CS0414, CS0219, CS0618
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.UI;
using UnityEngine.Events;
using ClockworkGrid;

/// <summary>
/// Editor tool that sets up the TitleScreenController with all serialized references:
///
///   1. Creates/finds TitleScreenController GameObject in the scene
///   2. Assigns Title_common UIPanel reference
///   3. Assigns Background, Image_TiteText, Button_Play, Button_Guest, Button_Facebook
///   4. Wires Button_Play onClick → TitleScreenController.OnPlayButtonClicked() (persistent)
///   5. Wires onGameStart → CafeSceneSetupV2.OnGameStarted() (persistent)
///   6. Wires onGameStart → MapGeneratorV2.RunGenerate() (persistent)
///
/// All connections are serialized and visible in the Inspector.
///
/// Menu: Tools > ClockworkCraft > Setup Title Screen
/// </summary>
public class SetupTitleScreen
{
    [MenuItem("Tools/ClockworkCraft/Setup Title Screen")]
    public static void Run()
    {
        // ── Find Title_common panel ────────────────────────────────

        UIPanel titlePanel = null;
        UIPanel[] allPanels = Object.FindObjectsOfType<UIPanel>(true);
        foreach (var p in allPanels)
        {
            if (p.pageId == "Title_common" && !p.isDarkMode)
            {
                titlePanel = p;
                break;
            }
        }

        if (titlePanel == null)
        {
            Debug.LogError("[SetupTitleScreen] Title_common UIPanel not found. " +
                           "Run 'Setup UI Panels' first to attach UIPanel components.");
            return;
        }

        // ── Ensure GameStateManager exists ──────────────────────────

        var gsm = Object.FindFirstObjectByType<ClockworkGrid.GameStateManager>();
        if (gsm == null)
        {
            GameObject gsmObj = new GameObject("GameStateManager");
            Undo.RegisterCreatedObjectUndo(gsmObj, "Create GameStateManager");
            gsm = gsmObj.AddComponent<ClockworkGrid.GameStateManager>();
            Debug.Log("[SetupTitleScreen] Created GameStateManager (initial state: TitleScreen)");
        }

        // ── Find or create TitleScreenController ───────────────────

        var controller = Object.FindFirstObjectByType<TitleScreenController>();
        if (controller == null)
        {
            GameObject obj = new GameObject("TitleScreenController");
            Undo.RegisterCreatedObjectUndo(obj, "Create TitleScreenController");
            controller = obj.AddComponent<TitleScreenController>();
            Debug.Log("[SetupTitleScreen] Created TitleScreenController GameObject");
        }

        // ── Assign panel reference ─────────────────────────────────

        controller.titlePanel = titlePanel;

        // ── Resolve element references from the panel ──────────────

        // Background illustration
        Transform bgTransform = titlePanel.Get("Background");
        if (bgTransform != null)
            controller.background = bgTransform.GetComponent<Image>();

        // Background color overlay (flat black behind the illustration)
        // Try exact name first, then common variations
        Transform bgColorTransform = titlePanel.Get("background color");
        if (bgColorTransform == null)
            bgColorTransform = titlePanel.Get("Background Color");
        if (bgColorTransform == null)
            bgColorTransform = titlePanel.Get("BackgroundColor");
        if (bgColorTransform != null)
        {
            controller.backgroundColorOverlay = bgColorTransform.gameObject;
            Debug.Log("[SetupTitleScreen] Found background color overlay");
        }

        // Title logo
        Transform logoTransform = titlePanel.Get("Image_TiteText");
        if (logoTransform != null)
            controller.titleLogo = logoTransform as RectTransform;

        // Buttons
        Transform playTransform = titlePanel.Get("Button_Play");
        if (playTransform != null)
            controller.playButton = playTransform.GetComponent<Button>();

        Transform guestTransform = titlePanel.Get("Button_Guest");
        if (guestTransform != null)
            controller.guestButton = guestTransform.GetComponent<Button>();

        Transform facebookTransform = titlePanel.Get("Button_Facebook");
        if (facebookTransform != null)
            controller.facebookButton = facebookTransform.GetComponent<Button>();

        // ── Wire Button_Play onClick → OnPlayButtonClicked ─────────

        if (controller.playButton != null)
        {
            // Clear any existing persistent listeners to avoid duplicates
            UnityEventTools.RemovePersistentListener(controller.playButton.onClick,
                new UnityAction(controller.OnPlayButtonClicked));

            // Add persistent listener (shows in Inspector)
            UnityEventTools.AddPersistentListener(
                controller.playButton.onClick,
                new UnityAction(controller.OnPlayButtonClicked));

            EditorUtility.SetDirty(controller.playButton);
            Debug.Log("[SetupTitleScreen] Wired Button_Play → OnPlayButtonClicked (persistent)");
        }

        // Also wire Guest button to OnPlayButtonClicked
        if (controller.guestButton != null)
        {
            UnityEventTools.RemovePersistentListener(controller.guestButton.onClick,
                new UnityAction(controller.OnPlayButtonClicked));

            UnityEventTools.AddPersistentListener(
                controller.guestButton.onClick,
                new UnityAction(controller.OnPlayButtonClicked));

            EditorUtility.SetDirty(controller.guestButton);
        }

        // ── Wire onGameStart → CafeSceneSetupV2.OnGameStarted ─────

        // Initialize the UnityEvent if null
        if (controller.onGameStart == null)
            controller.onGameStart = new UnityEvent();

        // Clear existing to avoid duplicates
        int existingCount = controller.onGameStart.GetPersistentEventCount();
        for (int i = existingCount - 1; i >= 0; i--)
        {
            UnityEventTools.RemovePersistentListener(controller.onGameStart, i);
        }

        // Wire CafeSceneSetupV2.OnGameStarted
        var cafeSetup = Object.FindFirstObjectByType<LittleCafe.CafeSceneSetupV2>();
        if (cafeSetup != null)
        {
            UnityEventTools.AddPersistentListener(
                controller.onGameStart,
                new UnityAction(cafeSetup.OnGameStarted));

            Debug.Log("[SetupTitleScreen] Wired onGameStart → CafeSceneSetupV2.OnGameStarted");
        }
        else
        {
            Debug.LogWarning("[SetupTitleScreen] CafeSceneSetupV2 not found — onGameStart not wired.");
        }

        // Wire MapGeneratorV2.RunGenerate
        var mapGen = Object.FindFirstObjectByType<ClockworkCraft.MapGeneratorV2>();
        if (mapGen != null)
        {
            UnityEventTools.AddPersistentListener(
                controller.onGameStart,
                new UnityAction(mapGen.RunGenerate));

            Debug.Log("[SetupTitleScreen] Wired onGameStart → MapGeneratorV2.RunGenerate");
        }
        else
        {
            Debug.LogWarning("[SetupTitleScreen] MapGeneratorV2 not found — onGameStart not wired.");
        }

        // ── Mark dirty and save ────────────────────────────────────

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(controller.gameObject);

        Debug.Log($"[SetupTitleScreen] Done! All references assigned and events wired:\n" +
                  $"  Panel: {(titlePanel != null ? titlePanel.pageId : "MISSING")}\n" +
                  $"  Background: {(controller.background != null ? "OK" : "MISSING")}\n" +
                  $"  Background Color Overlay: {(controller.backgroundColorOverlay != null ? "OK" : "MISSING")}\n" +
                  $"  Title Logo: {(controller.titleLogo != null ? "OK" : "MISSING")}\n" +
                  $"  Play Button: {(controller.playButton != null ? "OK" : "MISSING")}\n" +
                  $"  Guest Button: {(controller.guestButton != null ? "OK" : "MISSING")}\n" +
                  $"  Facebook Button: {(controller.facebookButton != null ? "OK" : "MISSING")}\n" +
                  $"  onGameStart listeners: {controller.onGameStart.GetPersistentEventCount()}");

        Selection.activeGameObject = controller.gameObject;
    }
}
#endif
