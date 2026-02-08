using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace ClockworkGrid
{
    /// <summary>
    /// Displays off-screen indicators for resource nodes.
    /// Shows arrow at screen edge pointing toward resources that are out of view.
    /// </summary>
    public class ResourceIndicatorUI : MonoBehaviour
    {
        public static ResourceIndicatorUI Instance { get; private set; }

        [Header("Indicator Settings")]
        [SerializeField] private GameObject indicatorPrefab; // Created at runtime if null
        [SerializeField] private Sprite resourceTokenIcon; // Set from GameSetup
        [SerializeField] private Transform indicatorContainer; // Parent for all indicators
        [SerializeField] private float edgeMargin = 50f; // Distance from screen edge
        [SerializeField] private float updateInterval = 0.1f; // How often to update (seconds)

        [Header("Visual Settings")]
        [SerializeField] private Vector2 indicatorSize = new Vector2(60f, 60f);
        [SerializeField] private Vector2 iconSize = new Vector2(30f, 30f);
        [SerializeField] private Color arrowColor = new Color(1f, 0.85f, 0.2f); // Gold
        [SerializeField] private Color iconBackgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f); // Dark background

        private Dictionary<ResourceNode, GameObject> activeIndicators = new Dictionary<ResourceNode, GameObject>();
        private List<ResourceNode> trackedResources = new List<ResourceNode>();
        private Camera mainCamera;
        private Canvas canvas;
        private RectTransform canvasRect;
        private float lastUpdateTime;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            mainCamera = Camera.main;
            canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvasRect = canvas.GetComponent<RectTransform>();
            }

            // Create indicator container if not assigned
            if (indicatorContainer == null)
            {
                GameObject containerObj = new GameObject("IndicatorContainer");
                containerObj.transform.SetParent(transform, false);
                RectTransform rect = containerObj.AddComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                indicatorContainer = rect;
            }
        }

        /// <summary>
        /// Set the resource token icon sprite (called from GameSetup).
        /// </summary>
        public void SetResourceTokenIcon(Sprite icon)
        {
            resourceTokenIcon = icon;
        }

        private void Update()
        {
            if (Time.time - lastUpdateTime < updateInterval) return;
            lastUpdateTime = Time.time;

            UpdateTrackedResources();
            UpdateIndicators();
        }

        /// <summary>
        /// Update list of tracked resource nodes from the grid.
        /// </summary>
        private void UpdateTrackedResources()
        {
            trackedResources.Clear();

            if (GridManager.Instance == null) return;

            int width = GridManager.Instance.Width;
            int height = GridManager.Instance.Height;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (GridManager.Instance.GetCellState(x, y) == CellState.Resource)
                    {
                        GameObject cellObj = GridManager.Instance.GetCellOccupant(x, y);
                        if (cellObj != null)
                        {
                            ResourceNode node = cellObj.GetComponent<ResourceNode>();
                            if (node != null && !node.IsDestroyed)
                            {
                                trackedResources.Add(node);
                            }
                        }
                    }
                }
            }

            // Remove indicators for destroyed resources
            List<ResourceNode> toRemove = new List<ResourceNode>();
            foreach (var kvp in activeIndicators)
            {
                if (!trackedResources.Contains(kvp.Key))
                {
                    Destroy(kvp.Value);
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var node in toRemove)
            {
                activeIndicators.Remove(node);
            }
        }

        /// <summary>
        /// Update indicator visibility and position based on resource visibility.
        /// </summary>
        private void UpdateIndicators()
        {
            if (mainCamera == null || canvasRect == null) return;

            foreach (ResourceNode node in trackedResources)
            {
                if (node == null || node.IsDestroyed) continue;

                Vector3 worldPos = node.transform.position;
                bool isVisible = IsResourceVisible(worldPos);

                if (!isVisible)
                {
                    // Show indicator
                    if (!activeIndicators.ContainsKey(node))
                    {
                        activeIndicators[node] = CreateIndicator();
                    }

                    UpdateIndicatorPosition(activeIndicators[node], worldPos);
                }
                else
                {
                    // Hide indicator
                    if (activeIndicators.ContainsKey(node))
                    {
                        Destroy(activeIndicators[node]);
                        activeIndicators.Remove(node);
                    }
                }
            }
        }

        /// <summary>
        /// Check if a resource is visible on screen.
        /// </summary>
        private bool IsResourceVisible(Vector3 worldPos)
        {
            Vector3 viewportPos = mainCamera.WorldToViewportPoint(worldPos);

            // Check if within viewport bounds (with small margin)
            float margin = 0.05f;
            return viewportPos.z > 0 &&
                   viewportPos.x >= -margin && viewportPos.x <= 1f + margin &&
                   viewportPos.y >= -margin && viewportPos.y <= 1f + margin;
        }

        /// <summary>
        /// Create an off-screen indicator UI element.
        /// </summary>
        private GameObject CreateIndicator()
        {
            if (indicatorPrefab != null)
            {
                return Instantiate(indicatorPrefab, indicatorContainer);
            }

            // Create indicator at runtime
            GameObject indicator = new GameObject("ResourceIndicator");
            indicator.transform.SetParent(indicatorContainer, false);

            RectTransform rect = indicator.AddComponent<RectTransform>();
            rect.sizeDelta = indicatorSize;

            // Arrow background
            Image arrowBg = indicator.AddComponent<Image>();
            arrowBg.color = arrowColor;
            arrowBg.sprite = CreateArrowSprite();

            // Icon background circle
            GameObject iconBgObj = new GameObject("IconBackground");
            iconBgObj.transform.SetParent(indicator.transform, false);
            RectTransform iconBgRect = iconBgObj.AddComponent<RectTransform>();
            iconBgRect.sizeDelta = iconSize + Vector2.one * 6f; // Slightly larger than icon
            iconBgRect.anchoredPosition = Vector2.zero;
            Image iconBg = iconBgObj.AddComponent<Image>();
            iconBg.color = iconBackgroundColor;
            iconBg.sprite = CreateCircleSprite();

            // Resource token icon
            if (resourceTokenIcon != null)
            {
                GameObject iconObj = new GameObject("TokenIcon");
                iconObj.transform.SetParent(indicator.transform, false);
                RectTransform iconRect = iconObj.AddComponent<RectTransform>();
                iconRect.sizeDelta = iconSize;
                iconRect.anchoredPosition = Vector2.zero;
                Image icon = iconObj.AddComponent<Image>();
                icon.sprite = resourceTokenIcon;
            }

            return indicator;
        }

        /// <summary>
        /// Update indicator position to point toward the resource.
        /// </summary>
        private void UpdateIndicatorPosition(GameObject indicator, Vector3 worldPos)
        {
            if (indicator == null || mainCamera == null || canvasRect == null) return;

            RectTransform indicatorRect = indicator.GetComponent<RectTransform>();
            if (indicatorRect == null) return;

            // Get viewport position
            Vector3 viewportPos = mainCamera.WorldToViewportPoint(worldPos);

            // Clamp to screen edges with margin
            Vector2 screenPos = new Vector2(
                viewportPos.x * canvasRect.rect.width,
                viewportPos.y * canvasRect.rect.height
            );

            // Calculate direction from screen center to resource
            Vector2 screenCenter = new Vector2(canvasRect.rect.width * 0.5f, canvasRect.rect.height * 0.5f);
            Vector2 direction = (screenPos - screenCenter).normalized;

            // Find intersection with screen edge
            Vector2 edgePos = GetScreenEdgePosition(screenCenter, direction, canvasRect.rect.width, canvasRect.rect.height);

            // Apply edge margin
            Vector2 marginOffset = -direction * edgeMargin;
            edgePos += marginOffset;

            // Convert to anchored position (canvas is anchored at center)
            indicatorRect.anchoredPosition = edgePos - screenCenter;

            // Rotate indicator to point toward resource
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            indicatorRect.rotation = Quaternion.Euler(0, 0, angle - 90f); // -90 because arrow points up by default
        }

        /// <summary>
        /// Get position on screen edge along a direction from center.
        /// </summary>
        private Vector2 GetScreenEdgePosition(Vector2 center, Vector2 direction, float width, float height)
        {
            // Calculate intersection with screen edges
            float halfWidth = width * 0.5f;
            float halfHeight = height * 0.5f;

            // Avoid division by zero
            if (Mathf.Abs(direction.x) < 0.001f) direction.x = 0.001f;
            if (Mathf.Abs(direction.y) < 0.001f) direction.y = 0.001f;

            // Calculate t values for edge intersections
            float tRight = (halfWidth) / direction.x;
            float tLeft = (-halfWidth) / direction.x;
            float tTop = (halfHeight) / direction.y;
            float tBottom = (-halfHeight) / direction.y;

            // Find closest positive t
            float t = float.MaxValue;
            if (tRight > 0 && tRight < t) t = tRight;
            if (tLeft > 0 && tLeft < t) t = tLeft;
            if (tTop > 0 && tTop < t) t = tTop;
            if (tBottom > 0 && tBottom < t) t = tBottom;

            Vector2 edgePos = center + direction * t;

            // Clamp to screen bounds
            edgePos.x = Mathf.Clamp(edgePos.x, 0, width);
            edgePos.y = Mathf.Clamp(edgePos.y, 0, height);

            return edgePos;
        }

        /// <summary>
        /// Create a simple arrow sprite (triangle pointing up).
        /// </summary>
        private Sprite CreateArrowSprite()
        {
            int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];

            // Triangle pointing up
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float centerX = size * 0.5f;
                    float triangleHeight = size * 0.8f;
                    float triangleWidth = size * 0.6f;

                    // Point at top
                    float topY = size * 0.9f;
                    float bottomY = size * 0.1f;

                    // Check if pixel is inside triangle
                    if (y >= bottomY && y <= topY)
                    {
                        float progress = (y - bottomY) / (topY - bottomY);
                        float halfWidth = triangleWidth * 0.5f * (1f - progress);

                        if (Mathf.Abs(x - centerX) <= halfWidth)
                        {
                            pixels[y * size + x] = Color.white;
                        }
                        else
                        {
                            pixels[y * size + x] = Color.clear;
                        }
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// Create a circle sprite for icon background.
        /// </summary>
        private Sprite CreateCircleSprite()
        {
            int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 1;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    pixels[y * size + x] = distance <= radius ? Color.white : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private void OnDestroy()
        {
            // Clean up all active indicators
            foreach (var indicator in activeIndicators.Values)
            {
                if (indicator != null) Destroy(indicator);
            }
            activeIndicators.Clear();
        }
    }
}
