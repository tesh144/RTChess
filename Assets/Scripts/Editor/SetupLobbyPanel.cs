#pragma warning disable CS0414, CS0219, CS0618
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using ClockworkGrid;
using LittleCafe;

/// <summary>
/// Editor tool that sets up the Lobby panel for gameplay:
///
///   1. Finds the Lobby UIPanel in the scene
///   2. Renames Button_Battle text to "Draw"
///   3. Creates/finds DrawButtonController and assigns serialized references
///   4. Wires Button_Battle onClick → DrawButtonController.OnDrawButtonClicked() (persistent)
///   5. Resolves Label_Tag03_Time as the cooldown timer bubble
///   6. Disables Button_Stage
///
/// All connections are serialized and visible in the Inspector.
///
/// Menu: Tools > ClockworkCraft > Setup Lobby Panel
/// </summary>
public class SetupLobbyPanel
{
    [MenuItem("Tools/ClockworkCraft/Setup Lobby Panel")]
    public static void Run()
    {
        // ── Find Lobby panel ─────────────────────────────────────────

        UIPanel lobbyPanel = null;
        UIPanel[] allPanels = Object.FindObjectsOfType<UIPanel>(true);
        foreach (var p in allPanels)
        {
            if (p.pageId == "Lobby" && !p.isDarkMode)
            {
                lobbyPanel = p;
                break;
            }
        }

        if (lobbyPanel == null)
        {
            Debug.LogError("[SetupLobbyPanel] Lobby UIPanel not found. " +
                           "Run 'Setup UI Panels' first to attach UIPanel components.");
            return;
        }

        Debug.Log($"[SetupLobbyPanel] Found Lobby panel: {lobbyPanel.gameObject.name}");

        // ── Find Button_Battle ───────────────────────────────────────

        Transform battleTransform = lobbyPanel.Get("Button_Battle");
        if (battleTransform == null)
            battleTransform = FindChildRecursive(lobbyPanel.transform, "Button_Battle");

        if (battleTransform == null)
        {
            Debug.LogError("[SetupLobbyPanel] Button_Battle not found in Lobby panel!");
            return;
        }

        Button battleButton = battleTransform.GetComponent<Button>();
        if (battleButton == null)
        {
            Debug.LogError("[SetupLobbyPanel] Button_Battle has no Button component!");
            return;
        }

        // ── Rename button text to "Draw" ─────────────────────────────

        Transform textTransform = battleTransform.Find("Text");
        TextMeshProUGUI buttonTMP = null;
        if (textTransform != null)
        {
            buttonTMP = textTransform.GetComponent<TextMeshProUGUI>();
            if (buttonTMP != null)
            {
                Undo.RecordObject(buttonTMP, "Rename Battle to Draw");
                buttonTMP.text = "Draw";
                EditorUtility.SetDirty(buttonTMP);
                Debug.Log("[SetupLobbyPanel] Renamed Button_Battle text to 'Draw'");
            }
        }

        // Also check for legacy Text component
        if (buttonTMP == null && textTransform != null)
        {
            var legacyText = textTransform.GetComponent<UnityEngine.UI.Text>();
            if (legacyText != null)
            {
                Undo.RecordObject(legacyText, "Rename Battle to Draw");
                legacyText.text = "Draw";
                EditorUtility.SetDirty(legacyText);
                Debug.Log("[SetupLobbyPanel] Renamed Button_Battle legacy text to 'Draw'");
            }
        }

        // ── Find timer bubble (Label_Tag03_Time) ─────────────────────

        Transform timerTransform = battleTransform.Find("Label_Tag03_Time");
        if (timerTransform == null)
            timerTransform = FindChildRecursive(battleTransform, "Label_Tag03_Time");

        GameObject timerBubble = timerTransform != null ? timerTransform.gameObject : null;

        // Find the TMP inside the timer bubble
        TextMeshProUGUI timerTMP = null;
        if (timerTransform != null)
        {
            timerTMP = timerTransform.GetComponentInChildren<TextMeshProUGUI>();
        }

        // ── Find or create DrawButtonController ──────────────────────

        var controller = Object.FindFirstObjectByType<DrawButtonController>();
        if (controller == null)
        {
            // Place it on the Lobby panel itself (not a separate GameObject)
            Undo.RecordObject(lobbyPanel.gameObject, "Add DrawButtonController");
            controller = Undo.AddComponent<DrawButtonController>(lobbyPanel.gameObject);
            Debug.Log("[SetupLobbyPanel] Created DrawButtonController on Lobby panel");
        }

        // ── Assign serialized references ─────────────────────────────

        Undo.RecordObject(controller, "Wire DrawButtonController references");

        var so = new SerializedObject(controller);

        var drawButtonProp = so.FindProperty("drawButton");
        if (drawButtonProp != null)
            drawButtonProp.objectReferenceValue = battleButton;

        var buttonTextProp = so.FindProperty("buttonText");
        if (buttonTextProp != null)
            buttonTextProp.objectReferenceValue = buttonTMP;

        var timerBubbleProp = so.FindProperty("timerBubble");
        if (timerBubbleProp != null)
            timerBubbleProp.objectReferenceValue = timerBubble;

        var timerTextProp = so.FindProperty("timerText");
        if (timerTextProp != null)
            timerTextProp.objectReferenceValue = timerTMP;

        so.ApplyModifiedProperties();

        // ── Wire Button_Battle onClick → OnDrawButtonClicked (persistent) ──

        // Clear existing persistent listeners to avoid duplicates
        int existingCount = battleButton.onClick.GetPersistentEventCount();
        for (int i = existingCount - 1; i >= 0; i--)
        {
            UnityEventTools.RemovePersistentListener(battleButton.onClick, i);
        }

        // Add persistent listener (shows in Inspector)
        UnityEventTools.AddPersistentListener(
            battleButton.onClick,
            new UnityAction(controller.OnDrawButtonClicked));

        EditorUtility.SetDirty(battleButton);
        Debug.Log("[SetupLobbyPanel] Wired Button_Battle onClick → DrawButtonController.OnDrawButtonClicked (persistent)");

        // ── Disable Button_Stage ─────────────────────────────────────

        Transform stageTransform = lobbyPanel.Get("Button_Stage");
        if (stageTransform == null)
            stageTransform = FindChildRecursive(lobbyPanel.transform, "Button_Stage");

        if (stageTransform != null)
        {
            Undo.RecordObject(stageTransform.gameObject, "Disable Button_Stage");
            stageTransform.gameObject.SetActive(false);
            EditorUtility.SetDirty(stageTransform.gameObject);
            Debug.Log("[SetupLobbyPanel] Disabled Button_Stage");
        }

        // ── Mark dirty and log summary ───────────────────────────────

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(lobbyPanel.gameObject);

        Debug.Log($"[SetupLobbyPanel] Done! Lobby panel wired:\n" +
                  $"  Draw Button: {(battleButton != null ? "OK" : "MISSING")}\n" +
                  $"  Button Text: {(buttonTMP != null ? "OK" : "MISSING")}\n" +
                  $"  Timer Bubble: {(timerBubble != null ? "OK" : "MISSING")}\n" +
                  $"  Timer Text: {(timerTMP != null ? "OK" : "MISSING")}\n" +
                  $"  Stage Button: {(stageTransform != null ? "Disabled" : "Not found")}");

        Selection.activeGameObject = controller.gameObject;
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
#endif
