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
    /// Objective type for each wave.
    /// </summary>
    public enum ObjectiveType
    {
        DestroyResources,
        KillSoldiers,
        KillNinjas,
        KillOgres
    }

    /// <summary>
    /// Defines a single wave objective.
    /// </summary>
    [System.Serializable]
    public struct WaveObjective
    {
        public ObjectiveType type;
        public int target;

        public WaveObjective(ObjectiveType type, int target)
        {
            this.type = type;
            this.target = target;
        }

        public string GetDisplayName()
        {
            switch (type)
            {
                case ObjectiveType.KillOgres: return "Ogres";
                case ObjectiveType.KillNinjas: return "Ninjas";
                case ObjectiveType.KillSoldiers: return "Soldiers";
                case ObjectiveType.DestroyResources: return "Mines";
                default: return "Targets";
            }
        }
    }

    /// <summary>
    /// Spawn type for each interval in the wave sequence.
    /// Codes: 0=Nothing, A=Soldier, B=Ninja, C=Ogre, S=Resource L1, M=Resource L2, L=Resource L3
    /// </summary>
    public enum SpawnType
    {
        Nothing,
        EnemySoldier,
        EnemyNinja,
        EnemyOgre,
        ResourceL1,
        ResourceL2,
        ResourceL3
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
        [Tooltip("List of wave sequences. Each string is one wave. Format: 0=Nothing, A=Soldier, B=Ninja, C=Ogre, S=Resource L1, M=Resource L2, L=Resource L3")]
        public List<string> waveSequences = new List<string> { "A0S0A00A0SC" };

        [Header("Timing")]
        [Tooltip("Number of interval ticks before starting the wave sequence (breathing room for player)")]
        [SerializeField] private int startDelayTicks = 0; // Start immediately
        [Tooltip("Ticks between wave advances (4 for 4-sided game)")]
        [SerializeField] private int ticksPerWaveAdvance = 4;
        [Tooltip("Ticks of peace period between waves")]
        [SerializeField] private int peacePeriodTicks = 8;

        [Header("Prefab References")]
        [SerializeField] private GameObject resourceNodePrefab; // Level 1 (legacy/fallback)
        [SerializeField] private GameObject resourceNodeLevel2Prefab;
        [SerializeField] private GameObject resourceNodeLevel3Prefab;

        [Header("Resource Spawn Settings")]
        [Tooltip("Maximum distance (in cells) from player units or revealed cells for resource spawning")]
        [SerializeField] private int resourceProximityRadius = 3;

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
        private int enemyUnitCount = 0; // Track living enemies for wave completion
        private int resourceNodeCount = 0; // Track living resource nodes for wave completion
        private bool allEnemiesSpawned = false; // True when wave sequence has finished spawning
        private bool isFirstResourceSpawn = true; // Track first resource for fog reveal logic

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
        // Wave objectives
        private List<WaveObjective> waveObjectives = new List<WaveObjective>();
        private int currentObjectiveProgress = 0;

        public void Initialize(List<string> waves, List<WaveObjective> objectives, int ticksPerAdvance = 4, int peaceTicks = 8)
        {
            waveSequences = waves;
            waveObjectives = objectives;
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
            enemyUnitCount = 0;
            resourceNodeCount = 0;
            allEnemiesSpawned = false;
            currentObjectiveProgress = 0;

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
                        allEnemiesSpawned = false;

                        // Reset objective progress for new wave
                        currentObjectiveProgress = 0;
                        if (ObjectiveUI.Instance != null)
                        {
                            ObjectiveUI.Instance.ResetProgress();
                        }

                        Debug.Log($"WaveManager: Peace period over, starting Wave {currentWaveNumber + 1}");
                    }
                    return;
                }

                // Increment tick counter
                ticksSinceLastAdvance++;
                Debug.Log($"[WaveManager] Tick counter: {ticksSinceLastAdvance}/{ticksPerWaveAdvance}");

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

                // Check if all entries in current wave have been spawned
                if (currentWaveIndex >= currentSeq.Length)
                {
                    Debug.Log($"[WaveManager] Wave {currentWaveNumber + 1} - all entries spawned, {enemyUnitCount} enemies remaining");
                    allEnemiesSpawned = true;
                    CheckWaveComplete();
                    CheckLoseCondition();
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

                    // Initialize wave objective
                    if (ObjectiveUI.Instance != null && currentWaveNumber < waveObjectives.Count)
                    {
                        WaveObjective obj = waveObjectives[currentWaveNumber];
                        ObjectiveUI.Instance.SetObjective(currentWaveNumber + 1, obj.target, obj.GetDisplayName());
                        ObjectiveUI.Instance.Show();
                        Debug.Log($"[WaveManager] Wave {currentWaveNumber + 1} objective set: {obj.type} x{obj.target}");
                    }
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
                    spawnType = SpawnType.Nothing;
                    break;

                case 'A':
                case 'a':
                    SpawnEnemyUnit(UnitType.Soldier, 1);
                    spawnType = SpawnType.EnemySoldier;
                    break;

                case 'B':
                case 'b':
                    SpawnEnemyUnit(UnitType.Ninja, 1);
                    spawnType = SpawnType.EnemyNinja;
                    break;

                case 'C':
                case 'c':
                    SpawnEnemyUnit(UnitType.Ogre, 1);
                    spawnType = SpawnType.EnemyOgre;
                    break;

                case 'S':
                case 's':
                    SpawnSingleResource(1);
                    spawnType = SpawnType.ResourceL1;
                    break;

                case 'M':
                case 'm':
                    SpawnSingleResource(2);
                    spawnType = SpawnType.ResourceL2;
                    break;

                case 'L':
                case 'l':
                    SpawnSingleResource(3);
                    spawnType = SpawnType.ResourceL3;
                    break;

                default:
                    Debug.LogWarning($"WaveManager: Unknown wave code '{code}' at index {index}");
                    break;
            }

            OnWaveEntryExecuted?.Invoke(index, spawnType);

            // Fire backward-compatibility event for systems like GridExpansionManager
            OnWaveComplete?.Invoke(new WaveData(index + 1));
        }

        // === AI PLACEMENT PRIORITY SYSTEM ===

        private static readonly Facing[] AllFacings = { Facing.North, Facing.East, Facing.South, Facing.West };
        private const int MinResourceSpacing = 5;

        /// <summary>
        /// Find the best strategic position for an enemy unit based on priority scoring.
        /// Evaluates all revealed empty cells and returns the highest-scoring one.
        /// </summary>
        private Vector2Int? FindStrategicEnemyPosition(int attackRange)
        {
            var candidates = GetRevealedEmptyCells();
            if (candidates.Count == 0)
            {
                Debug.LogWarning("[AI] No revealed empty cells for enemy placement");
                return null;
            }

            float bestScore = -1f;
            List<Vector2Int> bestPositions = new List<Vector2Int>();

            foreach (var pos in candidates)
            {
                float score = ScoreEnemyPosition(pos, attackRange);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestPositions.Clear();
                    bestPositions.Add(pos);
                }
                else if (Mathf.Approximately(score, bestScore))
                {
                    bestPositions.Add(pos);
                }
            }

            if (bestPositions.Count == 0) return null;

            var chosen = bestPositions[UnityEngine.Random.Range(0, bestPositions.Count)];
            Debug.Log($"[AI] Enemy placement: chose ({chosen.x},{chosen.y}) score={bestScore:F1}, candidates={candidates.Count}, ties={bestPositions.Count}");
            return chosen;
        }

        /// <summary>
        /// Score a candidate position for enemy placement.
        /// Priorities: 1.ContestResources 2.InterceptHarvesters 3.StealHarvests 4.BlockAccess 5.AttackPlayer 6.Random
        /// </summary>
        private float ScoreEnemyPosition(Vector2Int pos, int attackRange)
        {
            if (GridManager.Instance == null) return 0f;

            float totalScore = 0f;
            int prioritiesMet = 0;

            // Check all 4 facings - take the best attack score
            float bestFacingScore = 0f;
            int bestFacingPriorities = 0;
            foreach (var facing in AllFacings)
            {
                int facingPriorities = 0;
                float facingScore = ScoreFacingDirection(pos, facing, attackRange, ref facingPriorities);
                if (facingScore > bestFacingScore)
                {
                    bestFacingScore = facingScore;
                    bestFacingPriorities = facingPriorities;
                }
            }
            totalScore += bestFacingScore;
            prioritiesMet += bestFacingPriorities;

            // Priority 4: Block resource access (adjacent to a resource node)
            int adjResourceLevel = GetAdjacentResourceLevel(pos);
            if (adjResourceLevel > 0)
            {
                float blockScore = adjResourceLevel switch { 1 => 10f, 2 => 15f, _ => 20f };
                totalScore += blockScore;
                prioritiesMet++;
            }

            // Base random tiebreaker for positions with no targets
            if (totalScore == 0f)
            {
                totalScore = 1f + UnityEngine.Random.Range(0f, 0.5f);
            }

            // Multi-objective bonus: 1.5x if satisfying 2+ priorities
            if (prioritiesMet >= 2)
            {
                totalScore *= 1.5f;
            }

            return totalScore;
        }

        /// <summary>
        /// Score what an enemy unit would see in a specific facing direction.
        /// </summary>
        private float ScoreFacingDirection(Vector2Int pos, Facing facing, int attackRange, ref int prioritiesMet)
        {
            facing.ToGridOffset(out int dx, out int dy);
            float score = 0f;

            for (int r = 1; r <= attackRange; r++)
            {
                int targetX = pos.x + dx * r;
                int targetY = pos.y + dy * r;

                if (!GridManager.Instance.IsValidCell(targetX, targetY)) break;

                CellState state = GridManager.Instance.GetCellState(targetX, targetY);

                if (state == CellState.Resource)
                {
                    int level = GetResourceLevelAt(targetX, targetY);

                    // Priority 1: Contest resource (player is also targeting this resource)
                    if (IsPlayerTargetingCell(targetX, targetY))
                    {
                        score += level switch { 1 => 50f, 2 => 75f, _ => 100f };
                    }
                    else
                    {
                        // Priority 3: Steal harvest
                        score += level switch { 1 => 20f, 2 => 30f, _ => 40f };
                    }
                    prioritiesMet++;
                    break; // Can't see past resource
                }
                else if (state == CellState.PlayerUnit)
                {
                    // Priority 2: Intercept harvester (player unit near a resource)
                    int nearbyResourceLevel = GetAdjacentResourceLevel(new Vector2Int(targetX, targetY));
                    if (nearbyResourceLevel > 0)
                    {
                        score += nearbyResourceLevel switch { 1 => 40f, 2 => 60f, _ => 80f };
                    }
                    else
                    {
                        // Priority 5: Attack any player unit
                        score += 15f;
                    }
                    prioritiesMet++;
                    break; // Can't see past unit
                }
                else if (state == CellState.EnemyUnit)
                {
                    break; // Blocked by ally
                }
            }

            return score;
        }

        /// <summary>
        /// Check if any player unit on the grid is targeting a specific cell
        /// (facing it within attack range with no obstacles between).
        /// </summary>
        private bool IsPlayerTargetingCell(int targetX, int targetY)
        {
            if (GridManager.Instance == null) return false;

            int width = GridManager.Instance.Width;
            int height = GridManager.Instance.Height;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (GridManager.Instance.GetCellState(x, y) != CellState.PlayerUnit) continue;

                    GameObject obj = GridManager.Instance.GetCellOccupant(x, y);
                    if (obj == null) continue;

                    Unit unit = obj.GetComponent<Unit>();
                    if (unit == null || unit.Team != Team.Player) continue;

                    unit.CurrentFacing.ToGridOffset(out int dx, out int dy);
                    for (int r = 1; r <= unit.AttackRange; r++)
                    {
                        int checkX = x + dx * r;
                        int checkY = y + dy * r;
                        if (checkX == targetX && checkY == targetY) return true;
                        // Stop if blocked by any non-empty cell
                        if (GridManager.Instance.IsValidCell(checkX, checkY) &&
                            GridManager.Instance.GetCellState(checkX, checkY) != CellState.Empty)
                            break;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Get the highest resource level adjacent (4-directional) to a position.
        /// Returns 0 if no adjacent resources.
        /// </summary>
        private int GetAdjacentResourceLevel(Vector2Int pos)
        {
            if (GridManager.Instance == null) return 0;

            int maxLevel = 0;
            int[] dxs = { 0, 0, 1, -1 };
            int[] dys = { 1, -1, 0, 0 };

            for (int i = 0; i < 4; i++)
            {
                int nx = pos.x + dxs[i];
                int ny = pos.y + dys[i];

                if (GridManager.Instance.IsValidCell(nx, ny) &&
                    GridManager.Instance.GetCellState(nx, ny) == CellState.Resource)
                {
                    int level = GetResourceLevelAt(nx, ny);
                    if (level > maxLevel) maxLevel = level;
                }
            }

            return maxLevel;
        }

        /// <summary>
        /// Get the resource level at a specific grid position.
        /// </summary>
        private int GetResourceLevelAt(int gridX, int gridY)
        {
            if (GridManager.Instance == null) return 0;
            GameObject obj = GridManager.Instance.GetCellOccupant(gridX, gridY);
            if (obj == null) return 0;
            ResourceNode node = obj.GetComponent<ResourceNode>();
            return node != null ? node.Level : 0;
        }

        /// <summary>
        /// Get all revealed, empty cells on the grid.
        /// </summary>
        private List<Vector2Int> GetRevealedEmptyCells()
        {
            List<Vector2Int> cells = new List<Vector2Int>();
            if (GridManager.Instance == null || FogManager.Instance == null) return cells;

            int width = GridManager.Instance.Width;
            int height = GridManager.Instance.Height;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (GridManager.Instance.IsCellEmpty(x, y) && FogManager.Instance.IsCellRevealed(x, y))
                    {
                        cells.Add(new Vector2Int(x, y));
                    }
                }
            }

            return cells;
        }

        /// <summary>
        /// Get all unrevealed, empty cells on the grid.
        /// </summary>
        private List<Vector2Int> GetUnrevealedEmptyCells()
        {
            List<Vector2Int> cells = new List<Vector2Int>();
            if (GridManager.Instance == null || FogManager.Instance == null) return cells;

            int width = GridManager.Instance.Width;
            int height = GridManager.Instance.Height;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (GridManager.Instance.IsCellEmpty(x, y) && !FogManager.Instance.IsCellRevealed(x, y))
                    {
                        cells.Add(new Vector2Int(x, y));
                    }
                }
            }

            return cells;
        }

        /// <summary>
        /// Get all resource node positions on the grid.
        /// </summary>
        private List<Vector2Int> GetAllResourcePositions()
        {
            List<Vector2Int> positions = new List<Vector2Int>();
            if (GridManager.Instance == null) return positions;

            int width = GridManager.Instance.Width;
            int height = GridManager.Instance.Height;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (GridManager.Instance.GetCellState(x, y) == CellState.Resource)
                    {
                        positions.Add(new Vector2Int(x, y));
                    }
                }
            }

            return positions;
        }

        // === RESOURCE SPAWN LOGIC ===

        /// <summary>
        /// Filter out candidates that are adjacent (including diagonals) to any existing resource node.
        /// </summary>
        private List<Vector2Int> FilterAdjacentToResources(List<Vector2Int> candidates)
        {
            var resources = GetAllResourcePositions();
            if (resources.Count == 0) return candidates;

            List<Vector2Int> filtered = new List<Vector2Int>();
            foreach (var pos in candidates)
            {
                bool tooClose = false;
                foreach (var res in resources)
                {
                    if (Mathf.Abs(pos.x - res.x) <= 1 && Mathf.Abs(pos.y - res.y) <= 1)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (!tooClose)
                    filtered.Add(pos);
            }
            return filtered;
        }

        /// <summary>
        /// Find a spawn position for a resource node.
        /// First resource: Spawns in any revealed empty cell.
        /// Subsequent resources: Prioritizes unrevealed cells within proximity radius.
        /// Resource nodes never spawn adjacent to each other.
        /// </summary>
        private Vector2Int? FindResourceSpawnPosition()
        {
            var revealedCandidates = GetRevealedEmptyCells();
            if (revealedCandidates.Count == 0)
            {
                Debug.LogWarning("[AI] No revealed empty cells for resource placement");
                return null;
            }

            // First resource: use current logic (any revealed cell)
            if (isFirstResourceSpawn)
            {
                var existingResources = GetAllResourcePositions();

                // If no existing resources, pick any revealed empty cell
                if (existingResources.Count == 0)
                {
                    var firstChosen = revealedCandidates[UnityEngine.Random.Range(0, revealedCandidates.Count)];
                    Debug.Log($"[AI] First Resource: no existing resources, chose ({firstChosen.x},{firstChosen.y})");
                    return firstChosen;
                }

                // Maintain spacing for first resource if resources already exist
                List<Vector2Int> spacedCandidates = new List<Vector2Int>();
                Vector2Int bestFallback = revealedCandidates[0];
                int bestFallbackDist = 0;

                foreach (var pos in revealedCandidates)
                {
                    int minDist = int.MaxValue;
                    foreach (var res in existingResources)
                    {
                        int dist = Mathf.Abs(pos.x - res.x) + Mathf.Abs(pos.y - res.y);
                        if (dist < minDist) minDist = dist;
                    }

                    if (minDist >= MinResourceSpacing)
                        spacedCandidates.Add(pos);

                    if (minDist > bestFallbackDist)
                    {
                        bestFallbackDist = minDist;
                        bestFallback = pos;
                    }
                }

                var result = spacedCandidates.Count > 0
                    ? spacedCandidates[UnityEngine.Random.Range(0, spacedCandidates.Count)]
                    : bestFallback;
                Debug.Log($"[AI] First Resource: chose ({result.x},{result.y})");
                return result;
            }

            // Subsequent resources: prioritize unrevealed cells
            var unrevealedCandidates = GetUnrevealedEmptyCells();

            // Priority 1: Unrevealed cells within proximity
            if (unrevealedCandidates.Count > 0)
            {
                List<Vector2Int> unrevealedProximity = FindProximityCandidates(unrevealedCandidates);
                if (unrevealedProximity.Count > 0)
                {
                    var chosen = unrevealedProximity[UnityEngine.Random.Range(0, unrevealedProximity.Count)];
                    Debug.Log($"[AI] Resource (unrevealed+proximity): chose ({chosen.x},{chosen.y}), {unrevealedProximity.Count} candidates");
                    return chosen;
                }
            }

            // Priority 2: Revealed cells within proximity (current behavior)
            List<Vector2Int> revealedProximity = FindProximityCandidates(revealedCandidates);
            if (revealedProximity.Count > 0)
            {
                var chosen = revealedProximity[UnityEngine.Random.Range(0, revealedProximity.Count)];
                Debug.Log($"[AI] Resource (revealed+proximity): chose ({chosen.x},{chosen.y}), {revealedProximity.Count} candidates");
                return chosen;
            }

            // Priority 3: Any unrevealed cell
            if (unrevealedCandidates.Count > 0)
            {
                var chosen = unrevealedCandidates[UnityEngine.Random.Range(0, unrevealedCandidates.Count)];
                Debug.Log($"[AI] Resource (any unrevealed): chose ({chosen.x},{chosen.y})");
                return chosen;
            }

            // Priority 4: Any revealed cell (last resort)
            var fallback = revealedCandidates[UnityEngine.Random.Range(0, revealedCandidates.Count)];
            Debug.Log($"[AI] Resource (any revealed): chose ({fallback.x},{fallback.y})");
            return fallback;
        }

        /// <summary>
        /// Find cells within proximity radius of player units (priority) or other revealed cells.
        /// Works with any candidate list (revealed or unrevealed cells).
        /// </summary>
        private List<Vector2Int> FindProximityCandidates(List<Vector2Int> allCandidates)
        {
            List<Vector2Int> proximityCandidates = new List<Vector2Int>();

            // Priority 1: Check proximity to player units
            List<Vector2Int> playerUnitPositions = GetPlayerUnitPositions();
            if (playerUnitPositions.Count > 0)
            {
                foreach (var candidate in allCandidates)
                {
                    foreach (var unitPos in playerUnitPositions)
                    {
                        int dist = Mathf.Abs(candidate.x - unitPos.x) + Mathf.Abs(candidate.y - unitPos.y);
                        if (dist <= resourceProximityRadius)
                        {
                            proximityCandidates.Add(candidate);
                            break; // Found one unit close enough, no need to check others
                        }
                    }
                }

                if (proximityCandidates.Count > 0)
                {
                    Debug.Log($"[AI] Found {proximityCandidates.Count} cells within {resourceProximityRadius} of player units");
                    return proximityCandidates;
                }
            }

            // Priority 2: If no player units, check proximity to other revealed cells
            var allRevealedCells = GetRevealedEmptyCells();
            foreach (var candidate in allCandidates)
            {
                foreach (var revealedCell in allRevealedCells)
                {
                    if (candidate == revealedCell) continue; // Don't compare to self

                    int dist = Mathf.Abs(candidate.x - revealedCell.x) + Mathf.Abs(candidate.y - revealedCell.y);
                    if (dist <= resourceProximityRadius)
                    {
                        proximityCandidates.Add(candidate);
                        break;
                    }
                }
            }

            Debug.Log($"[AI] Found {proximityCandidates.Count} cells within {resourceProximityRadius} of revealed cells");
            return proximityCandidates;
        }

        /// <summary>
        /// Get all player unit positions on the grid.
        /// </summary>
        private List<Vector2Int> GetPlayerUnitPositions()
        {
            List<Vector2Int> positions = new List<Vector2Int>();
            if (GridManager.Instance == null) return positions;

            int width = GridManager.Instance.Width;
            int height = GridManager.Instance.Height;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (GridManager.Instance.GetCellState(x, y) == CellState.PlayerUnit)
                    {
                        positions.Add(new Vector2Int(x, y));
                    }
                }
            }

            return positions;
        }

        // === SPAWN METHODS ===

        /// <summary>
        /// Spawn a single enemy unit using AI placement priority system.
        /// Falls back to random edge spawn if strategic placement fails.
        /// </summary>
        private bool SpawnSingleEnemy(UnitType type)
        {
            if (RaritySystem.Instance == null)
            {
                Debug.LogError("WaveManager: RaritySystem.Instance is null");
                return false;
            }

            // Get enemy stats and prefab
            UnitStats enemyStats = RaritySystem.Instance.GetUnitStats(type);
            if (enemyStats == null)
            {
                Debug.LogWarning($"WaveManager: No stats found for {type}");
                return false;
            }

            // Find strategic spawn position (AI priority system)
            Vector2Int? spawnPos = FindStrategicEnemyPosition(enemyStats.attackRange);
            if (!spawnPos.HasValue) return false;
            Vector2Int pos = spawnPos.Value;

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
            enemyUnitCount++;

            // Play enemy placement SFX (lower pitch)
            if (SFXManager.Instance != null)
                SFXManager.Instance.PlayEnemyPlacement();

            return true;
        }

        /// <summary>
        /// Spawn a single resource node with proximity-based placement.
        /// First resource reveals fog radius, subsequent resources only reveal single cell.
        /// </summary>
        private bool SpawnSingleResource(int level)
        {
            // Select prefab based on level (all resources occupy 1 cell)
            GameObject prefab;
            Vector2Int gridSize = new Vector2Int(1, 1);
            switch (level)
            {
                case 2:
                    prefab = resourceNodeLevel2Prefab != null ? resourceNodeLevel2Prefab : resourceNodePrefab;
                    break;
                case 3:
                    prefab = resourceNodeLevel3Prefab != null ? resourceNodeLevel3Prefab : resourceNodePrefab;
                    break;
                default:
                    prefab = resourceNodePrefab;
                    break;
            }

            if (prefab == null)
            {
                Debug.LogWarning($"WaveManager: No resource node prefab for level {level}");
                return false;
            }

            // Find position with proximity-based spacing
            Vector2Int? spawnPos = FindResourceSpawnPosition();
            if (!spawnPos.HasValue) return false;
            Vector2Int pos = spawnPos.Value;

            // Instantiate and initialize
            Vector3 worldPos = GridManager.Instance.GridToWorldPosition(pos.x, pos.y);
            GameObject nodeObj = Instantiate(prefab, worldPos, Quaternion.identity);
            nodeObj.SetActive(true);

            ResourceNode node = nodeObj.GetComponent<ResourceNode>();
            if (node != null)
            {
                node.GridX = pos.x;
                node.GridY = pos.y;
                node.Initialize(gridSize);
            }

            // Mark all occupied cells
            for (int dx = 0; dx < gridSize.x; dx++)
            {
                for (int dy = 0; dy < gridSize.y; dy++)
                {
                    GridManager.Instance.PlaceUnit(pos.x + dx, pos.y + dy, nodeObj, CellState.Resource);
                }
            }
            resourceNodeCount++;

            // Play resource placement SFX
            if (SFXManager.Instance != null)
                SFXManager.Instance.PlayResourcePlacement();

            // Fog reveal logic
            if (FogManager.Instance != null)
            {
                if (isFirstResourceSpawn)
                {
                    // First resource: reveal fog radius (level 2+ reveal further)
                    int revealRadius = level >= 2 ? 2 : 1;
                    FogManager.Instance.RevealRadius(pos.x, pos.y, revealRadius);
                    isFirstResourceSpawn = false; // Mark first resource as spawned
                    Debug.Log($"[WaveManager] First resource spawned with radius reveal: {revealRadius}");
                }
                else
                {
                    // Subsequent resources: only reveal single cell
                    FogManager.Instance.RevealRadius(pos.x, pos.y, 0); // Radius 0 = single cell only
                    Debug.Log($"[WaveManager] Subsequent resource spawned with single cell reveal");
                }
            }

            return true;
        }

        /// <summary>
        /// Initialize the timeline UI (called once at wave start).
        /// Also shows the tick counter and gatcha button after countdown completes.
        /// </summary>
        private void InitializeTimelineUI()
        {
            if (SpawnTimelineUI.Instance != null)
            {
                // Convert wave sequence to spawn code format for existing UI
                string spawnCode = ConvertSequenceToSpawnCode();
                SpawnTimelineUI.Instance.InitializeWave(currentWaveNumber + 1, spawnCode);
            }

            // Show tick counter after countdown completes
            if (IntervalUI.Instance != null)
            {
                IntervalUI.Instance.ShowTickCounter();
            }

            // Show gatcha button after countdown completes (dock bar with cards stays visible throughout)
            if (DockBarManager.Instance != null)
            {
                DockBarManager.Instance.ShowWithAnimation();
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
        /// Complete the current wave. Shows the victory screen and waits for player input.
        /// </summary>
        private void CompleteCurrentWave()
        {
            Debug.Log($"WaveManager: Wave {currentWaveNumber + 1} complete!");

            // Pause the timer while showing victory screen
            if (IntervalTimer.Instance != null)
                IntervalTimer.Instance.Pause();

            // Show victory screen via GameOverManager (player taps to proceed)
            if (GameOverManager.Instance != null)
            {
                GameOverManager.Instance.ShowWaveComplete();
            }
            else
            {
                // No GameOverManager - proceed automatically
                ProceedAfterWaveComplete();
            }
        }

        /// <summary>
        /// Called by GameOverManager when the player dismisses the victory screen.
        /// Starts peace period or triggers final victory.
        /// </summary>
        public void ProceedAfterWaveComplete()
        {
            // Resume timer
            if (IntervalTimer.Instance != null)
                IntervalTimer.Instance.Resume();

            // Check if there are more waves
            if (currentWaveNumber + 1 < waveSequences.Count)
            {
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

            // Switch from lobby music to battle music
            if (MusicSystem.instance != null)
                MusicSystem.instance.SwitchToBattleTrack();
        }

        // Public accessors for UI/other systems
        public WaveState CurrentState => currentState;
        public int CurrentWaveIndex => currentWaveIndex;
        public int CurrentWaveNumber => currentWaveNumber + 1; // +1 for display (1-indexed)
        public int TotalWaves => waveSequences.Count;
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
        /// Notify WaveManager that an enemy unit was destroyed (for wave completion tracking).
        /// </summary>
        public void OnEnemyUnitDestroyed(UnitType unitType)
        {
            enemyUnitCount--;
            if (enemyUnitCount < 0) enemyUnitCount = 0;

            // Check if this kill counts toward the current objective
            if (currentWaveNumber < waveObjectives.Count)
            {
                WaveObjective obj = waveObjectives[currentWaveNumber];
                bool counts = false;
                switch (obj.type)
                {
                    case ObjectiveType.KillSoldiers: counts = (unitType == UnitType.Soldier); break;
                    case ObjectiveType.KillNinjas: counts = (unitType == UnitType.Ninja); break;
                    case ObjectiveType.KillOgres: counts = (unitType == UnitType.Ogre); break;
                }

                if (counts)
                {
                    currentObjectiveProgress++;
                    if (ObjectiveUI.Instance != null)
                        ObjectiveUI.Instance.IncrementCleared();
                }
            }

            Debug.Log($"[WaveManager] Enemy {unitType} destroyed. {enemyUnitCount} enemies remaining. Objective progress: {currentObjectiveProgress}");

            CheckWaveComplete();
        }

        /// <summary>
        /// Notify WaveManager that a resource node was destroyed (for wave completion tracking).
        /// </summary>
        public void OnResourceNodeDestroyed()
        {
            resourceNodeCount--;
            if (resourceNodeCount < 0) resourceNodeCount = 0;

            // Check if resource destruction counts toward the current objective
            if (currentWaveNumber < waveObjectives.Count && waveObjectives[currentWaveNumber].type == ObjectiveType.DestroyResources)
            {
                currentObjectiveProgress++;
                if (ObjectiveUI.Instance != null)
                    ObjectiveUI.Instance.IncrementCleared();
            }

            Debug.Log($"[WaveManager] Resource destroyed. {resourceNodeCount} resources remaining. Objective progress: {currentObjectiveProgress}");

            CheckWaveComplete();
        }

        /// <summary>
        /// Register an existing resource node (e.g. the starting resource spawned by GameSetup).
        /// </summary>
        public void RegisterResourceNode()
        {
            resourceNodeCount++;
        }

        /// <summary>
        /// Check if the current wave is complete (objective target met).
        /// </summary>
        private void CheckWaveComplete()
        {
            if (gameOver) return;

            // Wave is complete when the player meets the objective target
            if (currentWaveNumber < waveObjectives.Count)
            {
                int target = waveObjectives[currentWaveNumber].target;
                if (currentObjectiveProgress >= target && target > 0)
                {
                    Debug.Log($"[WaveManager] Wave {currentWaveNumber + 1} complete - objective met ({currentObjectiveProgress}/{target})!");
                    allEnemiesSpawned = false; // Reset for next wave
                    CompleteCurrentWave();
                }
            }
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
