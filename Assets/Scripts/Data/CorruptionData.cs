#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;

namespace LittleCafe
{
    /// <summary>
    /// Configuration data for a single corruption entity type.
    /// Stored in CorruptionDatabase. Stats are read by CorruptionHeart.Initialize()
    /// at spawn time so the prefab component carries no hardcoded values.
    /// </summary>
    [System.Serializable]
    public class CorruptionData
    {
        [Tooltip("Unique name used to reference this entity in CorruptionSpawnEntry.")]
        public string entityName;

        [Tooltip("Prefab to instantiate. Must have a CorruptionHeart component.")]
        public GameObject prefab;

        [Tooltip("Icon used in UI/map overlays.")]
        public Sprite icon;

        [Header("Combat")]
        [Tooltip("Max HP of the corruption entity.")]
        [Min(1)] public int hp = 10;

        [Tooltip("Attack power dealt per interaction.")]
        [Min(0)] public int attackPower = 1;

        [Header("Visuals")]
        [Tooltip("Billboard sprite prefab that floats above the entity. Optional — a placeholder quad is used if null.")]
        public GameObject floatingIndicatorPrefab;
    }
}
