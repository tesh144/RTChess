using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace LittleCafe
{
    /// <summary>
    /// ScriptableObject database containing all unit configurations (allied and enemy).
    /// Create via: Right-click → Create → LittleCafe → Unit Database
    /// </summary>
    [CreateAssetMenu(fileName = "UnitDatabase", menuName = "LittleCafe/Unit Database")]
    public class UnitDatabase : ScriptableObject
    {
        [Header("All Unit Assets")]
        [SerializeField] private List<UnitData> unitList = new List<UnitData>();

        public List<UnitData> AllUnits => unitList;

        /// <summary>
        /// Get unit data by asset name.
        /// </summary>
        public UnitData GetByName(string assetName)
        {
            return unitList.FirstOrDefault(u => u.assetName == assetName);
        }

        /// <summary>
        /// Get all units of a specific type.
        /// </summary>
        public List<UnitData> GetByType(GameUnitType type)
        {
            return unitList.Where(u => u.type == type).ToList();
        }

        /// <summary>
        /// Get all enemy units.
        /// </summary>
        public List<UnitData> GetEnemyUnits()
        {
            return unitList.Where(u => u.isEnemy).ToList();
        }

        /// <summary>
        /// Get all allied (non-enemy) units.
        /// </summary>
        public List<UnitData> GetAlliedUnits()
        {
            return unitList.Where(u => !u.isEnemy).ToList();
        }

        /// <summary>
        /// Get all functional units.
        /// </summary>
        public List<UnitData> GetFunctionalUnits()
        {
            return unitList.Where(u => u.isFunctional).ToList();
        }

        /// <summary>
        /// Add unit data to the database.
        /// </summary>
        public void AddUnit(UnitData data)
        {
            if (!unitList.Contains(data))
            {
                unitList.Add(data);
            }
        }

        /// <summary>
        /// Clear all unit data.
        /// </summary>
        public void Clear()
        {
            unitList.Clear();
        }

        /// <summary>
        /// Get count of unit entries.
        /// </summary>
        public int Count => unitList.Count;

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only: Populate database from all FBX files in a specified folder.
        /// </summary>
        [ContextMenu("Scan PEPO Folder and Populate")]
        public void ScanAndPopulate()
        {
            string[] fbxGuids = UnityEditor.AssetDatabase.FindAssets("t:Model", new[] { "Assets/PEPO" });

            Clear();

            foreach (string guid in fbxGuids)
            {
                string fbxPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);

                if (fbxPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                {
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(fbxPath);
                    UnitData data = new UnitData
                    {
                        assetName = fileName,
                        type = GameUnitType.Generic,
                        isEnemy = false,
                        isFunctional = false,
                        isWalkable = true,
                        gridSize = Vector2Int.one,
                        visualScale = 1.0f,
                        prefab = null,
                        icon = null
                    };
                    AddUnit(data);
                }
            }

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[UnitDatabase] Populated with {Count} assets");
        }
#endif
    }
}
