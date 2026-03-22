#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using ClockworkCraft;
using LittleCafe;

namespace ClockworkGrid
{
    /// <summary>
    /// Unit type classification
    /// </summary>
    public enum UnitType
    {
        Soldier,  // Balanced, common
        Ogre,     // Tank, epic
        Ninja,    // Fast, rare
        // LittleCafe equipment (appended to preserve existing serialized values)
        Table,
        Chair,
        Wall,
        Door,
        CookingStation,
        ServingCounter,
        WashingStation,
        PlateRack
    }

    /// <summary>
    /// Rarity tier affecting draw probability
    /// </summary>
    public enum Rarity
    {
        Common,   // 60% - Soldier
        Rare,     // 35% - Ninja
        Epic      // 5% - Ogre
    }

    /// <summary>
    /// ScriptableObject containing all stats for a unit type.
    /// Create instances in Unity: Right-click → Create → ClockworkGrid → Unit Stats
    /// </summary>
    [CreateAssetMenu(fileName = "New Unit Stats", menuName = "ClockworkGrid/Unit Stats")]
    public class UnitStats : ScriptableObject
    {
        [Header("Identity")]
        public UnitType unitType;
        public string unitName;
        public Rarity rarity;

        [Header("Tier")]
        [Tooltip("Building tier (0-3). Used by RandomTier0-3 to filter the draw pool. -1 = excluded from tier pools.")]
        public int tier = -1;

        [Header("Draw Weight")]
        [Tooltip("Relative draw likelihood. Default 1. Higher = more likely. Overrides rarity-based weight when > 0.")]
        public float drawWeight = 0f;  // 0 = use rarity-based weight (backwards compatible)

        [Header("Furniture (LittleCafe)")]
        [Tooltip("FurnitureType to apply at runtime, overriding whatever the prefab has serialized.")]
        public int furnitureTypeOverride = -1; // -1 = no override, 0+ = FurnitureType enum value

        [Header("Entity Behavior")]
        [Tooltip("Whether this object acts each interval tick (workers/units = true, furniture/buildings = false)")]
        public bool isActive = false;

        [Tooltip("Clockwork behavior pattern. RotateAndInteract = worker-style (attack). RotateAndMove = animal-style (wander).")]
        public BehaviorType behaviorType = BehaviorType.RotateAndInteract;

        [Tooltip("Allied entities (workers, buildings) are never attacked by the player's own workers.")]
        public bool isAllied = false;

        [Tooltip("When this entity is killed, does the attacker advance into its cell? Advance = true, Stay = false.")]
        public bool killerAdvances = true;

        [Header("Combat Stats")]
        public int maxHP = 10;
        public int attackDamage = 3;
        public int attackRange = 1; // Cells away from unit
        public int attackIntervalMultiplier = 2; // Attacks every X intervals

        [Header("Loot Settings")]
        [Tooltip("What resource currency this unit drops when hit/killed. None = no loot.")]
        public ResourceType lootResourceType = ResourceType.None;
        [Tooltip("HP removed per loot trigger. e.g. 2 means every 2 HP of damage triggers a loot drop.")]
        [Min(1)] public int lootHpCost = 1;
        [Tooltip("How many resource particles burst out each time lootHpCost HP has been removed.")]
        [Range(1, 10)] public int lootYield = 1;

        [Header("Economy")]
        public int resourceCost = 3; // Legacy single-cost field
        public int killReward = 2; // Tokens awarded to player when this enemy unit is killed

        [Header("Placement Cost (shown during drag, max 3)")]
        [Tooltip("Resource costs to place this object. Empty = free. Max 3 entries. " +
                 "Displayed near the arc line when dragging, NOT on the card.")]
        public System.Collections.Generic.List<LittleCafe.PlacementCost> placementCosts =
            new System.Collections.Generic.List<LittleCafe.PlacementCost>();

        [Header("Movement")]
        public int chargeDistance = 0; // Tiles to dash forward before attacking (0 = no dash)

        [Header("Fog of War - Iteration 7")]
        public int revealRadius = 1; // Cells revealed around unit when placed (Soldier: 1, Ninja: 2, Ogre: 1)

        [Header("Grid Footprint")]
        public Vector2Int gridSize = new Vector2Int(1, 1); // Cells occupied (e.g. 2x1 table, 2x2 cooking station)

        [Header("Random Pool")]
        [Tooltip("When true, this card can appear in random building draws (Statue output, draw button). Feast = false.")]
        public bool isRandomBuilding = true;

        [Header("Interaction Categories")]
        [Tooltip("Can allied workers interact with this object?")]
        public bool allyInteractible = false;
        [Tooltip("Can enemy/hostile entities attack this object?")]
        public bool enemyInteractible = true;
        [Tooltip("Can wild animals (Dinosaur, Mammoth) interact with this object?")]
        public bool wildAnimalInteractible = false;

        [Header("Meal Buff")]
        [Tooltip("When true, this object grants a meal buff to workers that interact with it.")]
        public bool isMealSource = false;

        [Header("Building Production")]
        [Tooltip("What must be dragged onto this building to start production. None = auto-produce.")]
        public ProductionInputType productionInputType = ProductionInputType.None;
        [Tooltip("What this building produces when its timer completes.")]
        public ProductionOutputType productionOutputType = ProductionOutputType.None;
        [Tooltip("Base seconds between production cycles. 0 = no production.")]
        public float productionInterval = 0f;
        [Tooltip("Extra seconds added to the interval each time a reward is collected.")]
        public float productionIntervalBonus = 0f;
        [Tooltip("For Currency output: which resource type to award.")]
        public ResourceType producedResourceType = ResourceType.None;
        [Tooltip("How many units of the reward per production cycle.")]
        public int productionAmount = 1;

        [Header("Visuals")]
        public Color unitColor = Color.blue;
        public Sprite iconSprite; // Icon for dock bar
        public float modelScale = 1f; // Visual scale multiplier

        [Header("References")]
        public GameObject unitPrefab; // Player prefab to spawn
        public GameObject enemyPrefab; // Enemy prefab to spawn (falls back to unitPrefab if null)

        /// <summary>
        /// Get effective draw weight. Uses drawWeight if set (> 0), otherwise falls back to rarity-based weight.
        /// </summary>
        public float GetEffectiveDrawWeight()
        {
            if (drawWeight > 0f) return drawWeight;

            // Fallback to rarity-based weights for backwards compatibility
            switch (rarity)
            {
                case Rarity.Common: return 60f;
                case Rarity.Rare: return 35f;
                case Rarity.Epic: return 5f;
                default: return 1f;
            }
        }

        /// <summary>
        /// Get rarity weight for draw probability (legacy, prefer GetEffectiveDrawWeight)
        /// </summary>
        public float GetRarityWeight()
        {
            return GetEffectiveDrawWeight();
        }

        /// <summary>
        /// Get rarity color for UI
        /// </summary>
        public Color GetRarityColor()
        {
            switch (rarity)
            {
                case Rarity.Common: return new Color(0.7f, 0.7f, 0.7f); // Gray
                case Rarity.Rare: return new Color(0.3f, 0.6f, 1f); // Blue
                case Rarity.Epic: return new Color(0.8f, 0.4f, 1f); // Purple
                default: return Color.white;
            }
        }
    }
}
