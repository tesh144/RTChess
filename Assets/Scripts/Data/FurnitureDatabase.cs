using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace LittleCafe
{
    /// <summary>
    /// ScriptableObject database containing all furniture configurations.
    /// Created once, edited in Inspector to configure the 114 PEPO assets.
    /// </summary>
    [CreateAssetMenu(fileName = "FurnitureDatabase", menuName = "LittleCafe/Furniture Database")]
    public class FurnitureDatabase : ScriptableObject
    {
        [Header("All Furniture Assets")]
        [SerializeField] private List<FurnitureData> furnitureList = new List<FurnitureData>();

        public List<FurnitureData> AllFurniture => furnitureList;

        /// <summary>
        /// Get furniture data by asset name.
        /// </summary>
        public FurnitureData GetByName(string assetName)
        {
            return furnitureList.FirstOrDefault(f => f.assetName == assetName);
        }

        /// <summary>
        /// Get all furniture of a specific type.
        /// </summary>
        public List<FurnitureData> GetByType(FurnitureType type)
        {
            return furnitureList.Where(f => f.type == type).ToList();
        }

        /// <summary>
        /// Get all functional furniture (Table, Chair, Wall).
        /// </summary>
        public List<FurnitureData> GetFunctionalFurniture()
        {
            return furnitureList.Where(f => f.isFunctional).ToList();
        }

        /// <summary>
        /// Get all decorations (non-functional).
        /// </summary>
        public List<FurnitureData> GetDecorations()
        {
            return furnitureList.Where(f => !f.isFunctional).ToList();
        }

        /// <summary>
        /// Add furniture data to the database.
        /// </summary>
        public void AddFurniture(FurnitureData data)
        {
            if (!furnitureList.Contains(data))
            {
                furnitureList.Add(data);
            }
        }

        /// <summary>
        /// Clear all furniture data.
        /// </summary>
        public void Clear()
        {
            furnitureList.Clear();
        }

        /// <summary>
        /// Get count of furniture entries.
        /// </summary>
        public int Count => furnitureList.Count;

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only: Populate database from all FBX files in PEPO folder.
        /// </summary>
        [ContextMenu("Scan PEPO Folder and Populate")]
        public void ScanAndPopulate()
        {
            string[] fbxGuids = UnityEditor.AssetDatabase.FindAssets("t:Model", new[] { "Assets/PEPO" });

            Clear();

            foreach (string guid in fbxGuids)
            {
                string fbxPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);

                // Only include .fbx files
                if (fbxPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                {
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(fbxPath);
                    FurnitureData data = new FurnitureData
                    {
                        assetName = fileName,
                        type = LittleCafe.FurnitureType.Decoration,
                        isFunctional = false,
                        isWalkable = false,
                        gridSize = UnityEngine.Vector2Int.one,
                        visualScale = 1.0f,
                        prefab = null,
                        icon = null
                    };
                    AddFurniture(data);
                }
            }

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[FurnitureDatabase] Populated with {Count} PEPO assets");
        }
#endif
    }
}
