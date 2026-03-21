using UnityEngine;
using ClockworkCraft;

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
    /// What a building produces when its production timer completes.
    /// </summary>
    public enum ProductionOutputType
    {
        None,       // No production
        Worker,     // Produces a worker card for the player's hand
        Currency,   // Produces currency that flies to the currency bar
        RandomBuilding  // Draws a random card from the deck (same as draw button)
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

        [Header("Economy")]
        [Tooltip("Relative likelihood of being drawn. Default 1. Higher = more likely.")]
        public float drawWeight = 1f;
        [Tooltip("Resource cost to place this building. 0 = free.")]
        public int placementCost = 0;

        [Header("Grid & Visual")]
        public Vector2Int gridSize = Vector2Int.one;
        public float visualScale = 1.0f;

        [Header("Prefab & Icon")]
        public GameObject prefab;
        public Sprite icon;

        [Header("Production")]
        [Tooltip("Base seconds between production cycles. 0 = no production.")]
        public float productionInterval = 0f;

        [Tooltip("Extra seconds added to the interval each time a reward is collected. Prevents worker spam.")]
        public float productionIntervalBonus = 0f;

        [Tooltip("What this building produces when its timer completes.")]
        public ProductionOutputType productionOutputType = ProductionOutputType.None;

        [Tooltip("For Currency output: which resource type to award.")]
        public ResourceType producedResourceType = ResourceType.None;

        [Tooltip("How many units of the reward per production cycle.")]
        public int productionAmount = 1;

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
