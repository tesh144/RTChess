using UnityEngine;
using System.Collections.Generic;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// Loads furniture layouts from JSON codes and instantiates them on the grid.
    /// </summary>
    public class LayoutLoader : MonoBehaviour
    {
        public static LayoutLoader Instance { get; private set; }

        [Header("References")]
        [SerializeField] private FurnitureDatabase furnitureDatabase;

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
        /// Load layout from JSON code.
        /// </summary>
        public bool LoadLayout(string code, bool clearExisting = true)
        {
            if (string.IsNullOrEmpty(code))
            {
                Debug.LogError("[LayoutLoader] Code is empty!");
                return false;
            }

            // Decompress code to JSON
            string json = LayoutSerializer.DecompressFromCode(code);
            if (json == null)
            {
                Debug.LogError("[LayoutLoader] Failed to decompress code!");
                return false;
            }

            // Deserialize JSON to LayoutData
            LayoutData layout = LayoutSerializer.DeserializeLayout(json);
            if (layout == null)
            {
                Debug.LogError("[LayoutLoader] Failed to deserialize layout!");
                return false;
            }

            // Clear existing furniture if requested
            if (clearExisting)
            {
                ClearAllFurniture();
            }

            // Instantiate each furniture piece
            int successCount = 0;
            int failCount = 0;

            foreach (SerializedFurniture data in layout.furniture)
            {
                bool success = InstantiateFurniture(data);
                if (success)
                    successCount++;
                else
                    failCount++;
            }

            Debug.Log($"[LayoutLoader] Loaded {successCount} furniture objects ({failCount} failed)");
            return true;
        }

        /// <summary>
        /// Load layout from JSON string directly (not encoded).
        /// </summary>
        public bool LoadLayoutFromJSON(string json, bool clearExisting = true)
        {
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError("[LayoutLoader] JSON is empty!");
                return false;
            }

            LayoutData layout = LayoutSerializer.DeserializeLayout(json);
            if (layout == null)
            {
                Debug.LogError("[LayoutLoader] Failed to deserialize layout!");
                return false;
            }

            if (clearExisting)
            {
                ClearAllFurniture();
            }

            int successCount = 0;
            int failCount = 0;

            foreach (SerializedFurniture data in layout.furniture)
            {
                bool success = InstantiateFurniture(data);
                if (success)
                    successCount++;
                else
                    failCount++;
            }

            Debug.Log($"[LayoutLoader] Loaded {successCount} furniture objects ({failCount} failed)");
            return true;
        }

        /// <summary>
        /// Instantiate a single furniture piece from serialized data.
        /// </summary>
        private bool InstantiateFurniture(SerializedFurniture data)
        {
            GridManager gm = GridManager.Instance;
            if (gm == null)
            {
                Debug.LogError("[LayoutLoader] GridManager not found!");
                return false;
            }

            if (furnitureDatabase == null)
            {
                Debug.LogError("[LayoutLoader] FurnitureDatabase not assigned!");
                return false;
            }

            // Find furniture data in database
            FurnitureData furnitureData = furnitureDatabase.GetByName(data.assetName);
            if (furnitureData == null)
            {
                Debug.LogWarning($"[LayoutLoader] Furniture not found in database: {data.assetName}");
                return false;
            }

            if (furnitureData.prefab == null)
            {
                Debug.LogWarning($"[LayoutLoader] Prefab not assigned for: {data.assetName}");
                return false;
            }

            // Check if grid cells are available
            if (!gm.AreAllCellsAvailable(data.gridX, data.gridY, furnitureData.gridSize))
            {
                Debug.LogWarning($"[LayoutLoader] Grid cells occupied for: {data.assetName} at ({data.gridX}, {data.gridY})");
                return false;
            }

            // Instantiate prefab
            Vector3 worldPos = gm.GetFootprintCenter(data.gridX, data.gridY, furnitureData.gridSize);
            worldPos.y = 0.01f; // Slight offset above ground

            GameObject furnitureObj = Instantiate(furnitureData.prefab, worldPos, Quaternion.Euler(0f, data.rotation, 0f));
            furnitureObj.SetActive(true);

            // Initialize FurnitureObject
            FurnitureObject furniture = furnitureObj.GetComponent<FurnitureObject>();
            if (furniture != null)
            {
                furniture.OnPlaced(data.gridX, data.gridY, furnitureData.gridSize);
            }
            else
            {
                Debug.LogWarning($"[LayoutLoader] No FurnitureObject component on: {data.assetName}");
                Destroy(furnitureObj);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Clear all placed furniture from the grid.
        /// </summary>
        public void ClearAllFurniture()
        {
            GridManager gm = GridManager.Instance;
            if (gm == null) return;

            List<GameObject> furnitureToRemove = new List<GameObject>();

            // Find all furniture objects
            for (int x = 0; x < gm.Width; x++)
            {
                for (int y = 0; y < gm.Height; y++)
                {
                    GameObject occupant = gm.GetCellOccupant(x, y);
                    if (occupant != null && !furnitureToRemove.Contains(occupant))
                    {
                        FurnitureObject furniture = occupant.GetComponent<FurnitureObject>();
                        if (furniture != null)
                        {
                            furnitureToRemove.Add(occupant);
                        }
                    }
                }
            }

            // Remove all furniture
            foreach (GameObject furnitureObj in furnitureToRemove)
            {
                FurnitureObject furniture = furnitureObj.GetComponent<FurnitureObject>();
                if (furniture != null)
                {
                    furniture.OnRemoved();
                }
                Destroy(furnitureObj);
            }

            Debug.Log($"[LayoutLoader] Cleared {furnitureToRemove.Count} furniture objects");
        }
    }
}
