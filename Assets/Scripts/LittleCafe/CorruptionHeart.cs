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
    /// GridPosition must be set by map gen before Start().
    ///
    /// Thorns: deals 1 damage back to any attacker on each hit.
    /// Spike spawning: periodically places spike units on adjacent empty cells when active.
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

        [Header("Spike Spawning")]
        [Tooltip("Seconds between spike spawn attempts when active. 0 = never spawns.")]
        [SerializeField] private float spikeSpawnInterval = 20f;

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

        private GameObject floatingIndicatorInstance;
        private float spikeSpawnTimer;

        private void Awake()
        {
            Health = gameObject.AddComponent<GridEntityHealth>();
        }

        private void Start()
        {
            // workerCanInteract=true so players can attack hearts
            // isAllied=false so workers will target it
            // isSlotTakeable=false so workers don't advance into the heart's cell on kill
            Health.Initialize(maxHP, atkPower: attackPower, canInteract: true, allied: false, slotTakeable: false);
            Health.OnEntityDestroyed += OnHeartDestroyed;
            Health.OnDamagedBy += OnDamagedByAttacker;

            if (CorruptionManager.Instance != null)
                CorruptionManager.Instance.RegisterHeart(this);

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
                TrySpawnSpike();
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
        /// Attempt to spawn a spike unit on a random adjacent empty cell.
        /// Requires UnitDatabase to be injected by MapGeneratorV2.
        /// </summary>
        private void TrySpawnSpike()
        {
            if (UnitDatabase == null) return;
            if (GridManager.Instance == null) return;
            if (GridEntityManager.Instance == null) return;

            // Gather spike entries from UnitDatabase (Corruption type, not the heart itself)
            var spikes = UnitDatabase.GetByType(GameUnitType.Corruption);
            var validSpikes = new List<UnitData>();
            foreach (var u in spikes)
            {
                if (u.assetName != "CorruptedHeart" && u.prefab != null)
                    validSpikes.Add(u);
            }
            if (validSpikes.Count == 0) return;

            // Find empty cardinal-adjacent cells
            var emptyNeighbours = new List<Vector2Int>();
            Vector2Int[] offsets = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            foreach (var offset in offsets)
            {
                Vector2Int cell = GridPosition + offset;
                if (GridManager.Instance.IsValidCell(cell.x, cell.y) &&
                    GridManager.Instance.IsCellEmpty(cell.x, cell.y))
                {
                    emptyNeighbours.Add(cell);
                }
            }
            if (emptyNeighbours.Count == 0) return;

            // Pick a random cell and random spike type
            Vector2Int target = emptyNeighbours[Random.Range(0, emptyNeighbours.Count)];
            UnitData spikeData = validSpikes[Random.Range(0, validSpikes.Count)];

            Vector3 worldPos = GridManager.Instance.GridToWorldPosition(target.x, target.y);
            GameObject spikeObj = Instantiate(spikeData.prefab, worldPos, Quaternion.identity);

            GridManager.Instance.PlaceUnit(target.x, target.y, spikeObj, CellState.EnemyUnit);

            GridEntityManager.Instance.AttachComponents(
                spikeObj,
                hp: spikeData.hp,
                attackPower: spikeData.attackPower,
                isActive: spikeData.isActive,
                behaviorType: spikeData.behaviorType,
                registryName: spikeData.assetName,
                allied: false,
                killerAdvances: spikeData.killerAdvances);

            Debug.Log($"[CorruptionHeart] Spawned {spikeData.assetName} at ({target.x},{target.y}).");
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
