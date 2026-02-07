using UnityEngine;

namespace ClockworkGrid
{
    public enum CellState
    {
        Empty,
        PlayerUnit,
        EnemyUnit,
        Resource
    }

    public class GridManager : MonoBehaviour
    {
        [Header("Grid Settings")]
        [SerializeField] private int gridWidth = 50;
        [SerializeField] private int gridHeight = 50;
        [SerializeField] private float cellSize = 1.5f;

        [Header("Grid Visual (Prefab-Based)")]
        [SerializeField] private GameObject gridTilePrefabA; // First tile (e.g., white squares)
        [SerializeField] private GameObject gridTilePrefabB; // Second tile (e.g., black squares) - alternates in checkerboard pattern
        [SerializeField] private Transform gridTilesContainer; // Optional parent for organization

        [Header("Tile Fog")]
        [SerializeField] private float fogDropDistance = 1.5f; // How far tiles drop when fogged

        private CellState[,] cellStates;
        private GameObject[,] cellOccupants;
        private GameObject[,] gridTiles; // Store instantiated tile prefabs

        public int Width => gridWidth;
        public int Height => gridHeight;
        public float CellSize => cellSize;

        public static GridManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // Initialize in Start so GameSetup.Awake can set fields first
            if (cellStates == null)
                InitializeGrid();
        }

