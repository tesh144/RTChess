#pragma warning disable CS0414, CS0219, CS0618
using UnityEditor;
using UnityEngine;
using LittleCafe;
using ClockworkGrid;

/// <summary>
/// One-time migration tool: converts legacy Vector2Int gridSize fields in
/// UnitStats, FurnitureData, and BuildingData to the new GridShape system.
///
/// Run via: Tools/RTChess/Migrate GridSize → GridShape
///
/// Migration logic is also applied automatically at runtime through each class's
/// ISerializationCallbackReceiver.OnAfterDeserialize implementation.
/// This tool forces the same migration for all assets in one pass and saves them.
/// </summary>
public class GridShapeMigrationTool
{
    [MenuItem("Tools/RTChess/Migrate GridSize \u2192 GridShape")]
    public static void RunMigration()
    {
        int unitCount     = 0;
        int furnitureCount = 0;
        int buildingCount  = 0;

        // ── UnitStats (individual ScriptableObjects) ──────────────────────
        string[] unitGuids = AssetDatabase.FindAssets("t:UnitStats");
        foreach (string guid in unitGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var stats = AssetDatabase.LoadAssetAtPath<ClockworkGrid.UnitStats>(path);
            if (stats == null) continue;

            // Force OnAfterDeserialize to run by marking dirty and reimporting.
            // The ISerializationCallbackReceiver on UnitStats handles the actual migration.
            EditorUtility.SetDirty(stats);
            unitCount++;
        }

        // ── FurnitureDatabase → FurnitureData entries ──────────────────────
        string[] furnitureDbGuids = AssetDatabase.FindAssets("t:FurnitureDatabase");
        foreach (string guid in furnitureDbGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var db = AssetDatabase.LoadAssetAtPath<FurnitureDatabase>(path);
            if (db == null) continue;

            bool modified = false;
            foreach (var data in db.AllFurniture)
            {
                if (data == null) continue;
                if (data.shape != null && !data.shape.IsEmpty) continue;  // already migrated

                // Migrate from legacy gridSize
                if (data.gridSize.x > 0 && data.gridSize.y > 0)
                {
                    data.shape = GridShape.Rectangle(data.gridSize.x, data.gridSize.y);
                    modified = true;
                    furnitureCount++;
                }
            }

            if (modified) EditorUtility.SetDirty(db);
        }

        // ── BuildingDatabase → BuildingData entries ────────────────────────
        string[] buildingDbGuids = AssetDatabase.FindAssets("t:BuildingDatabase");
        foreach (string guid in buildingDbGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var db = AssetDatabase.LoadAssetAtPath<BuildingDatabase>(path);
            if (db == null) continue;

            bool modified = false;
            foreach (var data in db.AllBuildings)
            {
                if (data == null) continue;
                if (data.shape != null && !data.shape.IsEmpty) continue;  // already migrated

                if (data.gridSize.x > 0 && data.gridSize.y > 0)
                {
                    data.shape = GridShape.Rectangle(data.gridSize.x, data.gridSize.y);
                    modified = true;
                    buildingCount++;
                }
            }

            if (modified) EditorUtility.SetDirty(db);
        }

        // ── Save all modified assets ───────────────────────────────────────
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int total = unitCount + furnitureCount + buildingCount;
        Debug.Log($"[GridShapeMigration] Done. Migrated {unitCount} UnitStats, " +
                  $"{furnitureCount} FurnitureData, {buildingCount} BuildingData entries. " +
                  $"Total: {total}");

        EditorUtility.DisplayDialog(
            "GridShape Migration Complete",
            $"Migrated:\n" +
            $"  UnitStats:     {unitCount}\n" +
            $"  FurnitureData: {furnitureCount}\n" +
            $"  BuildingData:  {buildingCount}\n\n" +
            $"All assets saved.",
            "OK");
    }
}
