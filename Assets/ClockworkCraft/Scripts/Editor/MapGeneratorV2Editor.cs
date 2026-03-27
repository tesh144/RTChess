#pragma warning disable CS0414, CS0219, CS0618
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using LittleCafe;

namespace ClockworkCraft
{
    [CustomEditor(typeof(MapGeneratorV2))]
    public class MapGeneratorV2Editor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            MapGeneratorV2 gen = (MapGeneratorV2)target;

            // Draw everything except custom-drawn fields
            DrawPropertiesExcluding(serializedObject,
                "centerEnvironmentName", "mapDensity", "spawnEntries",
                "unitSpawnEntries", "corruptionSpawnEntries");

            // ── Center dropdown ──────────────────────────────────────
            EditorGUILayout.Space();
            SerializedProperty centerProp = serializedObject.FindProperty("centerEnvironmentName");

            if (gen.environmentDatabase != null && gen.environmentDatabase.AllEnvironment.Count > 0)
            {
                var entries = gen.environmentDatabase.AllEnvironment;
                string[] names = entries.Select(e => e.assetName).ToArray();
                int currentIndex = System.Array.IndexOf(names, centerProp.stringValue);
                if (currentIndex < 0) currentIndex = 0;

                int newIndex = EditorGUILayout.Popup("Center Environment", currentIndex, names);
                centerProp.stringValue = names[newIndex];
            }
            else
            {
                EditorGUILayout.PropertyField(centerProp, new GUIContent("Center Environment"));
                if (gen.environmentDatabase == null)
                    EditorGUILayout.HelpBox("Assign an EnvironmentDatabase to get a dropdown.", MessageType.Info);
            }

            // ══════════════════════════════════════════════════════════
            // Spawn Distribution (combined environment + units + corruption)
            // ══════════════════════════════════════════════════════════
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Spawn Distribution", EditorStyles.boldLabel);

