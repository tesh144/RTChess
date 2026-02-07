using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Temporary debug script:
    /// Left-click: Place player Soldier (costs 3 tokens)
    /// Right-click: Place Level 1 Resource Node (free)
    /// Middle-click OR Shift+Left-click: Place enemy Soldier (free, for testing)
    /// </summary>
    public class CellDebugPlacer : MonoBehaviour
    {
        [Header("Debug Settings")]
        [SerializeField] private bool enableDebugPlacement = false; // Disabled by default for production

        [SerializeField] private GameObject soldierPrefab;
        [SerializeField] private GameObject enemySoldierPrefab;
        [SerializeField] private GameObject resourceNodePrefab;

        private Camera mainCam;

        private void Start()
        {
            mainCam = Camera.main;
        }

        /// <summary>
        /// Called by DebugMenu to toggle debug placement mode
        /// </summary>
        public void SetDebugMode(bool enabled)
        {
            enableDebugPlacement = enabled;
        }

        private void Update()
        {
            // Early exit if debug placement is disabled (dock bar is the primary placement method)
            if (!enableDebugPlacement) return;

            // Left-click: Player soldier (costs tokens) - DISABLED in production, use dock bar
            if (Input.GetMouseButtonDown(0) && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
            {
                TryPlaceUnit();
            }
            // Right-click: Resource node (free) - ENABLED for testing
            else if (Input.GetMouseButtonDown(1))
            {
                TryPlaceResource();
            }
            // Middle-click OR Shift+Left-click: Enemy soldier (free) - ENABLED for testing
            else if (Input.GetMouseButtonDown(2) || (Input.GetMouseButtonDown(0) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))))
            {
                TryPlaceEnemyUnit();
            }
        }

        private bool TryGetGridCell(out int gridX, out int gridY)
        {
            gridX = 0;
            gridY = 0;

            if (GridManager.Instance == null) return false;

            Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (!groundPlane.Raycast(ray, out float distance)) return false;

            Vector3 hitPoint = ray.GetPoint(distance);

            if (!GridManager.Instance.WorldToGridPosition(hitPoint, out gridX, out gridY))
                return false;

            return GridManager.Instance.IsCellEmpty(gridX, gridY);
        }

        private void TryPlaceUnit()
        {
            if (soldierPrefab == null) return;
            if (!TryGetGridCell(out int gridX, out int gridY)) return;

            // Check if we have enough tokens
            Unit soldierUnit = soldierPrefab.GetComponent<Unit>();
            if (soldierUnit != null && ResourceTokenManager.Instance != null)
            {
                int cost = soldierUnit.ResourceCost;
                if (!ResourceTokenManager.Instance.HasEnoughTokens(cost))
                {
                    Debug.Log($"Not enough tokens! Need {cost}, have {ResourceTokenManager.Instance.CurrentTokens}");
                    return;
                }

                // Spend tokens
                if (!ResourceTokenManager.Instance.SpendTokens(cost))
                    return;
            }

            Vector3 worldPos = GridManager.Instance.GridToWorldPosition(gridX, gridY);
            GameObject unitObj = Instantiate(soldierPrefab, worldPos, Quaternion.identity);
            unitObj.SetActive(true);

            Unit unit = unitObj.GetComponent<Unit>();
            if (unit != null)
            {
                unit.Initialize(Team.Player, gridX, gridY);
            }

            GridManager.Instance.PlaceUnit(gridX, gridY, unitObj, CellState.PlayerUnit);
        }

        private void TryPlaceResource()
        {
            if (resourceNodePrefab == null) return;
            if (!TryGetGridCell(out int gridX, out int gridY)) return;

            Vector3 worldPos = GridManager.Instance.GridToWorldPosition(gridX, gridY);
            GameObject nodeObj = Instantiate(resourceNodePrefab, worldPos, Quaternion.identity);
            nodeObj.SetActive(true);

            ResourceNode node = nodeObj.GetComponent<ResourceNode>();
            if (node != null)
            {
                node.GridX = gridX;
                node.GridY = gridY;
            }

            GridManager.Instance.PlaceUnit(gridX, gridY, nodeObj, CellState.Resource);
        }

        private void TryPlaceEnemyUnit()
        {
            if (enemySoldierPrefab == null) return;
            if (!TryGetGridCell(out int gridX, out int gridY)) return;

            // Enemy placement is free (for testing)
            Vector3 worldPos = GridManager.Instance.GridToWorldPosition(gridX, gridY);
            GameObject unitObj = Instantiate(enemySoldierPrefab, worldPos, Quaternion.identity);
            unitObj.SetActive(true);

            Unit unit = unitObj.GetComponent<Unit>();
            if (unit != null)
            {
                unit.Initialize(Team.Enemy, gridX, gridY);
            }

            GridManager.Instance.PlaceUnit(gridX, gridY, unitObj, CellState.EnemyUnit);
            Debug.Log($"Placed enemy unit at ({gridX}, {gridY})");
        }
    }
}
