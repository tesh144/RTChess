#pragma warning disable CS0414, CS0219, CS0618
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using ClockworkCraft;

[CustomEditor(typeof(POIDatabase))]
public class POIDatabaseEditor : Editor
{
    private bool[] foldouts = new bool[0];

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        POIDatabase db = (POIDatabase)target;

        SerializedProperty entriesProp = serializedObject.FindProperty("entries");

        EditorGUILayout.LabelField("Points of Interest", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Matches the PointsOfInterest Google Sheet. Sync via SheetSyncEditor or edit here.",
            MessageType.Info);
        EditorGUILayout.Space();

        // Resize foldouts array
        if (foldouts.Length != entriesProp.arraySize)
            foldouts = new bool[entriesProp.arraySize];

        for (int i = 0; i < entriesProp.arraySize; i++)
        {
            SerializedProperty entry = entriesProp.GetArrayElementAtIndex(i);

            SerializedProperty activeProp   = entry.FindPropertyRelative("active");
            SerializedProperty typeNameProp = entry.FindPropertyRelative("typeName");
            SerializedProperty labelProp    = entry.FindPropertyRelative("label");
            SerializedProperty groupProp    = entry.FindPropertyRelative("groupingType");
            SerializedProperty quantityProp = entry.FindPropertyRelative("quantityMinimum");
            SerializedProperty tierProp     = entry.FindPropertyRelative("tier");
            SerializedProperty rewardTypeProp = entry.FindPropertyRelative("rewardType");
            SerializedProperty rewardQtyProp  = entry.FindPropertyRelative("rewardQuantity");

            string entryName = string.IsNullOrEmpty(typeNameProp.stringValue)
                ? $"Entry {i}"
                : typeNameProp.stringValue;
            string tierLabel = ((POITier)tierProp.enumValueIndex).ToString();
            bool isActive = activeProp.boolValue;

            // Header with foldout — show name, tier color dot, and active toggle
            EditorGUILayout.BeginHorizontal();

            // Active toggle
            activeProp.boolValue = EditorGUILayout.Toggle(activeProp.boolValue, GUILayout.Width(16));

            // Color indicator
            Color dotColor = tierProp.enumValueIndex == 0 ? new Color(1f, 0.84f, 0f)  // Gold
                           : tierProp.enumValueIndex == 2 ? new Color(0.9f, 0.2f, 0.2f) // Red
                           : Color.grey;
            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = dotColor;
            GUILayout.Box("", GUILayout.Width(14), GUILayout.Height(14));
            GUI.backgroundColor = prev;

            // Foldout
            string header = isActive
                ? $"{entryName} — \"{labelProp.stringValue}\" ({tierLabel})"
                : $"{entryName} (inactive)";
            foldouts[i] = EditorGUILayout.Foldout(foldouts[i], header, true);

            // Delete button
            if (GUILayout.Button("✕", GUILayout.Width(22)))
            {
                entriesProp.DeleteArrayElementAtIndex(i);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            EditorGUILayout.EndHorizontal();

            if (foldouts[i])
            {
                EditorGUI.indentLevel++;

                // Row 1: Object + Name (matching sheet: Object, Name columns)
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(typeNameProp, new GUIContent("Object"));
                EditorGUILayout.PropertyField(labelProp, new GUIContent("Name"));
                EditorGUILayout.EndHorizontal();

                // Row 2: Grouping + Quantity (matching sheet: Grouping, Quantity Minimum)
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(groupProp, new GUIContent("Grouping"));
                EditorGUILayout.PropertyField(quantityProp, new GUIContent("Qty Min"));
                EditorGUILayout.EndHorizontal();

                // Row 3: Color + Reward (matching sheet: Color, Reward Type, Reward Quantity)
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(tierProp, new GUIContent("Color"));
                EditorGUILayout.PropertyField(rewardTypeProp, new GUIContent("Reward"));
                EditorGUILayout.PropertyField(rewardQtyProp, new GUIContent("Qty"), GUILayout.Width(80));
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
                EditorGUILayout.Space(4);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Add Entry"))
        {
            entriesProp.InsertArrayElementAtIndex(entriesProp.arraySize);
            var newEntry = entriesProp.GetArrayElementAtIndex(entriesProp.arraySize - 1);
            newEntry.FindPropertyRelative("active").boolValue = true;
            newEntry.FindPropertyRelative("typeName").stringValue = "";
            newEntry.FindPropertyRelative("label").stringValue = "";
            newEntry.FindPropertyRelative("groupingType").enumValueIndex = 0;
            newEntry.FindPropertyRelative("quantityMinimum").intValue = 1;
            newEntry.FindPropertyRelative("tier").enumValueIndex = 1; // Grey default
            newEntry.FindPropertyRelative("rewardType").enumValueIndex = 0;
            newEntry.FindPropertyRelative("rewardQuantity").intValue = 1;
        }
        if (GUILayout.Button("Sync from Sheet"))
        {
            EditorApplication.ExecuteMenuItem("Tools/ClockworkCraft/Sheet Sync");
        }
        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
