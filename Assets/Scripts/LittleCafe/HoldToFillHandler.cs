#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using ClockworkGrid;
using ClockworkCraft;

namespace LittleCafe
{
    public class HoldToFillHandler : MonoBehaviour
    {
        public static HoldToFillHandler Instance { get; private set; }

        [Header("Drain Timing")]
        [SerializeField] private float baseChunkInterval = 0.5f;
        [SerializeField] private float chunkDecayFactor = 0.85f;
        [SerializeField] private float minChunkInterval = 0.08f;

        [Header("Fill Bar")]
        [SerializeField] private Color fillBarColor = new Color(0.3f, 0.85f, 0.4f, 1f);
        [SerializeField] private Color fillBarBgColor = new Color(0.15f, 0.15f, 0.2f, 0.6f);
        [SerializeField] private float fillBarWidth = 1.2f;
        [SerializeField] private float fillBarHeight = 0.15f;
        [SerializeField] private float fillBarYOffset = -0.3f;

        // State
        private GameObject activeBuilding;
        private float chunkTimer;
        private float currentChunkInterval;
        private int chunksThisSession;

        // Fill bar UI tracking
        private Dictionary<GameObject, GameObject> fillBarCanvases = new Dictionary<GameObject, GameObject>();
        private Dictionary<GameObject, Image> fillBarImages = new Dictionary<GameObject, Image>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            var bpm = BuildingProductionManager.Instance;
            if (bpm != null)
                bpm.OnHoldFillStateChanged += OnHoldFillStateChanged;
        }

        private void OnDisable()
        {
            var bpm = BuildingProductionManager.Instance;
            if (bpm != null)
                bpm.OnHoldFillStateChanged -= OnHoldFillStateChanged;
        }

        private void OnHoldFillStateChanged(GameObject building, bool active)
        {
            if (active)
                CreateFillBar(building);
            else
                DestroyFillBar(building);
        }

        private void CreateFillBar(GameObject building)
        {
            if (building == null) return;
            // Avoid duplicates
            if (fillBarCanvases.ContainsKey(building)) return;

            // Root with world-space Canvas
            GameObject root = new GameObject($"FillBar_{building.name}");

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 55;

            // Disable raycasting — we handle input ourselves
            GraphicRaycaster gr = root.AddComponent<GraphicRaycaster>();
            gr.enabled = false;

            // Canvas rect size in canvas units; scale maps to world metres
            float canvasUnits = 100f;
            RectTransform canvasRect = root.GetComponent<RectTransform>();
            float worldScale = fillBarWidth / canvasUnits;
            canvasRect.sizeDelta = new Vector2(canvasUnits, canvasUnits * (fillBarHeight / fillBarWidth));
            canvasRect.localScale = Vector3.one * worldScale;

            // Background image (full bar, dark)
            GameObject bgObj = new GameObject("Background");
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.SetParent(canvasRect, false);
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = fillBarBgColor;
            bgImage.raycastTarget = false;

            // Foreground image (filled bar, green)
            GameObject fillObj = new GameObject("Fill");
            RectTransform fillRect = fillObj.AddComponent<RectTransform>();
            fillRect.SetParent(canvasRect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;

            Image fillImage = fillObj.AddComponent<Image>();
            fillImage.color = fillBarColor;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 0f;
            fillImage.raycastTarget = false;

            // Position at building base + y offset
            root.transform.position = building.transform.position + Vector3.up * fillBarYOffset;

            // Billboard toward camera on creation
            if (Camera.main != null)
                root.transform.rotation = Camera.main.transform.rotation;

            fillBarCanvases[building] = root;
            fillBarImages[building] = fillImage;
        }

        private void DestroyFillBar(GameObject building)
        {
            if (fillBarCanvases.TryGetValue(building, out GameObject canvas))
            {
                if (canvas != null) Destroy(canvas);
                fillBarCanvases.Remove(building);
            }
            fillBarImages.Remove(building);
        }

        private void LateUpdate()
        {
            // Update fill bars: position, billboard, fill amount
            UpdateFillBars();

            // LateUpdate ensures HandlePopupTap (in Update) runs first and consumes clicks.
            if (Input.GetMouseButtonDown(0) && !BuildingProductionManager.Instance.ClickConsumedThisFrame)
            {
                TryStartHold();
            }

            if (Input.GetMouseButton(0) && activeBuilding != null)
            {
                UpdateHold();
            }

            if (Input.GetMouseButtonUp(0))
            {
                StopHold();
            }
        }

        private void UpdateFillBars()
        {
            if (fillBarImages.Count == 0) return;

            var bpm = BuildingProductionManager.Instance;
            Camera cam = Camera.main;

            // Collect null keys to remove after iteration
            List<GameObject> toRemove = null;

            foreach (var kvp in fillBarImages)
            {
                GameObject building = kvp.Key;
                Image fillImage = kvp.Value;

                // Clean up destroyed buildings
                if (building == null || fillImage == null)
                {
                    if (toRemove == null) toRemove = new List<GameObject>();
                    toRemove.Add(building);
                    continue;
                }

                // Update fill amount
                if (bpm != null)
                {
                    var info = bpm.GetHoldFillInfo(building);
                    if (info.effectiveCost > 0)
                        fillImage.fillAmount = info.progress / (float)info.effectiveCost;
                }

                // Update canvas position and billboard
                if (fillBarCanvases.TryGetValue(building, out GameObject canvasObj) && canvasObj != null)
                {
                    canvasObj.transform.position = building.transform.position + Vector3.up * fillBarYOffset;
                    if (cam != null)
                        canvasObj.transform.rotation = cam.transform.rotation;
                }
            }

            // Remove stale entries
            if (toRemove != null)
            {
                foreach (var key in toRemove)
                {
                    if (fillBarCanvases.TryGetValue(key, out GameObject canvas))
                    {
                        if (canvas != null) Destroy(canvas);
                        fillBarCanvases.Remove(key);
                    }
                    fillBarImages.Remove(key);
                }
            }
        }

        private void TryStartHold()
        {
            // Input priority: don't activate if dragging
            if (DragDropHandler.Instance != null && DragDropHandler.Instance.IsDragging)
                return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 100f))
                return;

