#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using ClockworkCraft;

namespace LittleCafe
{
    /// <summary>
    /// Visual/category classification of environment objects.
    /// Used for grouping and art direction purposes.
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
    /// Tile layer classification. Determines which grid layer this environment entry occupies.
    /// Maps directly to the "Type" column in the Environment &amp; Loot sheet.
    ///   Object  — Tier 1: occupies the object slot (trees, rocks, buildings, etc.)
    ///   Surface — Tier 2: occupies the surface/overlay slot (water, corruption, lava, etc.)
    ///             Surfaces do not block object placement — a tile can hold both.
    /// </summary>
    public enum EnvironmentLayerType
    {
        Object,     // Default — occupies the Object layer (Tier 1)
        Surface     // Overlay — occupies the Surface layer (Tier 2), coexists with Objects
    }

    /// <summary>
    /// Configuration data for a single environment asset.
    /// Stored in EnvironmentDatabase ScriptableObject.
    /// </summary>
    [System.Serializable]
    public class EnvironmentData
    {
        [Header("Active")]
        [Tooltip("When false, this entry is hidden from all game systems")]
        public bool active = true;

        [Header("Map Generation")]
        [Tooltip("When true, this object is spawned by the map generator. When false, it is obtained through other means (e.g. building production).")]
        public bool isMapGenerated = true;

        [Header("Asset Identity")]
        public string assetName;

        [Header("Environment Properties")]
        public EnvironmentType type = EnvironmentType.Generic;
        [Tooltip("Tile layer this object occupies. Object = Tier 1 (blocks placement). Surface = Tier 2 (overlay, coexists with objects). Synced from the 'Type' column in Environment & Loot sheet.")]
        public EnvironmentLayerType layerType = EnvironmentLayerType.Object;
        public bool isFunctional = false;
        public bool isWalkable = false;
        [Tooltip("Active objects perform an action each interval tick. Environment objects are passive by default.")]
        public bool isActive = false;

        [Header("Killer's Behavior")]
        [Tooltip("When this object is destroyed, does the attacker advance into its cell? Advance = true, Stay = false.")]
        public bool killerAdvances = true;

        [Header("Interaction Categories")]
        [Tooltip("Can allied workers interact with this object? Maps to 'Ally Interactible' sheet column.")]
        public bool allyInteractible = true;
        [Tooltip("Can enemy entities attack this object? Maps to 'Enemy Interactible' sheet column.")]
        public bool enemyInteractible = false;
        [Tooltip("Can wild animals (Dinosaur, Mammoth) interact with this object? Maps to 'Wild Animal Interactible' sheet column.")]
        public bool wildAnimalInteractible = false;

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

        [Header("Drop on Death")]
        [Tooltip("Resource type dropped when this object is destroyed. None = no drop on death.")]
        public ResourceType dropOnDeath = ResourceType.None;

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
