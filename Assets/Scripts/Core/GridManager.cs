#pragma warning disable CS0414, CS0219, CS0618
using System.Collections.Generic;
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

    /// <summary>
    /// Surface layer type. Independent of CellState (Object layer).
    /// A tile can hold one Object AND one Surface simultaneously.
    /// </summary>
    public enum SurfaceType
    {
        None,
        Water,
        Corruption,
        Lava
    }

    public class GridManager : MonoBehaviour
    {
        [Header("Grid Settings")]
        [SerializeField] private int gridWidth = 50;
        [SerializeField] private int gridHeight = 50;
        [SerializeField] private float cellSize = 1.5f;

        [Header("Tile Prefabs")]
        [SerializeField] private GameObject gridTilePrefabA; // Checkerboard tile A (light)
        [SerializeField] private GameObject gridTilePrefabB; // Checkerboard tile B (dark)

        [Header("Grid Visual")]
        [SerializeField] private Transform gridTilesContainer; // Optional parent for organization

        [Header("Tile Fog")]
        [SerializeField] private float fogDropDistance = 1.5f; // How far tiles drop when fogged

        private CellState[,] cellStates;
        private GameObject[,] cellOccupants;
        private GameObject[,] gridTiles; // Store instantiated tile GameObjects

        // Surface layer — independent of Object layer (cellStates/cellOccupants)
        private SurfaceType[,] cellSurfaces;
        private GameObject[,] surfaceOccupants;

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
            // Initialize in Start so scene setup scripts can set fields first
            if (cellStates == null)
                InitializeGrid();
        }

        /// <summary>
        /// Initialize grid with explicit dimensions (called by MapGeneratorV2).
        /// </summary>
        public void InitializeGrid(int width, int height)
        {
            gridWidth = width;
            gridHeight = height;
            InitializeGrid();
        }

        public void InitializeGrid()
        {
            // Ensure singleton is set (scene setup may call this before our Awake runs)
            if (Instance == null) Instance = this;

            cellStates = new CellState[gridWidth, gridHeight];
            cellOccupants = new GameObject[gridWidth, gridHeight];
            gridTiles = new GameObject[gridWidth, gridHeight];
            cellSurfaces = new SurfaceType[gridWidth, gridHeight];
            surfaceOccupants = new GameObject[gridWidth, gridHeight];

            // Create container if not assigned
            if (gridTilesContainer == null)
            {
                GameObject containerObj = new GameObject("GridTiles");
                containerObj.transform.SetParent(transform);
                containerObj.transform.localPosition = Vector3.zero;
                gridTilesContainer = containerObj.transform;
            }

            // If no tile prefabs assigned, create default cubes
            EnsureTilePrefabs();

            int tilesCreated = 0;
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    cellStates[x, y] = CellState.Empty;
                    cellOccupants[x, y] = null;

                    // Checkerboard pattern: alternate A and B
                    GameObject prefabToUse = (x + y) % 2 == 0 ? gridTilePrefabA : gridTilePrefabB;
                    // Fallback: if B has no prefab, use A
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

            Debug.Log($"[GridManager] Initialized {gridWidth}x{gridHeight} grid with {tilesCreated} tiles. A={gridTilePrefabA?.name ?? "null"}, B={gridTilePrefabB?.name ?? "null"}");
        }

        /// <summary>
        /// Creates default cube tile prefabs if no prefabs are assigned in Inspector.
        /// </summary>
        private void EnsureTilePrefabs()
        {
            if (gridTilePrefabA != null) return;

            Debug.Log("[GridManager] No tile prefabs assigned, creating default white/gray cubes");

            // Create default cube for A
            GameObject cubeA = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeA.name = "DefaultTileA_White";
            var rendererA = cubeA.GetComponent<MeshRenderer>();
            if (rendererA != null)
            {
                rendererA.material = new Material(Shader.Find("Standard"));
                rendererA.material.color = Color.white;
            }
            var colliderA = cubeA.GetComponent<Collider>();
            if (colliderA != null) DestroyImmediate(colliderA);
            cubeA.SetActive(false);
            gridTilePrefabA = cubeA;

            // Create default cube for B
            GameObject cubeB = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeB.name = "DefaultTileB_Gray";
            var rendererB = cubeB.GetComponent<MeshRenderer>();
            if (rendererB != null)
            {
                rendererB.material = new Material(Shader.Find("Standard"));
                rendererB.material.color = new Color(0.7f, 0.7f, 0.7f);
            }
            var colliderB = cubeB.GetComponent<Collider>();
            if (colliderB != null) DestroyImmediate(colliderB);
            cubeB.SetActive(false);
            gridTilePrefabB = cubeB;
        }

        /// <summary>
        /// Converts grid coordinates to world position (center of cell).
        /// Grid is on the XZ plane, Y=0.
        /// </summary>
        public Vector3 GridToWorldPosition(int gridX, int gridY)
        {
            // Center grid so that one cell is centered at (0,0,0)
            // For a 5x5 grid, cell [2,2] will be at origin
            int centerX = gridWidth / 2;
            int centerZ = gridHeight / 2;
            float offsetX = centerX * cellSize;
            float offsetZ = centerZ * cellSize;

            Vector3 origin = transform.position;
            return new Vector3(
                origin.x + gridX * cellSize - offsetX,
                origin.y,
                origin.z + gridY * cellSize - offsetZ
            );
        }

        /// <summary>
        /// Converts world position to grid coordinates. Returns false if out of bounds.
        /// </summary>
        public bool WorldToGridPosition(Vector3 worldPos, out int gridX, out int gridY)
        {
            // Match the centering logic from GridToWorldPosition
            int centerX = gridWidth / 2;
            int centerZ = gridHeight / 2;
            float offsetX = centerX * cellSize;
            float offsetZ = centerZ * cellSize;

            Vector3 origin = transform.position;
            gridX = Mathf.RoundToInt((worldPos.x - origin.x + offsetX) / cellSize);
            gridY = Mathf.RoundToInt((worldPos.z - origin.z + offsetZ) / cellSize);

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
        /// Check whether a tile's fog has been revealed.
        /// Works universally — no dependency on FogManager.
        /// </summary>
        public bool IsTileRevealed(int gridX, int gridY)
        {
            if (!IsValidCell(gridX, gridY) || gridTiles == null) return false;
            GameObject tile = gridTiles[gridX, gridY];
            if (tile == null) return false;
            TileFog tileFog = tile.GetComponent<TileFog>();
            return tileFog == null || tileFog.IsRevealed;
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

        // ── Surface Layer API ────────────────────────────────────────────

        /// <summary>
        /// Place a surface on a tile. Does NOT affect the Object layer (cellStates/cellOccupants).
        /// A tile can hold one Object AND one Surface simultaneously.
        /// </summary>
        public bool PlaceSurface(int gridX, int gridY, SurfaceType type, GameObject go)
        {
            if (!IsValidCell(gridX, gridY)) return false;
            cellSurfaces[gridX, gridY] = type;
            surfaceOccupants[gridX, gridY] = go;
            return true;
        }

        public void RemoveSurface(int gridX, int gridY)
        {
            if (!IsValidCell(gridX, gridY)) return;
            cellSurfaces[gridX, gridY] = SurfaceType.None;
            surfaceOccupants[gridX, gridY] = null;
        }

        public SurfaceType GetSurface(int gridX, int gridY)
        {
            if (!IsValidCell(gridX, gridY)) return SurfaceType.None;
            return cellSurfaces[gridX, gridY];
        }

        public bool HasSurface(int gridX, int gridY)
        {
            return IsValidCell(gridX, gridY) && cellSurfaces[gridX, gridY] != SurfaceType.None;
        }

        public GameObject GetSurfaceOccupant(int gridX, int gridY)
        {
            if (!IsValidCell(gridX, gridY)) return null;
            return surfaceOccupants[gridX, gridY];
        }

        // ── End Surface Layer API ────────────────────────────────────────

        /// <summary>
        /// Get the GameObject occupying a cell, or null if empty.
        /// </summary>
        public GameObject GetOccupant(int gridX, int gridY)
        {
            if (!IsValidCell(gridX, gridY)) return null;
            return cellOccupants[gridX, gridY];
        }

        // --- Multi-Cell Helpers ---

        /// <summary>
        /// Check if ALL cells in a rectangular footprint are valid, empty, and revealed.
        /// </summary>
        public bool AreAllCellsAvailable(int anchorX, int anchorY, Vector2Int size)
        {
            for (int dx = 0; dx < size.x; dx++)
            {
                for (int dy = 0; dy < size.y; dy++)
                {
                    int cx = anchorX + dx;
                    int cy = anchorY + dy;
                    if (!IsCellEmpty(cx, cy)) return false;
                    if (!IsTileRevealed(cx, cy)) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Place a unit across multiple cells. All cells point to the same GameObject.
        /// Returns false without modifying state if any cell is occupied.
        /// </summary>
        public bool PlaceMultiCell(int anchorX, int anchorY, Vector2Int size, GameObject unit, CellState state)
        {
            // Pre-validate all cells
            for (int dx = 0; dx < size.x; dx++)
                for (int dy = 0; dy < size.y; dy++)
                    if (!IsCellEmpty(anchorX + dx, anchorY + dy)) return false;

            // Place across all cells
            for (int dx = 0; dx < size.x; dx++)
                for (int dy = 0; dy < size.y; dy++)
                    PlaceUnit(anchorX + dx, anchorY + dy, unit, state);

            return true;
        }

        /// <summary>
        /// Remove a unit from all cells in a rectangular footprint.
        /// </summary>
        public void RemoveMultiCell(int anchorX, int anchorY, Vector2Int size)
        {
            for (int dx = 0; dx < size.x; dx++)
                for (int dy = 0; dy < size.y; dy++)
                    RemoveUnit(anchorX + dx, anchorY + dy);
        }

        /// <summary>
        /// Get the world-space center of a multi-cell footprint.
        /// For 1x1 this returns the same as GridToWorldPosition.
        /// </summary>
        public Vector3 GetFootprintCenter(int anchorX, int anchorY, Vector2Int size)
        {
            Vector3 anchor = GridToWorldPosition(anchorX, anchorY);
            Vector3 far = GridToWorldPosition(anchorX + size.x - 1, anchorY + size.y - 1);
            return (anchor + far) * 0.5f;
        }

        // ── GridShape / Offset-based API ─────────────────────────────────────

        /// <summary>
        /// Check if ALL cells defined by shape offsets (at the given rotation) are
        /// valid grid coordinates, currently empty, and fog-revealed.
        /// Returns false immediately on the first failed cell.
        /// </summary>
        public bool AreOffsetCellsAvailable(int anchorX, int anchorY, GridShape shape, int rotation = 0)
        {
            if (shape == null || shape.IsEmpty) return IsCellEmpty(anchorX, anchorY);

            List<Vector2Int> offsets = shape.GetOffsets(rotation);
            foreach (var offset in offsets)
            {
                int cx = anchorX + offset.x;
                int cy = anchorY + offset.y;
                if (!IsCellEmpty(cx, cy)) return false;
                if (!IsTileRevealed(cx, cy)) return false;
            }
            return true;
        }

        /// <summary>
        /// Place a unit across all cells defined by shape offsets at the given rotation.
        /// All cells must be empty; returns false without modifying state if any cell is occupied.
        /// On success, every offset cell stores the same GameObject and CellState.
        /// </summary>
        public bool PlaceWithOffsets(int anchorX, int anchorY, GridShape shape, int rotation,
                                     GameObject unit, CellState state)
        {
            if (shape == null || shape.IsEmpty)
                return PlaceUnit(anchorX, anchorY, unit, state);

            List<Vector2Int> offsets = shape.GetOffsets(rotation);

            // Pre-validate — abort entirely if any cell is occupied
            foreach (var offset in offsets)
                if (!IsCellEmpty(anchorX + offset.x, anchorY + offset.y)) return false;

            // Commit placement
            foreach (var offset in offsets)
                PlaceUnit(anchorX + offset.x, anchorY + offset.y, unit, state);

            return true;
        }

        /// <summary>
        /// Remove a unit from all cells defined by shape offsets at the given rotation.
        /// </summary>
        public void RemoveWithOffsets(int anchorX, int anchorY, GridShape shape, int rotation = 0)
        {
            if (shape == null || shape.IsEmpty)
            {
                RemoveUnit(anchorX, anchorY);
                return;
            }

            List<Vector2Int> offsets = shape.GetOffsets(rotation);
            foreach (var offset in offsets)
                RemoveUnit(anchorX + offset.x, anchorY + offset.y);
        }

        /// <summary>
        /// Returns the world-space centroid of the shape's footprint at the given rotation.
        /// Falls back to GridToWorldPosition(anchorX, anchorY) if the shape is null or empty.
        /// </summary>
        public Vector3 GetOffsetFootprintCenter(int anchorX, int anchorY, GridShape shape, int rotation = 0)
        {
            if (shape == null || shape.IsEmpty)
                return GridToWorldPosition(anchorX, anchorY);

            List<Vector2Int> offsets = shape.GetOffsets(rotation);
            if (offsets.Count == 0)
                return GridToWorldPosition(anchorX, anchorY);

            Vector3 sum = Vector3.zero;
            foreach (var offset in offsets)
                sum += GridToWorldPosition(anchorX + offset.x, anchorY + offset.y);

            return sum / offsets.Count;
        }

    }
}