        public void InitializeGrid()
        {
            // Ensure singleton is set (GameSetup may call this before our Awake runs)
            if (Instance == null) Instance = this;

            cellStates = new CellState[gridWidth, gridHeight];
            cellOccupants = new GameObject[gridWidth, gridHeight];
            gridTiles = new GameObject[gridWidth, gridHeight];

            // Create container if not assigned
            if (gridTilesContainer == null)
            {
                GameObject containerObj = new GameObject("GridTiles");
                containerObj.transform.SetParent(transform);
                containerObj.transform.localPosition = Vector3.zero;
                gridTilesContainer = containerObj.transform;
            }

            // If no tile prefabs assigned in Inspector, create default cubes
            EnsureTilePrefabs();

            int tilesCreated = 0;
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    cellStates[x, y] = CellState.Empty;
                    cellOccupants[x, y] = null;

                    // Checkerboard pattern: alternate A and B
                    bool useA = (x + y) % 2 == 0;
                    GameObject prefabToUse = useA ? gridTilePrefabA : gridTilePrefabB;

                    // Fallback: if B is null, use A for both
                    if (prefabToUse == null) prefabToUse = gridTilePrefabA;

                    if (prefabToUse != null)
                    {
                        Vector3 tilePos = GridToWorldPosition(x, y);
                        // Position tile so top surface is at Y=0 (units walk on top)
                        tilePos.y = -cellSize / 2f;

                        GameObject tile = Instantiate(prefabToUse, tilePos, Quaternion.identity, gridTilesContainer);
                        tile.name = $"GridTile_{x}_{y}";
                        tile.SetActive(true);
                        // Use prefab's natural proportions — scale uniformly by cellSize
                        tile.transform.localScale = Vector3.one * cellSize;

                        // Attach fog component — tile starts fogged (lowered + faded)
                        TileFog tileFog = tile.AddComponent<TileFog>();
                        tileFog.InitializeFog(-cellSize / 2f, fogDropDistance);

                        gridTiles[x, y] = tile;
                        tilesCreated++;
                    }
                }
            }

            // Subscribe to FogManager reveal events
            if (FogManager.Instance != null)
            {
                FogManager.Instance.OnCellRevealed -= OnFogCellRevealed; // Avoid double-subscribe
                FogManager.Instance.OnCellRevealed += OnFogCellRevealed;
            }

            Debug.Log($"[GridManager] Initialized {gridWidth}x{gridHeight} grid with {tilesCreated} tiles (prefabA={(gridTilePrefabA != null ? gridTilePrefabA.name : "null")}, prefabB={(gridTilePrefabB != null ? gridTilePrefabB.name : "null")})");
        }

        /// <summary>
        /// Creates default cube tile prefabs if none are assigned in Inspector.
        /// </summary>
        private void EnsureTilePrefabs()
        {
            if (gridTilePrefabA != null) return; // Prefabs assigned in Inspector, nothing to do

            Debug.Log("[GridManager] No tile prefabs assigned, creating default white/gray cubes");

            gridTilePrefabA = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gridTilePrefabA.name = "DefaultTileA_White";
            var rendererA = gridTilePrefabA.GetComponent<MeshRenderer>();
            if (rendererA != null)
            {
                rendererA.material = new Material(Shader.Find("Standard"));
                rendererA.material.color = Color.white;
            }
            var colliderA = gridTilePrefabA.GetComponent<Collider>();
            if (colliderA != null) DestroyImmediate(colliderA);
            gridTilePrefabA.SetActive(false);

            gridTilePrefabB = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gridTilePrefabB.name = "DefaultTileB_Gray";
            var rendererB = gridTilePrefabB.GetComponent<MeshRenderer>();
            if (rendererB != null)
            {
                rendererB.material = new Material(Shader.Find("Standard"));
                rendererB.material.color = new Color(0.7f, 0.7f, 0.7f);
            }
            var colliderB = gridTilePrefabB.GetComponent<Collider>();
            if (colliderB != null) DestroyImmediate(colliderB);
            gridTilePrefabB.SetActive(false);
        }

        /// <summary>
        /// Converts grid coordinates to world position (center of cell).
        /// Grid is on the XZ plane, Y=0.
        /// </summary>
        public Vector3 GridToWorldPosition(int gridX, int gridY)
        {
            float offsetX = (gridWidth - 1) * cellSize * 0.5f;
            float offsetZ = (gridHeight - 1) * cellSize * 0.5f;

            return new Vector3(
                gridX * cellSize - offsetX,
                0f,
                gridY * cellSize - offsetZ
            );
        }

        /// <summary>
        /// Converts world position to grid coordinates. Returns false if out of bounds.
        /// </summary>
        public bool WorldToGridPosition(Vector3 worldPos, out int gridX, out int gridY)
        {
            float offsetX = (gridWidth - 1) * cellSize * 0.5f;
            float offsetZ = (gridHeight - 1) * cellSize * 0.5f;

            gridX = Mathf.RoundToInt((worldPos.x + offsetX) / cellSize);
            gridY = Mathf.RoundToInt((worldPos.z + offsetZ) / cellSize);

            return IsValidCell(gridX, gridY);
        }

        public bool IsValidCell(int gridX, int gridY)
        {
            return gridX >= 0 && gridX < gridWidth && gridY >= 0 && gridY < gridHeight;
        }

        public bool IsCellEmpty(int gridX, int gridY)
        {
            if (!IsValidCell(gridX, gridY)) return false;
            return cellStates[gridX, gridY] == CellState.Empty;
        }

        public CellState GetCellState(int gridX, int gridY)
        {
            if (!IsValidCell(gridX, gridY)) return CellState.Empty;
            return cellStates[gridX, gridY];
        }

        public GameObject GetCellOccupant(int gridX, int gridY)
        {
            if (!IsValidCell(gridX, gridY)) return null;
            return cellOccupants[gridX, gridY];
        }

        /// <summary>
        /// Get the visual grid tile GameObject at the specified coordinates.
        /// Useful for applying materials, textures, or effects to specific tiles.
        /// </summary>
        public GameObject GetGridTile(int gridX, int gridY)
        {
            if (!IsValidCell(gridX, gridY)) return null;
            if (gridTiles == null) return null;
            return gridTiles[gridX, gridY];
        }

        /// <summary>
        /// Reveal a tile's fog (tween it up to normal position).
        /// Called by FogManager when cells are revealed.
        /// </summary>
        public void RevealTile(int gridX, int gridY, bool immediate = false)
        {
            if (!IsValidCell(gridX, gridY)) return;
            if (gridTiles == null) return;

            GameObject tile = gridTiles[gridX, gridY];
            if (tile == null) return;

            TileFog tileFog = tile.GetComponent<TileFog>();
            if (tileFog == null) return;

            if (immediate)
                tileFog.RevealImmediate();
            else
                tileFog.Reveal();
        }

        /// <summary>
        /// Handler for FogManager.OnCellRevealed event.
        /// </summary>
        private void OnFogCellRevealed(int x, int y)
        {
            RevealTile(x, y);
        }

        public bool PlaceUnit(int gridX, int gridY, GameObject unit, CellState state)
        {
            if (!IsCellEmpty(gridX, gridY)) return false;

            cellStates[gridX, gridY] = state;
            cellOccupants[gridX, gridY] = unit;
            return true;
        }

        public void RemoveUnit(int gridX, int gridY)
        {
            if (!IsValidCell(gridX, gridY)) return;

            cellStates[gridX, gridY] = CellState.Empty;
            cellOccupants[gridX, gridY] = null;
        }

        /// <summary>
        /// Resize grid to new dimensions (Iteration 9: Grid Expansion).
        /// Preserves existing cells by centering them in the new grid.
        /// </summary>
        public void ResizeGrid(Vector2Int newSize)
        {
            if (newSize.x <= 0 || newSize.y <= 0)
            {
                Debug.LogWarning($"Invalid grid size: {newSize}");
                return;
            }

            Debug.Log($"Resizing grid from {gridWidth}×{gridHeight} to {newSize.x}×{newSize.y}");

            // Create new arrays
            CellState[,] newCellStates = new CellState[newSize.x, newSize.y];
            GameObject[,] newCellOccupants = new GameObject[newSize.x, newSize.y];

            // Initialize all new cells as empty
            for (int x = 0; x < newSize.x; x++)
            {
                for (int y = 0; y < newSize.y; y++)
                {
                    newCellStates[x, y] = CellState.Empty;
                    newCellOccupants[x, y] = null;
                }
            }

            // Copy existing cells (centered expansion)
            if (cellStates != null)
            {
                int offsetX = (newSize.x - gridWidth) / 2;
                int offsetY = (newSize.y - gridHeight) / 2;

                // Track which GameObjects we've already updated to avoid processing multi-cell resources multiple times
                System.Collections.Generic.HashSet<GameObject> processedObjects = new System.Collections.Generic.HashSet<GameObject>();

                for (int x = 0; x < gridWidth; x++)
                {
                    for (int y = 0; y < gridHeight; y++)
                    {
                        int newX = x + offsetX;
                        int newY = y + offsetY;

                        if (newX >= 0 && newX < newSize.x && newY >= 0 && newY < newSize.y)
                        {
                            newCellStates[newX, newY] = cellStates[x, y];
                            newCellOccupants[newX, newY] = cellOccupants[x, y];

                            // Update unit/resource grid positions (only once per GameObject)
                            GameObject occupant = cellOccupants[x, y];
                            if (occupant != null && !processedObjects.Contains(occupant))
                            {
                                processedObjects.Add(occupant);

                                Unit unit = occupant.GetComponent<Unit>();
                                if (unit != null)
                                {
                                    unit.GridX = newX;
                                    unit.GridY = newY;
                                }

                                ResourceNode resource = occupant.GetComponent<ResourceNode>();
                                if (resource != null)
                                {
                                    // Update anchor position (top-left corner for multi-cell resources)
                                    resource.GridX = newX;
                                    resource.GridY = newY;

                                    // For multi-cell resources, copy all occupied cells to new grid
                                    Vector2Int gridSize = resource.GridSize;
                                    for (int dx = 0; dx < gridSize.x; dx++)
                                    {
                                        for (int dy = 0; dy < gridSize.y; dy++)
                                        {
                                            int oldCellX = x + dx;
                                            int oldCellY = y + dy;
                                            int newCellX = newX + dx;
                                            int newCellY = newY + dy;

                                            // Ensure all cells are within bounds and mapped correctly
                                            if (oldCellX < gridWidth && oldCellY < gridHeight &&
                                                newCellX >= 0 && newCellX < newSize.x &&
                                                newCellY >= 0 && newCellY < newSize.y)
                                            {
                                                newCellStates[newCellX, newCellY] = cellStates[oldCellX, oldCellY];
                                                newCellOccupants[newCellX, newCellY] = occupant;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Update world positions of all units/resources
                UpdateAllWorldPositions();
            }

            // Destroy old grid tiles
            if (gridTiles != null)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    for (int y = 0; y < gridHeight; y++)
                    {
                        if (gridTiles[x, y] != null)
                        {
                            Destroy(gridTiles[x, y]);
                        }
                    }
                }
            }

            // Update grid dimensions
            gridWidth = newSize.x;
            gridHeight = newSize.y;
            cellStates = newCellStates;
            cellOccupants = newCellOccupants;

            // Ensure tile prefabs exist before creating new tiles
            EnsureTilePrefabs();

            // Create new grid tiles
            gridTiles = new GameObject[gridWidth, gridHeight];
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    // Determine which prefab to use (checkerboard pattern)
                    bool useA = (x + y) % 2 == 0;
                    GameObject prefabToUse = useA ? gridTilePrefabA : gridTilePrefabB;

                    // Fallback: if B is null, use A for both
                    if (prefabToUse == null) prefabToUse = gridTilePrefabA;

                    if (prefabToUse != null)
                    {
                        Vector3 tilePos = GridToWorldPosition(x, y);
                        // Position tile so top surface is at Y=0 (units walk on top)
                        tilePos.y = -cellSize / 2f;

                        GameObject tile = Instantiate(prefabToUse, tilePos, Quaternion.identity, gridTilesContainer);
                        tile.name = $"GridTile_{x}_{y}";
                        tile.SetActive(true);
                        // Use prefab's natural proportions — scale uniformly by cellSize
                        tile.transform.localScale = Vector3.one * cellSize;

                        // Attach fog component
                        TileFog tileFog = tile.AddComponent<TileFog>();
                        tileFog.InitializeFog(-cellSize / 2f, fogDropDistance);

                        // If FogManager knows this cell is revealed, reveal immediately
                        if (FogManager.Instance != null && FogManager.Instance.IsCellRevealed(x, y))
                        {
                            tileFog.RevealImmediate();
                        }

                        gridTiles[x, y] = tile;
                    }
                }
            }

            Debug.Log($"Grid resized successfully to {gridWidth}×{gridHeight}");
        }

        /// <summary>
        /// Update world positions of all units and resources after grid resize.
        /// </summary>
        private void UpdateAllWorldPositions()
        {
            if (cellOccupants == null) return;

            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    GameObject occupant = cellOccupants[x, y];
                    if (occupant != null)
                    {
                        // Recalculate world position with new grid size
                        Vector3 newWorldPos = GridToWorldPosition(x, y);
                        occupant.transform.position = newWorldPos;
                    }
                }
            }
        }
    }
}
