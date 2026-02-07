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
        [SerializeField] private int gridWidth = 4;
        [SerializeField] private int gridHeight = 4;
        [SerializeField] private float cellSize = 1.5f;

        private CellState[,] cellStates;
        private GameObject[,] cellOccupants;

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
            cellStates = new CellState[gridWidth, gridHeight];
            cellOccupants = new GameObject[gridWidth, gridHeight];

            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    cellStates[x, y] = CellState.Empty;
                    cellOccupants[x, y] = null;
                }
            }
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
    }
}
