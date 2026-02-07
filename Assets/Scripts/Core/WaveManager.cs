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
    /// Wave data structure
    /// </summary>
    [System.Serializable]
    public class WaveData
    {
        public int WaveNumber;
        public int SpawnInterval; // Which interval this wave spawns on
        public int EnemyCount;
        public string WaveType; // "Easy", "Medium", "Hard", "Boss"

        public WaveData(int number, int interval, int enemies, string type)
        {
            WaveNumber = number;
            SpawnInterval = interval;
            EnemyCount = enemies;
            WaveType = type;
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

        [Header("Wave Configuration")]
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

        public void Initialize(GameObject enemyPrefab)
        {
            enemySoldierPrefab = enemyPrefab;

            // Define spawn positions (right side of grid for now)
            if (GridManager.Instance != null)
            {
                int gridWidth = 4; // TODO: Get from GridManager
                int gridHeight = 4;

                // Spawn on right edge
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
            if (currentWaveIndex >= maxWaves)
            {
                TriggerVictory();
                return;
            }

            // Start downtime for next wave
            SetWaveState(WaveState.Preparation);
            downtimeRemaining = CalculateDowntime(currentWaveIndex + 1);
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
        /// Generate the next 5 waves
        /// </summary>
        private void GenerateUpcomingWaves()
        {
            upcomingWaves.Clear();

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

                WaveData wave = new WaveData(waveNum, interval, enemies, type);
                upcomingWaves.Add(wave);
            }

            // Set next wave interval
            if (upcomingWaves.Count > 0)
            {
                nextWaveInterval = upcomingWaves[0].SpawnInterval;
            }

            OnWavesUpdated?.Invoke(upcomingWaves);
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

            Debug.Log($"Spawning Wave {currentWave.WaveNumber}: {currentWave.EnemyCount} enemies ({currentWave.WaveType})");

            // Transition to Active state
            SetWaveState(WaveState.Active);

            // Spawn enemies and track count
            int spawned = SpawnEnemies(currentWave.EnemyCount);
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
        /// Spawn enemies at spawn positions
        /// </summary>
        /// <returns>Number of enemies successfully spawned</returns>
        private int SpawnEnemies(int count)
        {
            if (enemySoldierPrefab == null || GridManager.Instance == null)
            {
                Debug.LogWarning("Cannot spawn enemies: missing prefab or GridManager");
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

                // Spawn enemy
                Vector3 worldPos = GridManager.Instance.GridToWorldPosition(pos.x, pos.y);
                GameObject enemyObj = Instantiate(enemySoldierPrefab, worldPos, Quaternion.identity);
                enemyObj.SetActive(true);

                Unit unit = enemyObj.GetComponent<Unit>();
                if (unit != null)
                {
                    unit.Initialize(Team.Enemy, pos.x, pos.y);
                }

                GridManager.Instance.PlaceUnit(pos.x, pos.y, enemyObj, CellState.EnemyUnit);
                spawned++;
            }

            Debug.Log($"Successfully spawned {spawned}/{count} enemies");
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
