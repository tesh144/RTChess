#pragma warning disable CS0414, CS0219, CS0618
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
    /// A named prefab slot for grid tiles. Drag a prefab in and give it a label.
    /// Slots 0-1 are the base checkerboard pair. Slots 2-3 are for special surfaces (buildings, water, etc.).
    /// </summary>
    [System.Serializable]
    public class TilePrefabSlot
    {
        [Tooltip("Friendly label shown in the Inspector (e.g. 'Grass Light', 'Under Building')")]
        public string name = "Unnamed";
        [Tooltip("Prefab used for tiles in this slot")]
        public GameObject prefab;
    }

    public class GridManager : MonoBehaviour
    {
        [Header("Grid Settings")]
        [SerializeField] private int gridWidth = 50;
        [SerializeField] private int gridHeight = 50;
        [SerializeField] private float cellSize = 1.5f;

        [Header("Tile Prefabs — Drag prefabs here and name them")]
        [Tooltip("Slot 0 & 1 = base checkerboard pair. Slot 2+ = special (buildings, water, etc.)")]
        [SerializeField] private TilePrefabSlot[] tilePrefabSlots = new TilePrefabSlot[4]
        {
            new TilePrefabSlot { name = "Base Light" },
            new TilePrefabSlot { name = "Base Dark" },
            new TilePrefabSlot { name = "Under Building" },
            new TilePrefabSlot { name = "Water" }
        };

        [Header("Grid Visual")]
        [SerializeField] private Transform gridTilesContainer; // Optional parent for organization

        [Header("Tile Fog")]
        [SerializeField] private float fogDropDistance = 1.5f; // How far tiles drop when fogged

        private CellState[,] cellStates;
        private GameObject[,] cellOccupants;
        private int[,] tileSlotIndices; // Tracks which TilePrefabSlot each tile is using
        private GameObject[,] gridTiles; // Store instantiated tile GameObjects

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
            tileSlotIndices = new int[gridWidth, gridHeight];

            // Create container if not assigned
            if (gridTilesContainer == null)
            {
                GameObject containerObj = new GameObject("GridTiles");
                containerObj.transform.SetParent(transform);
                containerObj.transform.localPosition = Vector3.zero;
                gridTilesContainer = containerObj.transform;
            }

            // If no tile prefab slots have prefabs assigned, create default cubes
            EnsureTilePrefabs();

            int tilesCreated = 0;
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    cellStates[x, y] = CellState.Empty;
                    cellOccupants[x, y] = null;

                    // Checkerboard pattern: alternate slot 0 and slot 1
                    int slotIndex = (x + y) % 2 == 0 ? 0 : 1;
                    tileSlotIndices[x, y] = slotIndex;

                    GameObject prefabToUse = GetSlotPrefab(slotIndex);
                    // Fallback: if slot 1 has no prefab, use slot 0
                    if (prefabToUse == null) prefabToUse = GetSlotPrefab(0);

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

            string slotInfo = "";
            for (int i = 0; i < tilePrefabSlots.Length; i++)
            {
                var slot = tilePrefabSlots[i];
                slotInfo += $"\n  [{i}] \"{slot.name}\" = {(slot.prefab != null ? slot.prefab.name : "null")}";
            }
            Debug.Log($"[GridManager] Initialized {gridWidth}x{gridHeight} grid with {tilesCreated} tiles. Slots:{slotInfo}");
        }

        /// <summary>
        /// Get the prefab from a slot index, with bounds checking.
        /// </summary>
        private GameObject GetSlotPrefab(int slotIndex)
        {
            if (tilePrefabSlots == null || slotIndex < 0 || slotIndex >= tilePrefabSlots.Length) return null;
            return tilePrefabSlots[slotIndex]?.prefab;
        }

        /// <summary>
        /// Creates default cube tile prefabs if no prefabs are assigned in Inspector.
        /// </summary>
        private void EnsureTilePrefabs()
        {
            // Check if slot 0 has a prefab — if so, user has set things up
            if (tilePrefabSlots != null && tilePrefabSlots.Length > 0 &&
                tilePrefabSlots[0] != null && tilePrefabSlots[0].prefab != null) return;

            Debug.Log("[GridManager] No tile prefabs assigned in slots, creating default white/gray cubes");

            // Ensure we have at least 2 slots
            if (tilePrefabSlots == null || tilePrefabSlots.Length < 2)
            {
                tilePrefabSlots = new TilePrefabSlot[4]
                {
                    new TilePrefabSlot { name = "Base Light" },
                    new TilePrefabSlot { name = "Base Dark" },
                    new TilePrefabSlot { name = "Under Building" },
                    new TilePrefabSlot { name = "Water" }
                };
            }

            // Create default cube for slot 0
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
            tilePrefabSlots[0].prefab = cubeA;

            // Create default cube for slot 1
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
            tilePrefabSlots[1].prefab = cubeB;
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

        // --- Tile Prefab Slot System ---

        /// <summary>
        /// Get the TilePrefabSlot array for external read (e.g. editor tools).
        /// </summary>
        public TilePrefabSlot[] TilePrefabSlots => tilePrefabSlots;

        /// <summary>
        /// Swap a single tile to a different prefab slot (by index).
        /// Destroys the old tile, instantiates the new prefab, preserves fog state.
        /// </summary>
        public void SetTileSlot(int gridX, int gridY, int slotIndex)
        {
            if (!IsValidCell(gridX, gridY) || gridTiles == null) return;
            if (tilePrefabSlots == null || slotIndex < 0 || slotIndex >= tilePrefabSlots.Length) return;

            // Skip if already this slot
            if (tileSlotIndices[gridX, gridY] == slotIndex) return;

            GameObject newPrefab = GetSlotPrefab(slotIndex);
            if (newPrefab == null) return;

            GameObject oldTile = gridTiles[gridX, gridY];

            // Capture fog state from old tile before destroying
            bool wasRevealed = false;
            TileFog oldFog = null;
            if (oldTile != null)
            {
                oldFog = oldTile.GetComponent<TileFog>();
                wasRevealed = oldFog != null && oldFog.IsRevealed;
            }

            // Capture position and scale from old tile (or compute fresh)
            Vector3 tilePos;
            Vector3 tileScale;
            if (oldTile != null)
            {
                tilePos = oldTile.transform.position;
                tileScale = oldTile.transform.localScale;
                Destroy(oldTile);
            }
            else
            {
                tilePos = GridToWorldPosition(gridX, gridY);
                tilePos.y = -cellSize / 2f;
                tileScale = Vector3.one * cellSize;
            }

            // Instantiate new tile
            GameObject newTile = Instantiate(newPrefab, tilePos, Quaternion.identity, gridTilesContainer);
            newTile.name = $"GridTile_{gridX}_{gridY}";
            newTile.SetActive(true);
            newTile.transform.localScale = tileScale;

            // Attach fog component — match the revealed state of the old tile
            TileFog tileFog = newTile.AddComponent<TileFog>();
            if (wasRevealed)
            {
                // Tile was already revealed — init at normal position, immediately reveal
                tileFog.InitializeFog(-cellSize / 2f, fogDropDistance);
                tileFog.RevealImmediate();
            }
            else
            {
                // Tile is still fogged — init in fogged (lowered) state
                tileFog.InitializeFog(-cellSize / 2f, fogDropDistance);
            }

            gridTiles[gridX, gridY] = newTile;
            tileSlotIndices[gridX, gridY] = slotIndex;
        }

        /// <summary>
        /// Swap a single tile to a different prefab slot (by name, e.g. "Under Building").
        /// </summary>
        public void SetTileSlot(int gridX, int gridY, string slotName)
        {
            int index = GetSlotIndexByName(slotName);
            if (index >= 0)
                SetTileSlot(gridX, gridY, index);
            else
                Debug.LogWarning($"[GridManager] No tile prefab slot named '{slotName}'");
        }

        /// <summary>
        /// Swap all tiles in a rectangular area to a prefab slot.
        /// Covers anchorX/Y ± radius, plus the building's footprint size.
        /// This is the main method called when buildings are placed.
        /// </summary>
        public void SetTileSlotArea(int anchorX, int anchorY, Vector2Int footprint, int radius, int slotIndex)
        {
            for (int dx = -radius; dx <= radius + footprint.x - 1; dx++)
            {
                for (int dy = -radius; dy <= radius + footprint.y - 1; dy++)
                {
                    SetTileSlot(anchorX + dx, anchorY + dy, slotIndex);
                }
            }
        }

        /// <summary>
        /// Overload: use slot name instead of index.
        /// </summary>
        public void SetTileSlotArea(int anchorX, int anchorY, Vector2Int footprint, int radius, string slotName)
        {
            int index = GetSlotIndexByName(slotName);
            if (index >= 0)
                SetTileSlotArea(anchorX, anchorY, footprint, radius, index);
            else
                Debug.LogWarning($"[GridManager] No tile prefab slot named '{slotName}'");
        }

        /// <summary>
        /// Get which prefab slot a tile is currently using.
        /// </summary>
        public int GetTileSlotIndex(int gridX, int gridY)
        {
            if (!IsValidCell(gridX, gridY) || tileSlotIndices == null) return 0;
            return tileSlotIndices[gridX, gridY];
        }

        /// <summary>
        /// Get the name of the prefab slot a tile is currently using.
        /// </summary>
        public string GetTileSlotName(int gridX, int gridY)
        {
            int index = GetTileSlotIndex(gridX, gridY);
            if (tilePrefabSlots != null && index >= 0 && index < tilePrefabSlots.Length)
                return tilePrefabSlots[index]?.name ?? "Unknown";
            return "Unknown";
        }

        /// <summary>
        /// Find a slot index by name (case-insensitive).
        /// Returns -1 if not found.
        /// </summary>
        public int GetSlotIndexByName(string slotName)
        {
            if (tilePrefabSlots == null || string.IsNullOrEmpty(slotName)) return -1;
            for (int i = 0; i < tilePrefabSlots.Length; i++)
            {
                if (tilePrefabSlots[i] != null &&
                    string.Equals(tilePrefabSlots[i].name, slotName, System.StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
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

    }
}
