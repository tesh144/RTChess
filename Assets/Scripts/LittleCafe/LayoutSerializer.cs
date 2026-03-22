#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System.Collections.Generic;
using System.Text;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// Serializes placed furniture layouts to/from JSON strings.
    /// Enables saving/loading cafe layouts as copyable codes.
    /// </summary>
    public static class LayoutSerializer
    {
        /// <summary>
        /// Serialize all placed furniture to a JSON string.
        /// </summary>
        public static string SerializeLayout()
        {
            GridManager gm = GridManager.Instance;
            if (gm == null)
            {
                Debug.LogError("[LayoutSerializer] GridManager not found!");
                return null;
            }

            List<SerializedFurniture> furnitureList = new List<SerializedFurniture>();

            // Scan all grid cells for furniture
            HashSet<GameObject> processedObjects = new HashSet<GameObject>();

            for (int x = 0; x < gm.Width; x++)
            {
                for (int y = 0; y < gm.Height; y++)
                {
                    GameObject occupant = gm.GetCellOccupant(x, y);
                    if (occupant == null || processedObjects.Contains(occupant))
                        continue;

                    FurnitureObject furniture = occupant.GetComponent<FurnitureObject>();
                    if (furniture != null)
                    {
                        processedObjects.Add(occupant);

                        SerializedFurniture data = new SerializedFurniture
                        {
                            assetName = furniture.name.Replace("(Clone)", "").Trim(),
                            gridX = furniture.GridX,
                            gridY = furniture.GridY,
                            rotation = furniture.transform.eulerAngles.y, // Y rotation only
                            type = furniture.Type.ToString()
                        };

                        furnitureList.Add(data);
                    }
                }
            }

            LayoutData layout = new LayoutData
            {
                furniture = furnitureList.ToArray(),
                gridSize = new Vector2Int(gm.Width, gm.Height)
            };

            string json = JsonUtility.ToJson(layout, true);
            Debug.Log($"[LayoutSerializer] Serialized {furnitureList.Count} furniture objects");
            return json;
        }

        /// <summary>
        /// Deserialize JSON string to LayoutData.
        /// </summary>
        public static LayoutData DeserializeLayout(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError("[LayoutSerializer] JSON string is empty!");
                return null;
            }

            try
            {
                LayoutData layout = JsonUtility.FromJson<LayoutData>(json);
                Debug.Log($"[LayoutSerializer] Deserialized {layout.furniture.Length} furniture objects");
                return layout;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LayoutSerializer] Failed to deserialize JSON: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Compress JSON to a shorter "code" format (for easier sharing).
        /// </summary>
        public static string CompressToCode(string json)
        {
            // Simple Base64 encoding for now
            // TODO: Could use actual compression (gzip) for larger layouts
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            return System.Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// Decompress code back to JSON.
        /// </summary>
        public static string DecompressFromCode(string code)
        {
            try
            {
                byte[] bytes = System.Convert.FromBase64String(code);
                return Encoding.UTF8.GetString(bytes);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LayoutSerializer] Failed to decompress code: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Copy layout code to clipboard.
        /// </summary>
        public static void CopyToClipboard(string code)
        {
            GUIUtility.systemCopyBuffer = code;
            Debug.Log("[LayoutSerializer] Layout code copied to clipboard!");
        }

        /// <summary>
        /// Get layout code from clipboard.
        /// </summary>
        public static string GetFromClipboard()
        {
            return GUIUtility.systemCopyBuffer;
        }
    }

    /// <summary>
    /// Serialized furniture data.
    /// </summary>
    [System.Serializable]
    public class SerializedFurniture
    {
        public string assetName;
        public int gridX;
        public int gridY;
        public float rotation; // Y-axis rotation only
        public string type; // FurnitureType as string
    }

    /// <summary>
    /// Complete layout data (all placed furniture).
    /// </summary>
    [System.Serializable]
    public class LayoutData
    {
        public SerializedFurniture[] furniture;
        public Vector2Int gridSize;
    }
}
