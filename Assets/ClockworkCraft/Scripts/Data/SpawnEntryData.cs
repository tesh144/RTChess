#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;

namespace ClockworkCraft
{
    /// <summary>
    /// How an environment entry is distributed across the map.
    /// </summary>
    public enum SpawnMode
    {
        Scattered,  // Random per-tile placement with optional min spacing
        Clustered,  // BFS blobs from random seed points
        Edge,       // Spawns along the border of existing clusters of a specific type
        OnTop       // Spawns on the Object layer above existing Surface tiles
    }

    /// <summary>
    /// Spawn config for a single EnvironmentDatabase entry.
    /// Lives on MapGeneratorV2 so all distribution tuning is in one place.
    ///
    /// spawnWeight (1-20) is a RELATIVE proportion shared by ALL entries regardless
    /// of spawn mode. The master mapDensity slider controls total tile coverage,
    /// and each entry's weight determines its share of that budget.
    /// </summary>
    [System.Serializable]
    public class EnvironmentSpawnEntry
    {
        [HideInInspector] public string environmentName;

        [Tooltip("How this type is placed on the map.")]
        public SpawnMode spawnMode = SpawnMode.Scattered;

        [Tooltip("Relative weight (0-20). Determines this entry's share of the total tile budget. 0 = disabled. Higher = more tiles of this type.")]
        [Range(0f, 20f)] public float spawnWeight = 5f;

        [Tooltip("Minimum cell distance between two instances (Scattered mode only).")]
        [Min(0)] public int minSpacing = 0;

        [Tooltip("Won't appear within this many tiles of the starting gold mine (Scattered mode only). Independent of the global Clearing Radius — whichever is larger takes effect.")]
        [Min(0)] public int clearFromCenter = 0;

        // ── Cluster settings (only used when spawnMode == Clustered) ──
        [Tooltip("0 = few large cohesive blobs. 1 = many small clusters with loose break-off pieces.")]
        [Range(0f, 1f)] public float fragmentation = 0.3f;
        [Tooltip("Blob shape: 0 = stringy/organic, 1 = round/blobby.")]
        [Range(0f, 1f)] public float clusterSpread = 0.6f;

        // ── Edge settings (only used when spawnMode == Edge) ──
        [Tooltip("Name of the environment type whose clusters this should border.")]
        public string edgeBorderOf = "";

        // ── OnTop settings (only used when spawnMode == OnTop) ──
        [Tooltip("Surface type this object spawns on top of (OnTop mode only).")]
        public ClockworkGrid.SurfaceType requiredSurface = ClockworkGrid.SurfaceType.Water;

        [Tooltip("Fraction of qualifying surface tiles to cover (0-1). 0.3 = 30% of matching tiles.")]
        [Range(0f, 1f)] public float coveragePercent = 0.3f;
    }

    /// <summary>
    /// Spawn config for a single UnitDatabase entry (beasts, monsters, enemies).
    /// Same spawn modes as environment but spawns units with CellState.EnemyUnit.
    /// </summary>
    [System.Serializable]
    public class UnitSpawnEntry
    {
        [HideInInspector] public string unitName;

        [Tooltip("How this unit type is placed on the map.")]
        public SpawnMode spawnMode = SpawnMode.Scattered;

        [Tooltip("Relative weight (0-20). Determines this entry's share of the unit tile budget. 0 = disabled.")]
        [Range(0f, 20f)] public float spawnWeight = 2f;

        [Tooltip("Minimum cell distance between two instances (Scattered mode only).")]
        [Min(0)] public int minSpacing = 4;

        [Tooltip("Won't appear within this many tiles of the starting gold mine (Scattered mode only). Independent of the global Clearing Radius — whichever is larger takes effect.")]
        [Min(0)] public int clearFromCenter = 0;

        // ── Cluster settings ──
        [Tooltip("0 = few large packs. 1 = many small scattered groups.")]
        [Range(0f, 1f)] public float fragmentation = 0.5f;
        [Tooltip("Pack shape: 0 = stringy/spread out, 1 = tight/round.")]
        [Range(0f, 1f)] public float clusterSpread = 0.4f;

        // ── Edge settings ──
        [Tooltip("Name of the environment or unit type whose clusters this should spawn near.")]
        public string edgeBorderOf = "";
    }

    /// <summary>
    /// Spawn config for a single corruption heart prefab.
    /// Uses an explicit spawnCount instead of a proportional tile budget, because
    /// corruption entities are sparse and their exact count matters for game balance.
    /// Populated by SyncCorruptionSpawnEntries() from UnitDatabase entries of type Corruption.
    /// </summary>
    [System.Serializable]
    public class CorruptionSpawnEntry
    {
        /// <summary>Asset name from UnitData — used as the planGrid key. Populated during sync.</summary>
        [HideInInspector] public string entityName;

        [Tooltip("CorruptionHeart prefab to instantiate. Populated automatically by Sync from Database.")]
        public GameObject prefab;

        [Tooltip("How this corruption entity is distributed on the map.")]
        public SpawnMode spawnMode = SpawnMode.Scattered;

        [Tooltip("Exact number of this entity to place on the map. Set to 0 to disable.")]
        [Min(0)] public int spawnCount = 5;

        [Tooltip("Won't appear within this many tiles of the starting gold mine.")]
        [Min(0)] public int clearFromCenter = 10;

        [Tooltip("Minimum Chebyshev distance between two instances of this entity (Scattered mode).")]
        [Min(0)] public int minSpacing = 8;

        // ── Cluster settings (Clustered mode) ──
        [Tooltip("0 = few large clusters. 1 = many small scattered groups.")]
        [Range(0f, 1f)] public float fragmentation = 0.4f;
        [Tooltip("Cluster shape: 0 = stringy/organic, 1 = round/blobby.")]
        [Range(0f, 1f)] public float clusterSpread = 0.5f;

        // ── Edge settings (Edge mode) ──
        [Tooltip("Name of the environment or unit type whose clusters this should spawn near.")]
        public string edgeBorderOf = "";

        // ── Corruption seeding ──
        [Tooltip("Moore-neighborhood radius of tiles pre-corrupted around each heart at spawn time. " +
                 "1 = the 8 immediately surrounding tiles. 0 = no pre-seeding.")]
        [Min(0)] public int initialCorruptionRadius = 1;
    }
}
