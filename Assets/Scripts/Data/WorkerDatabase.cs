#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace LittleCafe
{
    /// <summary>
    /// ScriptableObject database containing all worker configurations.
    /// Create via: Right-click → Create → LittleCafe → Worker Database
    /// </summary>
    [CreateAssetMenu(fileName = "WorkerDatabase", menuName = "LittleCafe/Worker Database")]
    public class WorkerDatabase : ScriptableObject
    {
        [Header("All Worker Assets")]
        [SerializeField] private List<WorkerData> workerList = new List<WorkerData>();

        public List<WorkerData> AllWorkers => workerList;

        /// <summary>
        /// Get worker data by asset name.
        /// </summary>
        public WorkerData GetByName(string assetName)
        {
            return workerList.FirstOrDefault(w => w.assetName == assetName);
        }

        /// <summary>
        /// Get all workers of a specific type.
        /// </summary>
        public List<WorkerData> GetByType(WorkerType type)
        {
            return workerList.Where(w => w.type == type).ToList();
        }

        /// <summary>
        /// Get all functional workers.
        /// </summary>
        public List<WorkerData> GetFunctionalWorkers()
        {
            return workerList.Where(w => w.isFunctional).ToList();
        }

        /// <summary>
        /// Add worker data to the database.
        /// </summary>
        public void AddWorker(WorkerData data)
        {
            if (!workerList.Contains(data))
            {
                workerList.Add(data);
            }
        }

        /// <summary>
        /// Clear all worker data.
        /// </summary>
        public void Clear()
        {
            workerList.Clear();
        }

        /// <summary>
        /// Get count of worker entries.
        /// </summary>
        public int Count => workerList.Count;

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
                    WorkerData data = new WorkerData
                    {
                        assetName = fileName,
                        type = WorkerType.Generic,
                        isFunctional = false,
                        isWalkable = true,
                        gridSize = Vector2Int.one,
                        visualScale = 1.0f,
                        prefab = null,
                        icon = null
                    };
                    AddWorker(data);
                }
            }

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[WorkerDatabase] Populated with {Count} assets");
        }
#endif
    }
}
