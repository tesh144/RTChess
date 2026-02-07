using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClockworkGrid
{
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

        // Wave state
        private List<WaveData> upcomingWaves = new List<WaveData>();
        private int currentWaveIndex = 0;
        private int nextWaveInterval = 0;

        // Enemy spawning
        [SerializeField] private GameObject enemySoldierPrefab;
        private List<Vector2Int> spawnPositions = new List<Vector2Int>();

        // Events
        public event Action<WaveData> OnWaveSpawned;
        public event Action<WaveData> OnWaveWarning; // Fired 5 intervals before wave
        public event Action<List<WaveData>> OnWavesUpdated;

        // Public accessors
        public List<WaveData> UpcomingWaves => upcomingWaves;
        public int NextWaveInterval => nextWaveInterval;
        public int CurrentWaveNumber => currentWaveIndex;

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
        }

        private void OnDestroy()
        {
            if (IntervalTimer.Instance != null)
            {
                IntervalTimer.Instance.OnIntervalTick -= OnIntervalTick;
            }
        }

        private void OnIntervalTick(int currentInterval)
        {
            // Check for wave warning (5 intervals before spawn)
            if (currentInterval == nextWaveInterval - 5)
            {
                WaveData nextWave = upcomingWaves.Count > 0 ? upcomingWaves[0] : null;
                if (nextWave != null)
                {
                    OnWaveWarning?.Invoke(nextWave);
                    Debug.Log($"Wave {nextWave.WaveNumber} warning! Spawning in 5 intervals...");
                }
            }

            // Check for wave spawn
            if (currentInterval >= nextWaveInterval)
            {
                SpawnWave();
            }
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
            if (upcomingWaves.Count == 0) return;

            WaveData currentWave = upcomingWaves[0];
            upcomingWaves.RemoveAt(0);
            currentWaveIndex++;

            Debug.Log($"Spawning Wave {currentWave.WaveNumber}: {currentWave.EnemyCount} enemies ({currentWave.WaveType})");

            // Spawn enemies
            SpawnEnemies(currentWave.EnemyCount);

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
        private void SpawnEnemies(int count)
        {
            if (enemySoldierPrefab == null || GridManager.Instance == null)
            {
                Debug.LogWarning("Cannot spawn enemies: missing prefab or GridManager");
                return;
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
