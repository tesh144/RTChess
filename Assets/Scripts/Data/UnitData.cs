using UnityEngine;

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
        Boss            // Boss-tier enemy
    }

    /// <summary>
    /// Configuration data for a single unit asset (allied or enemy).
    /// Stored in UnitDatabase ScriptableObject.
    /// </summary>
    [System.Serializable]
    public class UnitData
    {
        [Header("Asset Identity")]
        public string assetName;

        [Header("Unit Properties")]
        public GameUnitType type = GameUnitType.Generic;
        public bool isEnemy = false;
        public bool isFunctional = false;
        public bool isWalkable = true;          // Units are walkable by default

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
