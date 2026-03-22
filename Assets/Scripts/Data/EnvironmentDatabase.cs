#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace LittleCafe
{
    /// <summary>
    /// ScriptableObject database containing all environment object configurations.
    /// Create via: Right-click → Create → LittleCafe → Environment Database
    /// </summary>
    [CreateAssetMenu(fileName = "EnvironmentDatabase", menuName = "LittleCafe/Environment Database")]
    public class EnvironmentDatabase : ScriptableObject
    {
        [Header("All Environment Assets")]
        [SerializeField] private List<EnvironmentData> environmentList = new List<EnvironmentData>();

        public List<EnvironmentData> AllEnvironment => environmentList;

        /// <summary>
        /// Get environment data by asset name.
        /// </summary>
        public EnvironmentData GetByName(string assetName)
        {
            return environmentList.FirstOrDefault(e => e.assetName == assetName);
        }

        /// <summary>
        /// Get all environment objects of a specific type.
        /// </summary>
        public List<EnvironmentData> GetByType(EnvironmentType type)
        {
            return environmentList.Where(e => e.type == type).ToList();
        }

        /// <summary>
        /// Get all functional environment objects.
        /// </summary>
        public List<EnvironmentData> GetFunctionalEnvironment()
        {
            return environmentList.Where(e => e.isFunctional).ToList();
        }

        /// <summary>
        /// Add environment data to the database.
        /// </summary>
        public void AddEnvironment(EnvironmentData data)
        {
            if (!environmentList.Contains(data))
            {
                environmentList.Add(data);
            }
        }

        /// <summary>
        /// Clear all environment data.
        /// </summary>
        public void Clear()
        {
            environmentList.Clear();
        }

        /// <summary>
        /// Get count of environment entries.
        /// </summary>
        public int Count => environmentList.Count;

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
                    EnvironmentData data = new EnvironmentData
                    {
                        assetName = fileName,
                        type = EnvironmentType.Generic,
                        isFunctional = false,
                        isWalkable = false,
                        gridSize = Vector2Int.one,
                        visualScale = 1.0f,
                        prefab = null,
                        icon = null
                    };
                    AddEnvironment(data);
                }
            }

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[EnvironmentDatabase] Populated with {Count} assets");
        }
#endif
    }
}