            // ── Density slider ───────────────────────────────────────
            SerializedProperty densityProp = serializedObject.FindProperty("mapDensity");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(densityProp, new GUIContent("Map Density",
                "Fraction of available tiles filled with objects (environment + units)."));
            EditorGUILayout.LabelField($"{densityProp.floatValue * 100f:F0}%", GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();

            // Coverage info
            {
                int totalTiles = gen.mapWidth * gen.mapHeight;
                int clearingSize = (2 * gen.clearingRadius + 1) * (2 * gen.clearingRadius + 1);
                int available = totalTiles - clearingSize;
                int totalBudget = Mathf.RoundToInt(available * densityProp.floatValue);
                float coveragePercent = ((float)totalBudget / available) * 100f;

                EditorGUILayout.HelpBox(
                    $"~{coveragePercent:F1}% map coverage (~{totalBudget} / {available} tiles)",
                    MessageType.Info);
            }

            // ── Sync button ──────────────────────────────────────────
            bool hasEnvDB = gen.environmentDatabase != null;
            bool hasUnitDB = gen.unitDatabase != null;

            if (hasEnvDB || hasUnitDB)
            {
                if (GUILayout.Button("Sync from Database"))
                {
                    Undo.RecordObject(gen, "Sync Spawn Entries");

                    // Auto-find databases if not assigned
                    if (gen.environmentDatabase == null)
                    {
                        string[] guids = AssetDatabase.FindAssets("t:EnvironmentDatabase");
                        if (guids.Length > 0)
                        {
                            gen.environmentDatabase = AssetDatabase.LoadAssetAtPath<EnvironmentDatabase>(
                                AssetDatabase.GUIDToAssetPath(guids[0]));
                            hasEnvDB = gen.environmentDatabase != null;
                        }
                    }
                    if (gen.unitDatabase == null)
                    {
                        string[] guids = AssetDatabase.FindAssets("t:UnitDatabase");
                        if (guids.Length > 0)
                        {
                            gen.unitDatabase = AssetDatabase.LoadAssetAtPath<UnitDatabase>(
                                AssetDatabase.GUIDToAssetPath(guids[0]));
                            hasUnitDB = gen.unitDatabase != null;
                        }
                    }

                    gen.SyncSpawnEntries();
                    gen.SyncCorruptionSpawnEntries();
                    EditorUtility.SetDirty(gen);
                    serializedObject.Update();
                }

                bool emptyEnv = hasEnvDB && gen.spawnEntries.Count == 0 && gen.environmentDatabase.AllEnvironment.Count > 0;
                bool emptyUnit = hasUnitDB && gen.unitSpawnEntries.Count == 0 && gen.unitDatabase.Count > 0;
                bool emptyCorruption = hasUnitDB && gen.corruptionSpawnEntries.Count == 0;
                if (emptyEnv || emptyUnit || emptyCorruption)
                {
                    EditorGUILayout.HelpBox("No spawn entries. Click 'Sync from Database' to populate.", MessageType.Warning);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Assign an EnvironmentDatabase and/or UnitDatabase to configure spawning, or click Sync to auto-detect.", MessageType.Info);
                if (GUILayout.Button("Sync from Database"))
                {
                    Undo.RecordObject(gen, "Sync Spawn Entries (auto-find)");

                    string[] envGuids = AssetDatabase.FindAssets("t:EnvironmentDatabase");
                    if (envGuids.Length > 0)
                        gen.environmentDatabase = AssetDatabase.LoadAssetAtPath<EnvironmentDatabase>(
                            AssetDatabase.GUIDToAssetPath(envGuids[0]));

                    string[] unitGuids = AssetDatabase.FindAssets("t:UnitDatabase");
                    if (unitGuids.Length > 0)
                        gen.unitDatabase = AssetDatabase.LoadAssetAtPath<UnitDatabase>(
                            AssetDatabase.GUIDToAssetPath(unitGuids[0]));

                    if (gen.environmentDatabase != null || gen.unitDatabase != null)
                    {
                        gen.SyncSpawnEntries();
                        gen.SyncCorruptionSpawnEntries();
                        EditorUtility.SetDirty(gen);
                        serializedObject.Update();
                    }
                }
            }

            // ── Collect all names for Edge/Border dropdowns ──────────
            var allNames = new List<string>();
            foreach (var e in gen.spawnEntries)
                if (!string.IsNullOrEmpty(e.environmentName)) allNames.Add(e.environmentName);
            foreach (var e in gen.unitSpawnEntries)
                if (!string.IsNullOrEmpty(e.unitName)) allNames.Add(e.unitName);
            foreach (var e in gen.corruptionSpawnEntries)
                if (!string.IsNullOrEmpty(e.entityName)) allNames.Add(e.entityName);
            string[] allNameArray = allNames.ToArray();

            // ── Combined weight + budget for all entries ────────────
            float totalCombinedWeight = 0f;
            foreach (var e in gen.spawnEntries)
                if (e.spawnWeight > 0f) totalCombinedWeight += e.spawnWeight;
            foreach (var e in gen.unitSpawnEntries)
                if (e.spawnWeight > 0f) totalCombinedWeight += e.spawnWeight;

            int totalTilesForBudget = gen.mapWidth * gen.mapHeight
                - (2 * gen.clearingRadius + 1) * (2 * gen.clearingRadius + 1);
            int totalSpawnBudget = Mathf.RoundToInt(totalTilesForBudget * densityProp.floatValue);

            // ── Environment entry cards ──────────────────────────────
            if (hasEnvDB && gen.spawnEntries.Count > 0)
            {
                SerializedProperty entriesProp = serializedObject.FindProperty("spawnEntries");
                for (int i = 0; i < entriesProp.arraySize; i++)
                {
                    SerializedProperty entryProp   = entriesProp.GetArrayElementAtIndex(i);
                    SerializedProperty nameProp     = entryProp.FindPropertyRelative("environmentName");
                    SerializedProperty modeProp     = entryProp.FindPropertyRelative("spawnMode");
                    SerializedProperty weightProp   = entryProp.FindPropertyRelative("spawnWeight");
                    SerializedProperty spacingProp  = entryProp.FindPropertyRelative("minSpacing");

                    string entryName = nameProp.stringValue;
                    if (string.IsNullOrEmpty(entryName)) entryName = "(unnamed)";

                    SpawnMode mode = (SpawnMode)modeProp.enumValueIndex;
                    float relPct = totalCombinedWeight > 0f ? weightProp.floatValue / totalCombinedWeight * 100f : 0f;
                    int entryBudget = totalCombinedWeight > 0f
                        ? Mathf.RoundToInt((weightProp.floatValue / totalCombinedWeight) * totalSpawnBudget)
                        : 0;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"\u2618  {entryName}  \u2014  {relPct:F0}% (~{entryBudget} tiles)", EditorStyles.boldLabel);

                    EditorGUI.indentLevel++;
                    DrawSpawnModeFields(entryProp, modeProp, weightProp, spacingProp, mode, allNameArray, "edgeBorderOf", "Border Of");
                    EditorGUI.indentLevel--;

                    EditorGUILayout.EndVertical();
                }
            }

            // ── Unit entry cards ─────────────────────────────────────
            if (!hasUnitDB && hasEnvDB)
            {
                EditorGUILayout.HelpBox(
                    "Unit Database not assigned. Click 'Sync from Database' to auto-detect, or assign manually in the Databases section above.",
                    MessageType.Warning);
            }

            if (hasUnitDB && gen.unitSpawnEntries.Count > 0)
            {
                SerializedProperty unitEntriesProp = serializedObject.FindProperty("unitSpawnEntries");
                for (int i = 0; i < unitEntriesProp.arraySize; i++)
                {
                    SerializedProperty entryProp   = unitEntriesProp.GetArrayElementAtIndex(i);
                    SerializedProperty nameProp     = entryProp.FindPropertyRelative("unitName");
                    SerializedProperty modeProp     = entryProp.FindPropertyRelative("spawnMode");
                    SerializedProperty weightProp   = entryProp.FindPropertyRelative("spawnWeight");
                    SerializedProperty spacingProp  = entryProp.FindPropertyRelative("minSpacing");

                    string entryName = nameProp.stringValue;
                    if (string.IsNullOrEmpty(entryName)) entryName = "(unnamed)";

                    SpawnMode mode = (SpawnMode)modeProp.enumValueIndex;
                    float relPct = totalCombinedWeight > 0f ? weightProp.floatValue / totalCombinedWeight * 100f : 0f;
                    int entryBudget = totalCombinedWeight > 0f
                        ? Mathf.RoundToInt((weightProp.floatValue / totalCombinedWeight) * totalSpawnBudget)
                        : 0;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"\u2694  {entryName}  \u2014  {relPct:F0}% (~{entryBudget} tiles)", EditorStyles.boldLabel);

                    EditorGUI.indentLevel++;
                    DrawSpawnModeFields(entryProp, modeProp, weightProp, spacingProp, mode, allNameArray, "edgeBorderOf", "Spawn Near");
                    EditorGUI.indentLevel--;

                    EditorGUILayout.EndVertical();
                }
            }

