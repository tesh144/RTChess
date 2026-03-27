#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using UnityEditor;
using LittleCafe;
using ClockworkCraft;

/// <summary>
/// Editor tool to add InteractionRegistry as a scene component on the CafeSceneSetupV2 GameObject.
/// Also provides a context menu to sync entries from databases.
/// </summary>
public class InteractionRegistrySetup
{
    [MenuItem("Tools/ClockworkCraft/Setup Interaction Registry")]
    public static void SetupInteractionRegistry()
    {
        // Find CafeSceneSetupV2 in the scene
        CafeSceneSetupV2 sceneSetup = Object.FindFirstObjectByType<CafeSceneSetupV2>();
        if (sceneSetup == null)
        {
            EditorUtility.DisplayDialog(
                "Interaction Registry Setup",
                "No CafeSceneSetupV2 found in scene. Please add one first.",
                "OK");
            return;
        }

        // Check if InteractionRegistry already exists on it
        InteractionRegistry existing = sceneSetup.GetComponent<InteractionRegistry>();
        if (existing != null)
        {
            // Offer to sync entries
            bool sync = EditorUtility.DisplayDialog(
                "Interaction Registry",
                "InteractionRegistry already exists on this GameObject.\n\nWould you like to sync entries from databases?",
                "Sync Entries", "Cancel");

            if (sync)
            {
                SyncEntries(existing);
            }

            Selection.activeGameObject = sceneSetup.gameObject;
            return;
        }

        // Check if one exists elsewhere in the scene
        InteractionRegistry otherRegistry = Object.FindFirstObjectByType<InteractionRegistry>();
        if (otherRegistry != null)
        {
            bool move = EditorUtility.DisplayDialog(
                "Interaction Registry",
                $"InteractionRegistry found on '{otherRegistry.gameObject.name}'.\n\nMove it to the CafeSceneSetupV2 GameObject instead?",
                "Move", "Keep Where It Is");

            if (move)
            {
                // Copy data, destroy old, create new
                var entries = otherRegistry.AllEntries;
                Undo.DestroyObjectImmediate(otherRegistry);

                InteractionRegistry newRegistry = Undo.AddComponent<InteractionRegistry>(sceneSetup.gameObject);
                // Entries will be populated on next play or via sync
                SyncEntries(newRegistry);
                EditorUtility.SetDirty(sceneSetup.gameObject);

                Debug.Log($"[InteractionRegistrySetup] Moved InteractionRegistry to '{sceneSetup.gameObject.name}'");
            }

            Selection.activeGameObject = move ? sceneSetup.gameObject : otherRegistry.gameObject;
            return;
        }

        // Add new InteractionRegistry component
        Undo.AddComponent<InteractionRegistry>(sceneSetup.gameObject);
        EditorUtility.SetDirty(sceneSetup.gameObject);

        // Try to auto-assign databases
        InteractionRegistry registry = sceneSetup.GetComponent<InteractionRegistry>();
        if (registry != null)
        {
            AutoAssignDatabases(registry);
            SyncEntries(registry);
        }

        Selection.activeGameObject = sceneSetup.gameObject;
        Debug.Log($"[InteractionRegistrySetup] Added InteractionRegistry to '{sceneSetup.gameObject.name}'. Assign databases and configure unlock states in Inspector.");
    }

    [MenuItem("Tools/ClockworkCraft/Sync Interaction Registry Entries")]
    public static void SyncFromMenu()
    {
        InteractionRegistry registry = Object.FindFirstObjectByType<InteractionRegistry>();
        if (registry == null)
        {
            EditorUtility.DisplayDialog(
                "Sync Entries",
                "No InteractionRegistry found in scene. Use Tools > ClockworkCraft > Setup Interaction Registry first.",
                "OK");
            return;
        }

        SyncEntries(registry);
        Selection.activeGameObject = registry.gameObject;
    }

    /// <summary>
    /// Sync entries from databases assigned on the registry (or found via MapGeneratorV2).
    /// Preserves existing unlock states for entries that already exist.
    /// </summary>
    static void SyncEntries(InteractionRegistry registry)
    {
        // Try to get databases from the registry's serialized fields
        SerializedObject so = new SerializedObject(registry);
        var envDBProp = so.FindProperty("environmentDatabase");
        var unitDBProp = so.FindProperty("unitDatabase");

        EnvironmentDatabase envDB = envDBProp?.objectReferenceValue as EnvironmentDatabase;
        UnitDatabase unitDB = unitDBProp?.objectReferenceValue as UnitDatabase;

        // Fall back to MapGeneratorV2's databases
        if (envDB == null || unitDB == null)
        {
            MapGeneratorV2 mapGen = Object.FindFirstObjectByType<MapGeneratorV2>();
            if (mapGen != null)
            {
                if (envDB == null)
                {
                    envDB = mapGen.environmentDatabase;
                    if (envDB != null)
                    {
                        envDBProp.objectReferenceValue = envDB;
                        Debug.Log("[InteractionRegistrySetup] Auto-assigned EnvironmentDatabase from MapGeneratorV2");
                    }
                }
                if (unitDB == null)
                {
                    unitDB = mapGen.unitDatabase;
                    if (unitDB != null)
                    {
                        unitDBProp.objectReferenceValue = unitDB;
                        Debug.Log("[InteractionRegistrySetup] Auto-assigned UnitDatabase from MapGeneratorV2");
                    }
                }
                so.ApplyModifiedProperties();
            }
        }

        if (envDB == null && unitDB == null)
        {
            EditorUtility.DisplayDialog(
                "Sync Entries",
                "No databases assigned on InteractionRegistry or MapGeneratorV2. Assign EnvironmentDatabase and UnitDatabase first.",
                "OK");
            return;
        }

        registry.PopulateFromDatabases(envDB, unitDB);
        EditorUtility.SetDirty(registry);

        int count = registry.AllEntries.Count;
        Debug.Log($"[InteractionRegistrySetup] Synced {count} entries from databases.");
    }

    /// <summary>
    /// Try to auto-assign databases from MapGeneratorV2 in the scene.
    /// </summary>
    static void AutoAssignDatabases(InteractionRegistry registry)
    {
        MapGeneratorV2 mapGen = Object.FindFirstObjectByType<MapGeneratorV2>();
        if (mapGen == null) return;

        SerializedObject so = new SerializedObject(registry);

        var envProp = so.FindProperty("environmentDatabase");
        if (mapGen.environmentDatabase != null && envProp != null && envProp.objectReferenceValue == null)
        {
            envProp.objectReferenceValue = mapGen.environmentDatabase;
        }
        var unitProp = so.FindProperty("unitDatabase");
        if (mapGen.unitDatabase != null && unitProp != null && unitProp.objectReferenceValue == null)
        {
            unitProp.objectReferenceValue = mapGen.unitDatabase;
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(registry);
        Debug.Log("[InteractionRegistrySetup] Auto-assigned databases from MapGeneratorV2");
    }
}
