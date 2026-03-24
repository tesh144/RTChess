#pragma warning disable CS0414, CS0219, CS0618
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
    /// What triggers a building's production cycle to start.
    /// None = auto-produce on timer. Worker/Fighter = requires dragging that unit onto the building.
    /// Any = accepts any card type dragged onto the building (Scrapper).
    /// </summary>
    public enum ProductionInputType
    {
        None,       // Auto-produce (Home, Statue, Kitchen)
        Worker,     // Requires a Worker card dragged onto the building (Barracks)
        Fighter,    // Requires a Fighter card dragged onto the building (future use)
        Any         // Accepts any card type — consumes and scraps it (Scrapper)
    }

    /// <summary>
    /// What a building produces when its production timer completes.
    /// Tier-based outputs randomly pick from the pool of items tagged with that tier.
    /// </summary>
    public enum ProductionOutputType
    {
        None,               // No production
        Worker,             // Produces a worker card for the player's hand
        Currency,           // Produces currency that flies to the currency bar
        RandomBuilding,     // Draws a random card from any building in the pool (no tier filter)
        Fighter,            // Produces a fighter card (from Barracks)
        Meal,               // Produces a meal card (from Kitchen)
        Tier0Building,      // Random building from tier 0 pool
        Tier1Building,      // Random building from tier 1 pool
        Tier2Building,      // Random building from tier 2 pool
        Tier3Building,      // Random building from tier 3 pool
        Tier0Unit,          // Random unit/worker from tier 0 pool
        Tier1Unit,          // Random unit/worker from tier 1 pool
        Tier2Unit,          // Random unit/worker from tier 2 pool
        Tier3Unit,          // Random unit/worker from tier 3 pool
        Scrap,              // Produces Scrap resource (from Scrapper — any card in → Scrap out)
        Lizard,             // Produces a Lizard environment entity (Rabbit Farm)
        TreeSeed            // Produces a Tree Seed entity (Garden)
    }

    /// <summary>
    /// Configuration data for a single building asset.
    /// Stored in BuildingDatabase ScriptableObject.
    /// </summary>
    [System.Serializable]
    public class BuildingData
    {
        [Header("Active")]
        [Tooltip("When false, this entry is hidden from all game systems (draw pools, spawning, etc.)")]
        public bool active = true;

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

        [Header("Tier")]
        [Tooltip("Building tier (0-3). Used by RandomTier0-3 production outputs to filter the draw pool. -1 = excluded from tier pools.")]
        public int tier = 0;

        [Header("Economy")]
        [Tooltip("Relative likelihood of being drawn. Default 1. Higher = more likely.")]
        public float drawWeight = 1f;
        [Tooltip("Resource cost to place this building. 0 = free.")]
        public int placementCost = 0;

        [Header("Grid & Visual")]
        public Vector2Int gridSize = Vector2Int.one;
        public float visualScale = 1.0f;

        [Header("Killer's Behavior")]
        [Tooltip("When this building is destroyed, does the attacker advance into its cell? Advance = true, Stay = false.")]
        public bool killerAdvances = true;

        [Header("Fog")]
        [Tooltip("How many cells outward this building reveals fog when placed. 1 = standard plus-shape, 2 = wider reveal (e.g. Torch).")]
        public int fogRevealRadius = 1;

        [Header("Prefab & Icon")]
        public GameObject prefab;
        public Sprite icon;

        [Header("Random Pool")]
        [Tooltip("When true, this building can appear in random building draws (Statue, draw button). Feast = false.")]
        public bool isRandomBuilding = true;

        [Header("Interaction Categories")]
        [Tooltip("Can allied workers interact with this building? (e.g., Feast = true for meal buff)")]
        public bool allyInteractible = false;
        [Tooltip("Can enemy/hostile entities attack this building?")]
        public bool enemyInteractible = true;
        [Tooltip("Can wild animals (Dinosaur, Mammoth) interact with this building?")]
        public bool wildAnimalInteractible = false;

        [Header("Meal Buff")]
        [Tooltip("When true, workers that interact with this building receive a meal buff.")]
        public bool isMealSource = false;

        [Header("Production")]
        [Tooltip("What must be dragged onto this building to start production. None = auto-produce on timer.")]
        public ProductionInputType productionInputType = ProductionInputType.None;

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

        [Header("Production Resource Cost")]
        [Tooltip("Resource type required to start each production cycle. None = no cost.")]
        public ResourceType productionCostResourceType = ResourceType.None;

        [Tooltip("Amount of productionCostResourceType consumed when the cycle starts. 0 = no cost.")]
        public int productionCostAmount = 0;

        [Tooltip("Amount added to productionCostAmount after each production cycle. Anti-spam scaling. 0 = flat cost.")]
        public int productionCostIncrement = 0;

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
