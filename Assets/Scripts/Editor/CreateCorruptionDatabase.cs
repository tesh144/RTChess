#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using UnityEditor;
using LittleCafe;

/// <summary>
/// Idempotent editor initialiser: creates the CorruptionDatabase asset at
/// Assets/Scripts/Data/CorruptionDatabase.asset if it does not yet exist,
/// then populates it with the Corruption Heart entry if the database is empty.
///
/// Runs once on domain reload. Safe to leave in the project — it will not
/// re-create or overwrite existing data.
/// </summary>
[InitializeOnLoad]
public static class CreateCorruptionDatabase
{
    private const string ASSET_PATH = "Assets/Scripts/Data/CorruptionDatabase.asset";

    static CreateCorruptionDatabase()
    {
        EditorApplication.delayCall += Run;
    }

    static void Run()
    {
        // ── 1. Find or create the asset ───────────────────────────────────
        CorruptionDatabase db = AssetDatabase.LoadAssetAtPath<CorruptionDatabase>(ASSET_PATH);

        if (db == null)
        {
            db = ScriptableObject.CreateInstance<CorruptionDatabase>();
            AssetDatabase.CreateAsset(db, ASSET_PATH);
            Debug.Log($"[CreateCorruptionDatabase] Created new CorruptionDatabase at {ASSET_PATH}");
        }

        // ── 2. Only populate if the database has no entries yet ───────────
        if (db.Count > 0) return;

        // Load the Corruption Heart prefab and indicator prefab by GUID
        // (GUIDs are stable and don't depend on file paths)
        GameObject heartPrefab     = AssetDatabase.LoadAssetAtPath<GameObject>(
            AssetDatabase.GUIDToAssetPath("8ec6bc77c596f0a4a901c5b5dd5b9370"));
        GameObject indicatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            AssetDatabase.GUIDToAssetPath("25c7124f3ecad1342b713f88c3719eec"));

        db.AddEntry(new CorruptionData
        {
            entityName             = "Corruption Heart",
            prefab                 = heartPrefab,
            icon                   = null,   // Assign in Inspector when art is ready
            hp                     = 10,
            attackPower            = 1,
            floatingIndicatorPrefab = indicatorPrefab,
        });

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[CreateCorruptionDatabase] Populated CorruptionDatabase with 'Corruption Heart' entry.");
    }
}
