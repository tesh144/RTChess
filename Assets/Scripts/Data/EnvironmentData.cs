using UnityEngine;
using ClockworkCraft;

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
        [Tooltip("Active objects perform an action each interval tick. Environment objects are passive by default.")]
        public bool isActive = false;

        [Header("Loot Settings")]
        [Tooltip("What resource currency this environment drops when hit. None = no loot. Assign from CurrencyDatabase entries.")]
        public ResourceType lootResourceType = ResourceType.None;
        [Tooltip("HP removed per loot trigger. e.g. 2 means every 2 HP of damage triggers a loot drop.")]
        [Min(1)] public int lootHpCost = 1;
        [Tooltip("How many resource particles burst out each time lootHpCost HP has been removed.")]
        [Range(1, 10)] public int lootYield = 1;

        [Header("Combat Stats")]
        [Tooltip("Health points. When HP reaches 0, triggers an event (removal, completion, etc.).")]
        public int hp = 5;
        [Tooltip("Damage dealt to target's HP per successful interaction.")]
        public int attackPower = 0;

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
