using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// ScriptableObject containing stats for a resource node level.
    /// Iteration 8: Supports multi-cell resources (Level 2: 2 cells, Level 3: 4 cells)
    /// </summary>
    [CreateAssetMenu(fileName = "New Resource Node Stats", menuName = "ClockworkGrid/Resource Node Stats")]
    public class ResourceNodeStats : ScriptableObject
    {
        [Header("Identity")]
        public int level = 1; // 1, 2, or 3
        public string nodeName = "Resource Node";

        [Header("Combat Stats")]
        public int maxHP = 10;

        [Header("Grid Occupation")]
        public Vector2Int gridSize = new Vector2Int(1, 1); // Level 1: 1x1, Level 2: 2x1, Level 3: 2x2

        [Header("Economy")]
        public int tokensPerHit = 1; // Tokens per HP damage
        public int bonusTokens = 3; // Bonus tokens when fully destroyed

        [Header("Visuals")]
        public Color nodeColor = new Color(0.2f, 0.85f, 0.4f); // Green
        public float modelScale = 1f; // Visual scale multiplier

        [Header("References")]
        public GameObject nodePrefab; // Prefab to spawn
    }
}
