using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Temporary debug script: click any empty grid cell to spawn a Soldier.
    /// No cost validation - purely for testing rotation and grid systems.
    /// </summary>
    public class CellDebugPlacer : MonoBehaviour
    {
        [SerializeField] private GameObject soldierPrefab;
        [SerializeField] private LayerMask gridLayerMask;

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
        }

        private void TryPlaceUnit()
        {
            if (GridManager.Instance == null || soldierPrefab == null) return;

            Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);

            // Raycast to the ground plane (Y=0)
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (!groundPlane.Raycast(ray, out float distance)) return;

            Vector3 hitPoint = ray.GetPoint(distance);

            if (!GridManager.Instance.WorldToGridPosition(hitPoint, out int gridX, out int gridY))
                return;

            if (!GridManager.Instance.IsCellEmpty(gridX, gridY))
                return;

            // Spawn the soldier
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
    }
}
