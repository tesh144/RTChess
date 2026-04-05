#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
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

        [Header("Resource Stream VFX")]
        [Tooltip("Duration of the pop-in scale (0→1) at the resource bar before the icon flies. Mirrors the gather burst phase.")]
        [SerializeField] private float streamPopInDuration = 0.15f;
        [Tooltip("Duration of the arc flight from resource bar to building.")]
        [SerializeField] private float streamFlyDuration = 0.55f;
        [SerializeField] private float streamArcHeight = 1.5f;
        [SerializeField] private float streamIconSize = 64f;

        [Header("Fill Bar")]
        [SerializeField] private Color fillBarColor = new Color(0.3f, 0.85f, 0.4f, 1f);
        [SerializeField] private Color fillBarBgColor = new Color(0.15f, 0.15f, 0.2f, 0.6f);
        [SerializeField] private float fillBarWidth = 1.2f;
        [SerializeField] private float fillBarHeight = 0.15f;
        [SerializeField] private float fillBarYOffset = 0.3f;

        [Header("Audio")]
        [SerializeField] private AudioClip chunkSFX;
        [SerializeField] private AudioClip completionSFX;
        [SerializeField] private float basePitch = 0.8f;
        [SerializeField] private float maxPitch = 1.4f;

        // State
        private AudioSource audioSource;
        private Camera mainCamera;
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
            audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            mainCamera = Camera.main;
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

            // Skip legacy fill bar if the bubble system handles it (Insert bubble has its own Fill image)
            if (BuildingProductionManager.Instance != null && BuildingProductionManager.Instance.HasBubblePrefab)
                return;

            // Root with world-space Canvas
            GameObject root = new GameObject($"FillBar_{building.name}");

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 55;

            // Depth-based sorting: closer bubbles render in front
            var depthSorter = root.AddComponent<BubbleDepthSorter>();
            depthSorter.Initialize(50);

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
            if (mainCamera != null)
                root.transform.rotation = mainCamera.transform.rotation;

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
            var bpm = BuildingProductionManager.Instance;
            if (bpm != null && Input.GetMouseButtonDown(0) && !bpm.ClickConsumedThisFrame)
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
            Camera cam = mainCamera;

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

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 100f))
                return;

            GameObject hitObj = hit.collider.gameObject;
            var bpm = BuildingProductionManager.Instance;
            if (bpm == null) return;

            // Resolve the hit (which may be a child collider/mesh or a ground tile) to the root building
            GameObject building = bpm.ResolveHitToBuilding(hitObj, hit.point);
            if (building == null) return;

            if (bpm.HasReadyPopupAt(building))
                return;

            if (!bpm.IsWaitingForHoldFill(building))
                return;

            if (bpm.IsBuildingPaused(building))
                return;

            // Stop any existing hold before starting new one
            StopHold();

            activeBuilding = building;
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

            // Trigger VFX per chunk (Task 5)
            SpawnResourceStream(activeBuilding, info.resourceType);

            var updatedInfo = bpm.GetHoldFillInfo(activeBuilding);
            float fillRatio = updatedInfo.effectiveCost > 0 ? (float)updatedInfo.progress / updatedInfo.effectiveCost : 0f;
            PlayChunkSound(fillRatio);

            // Accelerate
            currentChunkInterval = Mathf.Max(
                minChunkInterval,
                currentChunkInterval * chunkDecayFactor
            );

            if (fillComplete)
            {
                PlayCompletionSound();
                StopHold();
            }
        }

        private void PlayChunkSound(float fillRatio)
        {
            if (chunkSFX == null || audioSource == null) return;
            audioSource.pitch = Mathf.Lerp(basePitch, maxPitch, fillRatio);
            audioSource.PlayOneShot(chunkSFX);
        }

        private void PlayCompletionSound()
        {
            if (completionSFX == null || audioSource == null) return;
            audioSource.pitch = 1.0f;
            audioSource.PlayOneShot(completionSFX);
        }

        public void SpawnResourceStream(GameObject targetBuilding, ResourceType resourceType, System.Action onArrival = null)
        {
            if (targetBuilding == null) return;

            StartCoroutine(StreamParticleCoroutine(targetBuilding, resourceType, onArrival));
        }

        private IEnumerator StreamParticleCoroutine(GameObject targetBuilding, ResourceType resourceType, System.Action onArrival = null)
        {
            // Resolve the canvas used by ResourceLootFX / ResourceDisplayUI.
            // Prefer the canvas that hosts ResourceDisplayUI to avoid grabbing a world-space canvas.
            Canvas canvas = null;
            if (ResourceDisplayUI.Instance != null)
                canvas = ResourceDisplayUI.Instance.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                // Fallback: find a ScreenSpaceOverlay canvas
                foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        canvas = c;
                        break;
                    }
                }
            }
            if (canvas == null) yield break;

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            Camera cam = mainCamera;
            if (cam == null) yield break;

            // ── Determine start position: resource bar slot in canvas-local space ──
            Vector2 startLocal;
            if (ResourceDisplayUI.Instance != null && ResourceDisplayUI.Instance.GetSlotRect(resourceType) != null)
            {
                RectTransform slotRect = ResourceDisplayUI.Instance.GetSlotRect(resourceType);
                Vector3 slotScreenPos = RectTransformUtility.WorldToScreenPoint(
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam,
                    slotRect.position);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, slotScreenPos,
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam,
                    out startLocal);
            }
            else
            {
                // Fallback: top-right corner of canvas
                startLocal = new Vector2(canvasRect.rect.width * 0.4f, canvasRect.rect.height * 0.4f);
            }

            // ── Spawn the icon procedurally (same pattern as ResourceLootFX) ──
            Sprite iconSprite = ResourceDisplayUI.GetIconForResource(resourceType);

            GameObject icon = new GameObject("HoldFillParticle");
            icon.transform.SetParent(canvas.transform, false);

            RectTransform iconRect = icon.AddComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(streamIconSize, streamIconSize);
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = startLocal;
            iconRect.localScale = Vector3.zero;

            Image iconImage = icon.AddComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;
            if (iconSprite != null)
            {
                iconImage.sprite = iconSprite;
                iconImage.color = Color.white;
            }
            else
            {
                // Fallback: simple colored square
                iconImage.color = fillBarColor;
            }

            CanvasGroup cg = icon.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            // Render above other UI
            Canvas particleCanvas = icon.AddComponent<Canvas>();
            particleCanvas.overrideSorting = true;
            particleCanvas.sortingOrder = 100;

            icon.SetActive(true);

            // ── Pop-in at resource bar (mirrors gather's burst phase) ──
            float popElapsed = 0f;
            while (popElapsed < streamPopInDuration)
            {
                if (icon == null) yield break;
                popElapsed += Time.deltaTime;
                float popT = Mathf.Clamp01(popElapsed / streamPopInDuration);
                float popScale = Mathf.SmoothStep(0f, 1f, popT);
                iconRect.localScale = Vector3.one * popScale;
                yield return null;
            }
            iconRect.localScale = Vector3.one;

            // ── Fly from bar to building ──
            float elapsed = 0f;
            while (elapsed < streamFlyDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / streamFlyDuration);

                // Null-check: building may be destroyed mid-flight
                if (icon == null) yield break;
                if (targetBuilding == null)
                {
                    Destroy(icon);
                    yield break;
                }

                // Recompute end position each frame so it tracks moving buildings
                Vector3 buildingScreenPos = cam.WorldToScreenPoint(targetBuilding.transform.position);
                if (buildingScreenPos.z < 0f)
                {
                    Destroy(icon);
                    yield break;
                }
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, buildingScreenPos,
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam,
                    out Vector2 endLocal);

                // Smooth ease-in-out
                float eased = t * t * (3f - 2f * t);

                Vector2 pos = Vector2.Lerp(startLocal, endLocal, eased);
                // Sine arc — peaks in the middle of the flight
                float arc = Mathf.Sin(t * Mathf.PI) * streamArcHeight * 50f;
                pos.y += arc;

                iconRect.anchoredPosition = pos;

                // Scale: shrink to 0.5x in final 20% (mirrors ResourceLootFX gather arrival)
                float scale = t > 0.8f ? Mathf.Lerp(1f, 0.5f, (t - 0.8f) / 0.2f) : 1f;
                iconRect.localScale = Vector3.one * scale;

                // Fade out in final 20%
                cg.alpha = t < 0.8f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.8f) / 0.2f);

                yield return null;
            }

            if (icon != null)
                Destroy(icon);

            onArrival?.Invoke();
        }

        public void StopHold()
        {
            if (audioSource != null) audioSource.pitch = 1.0f;
            activeBuilding = null;
            chunksThisSession = 0;
        }

        /// <summary>Start a hold-to-fill on a building directly (called from bubble OnHoldStarted).</summary>
        public void StartHoldOnBuilding(GameObject building)
        {
            if (building == null) return;
            var bpm = BuildingProductionManager.Instance;
            if (bpm == null) return;
            if (!bpm.IsWaitingForHoldFill(building)) return;
            if (bpm.IsBuildingPaused(building)) return;

            StopHold();
            activeBuilding = building;
            chunksThisSession = 0;
            currentChunkInterval = baseChunkInterval;
            chunkTimer = 0f;
        }

        public void InterruptIfActive(GameObject building)
        {
            if (activeBuilding == building)
                StopHold();
        }
    }
}
