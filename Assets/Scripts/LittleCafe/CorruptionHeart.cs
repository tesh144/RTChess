#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System.Collections.Generic;
using ClockworkCraft;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// Placed on the map by map generation code. Represents the source of a corruption cluster.
    /// Dormant until the player reveals a tile within heartActivationRadius. Owns a set of
    /// corrupted tiles tracked by CorruptionManager. When destroyed, its entire cluster is cleared.
    ///
    /// Stats are serialized directly on the prefab — no CorruptionData/CorruptionDatabase needed.
    /// GridPosition and InitialCorruptedRadius must be set by map gen before Start().
    ///
    /// Thorns: deals thornsDamage back to any attacker on each hit.
    ///
    /// Spike growth: periodically scans the heart's corrupted cluster for tiles that are empty AND
    /// surrounded by enough corrupted neighbours (>= spikeMinCorruptedNeighbors). A spike only grows
    /// in well-established corruption — it will never appear on the edge of a fresh cluster.
    /// Spikes are never spawned by map generation; they emerge organically from within the cluster.
    /// </summary>
    public class CorruptionHeart : MonoBehaviour
    {
        // ── Stats — serialized on the prefab ─────────────────────────────
        [Header("Stats")]
        [SerializeField] private int maxHP = 10;
        [SerializeField] private int attackPower = 1;

        [Header("Thorns")]
        [Tooltip("Damage dealt back to attacker on each hit. 0 = no thorns.")]
        [SerializeField] private int thornsDamage = 1;

        [Header("Spike Growth")]
        [Tooltip("Seconds between spike-growth attempts when active. 0 = never grows spikes.")]
        [SerializeField] private float spikeSpawnInterval = 20f;

        [Tooltip("Minimum number of corrupted Moore neighbours (out of 8) a tile needs before a spike can grow there. " +
                 "8 = fully surrounded (all neighbours must be corrupted). " +
                 "Lower values allow spikes to appear on the edges of the cluster.")]
        [Range(1, 8)]
        [SerializeField] private int spikeMinCorruptedNeighbors = 8;

        [Header("Visuals")]
        [Tooltip("Billboard sprite prefab that floats above the heart. A magenta placeholder quad is used if null.")]
        [SerializeField] private GameObject floatingIndicatorPrefab;

        public bool IsActive { get; private set; } = false;

        /// <summary>Grid coordinates of this heart. Set by map generation before Start().</summary>
        public Vector2Int GridPosition { get; set; }

        public GridEntityHealth Health { get; private set; }

        /// <summary>
        /// UnitDatabase reference injected by MapGeneratorV2 after spawning.
        /// Used to look up spike prefabs at runtime.
        /// </summary>
        public UnitDatabase UnitDatabase { get; set; }

        /// <summary>
        /// Moore-neighbourhood radius of tiles pre-corrupted around this heart at Start().
        /// 1 = the 8 immediately surrounding tiles (3×3 minus centre).
        /// Set by MapGeneratorV2 from CorruptionSpawnEntry.initialCorruptionRadius.
        /// </summary>
        public int InitialCorruptedRadius { get; set; } = 1;

        private GameObject floatingIndicatorInstance;
        private float spikeSpawnTimer;
        private bool hasInitialized;

        private void Awake()
        {
            Health = gameObject.AddComponent<GridEntityHealth>();
        }

        private void Start()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// Idempotent initialization — safe to call from Start() or externally from
        /// MapGeneratorV2 (which must call this BEFORE FogHideable deactivates the GO,
        /// since Start() never fires on a deactivated GameObject).
        /// </summary>
        public void EnsureInitialized()
        {
            if (hasInitialized) return;
            hasInitialized = true;

            // workerCanInteract=true so players can attack hearts
            // isAllied=false so workers will target it
            // isSlotTakeable=false so workers don't advance into the heart's cell on kill
            Health.Initialize(maxHP, atkPower: attackPower, canInteract: true, allied: false, slotTakeable: false);
            Health.OnEntityDestroyed += OnHeartDestroyed;
            Health.OnDamagedBy += OnDamagedByAttacker;

            if (CorruptionManager.Instance != null)
            {
                CorruptionManager.Instance.RegisterHeart(this);

                // Pre-corrupt surrounding tiles immediately so the cluster has substance
                // the moment the player first sees it. The heart stays dormant (won't spread
                // or attack) until the player explores within heartActivationRadius.
                SeedInitialCorruption();
            }

            spikeSpawnTimer = spikeSpawnInterval;

            SpawnFloatingIndicator();
        }

        private void Update()
        {
            if (!IsActive) return;
            if (spikeSpawnInterval <= 0f) return;

            spikeSpawnTimer -= Time.deltaTime;
            if (spikeSpawnTimer <= 0f)
            {
                spikeSpawnTimer = spikeSpawnInterval;
                TryGrowSpike();
            }
        }

        private void OnDestroy()
        {
            if (Health != null)
            {
                Health.OnEntityDestroyed -= OnHeartDestroyed;
                Health.OnDamagedBy -= OnDamagedByAttacker;
            }

            if (floatingIndicatorInstance != null)
                Destroy(floatingIndicatorInstance);
        }

        // ── Public ────────────────────────────────────────────────────────

        /// <summary>Called by CorruptionManager when the player explores within activation radius.</summary>
        public void Activate()
        {
            if (IsActive) return;
            IsActive = true;
            Debug.Log($"[CorruptionHeart] Heart at {GridPosition} activated.");
        }

        // ── Private ───────────────────────────────────────────────────────

        /// <summary>
        /// Pre-corrupt all tiles in the Moore neighbourhood (radius = InitialCorruptedRadius)
        /// around this heart. Called once in Start(), immediately after RegisterHeart().
        /// The heart is still dormant — tiles are marked but corruption won't spread until
        /// the player activates the heart by exploring nearby.
        /// </summary>
        private void SeedInitialCorruption()
        {
            if (CorruptionManager.Instance == null) return;
            if (GridManager.Instance == null) return;
            if (InitialCorruptedRadius <= 0) return;

            int cx = GridPosition.x;
            int cy = GridPosition.y;

            for (int dx = -InitialCorruptedRadius; dx <= InitialCorruptedRadius; dx++)
            for (int dy = -InitialCorruptedRadius; dy <= InitialCorruptedRadius; dy++)
            {
                int x = cx + dx;
                int y = cy + dy;
                if (!GridManager.Instance.IsValidCell(x, y)) continue;
                // CorruptTile guards against double-corruption internally
                CorruptionManager.Instance.CorruptTile(x, y, this);
            }

            Debug.Log($"[CorruptionHeart] Seeded {(2 * InitialCorruptedRadius + 1) * (2 * InitialCorruptedRadius + 1)} tiles around heart at {GridPosition}.");
        }

        /// <summary>
        /// Thorns: deal thornsDamage back to whoever just attacked us.
        /// Only fires if thornsDamage > 0 and the attacker is alive.
        /// </summary>
        private void OnDamagedByAttacker(GridEntityHealth attacker, int damageReceived)
        {
            if (thornsDamage <= 0) return;
            if (attacker == null || attacker.IsDestroyed) return;

            // Use TakeDamage (not TakeDamageFrom) to avoid infinite retaliation loops
            attacker.TakeDamage(thornsDamage);
            Debug.Log($"[CorruptionHeart] Thorns dealt {thornsDamage} damage back to {attacker.gameObject.name}.");
        }

        private void OnHeartDestroyed(GridEntityHealth _)
        {
            if (CorruptionManager.Instance != null)
                CorruptionManager.Instance.ClearHeartCluster(this);

            Destroy(gameObject);
        }

        /// <summary>
        /// Attempt to grow a spike somewhere inside this heart's corrupted cluster.
        ///
        /// Rules:
        ///   - The target cell must already be corrupted (owned by this heart's cluster).
        ///   - The target cell must be empty (no unit or building occupying it).
        ///   - The target cell must have at least spikeMinCorruptedNeighbors corrupted Moore
        ///     neighbours — this ensures spikes only appear in well-established, dense corruption,
        ///     never on a freshly-spread edge tile.
        ///
        /// Requires UnitDatabase to be injected by MapGeneratorV2.
        /// </summary>
        private void TryGrowSpike()
        {
            if (UnitDatabase == null) return;
            if (GridManager.Instance == null) return;
            if (GridEntityManager.Instance == null) return;
            if (CorruptionManager.Instance == null) return;

            // Gather spike types from UnitDatabase — all Corruption-type entries that are NOT hearts.
            // A Corruption entry is a Heart if its prefab has a CorruptionHeart component anywhere in
            // its hierarchy, or (no prefab assigned) if its name contains "Heart".
            var spikes = UnitDatabase.GetByType(GameUnitType.Corruption);
            var validSpikes = new List<UnitData>();
            foreach (var u in spikes)
            {
                if (u.prefab == null) continue;
                bool isHeart = u.prefab.GetComponentInChildren<CorruptionHeart>() != null
                            || u.assetName.IndexOf("Heart", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (!isHeart)
                    validSpikes.Add(u);
            }
            if (validSpikes.Count == 0) return;

            // Scan this heart's cluster for cells that qualify as spike-growth sites
            var clusterTiles = CorruptionManager.Instance.GetHeartTiles(this);
            if (clusterTiles == null || clusterTiles.Count == 0) return;

            var candidates = new List<Vector2Int>();
            foreach (var coord in clusterTiles)
            {
                if (!GridManager.Instance.IsValidCell(coord.x, coord.y)) continue;
                if (!GridManager.Instance.IsCellEmpty(coord.x, coord.y)) continue;
                if (CountCorruptedNeighbors(coord.x, coord.y) < spikeMinCorruptedNeighbors) continue;
                candidates.Add(coord);
            }
            if (candidates.Count == 0) return;

            // Pick a random qualifying cell and a random spike type
            Vector2Int target   = candidates[Random.Range(0, candidates.Count)];
            UnitData spikeData  = validSpikes[Random.Range(0, validSpikes.Count)];

            Vector3 worldPos    = GridManager.Instance.GridToWorldPosition(target.x, target.y);
            GameObject spikeObj = Instantiate(spikeData.prefab, worldPos, Quaternion.identity);

            GridManager.Instance.PlaceUnit(target.x, target.y, spikeObj, CellState.EnemyUnit);

            GridEntityManager.Instance.AttachComponents(
                spikeObj,
                hp:           spikeData.hp,
                attackPower:  spikeData.attackPower,
                isActive:     spikeData.isActive,
                behaviorType: spikeData.behaviorType,
                registryName: spikeData.assetName,
                allied:       false,
                killerAdvances: spikeData.killerAdvances);

            Debug.Log($"[CorruptionHeart] Grew {spikeData.assetName} at ({target.x},{target.y}) " +
                      $"(corrupted neighbours: {CountCorruptedNeighbors(target.x, target.y)}).");
        }

        /// <summary>
        /// Count how many of the 8 Moore neighbours of (x, y) are currently corrupted.
        /// </summary>
        private int CountCorruptedNeighbors(int x, int y)
        {
            if (CorruptionManager.Instance == null) return 0;
            int count = 0;
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                if (CorruptionManager.Instance.IsCorrupted(x + dx, y + dy)) count++;
            }
            return count;
        }

        private void SpawnFloatingIndicator()
        {
            if (floatingIndicatorPrefab != null)
            {
                floatingIndicatorInstance = Instantiate(floatingIndicatorPrefab, transform.position + Vector3.up * 2.5f, Quaternion.identity);
                floatingIndicatorInstance.name = "CorruptionHeartIndicator";
            }
            else
            {
                // Placeholder: a magenta quad floating above the heart
                floatingIndicatorInstance = GameObject.CreatePrimitive(PrimitiveType.Quad);
                floatingIndicatorInstance.name = "CorruptionHeartIndicator_Placeholder";
                floatingIndicatorInstance.transform.position = transform.position + Vector3.up * 2.5f;
                floatingIndicatorInstance.transform.localScale = Vector3.one * 0.6f;

                var r = floatingIndicatorInstance.GetComponent<MeshRenderer>();
                if (r != null)
                {
                    r.material = new Material(Shader.Find("Sprites/Default"));
                    r.material.color = new Color(0.8f, 0f, 1f, 0.9f);
                    r.sortingOrder = 200;
                }

                var col = floatingIndicatorInstance.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }
        }
    }
}
