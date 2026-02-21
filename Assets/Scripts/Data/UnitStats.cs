using UnityEngine;

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

        [Header("Draw Weight")]
        [Tooltip("Relative draw likelihood. Default 1. Higher = more likely. Overrides rarity-based weight when > 0.")]
        public float drawWeight = 0f;  // 0 = use rarity-based weight (backwards compatible)

        [Header("Furniture (LittleCafe)")]
        [Tooltip("FurnitureType to apply at runtime, overriding whatever the prefab has serialized.")]
        public int furnitureTypeOverride = -1; // -1 = no override, 0+ = FurnitureType enum value

        [Header("Combat Stats")]
        public int maxHP = 10;
        public int attackDamage = 3;
        public int attackRange = 1; // Cells away from unit
        public int attackIntervalMultiplier = 2; // Attacks every X intervals

        [Header("Economy")]
        public int resourceCost = 3; // Currently unused (placement is free)
        public int killReward = 2; // Tokens awarded to player when this enemy unit is killed

        [Header("Movement")]
        public int chargeDistance = 0; // Tiles to dash forward before attacking (0 = no dash)

        [Header("Fog of War - Iteration 7")]
        public int revealRadius = 1; // Cells revealed around unit when placed (Soldier: 1, Ninja: 2, Ogre: 1)

        [Header("Grid Footprint")]
        public Vector2Int gridSize = new Vector2Int(1, 1); // Cells occupied (e.g. 2x1 table, 2x2 cooking station)

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
