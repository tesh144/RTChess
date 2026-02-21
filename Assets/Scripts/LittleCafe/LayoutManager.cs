using UnityEngine;
using System.Collections.Generic;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// Manages equipment placement on the grid, save/load to JSON.
    /// Assign equipment prefabs in the Inspector.
    /// </summary>
    public class LayoutManager : MonoBehaviour
    {
        public static LayoutManager Instance { get; private set; }

        [Header("Equipment Prefabs (assign in Inspector)")]
        [SerializeField] private GameObject cookingStationPrefab;
        [SerializeField] private GameObject servingCounterPrefab;
        [SerializeField] private GameObject washingStationPrefab;
        [SerializeField] private GameObject plateRackPrefab;
        [SerializeField] private GameObject wallPrefab;
        [SerializeField] private GameObject doorPrefab;
        [SerializeField] private GameObject tablePrefab;
        [SerializeField] private GameObject chairPrefab;

        [Header("Settings")]
        [SerializeField] private string defaultSaveFileName = "kitchen_layout.json";

        private Dictionary<Vector2Int, EquipmentType> equipmentMap = new Dictionary<Vector2Int, EquipmentType>();
        private Dictionary<Vector2Int, GameObject> equipmentObjects = new Dictionary<Vector2Int, GameObject>();

        public event System.Action<int, int, EquipmentType> OnEquipmentPlaced;
        public event System.Action<int, int> OnEquipmentRemoved;
        public event System.Action OnLayoutCleared;

        public int PlacedCount => equipmentMap.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public GameObject GetPrefab(EquipmentType type)
        {
            return type switch
            {
                EquipmentType.CookingStation => cookingStationPrefab,
                EquipmentType.ServingCounter => servingCounterPrefab,
                EquipmentType.WashingStation => washingStationPrefab,
                EquipmentType.PlateRack => plateRackPrefab,
                EquipmentType.Wall => wallPrefab,
                EquipmentType.Door => doorPrefab,
                EquipmentType.Table => tablePrefab,
                EquipmentType.Chair => chairPrefab,
                _ => null
            };
        }

        public bool PlaceEquipment(int gridX, int gridY, EquipmentType type)
        {
            if (!GridManager.Instance.IsCellEmpty(gridX, gridY))
                return false;

            GameObject prefab = GetPrefab(type);
            if (prefab == null)
            {
                Debug.LogError($"[LayoutManager] No prefab assigned for {type}");
                return false;
            }

            GameObject equipObj = Instantiate(prefab);
            Vector3 worldPos = GridManager.Instance.GridToWorldPosition(gridX, gridY);
            float cellSize = GridManager.Instance.CellSize;
            float height = cellSize * 0.6f;
            equipObj.transform.position = new Vector3(worldPos.x, height * 0.5f, worldPos.z);

            CafeEquipment equipComp = equipObj.GetComponent<CafeEquipment>();
            if (equipComp != null)
                equipComp.Initialize(gridX, gridY, Vector2Int.one);

            GridManager.Instance.PlaceUnit(gridX, gridY, equipObj, CellState.PlayerUnit);

            Vector2Int key = new Vector2Int(gridX, gridY);
            equipmentMap[key] = type;
            equipmentObjects[key] = equipObj;

            OnEquipmentPlaced?.Invoke(gridX, gridY, type);
            return true;
        }

        public bool RemoveEquipment(int gridX, int gridY)
        {
            Vector2Int key = new Vector2Int(gridX, gridY);
            if (!equipmentMap.ContainsKey(key))
                return false;

            GridManager.Instance.RemoveUnit(gridX, gridY);

            if (equipmentObjects.TryGetValue(key, out GameObject obj))
            {
                Destroy(obj);
                equipmentObjects.Remove(key);
            }

            equipmentMap.Remove(key);
            OnEquipmentRemoved?.Invoke(gridX, gridY);
            return true;
        }

        public EquipmentType? GetEquipmentAt(int gridX, int gridY)
        {
            Vector2Int key = new Vector2Int(gridX, gridY);
            if (equipmentMap.TryGetValue(key, out EquipmentType type))
                return type;
            return null;
        }

        public void ClearAll()
        {
            List<Vector2Int> keys = new List<Vector2Int>(equipmentMap.Keys);
            foreach (Vector2Int key in keys)
            {
                RemoveEquipment(key.x, key.y);
            }
            OnLayoutCleared?.Invoke();
            Debug.Log("[LayoutManager] Layout cleared");
        }

        // --- Save/Load ---

        public string SaveToJson()
        {
            KitchenLayoutData data = new KitchenLayoutData
            {
                gridWidth = GridManager.Instance.Width,
                gridHeight = GridManager.Instance.Height
            };

            foreach (var kvp in equipmentMap)
            {
                data.placements.Add(new EquipmentPlacement(kvp.Key.x, kvp.Key.y, kvp.Value));
            }

            return JsonUtility.ToJson(data, true);
        }

        public void LoadFromJson(string json)
        {
            ClearAll();

            KitchenLayoutData data = JsonUtility.FromJson<KitchenLayoutData>(json);
            if (data == null || data.placements == null) return;

            foreach (EquipmentPlacement placement in data.placements)
            {
                PlaceEquipment(placement.gridX, placement.gridY, placement.equipmentType);
            }
        }

        public void SaveToFile(string filename = null)
        {
            if (string.IsNullOrEmpty(filename))
                filename = defaultSaveFileName;

            string json = SaveToJson();
            string path = System.IO.Path.Combine(Application.persistentDataPath, filename);
            System.IO.File.WriteAllText(path, json);
            Debug.Log($"[LayoutManager] Layout saved to {path}");
        }

        public bool LoadFromFile(string filename = null)
        {
            if (string.IsNullOrEmpty(filename))
                filename = defaultSaveFileName;

            string path = System.IO.Path.Combine(Application.persistentDataPath, filename);
            if (!System.IO.File.Exists(path))
            {
                Debug.LogWarning($"[LayoutManager] No save file at {path}");
                return false;
            }

            string json = System.IO.File.ReadAllText(path);
            LoadFromJson(json);
            Debug.Log($"[LayoutManager] Layout loaded from {path}");
            return true;
        }

        public void InstantiateLayout(KitchenLayoutData data)
        {
            ClearAll();
            if (data == null || data.placements == null) return;

            foreach (EquipmentPlacement placement in data.placements)
            {
                PlaceEquipment(placement.gridX, placement.gridY, placement.equipmentType);
            }
        }
    }
}
