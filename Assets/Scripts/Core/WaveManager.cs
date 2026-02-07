using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Wave state machine
    /// </summary>
    public enum WaveState
    {
        Preparation,  // Downtime between waves
        Active,       // Wave is currently spawned
        Complete      // Wave cleared, transitioning to next
    }

    /// <summary>
    /// Wave configuration (Inspector-editable)
    /// Spawn code format: "10102" = Enemy, Empty, Enemy, Empty, Resource
    /// 0 = Empty tick, 1 = Enemy, 2 = Resource, 3 = Boss
    /// </summary>
    [System.Serializable]
    public class WaveConfiguration
    {
        [Tooltip("Wave number (1, 2, 3...)")]
        public int waveNumber = 1;

        [Tooltip("Spawn code: 0=Empty, 1=Enemy, 2=Resource, 3=Boss (e.g., '10102')")]
        public string spawnCode = "10102";

        [Tooltip("Number of ticks for peace period after this wave")]
        public int peacePeriodTicks = 10;

        [Tooltip("Description for designers (not used in gameplay)")]
        public string description = "";
    }

    /// <summary>
    /// Wave data structure (runtime)
    /// </summary>
    [System.Serializable]
    public class WaveData
    {
        public int WaveNumber;
        public int SpawnInterval; // Which interval this wave spawns on
        public int EnemyCount;
        public string WaveType; // "Easy", "Medium", "Hard", "Boss"
        public string SpawnCode; // Spawn code for timeline UI

        public WaveData(int number, int interval, int enemies, string type, string code = "")
        {
            WaveNumber = number;
            SpawnInterval = interval;
            EnemyCount = enemies;
            WaveType = type;
            SpawnCode = code;
        }
    }

    /// <summary>
    /// Manages enemy wave spawning system.
    /// Generates waves at fixed intervals with escalating difficulty.
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        // Singleton
        public static WaveManager Instance { get; private set; }

        [Header("Wave Mode")]
        [SerializeField] private bool useInspectorWaves = true;
        [Tooltip("Define custom waves in Inspector. Uncheck to use procedural generation.")]

        [Header("Inspector Wave Configuration")]
        [SerializeField] private List<WaveConfiguration> inspectorWaves = new List<WaveConfiguration>();

        [Header("Procedural Wave Configuration (if not using Inspector waves)")]
        [SerializeField] private int intervalsPerWave = 20; // Wave every 20 intervals (40 seconds)
        [SerializeField] private int baseEnemyCount = 2;
        [SerializeField] private float enemyScaling = 1.2f;
        [SerializeField] private int maxWaves = 20; // Win condition: survive 20 waves

        [Header("Downtime Configuration")]
        [SerializeField] private float baseDowntime = 10f; // Seconds between waves
        [SerializeField] private float minDowntime = 5f; // Minimum downtime (after wave 10)
        [SerializeField] private int downtimeDecreaseWave = 10; // Wave when downtime starts decreasing

        // Wave state
        private WaveState currentState = WaveState.Preparation;
        private List<WaveData> upcomingWaves = new List<WaveData>();
        private int currentWaveIndex = 0;
        private int nextWaveInterval = 0;

        // Downtime tracking
        private float downtimeRemaining = 0f;
        private int enemiesSpawnedThisWave = 0;

        // Enemy spawning
        [SerializeField] private GameObject enemySoldierPrefab;
        [SerializeField] private GameObject resourceNodePrefab;
        private List<Vector2Int> spawnPositions = new List<Vector2Int>();

        // Events
        public event Action<WaveData> OnWaveSpawned;
        public event Action<WaveData> OnWaveWarning; // Fired 5 intervals before wave
        public event Action<WaveData> OnWaveComplete; // Fired when wave is cleared
        public event Action<List<WaveData>> OnWavesUpdated;
        public event Action<WaveState> OnWaveStateChanged;
        public event Action OnVictory; // Player wins (survived 20 waves)
        public event Action OnDefeat; // Player loses

        // Public accessors
        public List<WaveData> UpcomingWaves => upcomingWaves;
        public int NextWaveInterval => nextWaveInterval;
        public int CurrentWaveNumber => currentWaveIndex;
        public WaveState CurrentState => currentState;
        public float DowntimeRemaining => downtimeRemaining;
        public bool IsGameOver { get; private set; } = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Initialize()
        {
            // Iteration 6: No longer needs enemy prefab, uses RaritySystem

            // Define spawn positions (right side of grid for now)
            if (GridManager.Instance != null)
            {
                int gridWidth = GridManager.Instance.Width;
                int gridHeight = GridManager.Instance.Height;

                // Spawn on right edge (all cells on rightmost column)
                for (int y = 0; y < gridHeight; y++)
                {
                    spawnPositions.Add(new Vector2Int(gridWidth - 1, y));
                }
            }

            // Generate initial waves
            GenerateUpcomingWaves();

            // Subscribe to interval timer
            if (IntervalTimer.Instance != null)
            {
                IntervalTimer.Instance.OnIntervalTick += OnIntervalTick;
            }

            // Start with preparation state (downtime before wave 1)
            SetWaveState(WaveState.Preparation);
            downtimeRemaining = CalculateDowntime(1);
        }

        private void OnDestroy()
        {
            if (IntervalTimer.Instance != null)
            {
                IntervalTimer.Instance.OnIntervalTick -= OnIntervalTick;
            }
        }

        private void Update()
        {
            if (IsGameOver) return;

            // Update downtime countdown
            if (currentState == WaveState.Preparation && downtimeRemaining > 0f)
            {
                downtimeRemaining -= Time.deltaTime;

                if (downtimeRemaining <= 0f)
                {
                    downtimeRemaining = 0f;
                    // Transition to Active state and spawn wave
                    SpawnWave();
                }
            }

            // Check for wave completion during Active state
            if (currentState == WaveState.Active)
            {
                if (AreAllEnemiesDead())
                {
                    OnWaveCleared();
                }
            }

            // Check win/lose conditions
            CheckGameOverConditions();
        }

        /// <summary>
        /// Set wave state and fire event
        /// </summary>
        private void SetWaveState(WaveState newState)
        {
            if (currentState != newState)
            {
                currentState = newState;
                OnWaveStateChanged?.Invoke(newState);
                Debug.Log($"Wave state changed to: {newState}");
            }
        }

        /// <summary>
        /// Calculate downtime for a wave (decreases after wave 10)
        /// </summary>
        private float CalculateDowntime(int waveNumber)
        {
            if (waveNumber >= downtimeDecreaseWave)
            {
                return minDowntime;
            }
            return baseDowntime;
        }

        /// <summary>
        /// Check if all enemies have been cleared
        /// </summary>
        private bool AreAllEnemiesDead()
        {
            if (GridManager.Instance == null) return false;

            // Count enemy units on grid
            int enemyCount = 0;
            for (int x = 0; x < GridManager.Instance.Width; x++)
            {
                for (int y = 0; y < GridManager.Instance.Height; y++)
                {
                    if (GridManager.Instance.GetCellState(x, y) == CellState.EnemyUnit)
                    {
                        enemyCount++;
                    }
                }
            }

            return enemyCount == 0 && enemiesSpawnedThisWave > 0;
        }

        /// <summary>
        /// Called when a wave is cleared
        /// </summary>
        private void OnWaveCleared()
        {
            if (currentState != WaveState.Active) return;

            Debug.Log($"Wave {currentWaveIndex} cleared!");

            // Fire completion event
            WaveData completedWave = new WaveData(currentWaveIndex, 0, enemiesSpawnedThisWave, "Cleared");
            OnWaveComplete?.Invoke(completedWave);

            // Transition to Complete state
            SetWaveState(WaveState.Complete);

            // Check for victory
            int maxWaveCount = useInspectorWaves && inspectorWaves != null ? inspectorWaves.Count : maxWaves;
            if (currentWaveIndex >= maxWaveCount)
            {
                TriggerVictory();
                return;
            }

            // Start downtime for next wave
            SetWaveState(WaveState.Preparation);

            // Get peace period from Inspector config or use default
            float peacePeriod = CalculatePeacePeriod(currentWaveIndex);
            downtimeRemaining = peacePeriod;

            // Show peace period UI with next wave preview
            if (SpawnTimelineUI.Instance != null && upcomingWaves.Count > 0)
            {
                int ticksRemaining = Mathf.CeilToInt(peacePeriod / 2f); // Convert seconds to ticks (2 sec per tick)
                SpawnTimelineUI.Instance.ShowPeacePeriod(ticksRemaining, upcomingWaves[0].WaveNumber, upcomingWaves[0].SpawnCode);
            }
        }

        /// <summary>
        /// Calculate peace period for current wave
        /// </summary>
        private float CalculatePeacePeriod(int completedWaveIndex)
        {
            if (useInspectorWaves && inspectorWaves != null && completedWaveIndex > 0 && completedWaveIndex <= inspectorWaves.Count)
            {
                // Use peace period from completed wave's config
                int configIndex = completedWaveIndex - 1;
                if (configIndex >= 0 && configIndex < inspectorWaves.Count)
                {
                    return inspectorWaves[configIndex].peacePeriodTicks * 2f; // Ticks to seconds (2 sec per tick)
                }
            }

            // Fallback to old downtime calculation
            return CalculateDowntime(completedWaveIndex + 1);
        }

        /// <summary>
        /// Check for game over conditions (win/lose)
        /// </summary>
        private void CheckGameOverConditions()
        {
            if (IsGameOver) return;

            // Check lose condition: no units on grid, no units in hand, can't afford to draw
            if (currentState == WaveState.Active)
            {
                int playerUnits = CountPlayerUnits();
                int handSize = HandManager.Instance != null ? HandManager.Instance.GetHandSize() : 0;
                int drawCost = HandManager.Instance != null ? HandManager.Instance.CalculateDrawCost() : 999;
                int tokens = ResourceTokenManager.Instance != null ? ResourceTokenManager.Instance.CurrentTokens : 0;

                if (playerUnits == 0 && handSize == 0 && tokens < drawCost)
                {
                    TriggerDefeat();
                }
            }
        }

        /// <summary>
        /// Count player units on the grid
        /// </summary>
        private int CountPlayerUnits()
        {
            if (GridManager.Instance == null) return 0;

            int count = 0;
            for (int x = 0; x < GridManager.Instance.Width; x++)
            {
                for (int y = 0; y < GridManager.Instance.Height; y++)
                {
                    if (GridManager.Instance.GetCellState(x, y) == CellState.PlayerUnit)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// Trigger victory state
        /// </summary>
        private void TriggerVictory()
        {
            IsGameOver = true;
            Debug.Log("VICTORY! Player survived 20 waves!");
            OnVictory?.Invoke();
        }

        /// <summary>
        /// Trigger defeat state
        /// </summary>
        private void TriggerDefeat()
        {
            IsGameOver = true;
            Debug.Log("DEFEAT! No units, no hand, no tokens!");
            OnDefeat?.Invoke();
        }

        private void OnIntervalTick(int currentInterval)
        {
            // Wave spawning is now handled by Update() based on downtime countdown
            // OnIntervalTick is kept for future use if needed
        }

        /// <summary>
        /// Generate the next 5 waves (Inspector mode or procedural)
        /// </summary>
        private void GenerateUpcomingWaves()
        {
            upcomingWaves.Clear();

            if (useInspectorWaves && inspectorWaves != null && inspectorWaves.Count > 0)
            {
                // Use Inspector-defined waves
                int startWave = currentWaveIndex + 1;

                for (int i = 0; i < 5 && (startWave + i - 1) < inspectorWaves.Count; i++)
                {
                    int waveIndex = startWave + i - 1;
                    WaveConfiguration config = inspectorWaves[waveIndex];

                    // Count enemies in spawn code
                    int enemies = CountEnemiesInSpawnCode(config.spawnCode);
                    string type = DetermineWaveType(config.spawnCode);

                    WaveData wave = new WaveData(
                        config.waveNumber,
                        0, // Interval not used in time-based system
                        enemies,
                        type,
                        config.spawnCode
                    );
                    upcomingWaves.Add(wave);
                }
            }
            else
            {
                // Use procedural generation
                int startWave = currentWaveIndex + 1;
                int startInterval = (currentWaveIndex == 0) ? intervalsPerWave : nextWaveInterval;

                for (int i = 0; i < 5; i++)
                {
                    int waveNum = startWave + i;
                    int interval = startInterval + (i * intervalsPerWave);
                    int enemies = Mathf.CeilToInt(baseEnemyCount * Mathf.Pow(enemyScaling, waveNum - 1));

                    // Determine wave type
                    string type = "Easy";
                    if (waveNum % 5 == 0)
                        type = "Boss";
                    else if (waveNum % 3 == 0)
                        type = "Hard";
                    else if (waveNum % 2 == 0)
                        type = "Medium";

                    // Generate simple spawn code for procedural waves
                    string spawnCode = GenerateProceduralSpawnCode(enemies);

                    WaveData wave = new WaveData(waveNum, interval, enemies, type, spawnCode);
                    upcomingWaves.Add(wave);
                }
            }

            // Set next wave interval
            if (upcomingWaves.Count > 0)
            {
                nextWaveInterval = upcomingWaves[0].SpawnInterval;
            }

            OnWavesUpdated?.Invoke(upcomingWaves);
        }

        /// <summary>
        /// Count enemies (1s and 3s) in spawn code
        /// </summary>
        private int CountEnemiesInSpawnCode(string spawnCode)
        {
            int count = 0;
            foreach (char c in spawnCode)
            {
                if (c == '1' || c == '3') count++; // 1=Enemy, 3=Boss
            }
            return count;
        }

        /// <summary>
        /// Determine wave type from spawn code
        /// </summary>
        private string DetermineWaveType(string spawnCode)
        {
            if (spawnCode.Contains('3')) return "Boss";

            int enemyCount = CountEnemiesInSpawnCode(spawnCode);
            if (enemyCount >= 5) return "Hard";
            if (enemyCount >= 3) return "Medium";
            return "Easy";
        }

        /// <summary>
        /// Generate procedural spawn code for old system
        /// </summary>
        private string GenerateProceduralSpawnCode(int enemyCount)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < enemyCount; i++)
            {
                sb.Append('1'); // All enemies
                if (i < enemyCount - 1)
                    sb.Append('0'); // Empty tick between
            }
            return sb.ToString();
        }

        /// <summary>
        /// Spawn the next wave
        /// </summary>
        private void SpawnWave()
        {
            if (upcomingWaves.Count == 0 || IsGameOver) return;

            WaveData currentWave = upcomingWaves[0];
            upcomingWaves.RemoveAt(0);
            currentWaveIndex++;

            Debug.Log($"Spawning Wave {currentWave.WaveNumber}: {currentWave.EnemyCount} enemies ({currentWave.WaveType}) - Code: {currentWave.SpawnCode}");

            // Transition to Active state
            SetWaveState(WaveState.Active);

            // Initialize SpawnTimelineUI with spawn code
            if (SpawnTimelineUI.Instance != null && !string.IsNullOrEmpty(currentWave.SpawnCode))
            {
                SpawnTimelineUI.Instance.InitializeWave(currentWave.WaveNumber, currentWave.SpawnCode);
            }

            // Spawn enemies based on spawn code
            int spawned = SpawnFromSpawnCode(currentWave.SpawnCode);
            enemiesSpawnedThisWave = spawned;

            // Fire event
            OnWaveSpawned?.Invoke(currentWave);

            // Generate next wave in the queue
            if (upcomingWaves.Count < 5)
            {
                GenerateUpcomingWaves();
            }
            else
            {
                // Just update next wave interval
                if (upcomingWaves.Count > 0)
                {
                    nextWaveInterval = upcomingWaves[0].SpawnInterval;
                }
            }
        }

        /// <summary>
        /// Spawn entities based on spawn code
        /// Spawn code: 0=Empty, 1=Enemy, 2=Resource, 3=Boss
        /// </summary>
        private int SpawnFromSpawnCode(string spawnCode)
        {
            if (string.IsNullOrEmpty(spawnCode))
            {
                Debug.LogWarning("Empty spawn code, using legacy SpawnEnemies");
                return SpawnEnemies(2); // Fallback
            }

            int totalSpawned = 0;

            foreach (char code in spawnCode)
            {
                if (code == '0')
                {
                    // Empty tick - do nothing
                    continue;
                }
                else if (code == '1')
                {
                    // Spawn enemy
                    if (SpawnSingleEnemy())
                        totalSpawned++;
                }
                else if (code == '2')
                {
                    // Spawn resource node
                    if (SpawnSingleResource())
                        totalSpawned++;
                }
                else if (code == '3')
                {
                    // Spawn boss (TODO: implement boss variant)
                    if (SpawnSingleEnemy(isBoss: true))
                        totalSpawned++;
                }

                // Advance timeline dot
                if (SpawnTimelineUI.Instance != null)
                {
                    SpawnTimelineUI.Instance.AdvanceDot();
                }
            }

            return totalSpawned;
        }

        /// <summary>
        /// Spawn a single enemy at a random spawn position
        /// </summary>
        private bool SpawnSingleEnemy(bool isBoss = false)
        {
            if (GridManager.Instance == null || RaritySystem.Instance == null)
                return false;

            // Find available spawn position
            List<Vector2Int> availablePositions = new List<Vector2Int>(spawnPositions);
            availablePositions.RemoveAll(pos => !GridManager.Instance.IsCellEmpty(pos.x, pos.y));

            if (availablePositions.Count == 0)
            {
                Debug.LogWarning("No available spawn positions for enemy");
                return false;
            }

            Vector2Int pos = availablePositions[UnityEngine.Random.Range(0, availablePositions.Count)];

            // Get enemy stats
            UnitStats enemyStats = RaritySystem.Instance.DrawRandomEnemyUnit(currentWaveIndex);
            if (enemyStats == null) return false;

            GameObject prefabToSpawn = enemyStats.enemyPrefab != null ? enemyStats.enemyPrefab : enemyStats.unitPrefab;
            if (prefabToSpawn == null) return false;

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
        /// Spawn a single resource node at a random spawn position
        /// </summary>
        private bool SpawnSingleResource()
        {
            if (GridManager.Instance == null) return false;

            // Find available spawn position
            List<Vector2Int> availablePositions = new List<Vector2Int>(spawnPositions);
            availablePositions.RemoveAll(pos => !GridManager.Instance.IsCellEmpty(pos.x, pos.y));

            if (availablePositions.Count == 0)
            {
                Debug.LogWarning("No available spawn positions for resource");
                return false;
            }

            Vector2Int pos = availablePositions[UnityEngine.Random.Range(0, availablePositions.Count)];

            // Check if resource node prefab is available
            if (resourceNodePrefab == null)
            {
                Debug.LogWarning("No resource node prefab assigned to WaveManager");
                return false;
            }

            // Spawn resource node
            Vector3 worldPos = GridManager.Instance.GridToWorldPosition(pos.x, pos.y);
            GameObject nodeObj = Instantiate(resourceNodePrefab, worldPos, Quaternion.identity);
            nodeObj.SetActive(true);

            ResourceNode node = nodeObj.GetComponent<ResourceNode>();
            if (node != null)
            {
                // Set grid position
                node.GridX = pos.x;
                node.GridY = pos.y;
                // Initialize with size (1x1 for basic resource)
                node.Initialize(new Vector2Int(1, 1));
            }

            GridManager.Instance.PlaceUnit(pos.x, pos.y, nodeObj, CellState.Resource);
            return true;
        }

        /// <summary>
        /// Spawn enemies at spawn positions (legacy method for procedural waves)
        /// Iteration 6: Uses RaritySystem for varied enemy types based on wave number
        /// </summary>
        /// <returns>Number of enemies successfully spawned</returns>
        private int SpawnEnemies(int count)
        {
            if (GridManager.Instance == null)
            {
                Debug.LogWarning("Cannot spawn enemies: missing GridManager");
                return 0;
            }

            if (RaritySystem.Instance == null)
            {
                Debug.LogWarning("Cannot spawn enemies: RaritySystem not initialized");
                return 0;
            }

            List<Vector2Int> availablePositions = new List<Vector2Int>(spawnPositions);
            int spawned = 0;

            while (spawned < count && availablePositions.Count > 0)
            {
                // Pick random spawn position
                int randIndex = UnityEngine.Random.Range(0, availablePositions.Count);
                Vector2Int pos = availablePositions[randIndex];
                availablePositions.RemoveAt(randIndex);

                // Check if position is empty
                if (!GridManager.Instance.IsCellEmpty(pos.x, pos.y))
                    continue;

                // Pick enemy type based on current wave number
                UnitStats enemyStats = RaritySystem.Instance.DrawRandomEnemyUnit(currentWaveIndex);
                if (enemyStats == null)
                {
                    Debug.LogWarning($"Failed to get enemy stats for wave {currentWaveIndex}");
                    continue;
                }

                // Use enemy prefab if available, fall back to player prefab
                GameObject prefabToSpawn = enemyStats.enemyPrefab != null ? enemyStats.enemyPrefab : enemyStats.unitPrefab;
                if (prefabToSpawn == null)
                {
                    Debug.LogWarning($"No prefab found for enemy type {enemyStats.unitType}");
                    continue;
                }

                // Spawn enemy (preserve prefab rotation for custom models)
                Vector3 worldPos = GridManager.Instance.GridToWorldPosition(pos.x, pos.y);
                GameObject enemyObj = Instantiate(prefabToSpawn, worldPos, prefabToSpawn.transform.rotation);
                enemyObj.SetActive(true);

                Unit unit = enemyObj.GetComponent<Unit>();
                if (unit != null)
                {
                    unit.Initialize(Team.Enemy, pos.x, pos.y, enemyStats);
                }

                GridManager.Instance.PlaceUnit(pos.x, pos.y, enemyObj, CellState.EnemyUnit);
                spawned++;
            }

            Debug.Log($"Successfully spawned {spawned}/{count} enemies for wave {currentWaveIndex}");
            return spawned;
        }

        /// <summary>
        /// Get intervals until next wave
        /// </summary>
        public int GetIntervalsUntilNextWave()
        {
            if (IntervalTimer.Instance == null) return 0;
            int current = IntervalTimer.Instance.CurrentInterval;
            return Mathf.Max(0, nextWaveInterval - current);
        }

        /// <summary>
        /// Get the next N upcoming waves
        /// </summary>
        public List<WaveData> GetUpcomingWaves(int count)
        {
            int n = Mathf.Min(count, upcomingWaves.Count);
            return upcomingWaves.GetRange(0, n);
        }
    }
}
