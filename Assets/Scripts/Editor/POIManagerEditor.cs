#pragma warning disable CS0414, CS0219, CS0618
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using ClockworkCraft;

[CustomEditor(typeof(POIManager))]
public class POIManagerEditor : Editor
{
    private SerializedObject dbSerializedObj;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Draw default POIManager fields
        DrawPropertiesExcluding(serializedObject, "m_Script");

        // ── POIDatabase inline entries ────────────────────────────────
        SerializedProperty dbProp = serializedObject.FindProperty("poiDatabase");
        if (dbProp.objectReferenceValue == null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Assign a POIDatabase to see entries.", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        POIDatabase db = (POIDatabase)dbProp.objectReferenceValue;
        if (dbSerializedObj == null || dbSerializedObj.targetObject != db)
            dbSerializedObj = new SerializedObject(db);

        dbSerializedObj.Update();
        SerializedProperty entriesProp = dbSerializedObj.FindProperty("entries");

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Points of Interest", EditorStyles.boldLabel);

        // Sync button
        if (GUILayout.Button("Sync from Database"))
        {
            EditorApplication.ExecuteMenuItem("Tools/ClockworkCraft/Sheet Sync");
        }

        if (entriesProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No POI entries. Click 'Sync from Database' to populate from Google Sheets.", MessageType.Warning);
        }

        // ── Entry cards ──────────────────────────────────────────────
        for (int i = 0; i < entriesProp.arraySize; i++)
        {
            SerializedProperty entry = entriesProp.GetArrayElementAtIndex(i);

            SerializedProperty activeProp     = entry.FindPropertyRelative("active");
            SerializedProperty typeNameProp   = entry.FindPropertyRelative("typeName");
            SerializedProperty labelProp      = entry.FindPropertyRelative("label");
            SerializedProperty groupProp      = entry.FindPropertyRelative("groupingType");
            SerializedProperty quantityProp   = entry.FindPropertyRelative("quantityMinimum");
            SerializedProperty tierProp       = entry.FindPropertyRelative("tier");
            SerializedProperty rewardTypeProp = entry.FindPropertyRelative("rewardType");
            SerializedProperty rewardQtyProp  = entry.FindPropertyRelative("rewardQuantity");

            string entryName = string.IsNullOrEmpty(typeNameProp.stringValue)
                ? "(unnamed)" : typeNameProp.stringValue;
            POITier tier = (POITier)tierProp.enumValueIndex;
            POIGrouping grouping = (POIGrouping)groupProp.enumValueIndex;
            bool isActive = activeProp.boolValue;

            // Tier icon
            string tierIcon = tier == POITier.Gold ? "\u2B50" // star
                            : tier == POITier.Red  ? "\u2620" // skull
                            : "\u25C6"; // diamond

            // Summary for header
            string groupSummary = grouping == POIGrouping.Singular
                ? "1 required"
                : $"{quantityProp.intValue}+ {grouping}";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Header
            if (!isActive) GUI.enabled = false;
            EditorGUILayout.LabelField(
                $"{tierIcon}  {entryName} — \"{labelProp.stringValue}\" ({groupSummary})",
                EditorStyles.boldLabel);
            if (!isActive) GUI.enabled = true;

            EditorGUI.indentLevel++;

            // Active
            EditorGUILayout.PropertyField(activeProp, new GUIContent("Active"));

            // Object + Name
            EditorGUILayout.PropertyField(typeNameProp, new GUIContent("Object"));
            EditorGUILayout.PropertyField(labelProp, new GUIContent("Name"));

            // Grouping + Quantity
            EditorGUILayout.PropertyField(groupProp, new GUIContent("Grouping"));
            EditorGUILayout.PropertyField(quantityProp, new GUIContent("Quantity Minimum"));

            // Color
            EditorGUILayout.PropertyField(tierProp, new GUIContent("Color"));

            // Reward
            EditorGUILayout.PropertyField(rewardTypeProp, new GUIContent("Reward Type"));
            EditorGUILayout.PropertyField(rewardQtyProp, new GUIContent("Reward Quantity"));

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        dbSerializedObj.ApplyModifiedProperties();
        serializedObject.ApplyModifiedProperties();
    }
}
#endif
