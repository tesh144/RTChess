#pragma warning disable CS0414, CS0219, CS0618
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using ClockworkCraft;

[CustomEditor(typeof(POIManager))]
public class POIManagerEditor : Editor
{
    private bool[] foldouts = new bool[0];
    private SerializedObject dbSerializedObj;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Draw default POIManager fields
        DrawPropertiesExcluding(serializedObject, "m_Script");

        // Draw POIDatabase entries inline
        SerializedProperty dbProp = serializedObject.FindProperty("poiDatabase");
        if (dbProp.objectReferenceValue != null)
        {
            POIDatabase db = (POIDatabase)dbProp.objectReferenceValue;
            if (dbSerializedObj == null || dbSerializedObj.targetObject != db)
                dbSerializedObj = new SerializedObject(db);

            dbSerializedObj.Update();
            SerializedProperty entriesProp = dbSerializedObj.FindProperty("entries");

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Points of Interest", EditorStyles.boldLabel);

            if (foldouts.Length != entriesProp.arraySize)
                foldouts = new bool[entriesProp.arraySize];

            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                SerializedProperty entry = entriesProp.GetArrayElementAtIndex(i);

                SerializedProperty activeProp    = entry.FindPropertyRelative("active");
                SerializedProperty typeNameProp  = entry.FindPropertyRelative("typeName");
                SerializedProperty labelProp     = entry.FindPropertyRelative("label");
                SerializedProperty groupProp     = entry.FindPropertyRelative("groupingType");
                SerializedProperty quantityProp  = entry.FindPropertyRelative("quantityMinimum");
                SerializedProperty tierProp      = entry.FindPropertyRelative("tier");
                SerializedProperty rewardTypeProp = entry.FindPropertyRelative("rewardType");
                SerializedProperty rewardQtyProp  = entry.FindPropertyRelative("rewardQuantity");

                string entryName = string.IsNullOrEmpty(typeNameProp.stringValue)
                    ? $"Entry {i}"
                    : typeNameProp.stringValue;
                string tierLabel = ((POITier)tierProp.enumValueIndex).ToString();
                bool isActive = activeProp.boolValue;

                // ── Header bar ──
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                // Active toggle
                activeProp.boolValue = EditorGUILayout.Toggle(activeProp.boolValue, GUILayout.Width(16));

                // Color dot
                Color dotColor = tierProp.enumValueIndex == 0 ? new Color(1f, 0.84f, 0f)
                               : tierProp.enumValueIndex == 2 ? new Color(0.9f, 0.2f, 0.2f)
                               : Color.grey;
                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = dotColor;
                GUILayout.Box("", GUILayout.Width(14), GUILayout.Height(14));
                GUI.backgroundColor = prev;

                // Foldout
                string header = isActive
                    ? $"{entryName} — \"{labelProp.stringValue}\" ({tierLabel})"
                    : $"{entryName} (inactive)";
                foldouts[i] = EditorGUILayout.Foldout(foldouts[i], header, true, EditorStyles.boldLabel);

                // Delete
                if (GUILayout.Button("✕", GUILayout.Width(22)))
                {
                    entriesProp.DeleteArrayElementAtIndex(i);
                    dbSerializedObj.ApplyModifiedProperties();
                    return;
                }

                EditorGUILayout.EndHorizontal();

                if (foldouts[i])
                {
                    EditorGUI.indentLevel++;

                    EditorGUILayout.PropertyField(typeNameProp, new GUIContent("Object"));
                    EditorGUILayout.PropertyField(labelProp, new GUIContent("Name"));
                    EditorGUILayout.PropertyField(groupProp, new GUIContent("Grouping"));
                    EditorGUILayout.PropertyField(quantityProp, new GUIContent("Quantity Minimum"));
                    EditorGUILayout.PropertyField(tierProp, new GUIContent("Color"));
                    EditorGUILayout.PropertyField(rewardTypeProp, new GUIContent("Reward Type"));
                    EditorGUILayout.PropertyField(rewardQtyProp, new GUIContent("Reward Quantity"));

                    EditorGUI.indentLevel--;
                    EditorGUILayout.Space(4);
                }
            }

            // Buttons
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Add POI Entry"))
            {
                entriesProp.InsertArrayElementAtIndex(entriesProp.arraySize);
                var newEntry = entriesProp.GetArrayElementAtIndex(entriesProp.arraySize - 1);
                newEntry.FindPropertyRelative("active").boolValue = true;
                newEntry.FindPropertyRelative("typeName").stringValue = "";
                newEntry.FindPropertyRelative("label").stringValue = "";
                newEntry.FindPropertyRelative("groupingType").enumValueIndex = 0;
                newEntry.FindPropertyRelative("quantityMinimum").intValue = 1;
                newEntry.FindPropertyRelative("tier").enumValueIndex = 1;
                newEntry.FindPropertyRelative("rewardType").enumValueIndex = 0;
                newEntry.FindPropertyRelative("rewardQuantity").intValue = 1;
            }
            if (GUILayout.Button("Sync from Sheet"))
            {
                EditorApplication.ExecuteMenuItem("Tools/ClockworkCraft/Sheet Sync");
            }
            EditorGUILayout.EndHorizontal();

            dbSerializedObj.ApplyModifiedProperties();
        }
        else
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Assign a POIDatabase to see entries here.", MessageType.Info);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
