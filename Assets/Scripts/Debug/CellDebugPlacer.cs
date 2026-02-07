using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Temporary debug script:
    /// Left-click empty cell to spawn a Soldier.
    /// Right-click empty cell to spawn a Level 1 Resource Node.
    /// </summary>
    public class CellDebugPlacer : MonoBehaviour
    {
        [SerializeField] private GameObject soldierPrefab;
        [SerializeField] private GameObject resourceNodePrefab;

        private Camera mainCam;

        private void Start()
        {
            mainCam = Camera.main;
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                TryPlaceUnit();
            }
            if (Input.GetMouseButtonDown(1))
            {
                TryPlaceResource();
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
    }
}
