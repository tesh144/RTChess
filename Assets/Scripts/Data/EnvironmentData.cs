using UnityEngine;

namespace LittleCafe
{
    /// <summary>
    /// Classification of environment object types.
    /// </summary>
    public enum EnvironmentType
    {
        Generic,        // Default - unclassified
        Tree,           // Trees, bushes
        Rock,           // Rocks, boulders
        Water,          // Ponds, fountains, wells
        Path,           // Roads, pathways, bridges
        Fence,          // Fences, walls, gates
        Terrain,        // Hills, mounds, terrain features
        Flora           // Flowers, grass, crops
    }

    /// <summary>
    /// Configuration data for a single environment asset.
    /// Stored in EnvironmentDatabase ScriptableObject.
    /// </summary>
    [System.Serializable]
    public class EnvironmentData
    {
        [Header("Asset Identity")]
        public string assetName;

        [Header("Environment Properties")]
        public EnvironmentType type = EnvironmentType.Generic;
        public bool isFunctional = false;
        public bool isWalkable = false;

        [Header("Combat Stats")]
        [Tooltip("Health points. When HP reaches 0, triggers an event (removal, completion, etc.).")]
        public int hp = 5;
        [Tooltip("Damage dealt to target's HP per successful interaction.")]
        public int attackPower = 0;

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