            // ── Corruption entry cards ───────────────────────────────
            if (hasUnitDB && gen.corruptionSpawnEntries.Count > 0)
            {
                SerializedProperty corruptionEntriesProp = serializedObject.FindProperty("corruptionSpawnEntries");
                for (int i = 0; i < corruptionEntriesProp.arraySize; i++)
                {
                    SerializedProperty entryProp    = corruptionEntriesProp.GetArrayElementAtIndex(i);
                    SerializedProperty nameProp      = entryProp.FindPropertyRelative("entityName");
                    SerializedProperty modeProp      = entryProp.FindPropertyRelative("spawnMode");
                    SerializedProperty countProp     = entryProp.FindPropertyRelative("spawnCount");
                    SerializedProperty minDistProp   = entryProp.FindPropertyRelative("clearFromCenter");
                    SerializedProperty spacingProp   = entryProp.FindPropertyRelative("minSpacing");

                    string entryName = nameProp.stringValue;
                    if (string.IsNullOrEmpty(entryName)) entryName = "(unnamed)";

                    SpawnMode mode = (SpawnMode)modeProp.enumValueIndex;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"\u2620  {entryName}  \u2014  {countProp.intValue} hearts", EditorStyles.boldLabel);

                    EditorGUI.indentLevel++;

                    EditorGUILayout.PropertyField(modeProp, new GUIContent("Spawn Mode"));
                    EditorGUILayout.PropertyField(countProp, new GUIContent("Spawn Count",
                        "Exact number of this entity to place on the map."));
                    EditorGUILayout.PropertyField(minDistProp, new GUIContent("Clear From Center",
                        "Won't appear within this many tiles of the starting gold mine."));

                    switch (mode)
                    {
                        case SpawnMode.Scattered:
                            EditorGUILayout.PropertyField(spacingProp, new GUIContent("Min Spacing"));
                            break;

                        case SpawnMode.Clustered:
                            SerializedProperty fragProp   = entryProp.FindPropertyRelative("fragmentation");
                            SerializedProperty spreadProp = entryProp.FindPropertyRelative("clusterSpread");

                            float f = fragProp.floatValue;
                            int previewClusters = Mathf.Max(2, Mathf.RoundToInt(Mathf.Lerp(2f, 12f, f)));
                            float loosePct = Mathf.Lerp(0f, 40f, f);

                            EditorGUILayout.PropertyField(fragProp, new GUIContent("Fragmentation",
                                "0 = few large cohesive blobs. 1 = many small clusters with loose break-off pieces."));
                            EditorGUILayout.HelpBox(
                                $"~{previewClusters} blobs + {loosePct:F0}% loose pieces",
                                MessageType.None);
                            EditorGUILayout.PropertyField(spreadProp, new GUIContent("Spread",
                                "Blob shape: 0 = stringy/organic, 1 = round/blobby."));
                            break;

                        case SpawnMode.Edge:
                            SerializedProperty borderProp = entryProp.FindPropertyRelative("edgeBorderOf");
                            if (allNameArray.Length > 0)
                            {
                                int curIdx = System.Array.IndexOf(allNameArray, borderProp.stringValue);
                                if (curIdx < 0) curIdx = 0;
                                int newIdx = EditorGUILayout.Popup("Spawn Near", curIdx, allNameArray);
                                borderProp.stringValue = allNameArray[newIdx];
                            }
                            else
                            {
                                EditorGUILayout.PropertyField(borderProp, new GUIContent("Spawn Near"));
                            }
                            break;
                    }

                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndVertical();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Shared drawing logic for spawn mode fields (used by both environment and unit entries).
        /// </summary>
        void DrawSpawnModeFields(
            SerializedProperty entryProp,
            SerializedProperty modeProp,
            SerializedProperty weightProp,
            SerializedProperty spacingProp,
            SpawnMode mode,
            string[] allNames,
            string borderPropertyName,
            string borderLabel)
        {
            EditorGUILayout.PropertyField(modeProp, new GUIContent("Spawn Mode"));
            EditorGUILayout.PropertyField(weightProp, new GUIContent("Weight",
                "Relative weight (0-20). Determines this entry's share of the tile budget."));

            switch (mode)
            {
                case SpawnMode.Scattered:
                    SerializedProperty clearProp = entryProp.FindPropertyRelative("clearFromCenter");
                    if (clearProp != null)
                        EditorGUILayout.PropertyField(clearProp, new GUIContent("Clear From Center",
                            "Won't appear within this many tiles of the starting gold mine."));
                    EditorGUILayout.PropertyField(spacingProp, new GUIContent("Min Spacing"));
                    break;

                case SpawnMode.Clustered:
                    SerializedProperty fragProp   = entryProp.FindPropertyRelative("fragmentation");
                    SerializedProperty spreadProp = entryProp.FindPropertyRelative("clusterSpread");

                    float f = fragProp.floatValue;
                    int previewClusters = Mathf.Max(2, Mathf.RoundToInt(Mathf.Lerp(2f, 12f, f)));
                    float loosePct = Mathf.Lerp(0f, 40f, f);

                    EditorGUILayout.PropertyField(fragProp, new GUIContent("Fragmentation",
                        "0 = few large cohesive blobs. 1 = many small clusters with loose break-off pieces."));
                    EditorGUILayout.HelpBox(
                        $"~{previewClusters} blobs + {loosePct:F0}% loose pieces",
                        MessageType.None);
                    EditorGUILayout.PropertyField(spreadProp, new GUIContent("Spread",
                        "Blob shape: 0 = stringy/organic, 1 = round/blobby."));
                    break;

                case SpawnMode.Edge:
                    SerializedProperty borderProp = entryProp.FindPropertyRelative(borderPropertyName);
                    if (allNames.Length > 0)
                    {
                        int curIdx = System.Array.IndexOf(allNames, borderProp.stringValue);
                        if (curIdx < 0) curIdx = 0;
                        int newIdx = EditorGUILayout.Popup(borderLabel, curIdx, allNames);
                        borderProp.stringValue = allNames[newIdx];
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(borderProp, new GUIContent(borderLabel));
                    }
                    break;
            }
        }
    }
}
#endif
