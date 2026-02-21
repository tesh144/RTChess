using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace LittleCafe
{
    /// <summary>
    /// ScriptableObject database containing all building configurations.
    /// Create via: Right-click → Create → LittleCafe → Building Database
    /// </summary>
    [CreateAssetMenu(fileName = "BuildingDatabase", menuName = "LittleCafe/Building Database")]
    public class BuildingDatabase : ScriptableObject
    {
        [Header("All Building Assets")]
        [SerializeField] private List<BuildingData> buildingList = new List<BuildingData>();

        public List<BuildingData> AllBuildings => buildingList;

        /// <summary>
        /// Get building data by asset name.
        /// </summary>
        public BuildingData GetByName(string assetName)
        {
            return buildingList.FirstOrDefault(b => b.assetName == assetName);
        }

        /// <summary>
        /// Get all buildings of a specific type.
        /// </summary>
        public List<BuildingData> GetByType(BuildingType type)
        {
            return buildingList.Where(b => b.type == type).ToList();
        }

        /// <summary>
        /// Get all functional buildings.
        /// </summary>
        public List<BuildingData> GetFunctionalBuildings()
        {
            return buildingList.Where(b => b.isFunctional).ToList();
        }

        /// <summary>
        /// Add building data to the database.
        /// </summary>
        public void AddBuilding(BuildingData data)
        {
            if (!buildingList.Contains(data))
            {
                buildingList.Add(data);
            }
        }

        /// <summary>
        /// Clear all building data.
        /// </summary>
        public void Clear()
        {
            buildingList.Clear();
        }

        /// <summary>
        /// Get count of building entries.
        /// </summary>
        public int Count => buildingList.Count;

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
                    BuildingData data = new BuildingData
                    {
                        assetName = fileName,
                        type = BuildingType.Generic,
                        isFunctional = false,
                        isWalkable = false,
                        gridSize = Vector2Int.one,
                        visualScale = 1.0f,
                        prefab = null,
                        icon = null
                    };
                    AddBuilding(data);
                }
            }

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[BuildingDatabase] Populated with {Count} assets");
        }
#endif
    }
}
