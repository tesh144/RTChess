using System.Collections.Generic;
using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Manages automatic spawning of resource nodes throughout the game.
    /// Iteration 8: Spawns nodes on timer with level distribution and caps.
    /// </summary>
    public class ResourceSpawner : MonoBehaviour
    {
        // Singleton
        public static ResourceSpawner Instance { get; private set; }

        [Header("Spawn Timing")]
        [SerializeField] private int spawnIntervalCount = 10; // Spawn every X intervals (default: 10 = 20 seconds)

        [Header("Node Caps")]
        [SerializeField] private int maxTotalNodes = 5;
        [SerializeField] private int maxLevel2Nodes = 2;
        [SerializeField] private int maxLevel3Nodes = 1;

        [Header("Level Probabilities (must sum to 100)")]
        [SerializeField] private float level1Probability = 60f; // 60%
        [SerializeField] private float level2Probability = 35f; // 35%
        [SerializeField] private float level3Probability = 5f;  // 5%

        [Header("Spawn Preferences")]
        [SerializeField] private bool preferFoggedSpawns = true;
        [SerializeField] private float foggedSpawnWeight = 70f; // 70% chance fogged, 30% revealed

        [Header("Resource Node Prefabs")]
        [SerializeField] private GameObject level1Prefab;
        [SerializeField] private GameObject level2Prefab;
        [SerializeField] private GameObject level3Prefab;

        [Header("Resource Node Stats")]
        [SerializeField] private ResourceNodeStats level1Stats;
        [SerializeField] private ResourceNodeStats level2Stats;
        [SerializeField] private ResourceNodeStats level3Stats;

        // Active tracking
        private List<ResourceNode> activeNodes = new List<ResourceNode>();
        private int currentIntervalCount = 0;

        // Spawn attempt tracking
        private const int MaxSpawnAttempts = 10;

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
        /// Initialize the spawner and spawn starting resource in revealed area.
        /// </summary>
        public void Initialize()
        {
            // Subscribe to interval timer
            if (IntervalTimer.Instance != null)
            {
                IntervalTimer.Instance.OnIntervalTick += OnIntervalTick;
            }

            // Spawn initial resource in revealed center area
            SpawnInitialResource();

            Debug.Log($"ResourceSpawner initialized: Spawn every {spawnIntervalCount} intervals, max {maxTotalNodes} total nodes");
        }

        private void OnDestroy()
        {
            if (IntervalTimer.Instance != null)
            {
                IntervalTimer.Instance.OnIntervalTick -= OnIntervalTick;
            }
        }

        /// <summary>
        /// Called every interval. Attempts to spawn resource when spawn interval reached.
        /// Only spawns during wave downtime (Preparation state), not during active waves.
        /// </summary>
        private void OnIntervalTick(int intervalCount)
        {
            currentIntervalCount++;

            if (currentIntervalCount >= spawnIntervalCount)
            {
                currentIntervalCount = 0;

                // Only spawn during wave downtime (not during active waves)
                if (WaveManager.Instance != null && WaveManager.Instance.CurrentState == WaveState.Active)
                {
                    Debug.Log("Skipping resource spawn (wave is active)");
                    return;
                }

                AttemptSpawn();
            }
        }

        /// <summary>
        /// Spawn initial resource in the revealed center area.
        /// Ensures players always have at least one resource to harvest.
        /// </summary>
        private void SpawnInitialResource()
        {
            if (GridManager.Instance == null || FogManager.Instance == null)
            {
                Debug.LogWarning("Cannot spawn initial resource: GridManager or FogManager not ready");
                return;
            }

            // Find revealed cells in center area
            List<Vector2Int> revealedCenterCells = new List<Vector2Int>();
            int centerX = GridManager.Instance.Width / 2;
            int centerY = GridManager.Instance.Height / 2;
            int searchRadius = 2; // Search within 2 cells of center

            for (int dx = -searchRadius; dx <= searchRadius; dx++)
            {
                for (int dy = -searchRadius; dy <= searchRadius; dy++)
                {
                    int x = centerX + dx;
                    int y = centerY + dy;

                    if (FogManager.Instance.IsCellRevealed(x, y) && GridManager.Instance.IsCellEmpty(x, y))
                    {
                        revealedCenterCells.Add(new Vector2Int(x, y));
                    }
                }
            }

            if (revealedCenterCells.Count > 0)
            {
                // Pick random cell from revealed center
                Vector2Int spawnPos = revealedCenterCells[Random.Range(0, revealedCenterCells.Count)];
                SpawnResourceNode(1, spawnPos); // Always spawn Level 1 for starting resource
                Debug.Log($"Spawned initial resource at ({spawnPos.x}, {spawnPos.y}) in revealed center area");
            }
            else
            {
                Debug.LogWarning("No valid cells in revealed center area for initial resource");
            }
        }

        /// <summary>
        /// Attempt to spawn a new resource node if under cap.
        /// </summary>
        public void AttemptSpawn()
        {
            // Check total cap
            CleanupDestroyedNodes();
            if (activeNodes.Count >= maxTotalNodes)
            {
                Debug.Log($"At max node cap ({maxTotalNodes}), skipping spawn");
                return;
            }

            // Choose random level with distribution
            int chosenLevel = GetRandomLevel();

            // Check if level is at cap, reroll if needed
            if (!CanSpawnLevel(chosenLevel))
            {
                // Reroll to Level 1 (safest fallback)
                chosenLevel = 1;
            }

            // Find valid spawn location
            Vector2Int? spawnPos = FindValidSpawnLocation(chosenLevel);
            if (!spawnPos.HasValue)
            {
                Debug.Log($"No valid spawn location for Level {chosenLevel} node, skipping spawn");
                return;
            }

            // Spawn the node
            SpawnResourceNode(chosenLevel, spawnPos.Value);
        }

        /// <summary>
        /// Get random resource level based on probabilities (60/35/5).
        /// </summary>
        private int GetRandomLevel()
        {
            float roll = Random.Range(0f, 100f);

            if (roll < level1Probability)
                return 1;
            else if (roll < level1Probability + level2Probability)
                return 2;
            else
                return 3;
        }

        /// <summary>
        /// Check if we can spawn a node of this level (not at cap).
        /// </summary>
        private bool CanSpawnLevel(int level)
        {
            int currentLevel2Count = 0;
            int currentLevel3Count = 0;

            foreach (ResourceNode node in activeNodes)
            {
                if (node == null) continue;
                if (node.Level == 2) currentLevel2Count++;
                if (node.Level == 3) currentLevel3Count++;
            }

            if (level == 2 && currentLevel2Count >= maxLevel2Nodes)
                return false;
            if (level == 3 && currentLevel3Count >= maxLevel3Nodes)
                return false;

            return true;
        }

        /// <summary>
        /// Find a valid spawn location for a resource of given level.
        /// Prefers fogged cells over revealed cells.
        /// </summary>
        private Vector2Int? FindValidSpawnLocation(int level)
        {
            if (GridManager.Instance == null) return null;

            Vector2Int gridSize = GetGridSizeForLevel(level);

            // Get fogged and revealed empty cells
            List<Vector2Int> foggedCells = GetEmptyFoggedCells(gridSize);
            List<Vector2Int> revealedCells = GetEmptyRevealedCells(gridSize);

            // Build weighted candidate list
            List<Vector2Int> candidates = new List<Vector2Int>();

            // Add fogged cells multiple times for weight (70%)
            if (preferFoggedSpawns && foggedCells.Count > 0)
            {
                int foggedWeight = Mathf.RoundToInt(foggedSpawnWeight / (100f - foggedSpawnWeight));
                for (int i = 0; i < foggedWeight; i++)
                {
                    candidates.AddRange(foggedCells);
                }
            }

            // Add revealed cells once (30%)
            candidates.AddRange(revealedCells);

            // Pick random from candidates
            if (candidates.Count == 0)
                return null;

            return candidates[Random.Range(0, candidates.Count)];
        }

        /// <summary>
        /// Get grid size (occupation) for a resource level.
        /// </summary>
        private Vector2Int GetGridSizeForLevel(int level)
        {
            switch (level)
            {
                case 1: return new Vector2Int(1, 1);
                case 2:
                    // Randomly choose horizontal or vertical
                    return Random.Range(0, 2) == 0 ? new Vector2Int(2, 1) : new Vector2Int(1, 2);
                case 3: return new Vector2Int(2, 2);
                default: return new Vector2Int(1, 1);
            }
        }

        /// <summary>
        /// Get all empty fogged cells that can fit a resource of given size.
        /// </summary>
        private List<Vector2Int> GetEmptyFoggedCells(Vector2Int size)
        {
            List<Vector2Int> cells = new List<Vector2Int>();
            if (GridManager.Instance == null || FogManager.Instance == null) return cells;

            for (int x = 0; x < GridManager.Instance.Width; x++)
            {
                for (int y = 0; y < GridManager.Instance.Height; y++)
                {
                    Vector2Int pos = new Vector2Int(x, y);

                    // Check if position is fogged
                    if (!FogManager.Instance.IsCellRevealed(x, y))
                    {
                        // Check if multi-cell area is empty
                        if (CheckMultiCellEmpty(pos, size))
                        {
                            cells.Add(pos);
                        }
                    }
                }
            }

            return cells;
        }

        /// <summary>
        /// Get all empty revealed cells that can fit a resource of given size.
        /// </summary>
        private List<Vector2Int> GetEmptyRevealedCells(Vector2Int size)
        {
            List<Vector2Int> cells = new List<Vector2Int>();
            if (GridManager.Instance == null || FogManager.Instance == null) return cells;

            for (int x = 0; x < GridManager.Instance.Width; x++)
            {
                for (int y = 0; y < GridManager.Instance.Height; y++)
                {
                    Vector2Int pos = new Vector2Int(x, y);

                    // Check if position is revealed
                    if (FogManager.Instance.IsCellRevealed(x, y))
                    {
                        // Check if multi-cell area is empty
                        if (CheckMultiCellEmpty(pos, size))
                        {
                            cells.Add(pos);
                        }
                    }
                }
            }

            return cells;
        }

        /// <summary>
        /// Check if a multi-cell area is empty (all cells empty and in bounds).
        /// </summary>
        private bool CheckMultiCellEmpty(Vector2Int pos, Vector2Int size)
        {
            if (GridManager.Instance == null) return false;

            for (int dx = 0; dx < size.x; dx++)
            {
                for (int dy = 0; dy < size.y; dy++)
                {
                    int checkX = pos.x + dx;
                    int checkY = pos.y + dy;

                    if (!GridManager.Instance.IsValidCell(checkX, checkY))
                        return false;

                    if (!GridManager.Instance.IsCellEmpty(checkX, checkY))
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Spawn a resource node at the given position.
        /// </summary>
        private void SpawnResourceNode(int level, Vector2Int position)
        {
            GameObject prefab = GetPrefabForLevel(level);
            if (prefab == null)
            {
                Debug.LogWarning($"No prefab for Level {level} resource node");
                return;
            }

            Vector2Int gridSize = GetGridSizeForLevel(level);

            // Calculate center position for multi-cell resources
            Vector3 worldPos = GridManager.Instance.GridToWorldPosition(position.x, position.y);

            // Offset for multi-cell resources to center them
            if (gridSize.x > 1 || gridSize.y > 1)
            {
                float offsetX = (gridSize.x - 1) * GridManager.Instance.CellSize * 0.5f;
                float offsetY = (gridSize.y - 1) * GridManager.Instance.CellSize * 0.5f;
                worldPos += new Vector3(offsetX, 0f, offsetY);
            }

            // Instantiate node
            GameObject nodeObj = Instantiate(prefab, worldPos, Quaternion.identity);
            nodeObj.SetActive(true);

            ResourceNode node = nodeObj.GetComponent<ResourceNode>();
            if (node != null)
            {
                node.GridX = position.x;
                node.GridY = position.y;

                // Initialize multi-cell occupation
                node.Initialize(gridSize);

                // Register with spawner
                activeNodes.Add(node);

                // Occupy grid cells
                OccupyMultiCell(position, gridSize, nodeObj);

                Debug.Log($"Spawned Level {level} resource at ({position.x}, {position.y}) - Grid size: {gridSize}");
            }
        }

        /// <summary>
        /// Occupy multiple grid cells for a multi-cell resource.
        /// </summary>
        private void OccupyMultiCell(Vector2Int pos, Vector2Int size, GameObject nodeObj)
        {
            if (GridManager.Instance == null) return;

            for (int dx = 0; dx < size.x; dx++)
            {
                for (int dy = 0; dy < size.y; dy++)
                {
                    int cellX = pos.x + dx;
                    int cellY = pos.y + dy;

                    GridManager.Instance.PlaceUnit(cellX, cellY, nodeObj, CellState.Resource);
                }
            }
        }

        /// <summary>
        /// Get prefab for given level.
        /// </summary>
        private GameObject GetPrefabForLevel(int level)
        {
            switch (level)
            {
                case 1: return level1Prefab;
                case 2: return level2Prefab;
                case 3: return level3Prefab;
                default: return level1Prefab;
            }
        }

        /// <summary>
        /// Register a resource node with the spawner.
        /// </summary>
        public void RegisterNode(ResourceNode node)
        {
            if (!activeNodes.Contains(node))
            {
                activeNodes.Add(node);
            }
        }

        /// <summary>
        /// Unregister a resource node (when destroyed).
        /// </summary>
        public void UnregisterNode(ResourceNode node)
        {
            activeNodes.Remove(node);
        }

        /// <summary>
        /// Remove destroyed nodes from active list.
        /// </summary>
        private void CleanupDestroyedNodes()
        {
            activeNodes.RemoveAll(node => node == null || node.IsDestroyed);
        }

        /// <summary>
        /// Get count of active nodes of a specific level.
        /// </summary>
        public int GetActiveNodeCount(int level)
        {
            CleanupDestroyedNodes();
            int count = 0;
            foreach (ResourceNode node in activeNodes)
            {
                if (node != null && node.Level == level)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Get total active node count.
        /// </summary>
        public int GetTotalActiveNodeCount()
        {
            CleanupDestroyedNodes();
            return activeNodes.Count;
        }
    }
}