            GameObject hitObj = hit.collider.gameObject;
            var bpm = BuildingProductionManager.Instance;
            if (bpm == null) return;

            if (bpm.HasReadyPopupAt(hitObj))
                return;

            if (!bpm.IsWaitingForHoldFill(hitObj))
                return;

            if (bpm.IsBuildingPaused(hitObj))
                return;

            // Stop any existing hold before starting new one
            StopHold();

            activeBuilding = hitObj;
            chunksThisSession = 0;
            currentChunkInterval = baseChunkInterval;
            chunkTimer = 0f;
        }

        private void UpdateHold()
        {
            if (activeBuilding == null) return;

            var bpm = BuildingProductionManager.Instance;
            if (bpm == null || !bpm.IsWaitingForHoldFill(activeBuilding))
            {
                StopHold();
                return;
            }

            chunkTimer += Time.deltaTime;
            if (chunkTimer >= currentChunkInterval)
            {
                chunkTimer -= currentChunkInterval;
                TryDrainChunk();
            }
        }

        private void TryDrainChunk()
        {
            var bpm = BuildingProductionManager.Instance;
            var info = bpm.GetHoldFillInfo(activeBuilding);

            // Check if player can afford 1 unit
            var rm = ResourceManager.Instance;
            if (rm == null || rm.GetResource(info.resourceType) < 1)
                return; // Pause — no resources, but don't stop hold

            // Spend 1 resource
            rm.SpendResources(new Dictionary<ResourceType, int>
            {
                { info.resourceType, 1 }
            });

            // Increment fill
            bool fillComplete = bpm.IncrementHoldFill(activeBuilding);

            chunksThisSession++;

            // TODO Task 5: trigger VFX per chunk
            // TODO Task 6: play chunk SFX

            // Accelerate
            currentChunkInterval = Mathf.Max(
                minChunkInterval,
                currentChunkInterval * chunkDecayFactor
            );

            if (fillComplete)
            {
                // TODO Task 6: play completion SFX
                StopHold();
            }
        }

        private void StopHold()
        {
            activeBuilding = null;
            chunksThisSession = 0;
        }

        public void InterruptIfActive(GameObject building)
        {
            if (activeBuilding == building)
                StopHold();
        }
    }
}
