using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Spawn type for each interval in the wave sequence.
    /// </summary>
    public enum SpawnType
    {
        Nothing = 0,    // Nothing happens this interval
        Enemies = 1,    // Standard enemy units spawn
        Resources = 2,  // Resource nodes spawn
        Boss = 3        // High-value/boss enemy units spawn
    }

    /// <summary>
    /// Configuration for one interval in the wave sequence.
    /// Each entry defines what spawns during that interval tick.
    /// </summary>
    [System.Serializable]
    public class WaveEntry
    {
        [Tooltip("What spawns this interval (0=Nothing, 1=Enemies, 2=Resources, 3=Boss)")]
        public SpawnType spawnType = SpawnType.Nothing;

        [Header("Enemies (Type 1) Config")]
        [Tooltip("Number of Soldier units to spawn")]
        public int enemySoldierCount = 0;
        [Tooltip("Number of Ninja units to spawn")]
        public int enemyNinjaCount = 0;
        [Tooltip("Number of Ogre units to spawn")]
        public int enemyOgreCount = 0;

        [Header("Resources (Type 2) Config")]
        [Tooltip("Number of resource nodes to spawn")]
        public int resourceNodeCount = 0;
        [Tooltip("Resource node level (1, 2, or 3)")]
        public int resourceNodeLevel = 1;

        [Header("Boss (Type 3) Config")]
        [Tooltip("Number of boss units to spawn")]
        public int bossCount = 1;
        [Tooltip("Boss HP value")]
        public int bossHP = 30;
        [Tooltip("Boss damage value")]
        public int bossDamage = 5;
    }

    /// <summary>
    /// Wave data for backward compatibility with existing systems (e.g., GridExpansionManager).
    /// </summary>
    [System.Serializable]
    public class WaveData
    {
        public int WaveNumber;

        public WaveData(int waveNumber)
        {
            WaveNumber = waveNumber;
        }
    }

    /// <summary>
    /// Data-driven wave system manager.
    /// Executes a designer-defined sequence of spawn events, one per interval tick.
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        // Singleton
        public static WaveManager Instance { get; private set; }

        [Header("=== WAVE SEQUENCE ===")]
        [Tooltip("Each entry = 1 interval tick. 0=Nothing, 1=Enemies, 2=Resources, 3=Boss. Click + to add more intervals.")]
        public WaveEntry[] waveSequence;

        [Header("Timing")]
        [Tooltip("Number of interval ticks before starting the wave sequence (breathing room for player)")]
        [SerializeField] private int startDelayTicks = 5;

        [Header("Prefab References")]
        [SerializeField] private GameObject resourceNodePrefab;

        // State
        private int currentWaveIndex = -1; // -1 = not started, increments each interval
        private int delayTicksRemaining;
        private bool sequenceComplete = false;
        private bool gameOver = false;

        // Spawn positions (edges of grid)
        private List<Vector2Int> spawnPositions = new List<Vector2Int>();

        // Events
        public event Action<int, SpawnType> OnWaveEntryExecuted; // (index, spawnType)
        public event Action<WaveData> OnWaveComplete; // For backward compatibility
        public event Action OnSequenceComplete;
        public event Action OnVictory;
        public event Action OnDefeat;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Initialize wave system (called by GameSetup).
        /// </summary>
        public void Initialize()
        {
            InitializeSpawnPositions();

            delayTicksRemaining = startDelayTicks;
            currentWaveIndex = -1;
            sequenceComplete = false;
            gameOver = false;

            // Subscribe to interval timer
            if (IntervalTimer.Instance != null)
            {
                IntervalTimer.Instance.OnIntervalTick += OnIntervalTick;
            }

            Debug.Log($"WaveManager initialized with {waveSequence.Length} wave entries, {startDelayTicks} tick delay");
        }

        private void OnDestroy()
        {
            if (IntervalTimer.Instance != null)
            {
                IntervalTimer.Instance.OnIntervalTick -= OnIntervalTick;
            }
        }

        /// <summary>
        /// Initialize spawn positions at grid edges.
        /// </summary>
        private void InitializeSpawnPositions()
        {
            if (GridManager.Instance == null) return;

            spawnPositions.Clear();

            int width = GridManager.Instance.GridWidth;
            int height = GridManager.Instance.GridHeight;

            // Top edge
            for (int x = 0; x < width; x++)
                spawnPositions.Add(new Vector2Int(x, height - 1));

            // Bottom edge
            for (int x = 0; x < width; x++)
                spawnPositions.Add(new Vector2Int(x, 0));

            // Left edge (excluding corners)
            for (int y = 1; y < height - 1; y++)
                spawnPositions.Add(new Vector2Int(0, y));

            // Right edge (excluding corners)
            for (int y = 1; y < height - 1; y++)
                spawnPositions.Add(new Vector2Int(width - 1, y));

            Debug.Log($"WaveManager: Initialized {spawnPositions.Count} spawn positions");
        }

        /// <summary>
        /// Called every interval tick by IntervalTimer.
        /// </summary>
        private void OnIntervalTick(int intervalCount)
        {
            if (gameOver || sequenceComplete) return;

            // Handle start delay
            if (delayTicksRemaining > 0)
            {
                delayTicksRemaining--;
                Debug.Log($"WaveManager: Delay tick {startDelayTicks - delayTicksRemaining}/{startDelayTicks}");
                return;
            }

            // Advance to next wave entry
            currentWaveIndex++;

            // Check if sequence is complete
            if (currentWaveIndex >= waveSequence.Length)
            {
                CompleteSequence();
                return;
            }

            // Execute current wave entry
            ExecuteWaveEntry(currentWaveIndex);

            // Update timeline UI
            UpdateTimelineUI();

            // Check lose condition after each interval
            CheckLoseCondition();
        }

        /// <summary>
        /// Execute the spawn configuration for a wave entry.
        /// </summary>
        private void ExecuteWaveEntry(int index)
        {
            WaveEntry entry = waveSequence[index];

            Debug.Log($"WaveManager: Executing entry {index}/{waveSequence.Length - 1} - Type: {entry.spawnType}");

            switch (entry.spawnType)
            {
                case SpawnType.Nothing:
                    // Do nothing this interval
                    break;

                case SpawnType.Enemies:
                    SpawnEnemies(entry);
                    break;

                case SpawnType.Resources:
                    SpawnResources(entry);
                    break;

                case SpawnType.Boss:
                    SpawnBoss(entry);
                    break;
            }

            OnWaveEntryExecuted?.Invoke(index, entry.spawnType);

            // Fire backward-compatibility event for systems like GridExpansionManager
            OnWaveComplete?.Invoke(new WaveData(index + 1));
        }

        /// <summary>
        /// Spawn enemy units based on entry configuration.
        /// </summary>
        private void SpawnEnemies(WaveEntry entry)
        {
            int totalSpawned = 0;

            // Spawn soldiers
            for (int i = 0; i < entry.enemySoldierCount; i++)
            {
                if (SpawnSingleEnemy(UnitType.Soldier))
                    totalSpawned++;
            }

            // Spawn ninjas
            for (int i = 0; i < entry.enemyNinjaCount; i++)
            {
                if (SpawnSingleEnemy(UnitType.Ninja))
                    totalSpawned++;
            }

            // Spawn ogres
            for (int i = 0; i < entry.enemyOgreCount; i++)
            {
                if (SpawnSingleEnemy(UnitType.Ogre))
                    totalSpawned++;
            }

            Debug.Log($"WaveManager: Spawned {totalSpawned} enemies (S:{entry.enemySoldierCount} N:{entry.enemyNinjaCount} O:{entry.enemyOgreCount})");
        }

        /// <summary>
        /// Spawn a single enemy unit at a random spawn position.
        /// </summary>
        private bool SpawnSingleEnemy(UnitType type)
        {
            if (GridManager.Instance == null || RaritySystem.Instance == null)
                return false;

            // Find available spawn position
            List<Vector2Int> availablePositions = new List<Vector2Int>(spawnPositions);
            availablePositions.RemoveAll(pos => !GridManager.Instance.IsCellEmpty(pos.x, pos.y));

            if (availablePositions.Count == 0)
            {
                Debug.LogWarning($"WaveManager: No available spawn positions for {type}");
                return false;
            }

            Vector2Int pos = availablePositions[UnityEngine.Random.Range(0, availablePositions.Count)];

            // Get enemy stats
            UnitStats enemyStats = RaritySystem.Instance.GetUnitStats(type);
            if (enemyStats == null)
            {
                Debug.LogWarning($"WaveManager: No stats found for {type}");
                return false;
            }

            GameObject prefabToSpawn = enemyStats.enemyPrefab != null ? enemyStats.enemyPrefab : enemyStats.unitPrefab;
            if (prefabToSpawn == null)
            {
                Debug.LogWarning($"WaveManager: No prefab for {type}");
                return false;
            }

            // Spawn enemy
            Vector3 worldPos = GridManager.Instance.GridToWorldPosition(pos.x, pos.y);
            GameObject enemyObj = Instantiate(prefabToSpawn, worldPos, prefabToSpawn.transform.rotation);
            enemyObj.SetActive(true);

            Unit unit = enemyObj.GetComponent<Unit>();
            if (unit != null)
            {
                unit.Initialize(Team.Enemy, pos.x, pos.y, enemyStats);
            }

            GridManager.Instance.PlaceUnit(pos.x, pos.y, enemyObj, CellState.EnemyUnit);
            return true;
        }

        /// <summary>
        /// Spawn resource nodes based on entry configuration.
        /// </summary>
        private void SpawnResources(WaveEntry entry)
        {
            if (resourceNodePrefab == null)
            {
                Debug.LogWarning("WaveManager: No resource node prefab assigned");
                return;
            }

            int spawned = 0;

            for (int i = 0; i < entry.resourceNodeCount; i++)
            {
                if (SpawnSingleResource(entry.resourceNodeLevel))
                    spawned++;
            }

            Debug.Log($"WaveManager: Spawned {spawned}/{entry.resourceNodeCount} resource nodes (Level {entry.resourceNodeLevel})");
        }

        /// <summary>
        /// Spawn a single resource node at a random spawn position.
        /// </summary>
        private bool SpawnSingleResource(int level)
        {
            if (GridManager.Instance == null) return false;

            // Find available spawn position
            List<Vector2Int> availablePositions = new List<Vector2Int>(spawnPositions);
            availablePositions.RemoveAll(pos => !GridManager.Instance.IsCellEmpty(pos.x, pos.y));

            if (availablePositions.Count == 0)
            {
                Debug.LogWarning("WaveManager: No available spawn positions for resource");
                return false;
            }

            Vector2Int pos = availablePositions[UnityEngine.Random.Range(0, availablePositions.Count)];

            // Spawn resource node
            Vector3 worldPos = GridManager.Instance.GridToWorldPosition(pos.x, pos.y);
            GameObject nodeObj = Instantiate(resourceNodePrefab, worldPos, Quaternion.identity);
            nodeObj.SetActive(true);

            ResourceNode node = nodeObj.GetComponent<ResourceNode>();
            if (node != null)
            {
                node.GridX = pos.x;
                node.GridY = pos.y;
                // Initialize with size based on level (1x1 for level 1)
                node.Initialize(new Vector2Int(1, 1));
            }

            GridManager.Instance.PlaceUnit(pos.x, pos.y, nodeObj, CellState.Resource);
            return true;
        }

        /// <summary>
        /// Spawn boss enemies based on entry configuration.
        /// </summary>
        private void SpawnBoss(WaveEntry entry)
        {
            int spawned = 0;

            for (int i = 0; i < entry.bossCount; i++)
            {
                if (SpawnSingleBoss(entry.bossHP, entry.bossDamage))
                    spawned++;
            }

            Debug.Log($"WaveManager: Spawned {spawned}/{entry.bossCount} bosses (HP:{entry.bossHP} DMG:{entry.bossDamage})");
        }

        /// <summary>
        /// Spawn a single boss unit with custom stats.
        /// </summary>
        private bool SpawnSingleBoss(int hp, int damage)
        {
            if (GridManager.Instance == null || RaritySystem.Instance == null)
                return false;

            // Find available spawn position
            List<Vector2Int> availablePositions = new List<Vector2Int>(spawnPositions);
            availablePositions.RemoveAll(pos => !GridManager.Instance.IsCellEmpty(pos.x, pos.y));

            if (availablePositions.Count == 0)
            {
                Debug.LogWarning("WaveManager: No available spawn positions for boss");
                return false;
            }

            Vector2Int pos = availablePositions[UnityEngine.Random.Range(0, availablePositions.Count)];

            // Use Ogre as boss prefab
            UnitStats bossStats = RaritySystem.Instance.GetUnitStats(UnitType.Ogre);
            if (bossStats == null)
            {
                Debug.LogWarning("WaveManager: No Ogre stats for boss");
                return false;
            }

            // Create custom boss stats
            UnitStats customBossStats = ScriptableObject.CreateInstance<UnitStats>();
            customBossStats.unitType = bossStats.unitType;
            customBossStats.unitName = "BOSS";
            customBossStats.rarity = Rarity.Epic;
            customBossStats.maxHP = hp;
            customBossStats.attackDamage = damage;
            customBossStats.attackRange = bossStats.attackRange;
            customBossStats.attackIntervalMultiplier = bossStats.attackIntervalMultiplier;
            customBossStats.resourceCost = 0;
            customBossStats.unitPrefab = bossStats.unitPrefab;
            customBossStats.enemyPrefab = bossStats.enemyPrefab;

            GameObject prefabToSpawn = customBossStats.enemyPrefab != null ? customBossStats.enemyPrefab : customBossStats.unitPrefab;
            if (prefabToSpawn == null)
            {
                Debug.LogWarning("WaveManager: No boss prefab");
                return false;
            }

            // Spawn boss
            Vector3 worldPos = GridManager.Instance.GridToWorldPosition(pos.x, pos.y);
            GameObject bossObj = Instantiate(prefabToSpawn, worldPos, prefabToSpawn.transform.rotation);
            bossObj.SetActive(true);

            Unit unit = bossObj.GetComponent<Unit>();
            if (unit != null)
            {
                unit.Initialize(Team.Enemy, pos.x, pos.y, customBossStats);
            }

            GridManager.Instance.PlaceUnit(pos.x, pos.y, bossObj, CellState.EnemyUnit);
            return true;
        }

        /// <summary>
        /// Update the timeline UI with current progress.
        /// </summary>
        private void UpdateTimelineUI()
        {
            if (SpawnTimelineUI.Instance != null)
            {
                // Convert wave sequence to spawn code format for existing UI
                string spawnCode = ConvertSequenceToSpawnCode();
                SpawnTimelineUI.Instance.InitializeWave(1, spawnCode);

                // Advance to current index
                for (int i = 0; i <= currentWaveIndex; i++)
                {
                    SpawnTimelineUI.Instance.AdvanceDot();
                }
            }
        }

        /// <summary>
        /// Convert WaveEntry array to spawn code format (for timeline UI compatibility).
        /// </summary>
        private string ConvertSequenceToSpawnCode()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            foreach (WaveEntry entry in waveSequence)
            {
                char code = '0';
                switch (entry.spawnType)
                {
                    case SpawnType.Nothing: code = '0'; break;
                    case SpawnType.Enemies: code = '1'; break;
                    case SpawnType.Resources: code = '2'; break;
                    case SpawnType.Boss: code = '3'; break;
                }
                sb.Append(code);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Complete the wave sequence (player survived all entries).
        /// </summary>
        private void CompleteSequence()
        {
            sequenceComplete = true;
            gameOver = true;

            Debug.Log("WaveManager: Sequence complete - VICTORY!");

            OnSequenceComplete?.Invoke();
            OnVictory?.Invoke();

            // Pause interval timer
            if (IntervalTimer.Instance != null)
            {
                IntervalTimer.Instance.Pause();
            }
        }

        /// <summary>
        /// Check lose condition:
        /// - Zero player units on grid
        /// - Zero units in hand (dock)
        /// - Not enough tokens to draw
        /// </summary>
        private void CheckLoseCondition()
        {
            if (gameOver) return;

            // Check if player has any units on grid
            bool hasUnitsOnGrid = false;
            if (GridManager.Instance != null)
            {
                for (int x = 0; x < GridManager.Instance.GridWidth; x++)
                {
                    for (int y = 0; y < GridManager.Instance.GridHeight; y++)
                    {
                        if (GridManager.Instance.GetCellState(x, y) == CellState.PlayerUnit)
                        {
                            hasUnitsOnGrid = true;
                            break;
                        }
                    }
                    if (hasUnitsOnGrid) break;
                }
            }

            // Check if player has units in hand
            bool hasUnitsInHand = false;
            if (DockBarManager.Instance != null)
            {
                hasUnitsInHand = DockBarManager.Instance.GetUnitCount() > 0;
            }

            // Check if player can afford to draw
            bool canAffordDraw = false;
            if (DockBarManager.Instance != null && ResourceTokenManager.Instance != null)
            {
                int drawCost = DockBarManager.Instance.GetCurrentDrawCost();
                canAffordDraw = ResourceTokenManager.Instance.HasEnoughTokens(drawCost);
            }

            // Lose condition: no units on grid AND no units in hand AND can't afford draw
            if (!hasUnitsOnGrid && !hasUnitsInHand && !canAffordDraw)
            {
                TriggerDefeat();
            }
        }

        /// <summary>
        /// Trigger defeat (lose condition met).
        /// </summary>
        private void TriggerDefeat()
        {
            gameOver = true;

            Debug.Log("WaveManager: Lose condition met - DEFEAT!");

            OnDefeat?.Invoke();

            // Pause interval timer
            if (IntervalTimer.Instance != null)
            {
                IntervalTimer.Instance.Pause();
            }
        }

        // Public accessors for UI/other systems
        public int CurrentWaveIndex => currentWaveIndex;
        public int TotalWaveEntries => waveSequence.Length;
        public bool IsSequenceComplete => sequenceComplete;
        public bool IsGameOver => gameOver;

        /// <summary>
        /// Get the current wave entry being executed.
        /// </summary>
        public WaveEntry GetCurrentEntry()
        {
            if (currentWaveIndex >= 0 && currentWaveIndex < waveSequence.Length)
                return waveSequence[currentWaveIndex];
            return null;
        }
    }
}
