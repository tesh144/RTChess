using UnityEngine;

namespace ClockworkCraft
{
    [CreateAssetMenu(fileName = "MapGenerationSettings", menuName = "ClockworkCraft/Map Generation Settings")]
    public class MapGenerationSettings : ScriptableObject
    {
        [Header("Map Size")]
        public int mapWidth = 40;
        public int mapHeight = 40;
        [Tooltip("0 = random seed each run. Any other value = deterministic map.")]
        public int seed = 0;

        [Header("Clear Zone (around Town Hall)")]
        [Tooltip("Cells within this radius of the Town Hall center are always kept empty.")]
        public int clearRadius = 3;

        [Header("Guaranteed Starting Resources")]
        [Tooltip("Min distance from Town Hall for the guaranteed starting Gold Mine.")]
        public int goldMineMinDist = 4;
        [Tooltip("Max distance from Town Hall for the guaranteed starting Gold Mine.")]
        public int goldMineMaxDist = 7;
        [Tooltip("How many tree clusters to guarantee near the starting area.")]
        [Range(1, 5)] public int guaranteedTreeClusters = 3;
        [Tooltip("How many Wild Farm nodes to guarantee near the starting area.")]
        [Range(1, 3)] public int guaranteedFarms = 1;

        [Header("Trees (Perlin Noise)")]
        [Tooltip("Perlin noise threshold. Higher = fewer trees. 0.4 = ~40% coverage.")]
        [Range(0f, 1f)] public float treeDensityThreshold = 0.42f;
        [Tooltip("Noise scale. Larger value = bigger forest blobs.")]
        public float treeNoiseScale = 6f;
        [Tooltip("Post-process pass that fills gaps between nearby trees to create strings.")]
        public bool enableStringPass = true;
        [Tooltip("Max gap length to fill during the string pass (in cells).")]
        [Range(1, 3)] public int stringFillGap = 1;

        [Header("Rivers / Water")]
        [Range(0, 4)] public int riverCount = 2;
        [Tooltip("Minimum river length in tiles.")]
        public int riverMinLength = 12;
        [Tooltip("Maximum river length in tiles.")]
        public int riverMaxLength = 30;
        [Tooltip("Chance each river step also marks an adjacent cell as Water (widens the river).")]
        [Range(0f, 0.5f)] public float riverWidenChance = 0.15f;

        [Header("Gold Mines")]
        [Tooltip("Per-cell probability of spawning a Gold Mine (outside clear zone).")]
        [Range(0f, 0.1f)] public float goldMineDensity = 0.015f;
        [Tooltip("Minimum distance between any two Gold Mine nodes.")]
        public int goldMineMinSpacing = 6;

        [Header("Wild Farms")]
        [Range(0f, 0.1f)] public float farmDensity = 0.020f;
        public int farmMinSpacing = 5;

        [Header("Rocks (Locked)")]
        [Range(0f, 0.1f)] public float rockDensity = 0.012f;
        public int rockMinSpacing = 4;

        [Header("Fog of War")]
        [Tooltip("Number of cells revealed around the Town Hall at run start.")]
        public int startingRevealRadius = 4;
    }
}
