#pragma warning disable CS0414, CS0219, CS0618
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using ClockworkGrid;

/// <summary>
/// Custom Inspector for UIPanel that adds a "Rescan Hierarchy" button.
/// Rescanning is additive-safe — existing elements keep their paths/names,
/// new children get appended, removed children get cleaned out.
/// Serialized references from other scripts (like TitleScreenController)
/// remain valid because they point to the Transform directly, not by index.
/// </summary>
[CustomEditor(typeof(UIPanel))]
public class UIPanelEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        UIPanel panel = (UIPanel)target;

        EditorGUILayout.Space(10);

        // ── Rescan Button ────────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("Rescan Hierarchy", GUILayout.Height(30)))
        {
            Undo.RecordObject(panel, "Rescan UIPanel Hierarchy");
            panel.ScanHierarchy();
            EditorUtility.SetDirty(panel);
            Debug.Log($"[UIPanel:{panel.pageId}] Rescanned — {panel.ElementCount} elements indexed.");
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        // ── Info ─────────────────────────────────────────────────────
        EditorGUILayout.HelpBox(
            $"{panel.ElementCount} elements indexed.\n" +
            "Rescanning is safe — it rebuilds the element list from the current hierarchy. " +
            "Serialized references from other scripts (buttons, images, etc.) are unaffected.",
            MessageType.Info);

        // ── Debug Log Button ─────────────────────────────────────────
        if (GUILayout.Button("Log All Elements"))
        {
            panel.DebugLogHierarchy();
        }
    }
}
#endif
