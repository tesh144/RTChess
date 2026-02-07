using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Wave state for backward compatibility with ResourceSpawner.
    /// </summary>
    public enum WaveState
    {
        Preparation,  // Downtime/breathing room
        Active,       // Wave is currently executing
        Complete      // Wave finished
    }

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

    // WaveEntry class removed - using string-based wave sequence now (Iteration 10)

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

        [Header("=== WAVE SEQUENCES (Iteration 11) ===")]
        [Tooltip("List of wave sequences. Each string is one wave. Format: 0=Nothing, 1=Soldier, 2=Resource, 3=Ogre")]
        public List<string> waveSequences = new List<string> { "1012103" };

        [Header("Timing")]
        [Tooltip("Number of interval ticks before starting the wave sequence (breathing room for player)")]
        [SerializeField] private int startDelayTicks = 0; // Start immediately
        [Tooltip("Ticks between wave advances (4 for 4-sided game)")]
        [SerializeField] private int ticksPerWaveAdvance = 4;
        [Tooltip("Ticks of peace period between waves")]
        [SerializeField] private int peacePeriodTicks = 8;

        [Header("Prefab References")]
        [SerializeField] private GameObject resourceNodePrefab;

        // State
        private WaveState currentState = WaveState.Preparation;
        private int currentWaveNumber = 0; // Which wave we're on (0-indexed into waveSequences list)
        private int currentWaveIndex = -1; // -1 = not started, increments each interval within current wave
        private int delayTicksRemaining;
        private int ticksSinceLastAdvance = 0; // Counter for wave advance timing
        private int peacePeriodTicksRemaining = 0; // Countdown for peace period between waves
        private bool inPeacePeriod = false;
        private bool sequenceComplete = false;
        private bool gameOver = false;
        private bool hasWaveStarted = false; // Wave doesn't start until player places first unit
        private int playerUnitCount = 0; // Cached count to avoid O(N²) grid iteration

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
        public void Initialize(List<string> waves, int ticksPerAdvance = 4, int peaceTicks = 8)
        {
            InitializeSpawnPositions();

            waveSequences = waves;
            ticksPerWaveAdvance = ticksPerAdvance;
            peacePeriodTicks = peaceTicks;
            delayTicksRemaining = startDelayTicks;
            currentWaveNumber = 0;
            currentWaveIndex = -1;
            ticksSinceLastAdvance = 0;
            peacePeriodTicksRemaining = 0;
            inPeacePeriod = false;
            sequenceComplete = false;
            gameOver = false;

            // Subscribe to interval timer
            if (IntervalTimer.Instance != null)
            {
                IntervalTimer.Instance.OnIntervalTick += OnIntervalTick;
            }

            Debug.Log($"WaveManager initialized with {waveSequences.Count} waves, {ticksPerWaveAdvance} ticks per advance, {peacePeriodTicks} peace ticks");
        }

        /// <summary>
        /// Get the current wave sequence string.
        /// </summary>
        private string GetCurrentWaveSequence()
        {
            if (currentWaveNumber >= 0 && currentWaveNumber < waveSequences.Count)
                return waveSequences[currentWaveNumber];
            return "";
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

            int width = GridManager.Instance.Width;
            int height = GridManager.Instance.Height;

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
            Debug.Log($"[WaveManager] OnIntervalTick STARTED - Interval: {intervalCount}");

            try
            {
                // Don't start wave until player places first unit
                if (!hasWaveStarted)
                {
                    Debug.Log($"[WaveManager] Waiting for player to place first unit - wave not started yet");
                    return;
                }

                if (gameOver || sequenceComplete)
                {
                    Debug.Log($"[WaveManager] Skipping - gameOver: {gameOver}, sequenceComplete: {sequenceComplete}");
                    return;
                }

                // Handle start delay
                if (delayTicksRemaining > 0)
                {
                    delayTicksRemaining--;
                    Debug.Log($"WaveManager: Delay tick {startDelayTicks - delayTicksRemaining}/{startDelayTicks}");
                    return;
                }

                // Handle peace period between waves
                if (inPeacePeriod)
                {
                    peacePeriodTicksRemaining--;
                    Debug.Log($"WaveManager: Peace period tick remaining: {peacePeriodTicksRemaining}");

                    // Update peace period UI
                    if (SpawnTimelineUI.Instance != null && currentWaveNumber + 1 < waveSequences.Count)
                    {
                        string nextWaveSeq = waveSequences[currentWaveNumber + 1];
                        SpawnTimelineUI.Instance.ShowPeacePeriod(peacePeriodTicksRemaining, currentWaveNumber + 2, nextWaveSeq);
                    }

                    // Check if peace period is over
                    if (peacePeriodTicksRemaining <= 0)
                    {
                        // Start next wave
                        inPeacePeriod = false;
                        currentWaveNumber++;
                        currentWaveIndex = -1; // Reset for new wave
                        ticksSinceLastAdvance = 0;

                        Debug.Log($"WaveManager: Peace period over, starting Wave {currentWaveNumber + 1}");

                        // Show countdown for next wave
                        if (SpawnTimelineUI.Instance != null)
                        {
                            SpawnTimelineUI.Instance.ShowCountdown(ticksPerWaveAdvance);
                        }
                    }
                    return;
                }

                // Increment tick counter
                ticksSinceLastAdvance++;
                Debug.Log($"[WaveManager] Tick counter: {ticksSinceLastAdvance}/{ticksPerWaveAdvance}");

                // Update countdown UI during grace period
                if (SpawnTimelineUI.Instance != null && currentWaveIndex == -1)
                {
                    int remaining = ticksPerWaveAdvance - ticksSinceLastAdvance;
                    SpawnTimelineUI.Instance.UpdateCountdown(remaining);
                }

                // Only advance wave every N ticks (for 4-sided game, N=4)
                if (ticksSinceLastAdvance < ticksPerWaveAdvance)
                {
                    Debug.Log($"[WaveManager] Waiting for wave advance ({ticksSinceLastAdvance}/{ticksPerWaveAdvance} ticks)");
                    return;
                }

                // Reset counter and advance to next wave entry
                ticksSinceLastAdvance = 0;
                currentWaveIndex++;
                string currentSeq = GetCurrentWaveSequence();
                Debug.Log($"[WaveManager] Advanced to wave index: {currentWaveIndex}/{currentSeq.Length}");

                // Check if current wave is complete
                if (currentWaveIndex >= currentSeq.Length)
                {
                    Debug.Log($"[WaveManager] Wave {currentWaveNumber + 1} complete!");
                    CompleteCurrentWave();
                    return;
                }

                // Set state to Active when executing wave entries
                currentState = WaveState.Active;

                // Execute current wave entry
                Debug.Log($"[WaveManager] About to execute wave entry {currentWaveIndex}");
                ExecuteWaveEntry(currentWaveIndex);
                Debug.Log($"[WaveManager] Wave entry {currentWaveIndex} executed successfully");

                // Update timeline UI (initialize on first entry, then just advance)
                if (currentWaveIndex == 0)
                {
                    Debug.Log($"[WaveManager] Initializing timeline UI");
                    InitializeTimelineUI();
                }
                else
                {
                    Debug.Log($"[WaveManager] Advancing timeline UI");
                    AdvanceTimelineUI();
                }

                // Check lose condition after each interval
                Debug.Log($"[WaveManager] Checking lose condition");
                CheckLoseCondition();

                Debug.Log($"[WaveManager] OnIntervalTick COMPLETED successfully");
            }
            catch (Exception e)
            {
                Debug.LogError($"[WaveManager] EXCEPTION in OnIntervalTick: {e.Message}\n{e.StackTrace}");
                throw; // Re-throw to prevent silent failures
            }
        }

        /// <summary>
        /// Execute the spawn configuration for a wave entry.
        /// </summary>
        private void ExecuteWaveEntry(int index)
        {
            string currentSeq = GetCurrentWaveSequence();
            if (index < 0 || index >= currentSeq.Length)
            {
                Debug.LogError($"WaveManager: Invalid wave index {index}");
                return;
            }

            char code = currentSeq[index];
            Debug.Log($"WaveManager: Executing entry {index}/{currentSeq.Length - 1} - Code: '{code}'");

            SpawnType spawnType = SpawnType.Nothing;

            switch (code)
            {
                case '0':
                    // Do nothing this wave tick
                    spawnType = SpawnType.Nothing;
                    break;

                case '1':
                    // Spawn 2 soldiers
                    SpawnEnemyUnit(UnitType.Soldier, 2);
                    spawnType = SpawnType.Enemies;
                    break;

                case '2':
                    // Spawn 1 resource node
                    SpawnResourceNodes(1);
                    spawnType = SpawnType.Resources;
                    break;

                case '3':
                    // Spawn 1 ogre (boss/strong unit)
                    SpawnEnemyUnit(UnitType.Ogre, 1);
                    spawnType = SpawnType.Boss;
                    break;

                default:
                    Debug.LogWarning($"WaveManager: Unknown wave code '{code}' at index {index}");
                    break;
            }

            OnWaveEntryExecuted?.Invoke(index, spawnType);

            // Fire backward-compatibility event for systems like GridExpansionManager
            OnWaveComplete?.Invoke(new WaveData(index + 1));
        }

        // CONSOLIDATED SPAWN LOGIC (reduces ~140 lines to ~80 lines)

        /// <summary>
        /// Generic spawn method - finds random edge spawn position and places object on grid.
        /// Returns the spawn position if successful, null otherwise.
        /// </summary>
        private Vector2Int? FindRandomSpawnPosition(string debugContext)
        {
            if (GridManager.Instance == null)
            {
                Debug.LogError($"WaveManager: GridManager.Instance is null ({debugContext})");
                return null;
            }

            // Find available spawn positions
            List<Vector2Int> availablePositions = new List<Vector2Int>(spawnPositions);
            availablePositions.RemoveAll(pos => !GridManager.Instance.IsCellEmpty(pos.x, pos.y));

            if (availablePositions.Count == 0)
            {
                Debug.LogWarning($"WaveManager: No available spawn positions ({debugContext})");
                return null;
            }

            return availablePositions[UnityEngine.Random.Range(0, availablePositions.Count)];
        }

        /// <summary>
        /// Spawn a single enemy unit at a random spawn position.
        /// </summary>
        private bool SpawnSingleEnemy(UnitType type)
        {
            if (RaritySystem.Instance == null)
            {
                Debug.LogError("WaveManager: RaritySystem.Instance is null");
                return false;
            }

            // Find spawn position
            Vector2Int? spawnPos = FindRandomSpawnPosition($"{type} enemy");
            if (!spawnPos.HasValue) return false;
            Vector2Int pos = spawnPos.Value;

            // Get enemy stats and prefab
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

            // Instantiate and initialize
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
        /// Spawn a single resource node at a random spawn position.
        /// </summary>
        private bool SpawnSingleResource(int level)
        {
            if (resourceNodePrefab == null)
            {
                Debug.LogWarning("WaveManager: No resource node prefab assigned");
                return false;
            }

            // Find spawn position
            Vector2Int? spawnPos = FindRandomSpawnPosition($"level {level} resource");
            if (!spawnPos.HasValue) return false;
            Vector2Int pos = spawnPos.Value;

            // Instantiate and initialize
            Vector3 worldPos = GridManager.Instance.GridToWorldPosition(pos.x, pos.y);
            GameObject nodeObj = Instantiate(resourceNodePrefab, worldPos, Quaternion.identity);
            nodeObj.SetActive(true);

            ResourceNode node = nodeObj.GetComponent<ResourceNode>();
            if (node != null)
            {
                node.GridX = pos.x;
                node.GridY = pos.y;
                node.Initialize(new Vector2Int(1, 1)); // Level 1: 1x1
            }

            GridManager.Instance.PlaceUnit(pos.x, pos.y, nodeObj, CellState.Resource);
            return true;
        }

        /// <summary>
        /// Initialize the timeline UI (called once at wave start).
        /// Also shows the tick counter after countdown completes.
        /// </summary>
        private void InitializeTimelineUI()
        {
            if (SpawnTimelineUI.Instance != null)
            {
                // Convert wave sequence to spawn code format for existing UI
                string spawnCode = ConvertSequenceToSpawnCode();
                SpawnTimelineUI.Instance.InitializeWave(currentWaveNumber + 1, spawnCode);
            }

            // Show tick counter after countdown completes (dock bar stays visible throughout)
            if (IntervalUI.Instance != null)
            {
                IntervalUI.Instance.ShowTickCounter();
            }
        }

        /// <summary>
        /// Advance the timeline UI to the next dot.
        /// </summary>
        private void AdvanceTimelineUI()
        {
            if (SpawnTimelineUI.Instance != null)
            {
                SpawnTimelineUI.Instance.AdvanceDot();
            }
        }

        /// <summary>
        /// Helper: Spawn multiple enemy units of a specific type.
        /// </summary>
        private void SpawnEnemyUnit(UnitType type, int count)
        {
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                if (SpawnSingleEnemy(type))
                    spawned++;
            }
            Debug.Log($"WaveManager: Spawned {spawned}/{count} {type} enemies");
        }

        /// <summary>
        /// Helper: Spawn multiple resource nodes.
        /// </summary>
        private void SpawnResourceNodes(int count)
        {
            if (resourceNodePrefab == null)
            {
                Debug.LogWarning("WaveManager: No resource node prefab assigned");
                return;
            }

            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                if (SpawnSingleResource(1)) // Level 1 resources
                    spawned++;
            }
            Debug.Log($"WaveManager: Spawned {spawned}/{count} resource nodes");
        }

        /// <summary>
        /// Return current wave sequence string directly (already in spawn code format).
        /// </summary>
        private string ConvertSequenceToSpawnCode()
        {
            return GetCurrentWaveSequence();
        }

        /// <summary>
        /// Complete the current wave and either start peace period or end game.
        /// </summary>
        private void CompleteCurrentWave()
        {
            Debug.Log($"WaveManager: Wave {currentWaveNumber + 1} complete!");

            // Check if there are more waves
            if (currentWaveNumber + 1 < waveSequences.Count)
            {
                // Start peace period before next wave
                StartPeacePeriod();
            }
            else
            {
                // All waves complete - VICTORY!
                CompleteSequence();
            }
        }

        /// <summary>
        /// Start peace period between waves.
        /// </summary>
        private void StartPeacePeriod()
        {
            inPeacePeriod = true;
            peacePeriodTicksRemaining = peacePeriodTicks;
            currentState = WaveState.Preparation;

            Debug.Log($"WaveManager: Starting peace period ({peacePeriodTicks} ticks) before Wave {currentWaveNumber + 2}");

            // Show peace period UI
            if (SpawnTimelineUI.Instance != null)
            {
                string nextWaveSeq = waveSequences[currentWaveNumber + 1];
                SpawnTimelineUI.Instance.ShowPeacePeriod(peacePeriodTicksRemaining, currentWaveNumber + 2, nextWaveSeq);
            }
        }

        /// <summary>
        /// Complete the wave sequence (player survived all entries).
        /// </summary>
        private void CompleteSequence()
        {
            currentState = WaveState.Complete;
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

            // Use cached player unit count instead of iterating grid (performance optimization)
            bool hasUnitsOnGrid = playerUnitCount > 0;

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

        /// <summary>
        /// Trigger wave start (called when player places first unit).
        /// </summary>
        public void StartWave()
        {
            if (hasWaveStarted)
            {
                Debug.LogWarning("WaveManager: StartWave called but wave already started!");
                return;
            }

            hasWaveStarted = true;
            Debug.Log("WaveManager: Wave started by player placing first unit!");

            // Show countdown UI
            if (SpawnTimelineUI.Instance != null)
            {
                SpawnTimelineUI.Instance.ShowCountdown(ticksPerWaveAdvance);
            }
        }

        // Public accessors for UI/other systems
        public WaveState CurrentState => currentState;
        public int CurrentWaveIndex => currentWaveIndex;
        public int CurrentWaveNumber => currentWaveNumber + 1; // +1 for display (1-indexed)
        public int TotalWaves => waveSequences.Count;
        public int TotalWaveEntries => GetCurrentWaveSequence().Length;
        public bool IsSequenceComplete => sequenceComplete;
        public bool IsGameOver => gameOver;
        public bool HasWaveStarted => hasWaveStarted;
        public bool InPeacePeriod => inPeacePeriod;

        /// <summary>
        /// Notify WaveManager that a player unit was placed (for lose condition tracking).
        /// </summary>
        public void OnPlayerUnitPlaced()
        {
            playerUnitCount++;
        }

        /// <summary>
        /// Notify WaveManager that a player unit was destroyed (for lose condition tracking).
        /// </summary>
        public void OnPlayerUnitDestroyed()
        {
            playerUnitCount--;
            if (playerUnitCount < 0) playerUnitCount = 0; // Safety check
        }

        /// <summary>
        /// Get the current wave code character being executed.
        /// </summary>
        public char GetCurrentCode()
        {
            string currentSeq = GetCurrentWaveSequence();
            if (currentWaveIndex >= 0 && currentWaveIndex < currentSeq.Length)
                return currentSeq[currentWaveIndex];
            return '0';
        }
    }
}
