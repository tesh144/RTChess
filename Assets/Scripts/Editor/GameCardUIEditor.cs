#pragma warning disable CS0414, CS0219, CS0618
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using ClockworkGrid;

/// <summary>
/// Custom Inspector for GameCardUI. Adds:
///   - Green "Rescan Hierarchy" button (re-indexes all children)
///   - Element count info box
///   - "Log All Elements" debug button
///
/// Same pattern as UIPanelEditor.
/// </summary>
[CustomEditor(typeof(GameCardUI))]
public class GameCardUIEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GameCardUI card = (GameCardUI)target;

        EditorGUILayout.Space(10);

        // Info box
        EditorGUILayout.HelpBox(
            $"Elements indexed: {card.ElementCount}",
            card.ElementCount > 0 ? MessageType.Info : MessageType.Warning);

        // Rescan button
        GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
        if (GUILayout.Button("Rescan Hierarchy", GUILayout.Height(30)))
        {
            Undo.RecordObject(card, "Rescan GameCardUI Hierarchy");
            card.ScanHierarchy();
            EditorUtility.SetDirty(card);
            Debug.Log($"[GameCardUI] Rescanned — {card.ElementCount} elements indexed");
        }
        GUI.backgroundColor = Color.white;

        // Log button
        if (card.ElementCount > 0)
        {
            if (GUILayout.Button("Log All Elements"))
            {
                card.DebugLogHierarchy();
            }
        }
    }
}
#endif
