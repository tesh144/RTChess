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
///   2. Finds Button_Main (draw/sacrifice button) and wires all references
///   3. Resolves Label_Tag03_Time (cooldown timer) and Label_Tag03_Buy (cost/upgrade)
///   4. Wires onClick → DrawButtonController.OnDrawButtonClicked() (persistent)
///   5. Disables Button_Stage
///
/// Button_Main hierarchy:
///   ├── Label_Tag03_Time  (Icon, Text)
///   ├── Label_Tag03_Buy   (Text, Icon, Cost)
///   ├── Icon              (crown)
///   └── Text              ("Level X")
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

        // ── Find Button_Main (draw button) ────────────────────────────

        Transform buttonMainTransform = FindChildRecursive(lobbyPanel.transform, "Button_Main");

        // Fallback to Button_Battle for backward compat
        if (buttonMainTransform == null)
            buttonMainTransform = FindChildRecursive(lobbyPanel.transform, "Button_Battle");

        if (buttonMainTransform == null)
        {
            Debug.LogError("[SetupLobbyPanel] Button_Main not found in Lobby panel!");
            return;
        }

        Button mainButton = buttonMainTransform.GetComponent<Button>();
        if (mainButton == null)
        {
            Debug.LogError("[SetupLobbyPanel] Button_Main has no Button component!");
            return;
        }

        // ── Find button text and icon ──────────────────────────────────

        Transform textTransform = buttonMainTransform.Find("Text");
        TextMeshProUGUI buttonTMP = textTransform != null
            ? textTransform.GetComponent<TextMeshProUGUI>()
            : null;

        Transform iconTransform = buttonMainTransform.Find("Icon");
        GameObject buttonIcon = iconTransform != null ? iconTransform.gameObject : null;

        // ── Find Label_Tag03_Time (cooldown timer) ─────────────────────

        Transform timerTransform = buttonMainTransform.Find("Label_Tag03_Time");
        if (timerTransform == null)
            timerTransform = FindChildRecursive(buttonMainTransform, "Label_Tag03_Time");

        GameObject timerBubble = timerTransform != null ? timerTransform.gameObject : null;

        TextMeshProUGUI timerTMP = null;
        if (timerTransform != null)
        {
            // The Text child inside Label_Tag03_Time
            Transform timerTextT = timerTransform.Find("Text");
            if (timerTextT != null)
                timerTMP = timerTextT.GetComponent<TextMeshProUGUI>();
            if (timerTMP == null)
                timerTMP = timerTransform.GetComponentInChildren<TextMeshProUGUI>();
        }

        // ── Find Label_Tag03_Buy (cost/upgrade tag) ────────────────────

        Transform buyTransform = buttonMainTransform.Find("Label_Tag03_Buy");
        if (buyTransform == null)
            buyTransform = FindChildRecursive(buttonMainTransform, "Label_Tag03_Buy");

        GameObject costBubble = buyTransform != null ? buyTransform.gameObject : null;

        TextMeshProUGUI costTMP = null;
        Image costIconImage = null;
        if (buyTransform != null)
        {
            // Cost number text — look for child named "Cost"
            Transform costTransform = buyTransform.Find("Cost");
            if (costTransform != null)
                costTMP = costTransform.GetComponent<TextMeshProUGUI>();

            // Cost icon — look for child named "Icon"
            Transform costIconTransform = buyTransform.Find("Icon");
            if (costIconTransform != null)
                costIconImage = costIconTransform.GetComponent<Image>();
        }

        // ── Find or create DrawButtonController ──────────────────────

        var controller = Object.FindFirstObjectByType<DrawButtonController>();
        if (controller == null)
        {
            Undo.RecordObject(lobbyPanel.gameObject, "Add DrawButtonController");
            controller = Undo.AddComponent<DrawButtonController>(lobbyPanel.gameObject);
            Debug.Log("[SetupLobbyPanel] Created DrawButtonController on Lobby panel");
        }

        // ── Assign serialized references ─────────────────────────────

        Undo.RecordObject(controller, "Wire DrawButtonController references");

        var so = new SerializedObject(controller);

        SetRef(so, "drawButton", mainButton);
        SetRef(so, "buttonText", buttonTMP);
        SetRef(so, "buttonIcon", buttonIcon);
        SetRef(so, "timerBubble", timerBubble);
        SetRef(so, "timerText", timerTMP);
        SetRef(so, "costBubble", costBubble);
        SetRef(so, "costNumberText", costTMP);
        SetRef(so, "costIcon", costIconImage);

        so.ApplyModifiedProperties();

        // ── Wire onClick → OnDrawButtonClicked (persistent) ──────────

        int existingCount = mainButton.onClick.GetPersistentEventCount();
        for (int i = existingCount - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(mainButton.onClick, i);

        UnityEventTools.AddPersistentListener(
            mainButton.onClick,
            new UnityAction(controller.OnDrawButtonClicked));

        EditorUtility.SetDirty(mainButton);
        Debug.Log("[SetupLobbyPanel] Wired Button_Main onClick → DrawButtonController.OnDrawButtonClicked");

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
                  $"  Draw Button:   {(mainButton != null ? "OK" : "MISSING")}\n" +
                  $"  Button Text:   {(buttonTMP != null ? "OK" : "MISSING")}\n" +
                  $"  Button Icon:   {(buttonIcon != null ? "OK" : "MISSING")}\n" +
                  $"  Timer Bubble:  {(timerBubble != null ? "OK" : "MISSING")}\n" +
                  $"  Timer Text:    {(timerTMP != null ? "OK" : "MISSING")}\n" +
                  $"  Cost Bubble:   {(costBubble != null ? "OK" : "MISSING")}\n" +
                  $"  Cost Number:   {(costTMP != null ? "OK" : "MISSING")}\n" +
                  $"  Cost Icon:     {(costIconImage != null ? "OK" : "MISSING")}\n" +
                  $"  Stage Button:  {(stageTransform != null ? "Disabled" : "Not found")}");

        Selection.activeGameObject = controller.gameObject;
    }

    private static void SetRef(SerializedObject so, string propName, Object value)
    {
        var prop = so.FindProperty(propName);
        if (prop != null)
            prop.objectReferenceValue = value;
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
