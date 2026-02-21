using UnityEngine;

namespace LittleCafe
{
    /// <summary>
    /// Classification of worker types.
    /// </summary>
    public enum WorkerType
    {
        Generic,        // Default - unclassified worker
        Villager,       // Basic worker
        Farmer,         // Agriculture
        Miner,          // Resource extraction
        Builder,        // Construction
        Merchant,       // Trading
        Guard,          // Defense / patrol
        Crafter         // Artisan / crafting
    }

    /// <summary>
    /// Configuration data for a single worker asset.
    /// Stored in WorkerDatabase ScriptableObject.
    /// </summary>
    [System.Serializable]
    public class WorkerData
    {
        [Header("Asset Identity")]
        public string assetName;

        [Header("Worker Properties")]
        public WorkerType type = WorkerType.Generic;
        public bool isFunctional = false;
        public bool isWalkable = true;          // Workers are walkable by default

        [Header("Draw Weight")]
        [Tooltip("Relative likelihood of being drawn. Default 1. Higher = more likely.")]
        public float drawWeight = 1f;

        [Header("Grid & Visual")]
        public Vector2Int gridSize = Vector2Int.one;
        public float visualScale = 1.0f;

        [Header("Prefab & Icon")]
        public GameObject prefab;
        public Sprite icon;

        /// <summary>
        /// Get clean asset name without file extension.
        /// </summary>
        public string GetCleanName()
        {
            if (string.IsNullOrEmpty(assetName))
                return "Unknown";

            return assetName.Replace(".fbx", "").Replace(".FBX", "");
        }
    }
}
