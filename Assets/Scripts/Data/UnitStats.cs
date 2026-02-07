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
        Ninja     // Fast, rare
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

        [Header("Combat Stats")]
        public int maxHP = 10;
        public int attackDamage = 3;
        public int attackRange = 1; // Cells away from unit
        public int attackIntervalMultiplier = 2; // Attacks every X intervals

        [Header("Economy")]
        public int resourceCost = 3; // Currently unused (placement is free)
        public int killReward = 2; // Tokens awarded to player when this enemy unit is killed

        [Header("Fog of War - Iteration 7")]
        public int revealRadius = 1; // Cells revealed around unit when placed (Soldier: 1, Ninja: 2, Ogre: 1)

        [Header("Visuals")]
        public Color unitColor = Color.blue;
        public Sprite iconSprite; // Icon for dock bar
        public float modelScale = 1f; // Visual scale multiplier

        [Header("References")]
        public GameObject unitPrefab; // Player prefab to spawn
        public GameObject enemyPrefab; // Enemy prefab to spawn (falls back to unitPrefab if null)

        /// <summary>
        /// Get rarity weight for draw probability
        /// </summary>
        public float GetRarityWeight()
        {
            switch (rarity)
            {
                case Rarity.Common: return 60f;
                case Rarity.Rare: return 35f;
                case Rarity.Epic: return 5f;
                default: return 1f;
            }
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
