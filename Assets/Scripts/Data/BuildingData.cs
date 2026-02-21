using UnityEngine;

namespace LittleCafe
{
    /// <summary>
    /// Classification of building types.
    /// </summary>
    public enum BuildingType
    {
        Generic,        // Default - unclassified building
        House,          // Residential
        Shop,           // Commercial / retail
        Workshop,       // Crafting / production
        Storage,        // Warehouses, barns
        Civic,          // Town hall, fountain, well
        Military,       // Barracks, watchtower
        Religious       // Temple, shrine
    }

    /// <summary>
    /// Configuration data for a single building asset.
    /// Stored in BuildingDatabase ScriptableObject.
    /// </summary>
    [System.Serializable]
    public class BuildingData
    {
        [Header("Asset Identity")]
        public string assetName;

        [Header("Building Properties")]
        public BuildingType type = BuildingType.Generic;
        public bool isFunctional = false;
        public bool isWalkable = false;
        [Tooltip("Active objects perform an action each interval tick. Buildings are passive by default.")]
        public bool isActive = false;

        [Header("Combat Stats")]
        [Tooltip("Health points. When HP reaches 0, triggers an event (removal, completion, etc.).")]
        public int hp = 10;
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
