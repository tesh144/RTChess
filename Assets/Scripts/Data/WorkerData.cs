#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// Classification of worker types.
    /// </summary>
    public enum WorkerType
    {
        Generic,        // Default - unclassified worker
        Villager,       // Basic worker
        Farmer,         // Agriculture
        Miner,          // Resource extraction
        Builder,        // Construction
        Merchant,       // Trading
        Guard,          // Defense / patrol
        Crafter,        // Artisan / crafting
        Fighter         // Combat unit (trained from worker)
    }

    /// <summary>
    /// Configuration data for a single worker asset.
    /// Stored in WorkerDatabase ScriptableObject.
    /// </summary>
    [System.Serializable]
    public class WorkerData
    {
        [Header("Active")]
        [Tooltip("When false, this entry is hidden from all game systems")]
        public bool active = true;

        [Header("Asset Identity")]
        public string assetName;

        [Header("Worker Properties")]
        public WorkerType type = WorkerType.Generic;
        public bool isFunctional = false;
        public bool isWalkable = true;          // Workers are walkable by default
        [Tooltip("Active objects perform an action each interval tick. Workers act by default.")]
        public bool isActive = true;

        [Header("Behavior")]
        [Tooltip("Clockwork behavior pattern. RotateAndInteract = worker-style (attack). RotateAndMove = animal-style (wander).")]
        public BehaviorType behaviorType = BehaviorType.RotateAndInteract;

        [Header("Killer's Behavior")]
        [Tooltip("When this worker is killed, does the attacker advance into its cell? Advance = true, Stay = false.")]
        public bool killerAdvances = false;

        [Header("Tier")]
        [Tooltip("Worker tier (0-3). Used by tier-based systems. -1 = excluded from tier pools.")]
        public int tier = -1;

        [Header("Combat Stats")]
        [Tooltip("Health points. When HP reaches 0, triggers an event (removal, completion, etc.).")]
        public int hp = 3;
        [Tooltip("Damage dealt to target's HP per successful interaction.")]
        public int attackPower = 1;

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
