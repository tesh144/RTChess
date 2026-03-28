#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using ClockworkCraft;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// Classification of unit types — covers both allied and enemy units.
    /// </summary>
    public enum GameUnitType
    {
        Generic,        // Default - unclassified unit
        Villager,       // Basic allied worker
        Farmer,         // Agriculture worker
        Miner,          // Resource extraction worker
        Builder,        // Construction worker
        Merchant,       // Trading worker
        Guard,          // Defense / patrol
        Crafter,        // Artisan / crafting
        Soldier,        // Melee enemy combatant
        Archer,         // Ranged enemy combatant
        Beast,          // Wild creature / monster
        Boss,           // Boss-tier enemy
        Corruption      // Corruption-type entity (hearts, spikes)
    }

    /// <summary>
    /// Configuration data for a single unit asset (allied or enemy).
    /// Stored in UnitDatabase ScriptableObject.
    /// </summary>
    [System.Serializable]
    public class UnitData
    {
        [Header("Active")]
        [Tooltip("When false, this entry is hidden from all game systems")]
        public bool active = true;

        [Header("Asset Identity")]
        public string assetName;

        [Header("Unit Properties")]
        public GameUnitType type = GameUnitType.Generic;
        public bool isEnemy = false;
        public bool isFunctional = false;
        public bool isWalkable = true;          // Units are walkable by default
        [Tooltip("Active objects perform an action each interval tick. Units act by default.")]
        public bool isActive = true;

        [Tooltip("When true, MapGeneratorV2 includes this unit in its spawn pool. When false, this unit is only created by other systems (e.g. CorruptionHeart spawns Spikes).")]
        public bool isMapGenerated = false;

        [Header("Behavior")]
        [Tooltip("Clockwork behavior pattern. RotateAndInteract = worker-style (attack). RotateAndMove = animal-style (wander).")]
        public BehaviorType behaviorType = BehaviorType.RotateAndMove;

        [Tooltip("Surface types this unit can walk on. 'None' = only empty tiles. 'Corruption' = only corruption tiles. Comma-separated for multiple (e.g. 'None+Water'). Synced from Walkable column.")]
        public string walkable = "None";

        [Tooltip("When this unit is killed, does the attacker advance into its cell? Advance = true, Stay = false. Mirrors sheet 'Killer's Behavior' column.")]
        public bool killerAdvances = false;

        [Header("Drop on Death")]
        [Tooltip("Resource type dropped when this unit is destroyed. None = no drop on death.")]
        public ResourceType dropOnDeath = ResourceType.None;

        [Header("Loot Settings")]
        [Tooltip("What resource currency this unit drops when hit/killed. None = no loot. Assign from CurrencyDatabase entries.")]
        public ResourceType lootResourceType = ResourceType.None;
        [Tooltip("HP removed per loot trigger. e.g. 2 means every 2 HP of damage triggers a loot drop.")]
        [Min(1)] public int lootHpCost = 1;
        [Tooltip("How many resource particles burst out each time lootHpCost HP has been removed.")]
        [Range(1, 10)] public int lootYield = 1;

        [Header("Combat Stats")]
        [Tooltip("Health points. When HP reaches 0, triggers an event (removal, completion, etc.).")]
        public int hp = 3;
        [Tooltip("Damage dealt to target's HP per successful interaction.")]
        public int attackPower = 1;

        [Header("Tier")]
        [Tooltip("Unit tier (0-3). Used by tier-based production outputs to filter the draw pool. -1 = excluded from tier pools.")]
        public int tier = -1;

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
