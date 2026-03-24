#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// Camera system for the cafe builder. Orbits the grid with smooth movement,
    /// auto-rotation, zoom, pan, shake, drag-follow, and center-of-interest tracking.
    /// Implements ICameraSystem so DragDropHandler works with it via CameraSystemLocator.
    ///
    /// Adaptive zoom: maxDistance and defaultDistance grow proportionally as
    /// the player reveals more tiles. Scroll wheel is free within [minDistance, currentMaxDistance].
    /// After placing an object the camera zooms out to defaultDistance.
    /// </summary>
    public class GridCamera : MonoBehaviour, ICameraSystem
    {
        public static GridCamera Instance { get; private set; }

        [Header("Orbit")]
        [SerializeField] private float yaw = 45f;
        [SerializeField] private float pitch = 35f;

        [Header("Zoom - Fixed Limits")]
        [SerializeField] private float minDistance = 8f;           // Closest zoom (hard cap)
        [SerializeField] private float absoluteMaxDistance = 80f;  // Never zoom out further than this (increased for 120x120 grid)
        [SerializeField] private float zoomSpeed = 3f;
        [SerializeField] private float zoomSmoothing = 8f;

        [Header("Zoom - Adaptive Scaling")]
        [SerializeField] private float baseDefaultDistance = 14f;  // Default zoom with few revealed tiles
        [SerializeField] private float baseMaxDistance = 20f;      // Max zoom with few revealed tiles
        [SerializeField] private float distancePerRevealedTile = 0.06f; // Extra distance per revealed tile (increased for smoother late-game zoom)
        [SerializeField] private float maxDistanceMultiplier = 1.3f;    // Max = default * this multiplier

        [Header("Smoothing")]
        [SerializeField] private float smoothSpeed = 7f;
        [SerializeField] private float orbitSmoothTime = 0.1f;

        [Header("Manual Rotation")]
        [SerializeField] private float manualRotateSpeed = 0.3f;

        [Header("Pan (WASD)")]
        [SerializeField] private float panSpeed = 5f;
        [SerializeField] private float basePanDistance = 5f;              // Pan radius with few revealed tiles
        [SerializeField] private float panDistancePerRevealedTile = 0.05f; // Extra pan radius per revealed tile
        [SerializeField] private float absoluteMaxPanDistance = 80f;      // Never pan further than this

        [Header("Auto Rotation")]
        [SerializeField] private bool autoRotateOnStart = true;
        [SerializeField] private float autoRotateSpeed = 6f;

        [Header("Center of Interest")]
        [SerializeField] private float coiBlendSpeed = 3f;

        [Header("Camera Setup")]
        [SerializeField] private float fieldOfView = 60f;
        [SerializeField] private float nearClip = 0.3f;
        [SerializeField] private float farClip = 1000f;
        [SerializeField] private float distancePadding = 1.5f;

        // Adaptive pan distance (grows with revealed tiles, like zoom)
        private float currentMaxPanDistance;

        // Target state (what we're smoothing toward)
        private Vector3 targetLookPoint;
        private float targetYaw;
        private float targetPitch;
        private float targetDistance;

        // Current smoothed state
        private float currentYaw;
        private float currentPitch;
        private float currentDistance;
        private Vector3 smoothedLookPoint;

        // Grid reference point
        private Vector3 gridCenter;

        // Smoothing velocities
        private float yawVelocity;
        private float pitchVelocity;
        private float distanceVelocity;

        // Pan state
        private bool isPlayerPanning;
        private bool panOverridesCOI;  // Once player pans, stop pulling back to center of interest

        // Manual rotation state
        private bool isRightDragging;
        private Vector3 lastMousePos;
        private float lastDragDeltaX;           // Last frame's horizontal drag delta
        private float autoRotateDirection = 1f; // +1 = clockwise, -1 = counter-clockwise

        // Auto-rotation
        private bool isAutoRotating;

        // Shake
        private float shakeIntensity;
        private float shakeDuration;
        private float shakeTimer;

        // Center of interest
        private Vector3 centerOfInterest;

        // Camera ref
        private Camera cam;

        // Adaptive zoom state
        private float currentDefaultDistance;   // The "standard" zoom level (grows with revealed tiles)
        private float currentMaxDistance;        // The furthest the player can scroll out (grows too)

        // --- ICameraSystem properties ---
        public Vector3 CurrentTarget => targetLookPoint;
        public float CurrentDistance => targetDistance;
        public bool IsAutoRotating => isAutoRotating;

        /// <summary>
        /// The current adaptive default zoom distance.
        /// DragDropHandler uses this to know what to zoom out to after placement.
        /// </summary>
        public float DefaultDistance => currentDefaultDistance;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            CameraSystemLocator.Register(this);

            cam = GetComponent<Camera>();

            // Initialize current = target (no smooth transition on first frame)
            targetYaw = yaw;
            targetPitch = pitch;
            currentYaw = yaw;
            currentPitch = pitch;

            currentDefaultDistance = baseDefaultDistance;
            currentMaxDistance = baseMaxDistance;

            isAutoRotating = autoRotateOnStart;
        }

        private void OnEnable()
        {
            CameraSystemLocator.Register(this);
        }

        private void OnDestroy()
        {
            if (FogManager.Instance != null)
                FogManager.Instance.OnCellRevealed -= OnFogCellRevealed;
            CameraSystemLocator.Unregister(this);
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Compute grid center from actual tile positions and set initial camera state.
        /// Call after GridManager.InitializeGrid().
        /// </summary>
        public void PointAtGrid()
        {
            GridManager gm = GridManager.Instance;
            if (gm == null)
            {
                Debug.LogError("[GridCamera] No GridManager found!");
                return;
            }

            int centerGridX = Mathf.RoundToInt((gm.Width - 1) * 0.5f);
            int centerGridY = Mathf.RoundToInt((gm.Height - 1) * 0.5f);

            gridCenter = gm.GridToWorldPosition(centerGridX, centerGridY);
            gridCenter.y = 0f;

            Debug.Log($"[GridCamera] Grid center: {gridCenter} (grid cell: {centerGridX}, {centerGridY}, cell size: {gm.CellSize})");

            targetLookPoint = gridCenter;
            smoothedLookPoint = gridCenter;
            centerOfInterest = gridCenter;

            // Initial zoom levels
            RecalculateZoomLevels();
            targetDistance = currentDefaultDistance;
            currentDistance = targetDistance;

            // Subscribe to fog reveal events so zoom cap grows as player explores
            if (FogManager.Instance != null)
                FogManager.Instance.OnCellRevealed += OnFogCellRevealed;

            if (cam != null)
            {
                cam.fieldOfView = fieldOfView;
                cam.nearClipPlane = nearClip;
                cam.farClipPlane = farClip;
            }

            ApplyOrbitPosition(smoothedLookPoint, currentYaw, currentPitch, currentDistance);
            Debug.Log($"[GridCamera] Ready: center={gridCenter}, default={currentDefaultDistance:F1}, max={currentMaxDistance:F1}");
        }

        /// <summary>
        /// Called whenever a fog cell is revealed. Updates zoom bounds so the
        /// player's max zoom-out grows progressively as they explore the map.
        /// </summary>
        private void OnFogCellRevealed(int x, int y)
        {
            RecalculateZoomLevels();
        }

        /// <summary>
        /// Recalculate the default and max zoom distances based on revealed tile count.
        /// Called after each placement so the zoom levels grow as the player builds.
        /// </summary>
        public void RecalculateZoomLevels()
        {
            int revealedTiles = 0;
            if (FogManager.Instance != null)
            {
                revealedTiles = FogManager.Instance.GetRevealedCount();
            }

            // Default distance grows with revealed tiles (gentle curve)
            currentDefaultDistance = baseDefaultDistance + revealedTiles * distancePerRevealedTile;
            currentDefaultDistance = Mathf.Clamp(currentDefaultDistance, minDistance, absoluteMaxDistance);

            // Max distance is a multiplier above default
            currentMaxDistance = currentDefaultDistance * maxDistanceMultiplier;
            currentMaxDistance = Mathf.Clamp(currentMaxDistance, baseMaxDistance, absoluteMaxDistance);

            // Ensure max is always >= default
            if (currentMaxDistance < currentDefaultDistance)
                currentMaxDistance = currentDefaultDistance;

            // Pan distance also grows with revealed tiles
            currentMaxPanDistance = basePanDistance + revealedTiles * panDistancePerRevealedTile;
            currentMaxPanDistance = Mathf.Clamp(currentMaxPanDistance, basePanDistance, absoluteMaxPanDistance);

            Debug.Log($"[GridCamera] Zoom recalc: revealed={revealedTiles}, default={currentDefaultDistance:F1}, max={currentMaxDistance:F1}, pan={currentMaxPanDistance:F1}");
        }

        /// <summary>
        /// Smoothly zoom to the current default distance (call after placing an object).
        /// </summary>
        public void ZoomToDefault()
        {
            RecalculateZoomLevels();
            targetDistance = currentDefaultDistance;
        }

        // --- LateUpdate pipeline ---

        private void LateUpdate()
        {
            HandleInput();
            UpdateAutoRotation();
            UpdateCenterOfInterest();
            SmoothApplyOrbit();
            ApplyShake();
            EnforcePanBounds();
        }

        private void HandleInput()
        {
            // While dragging a multi-cell card, scroll wheel and right-click are consumed
            // by DragDropHandler for rotation — skip camera input entirely in that case.
            bool dragHandlesInput = DragDropHandler.Instance != null && DragDropHandler.Instance.IsDragging;

            // Scroll zoom (free within [minDistance, currentMaxDistance])
            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0f && !dragHandlesInput)
            {
                targetDistance -= scroll * zoomSpeed;
                targetDistance = Mathf.Clamp(targetDistance, minDistance, currentMaxDistance);
            }

            // Right-click drag to rotate
            if (Input.GetMouseButtonDown(1) && !dragHandlesInput)
            {
                isRightDragging = true;
                lastMousePos = Input.mousePosition;
            }
            if (Input.GetMouseButtonUp(1))
            {
                isRightDragging = false;
                // Match auto-rotation direction to the player's last swipe
                if (Mathf.Abs(lastDragDeltaX) > 0.01f)
                    autoRotateDirection = Mathf.Sign(lastDragDeltaX);
            }
            if (isRightDragging && !dragHandlesInput)
            {
                Vector3 delta = Input.mousePosition - lastMousePos;
                lastDragDeltaX = delta.x;
                targetYaw += delta.x * manualRotateSpeed;

                float pitchMin = 20f;
                float pitchMax = 70f;
                float edgeZone = 8f;
                float dampFactor = 1f;
                if (targetPitch < pitchMin + edgeZone)
                    dampFactor = Mathf.Clamp01((targetPitch - pitchMin) / edgeZone);
                else if (targetPitch > pitchMax - edgeZone)
                    dampFactor = Mathf.Clamp01((pitchMax - targetPitch) / edgeZone);
                dampFactor = Mathf.Max(dampFactor, 0.1f);

                targetPitch -= delta.y * manualRotateSpeed * dampFactor;
                targetPitch = Mathf.Clamp(targetPitch, pitchMin, pitchMax);
                lastMousePos = Input.mousePosition;
            }

            // WASD pan
            float h = 0f, v = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v -= 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;

            isPlayerPanning = (h != 0f || v != 0f);

            if (isPlayerPanning)
            {
                // Player is actively panning — stop center-of-interest pull
                panOverridesCOI = true;

                Vector3 right = transform.right;
                Vector3 forward = Vector3.Cross(right, Vector3.up).normalized;
                Vector3 panDelta = (right * h + forward * v) * panSpeed * Time.deltaTime;
                targetLookPoint += panDelta;
            }
        }

        private void UpdateAutoRotation()
        {
            if (!isAutoRotating || isRightDragging) return;
            targetYaw += autoRotateDirection * autoRotateSpeed * Time.deltaTime;
            if (targetYaw > 360f) targetYaw -= 360f;
            if (targetYaw < 0f) targetYaw += 360f;
        }

        private void UpdateCenterOfInterest()
        {
            // Once the player has panned with WASD, stop pulling back to center of interest.
            // The override is cleared when FocusOnPosition/FocusOnCell is called (e.g. after placement).
            if (isPlayerPanning || panOverridesCOI) return;

            targetLookPoint = Vector3.Lerp(targetLookPoint, centerOfInterest, Time.deltaTime * coiBlendSpeed);
        }

        private void SmoothApplyOrbit()
        {
            currentYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref yawVelocity, orbitSmoothTime);
            currentPitch = Mathf.SmoothDamp(currentPitch, targetPitch, ref pitchVelocity, orbitSmoothTime);
            currentDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref distanceVelocity, 1f / zoomSmoothing);

            smoothedLookPoint = Vector3.Lerp(smoothedLookPoint, targetLookPoint, Time.deltaTime * smoothSpeed);

            ApplyOrbitPosition(smoothedLookPoint, currentYaw, currentPitch, currentDistance);
        }

        private void ApplyOrbitPosition(Vector3 lookPoint, float yawDeg, float pitchDeg, float dist)
        {
            float yawRad = yawDeg * Mathf.Deg2Rad;
            float pitchRad = pitchDeg * Mathf.Deg2Rad;

            Vector3 offset = new Vector3(
                Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
                Mathf.Sin(pitchRad),
                Mathf.Cos(yawRad) * Mathf.Cos(pitchRad)
            ) * dist;

            transform.position = lookPoint + offset;
            transform.LookAt(lookPoint);
        }

        private void ApplyShake()
        {
            if (shakeTimer <= 0f) return;

            shakeTimer -= Time.deltaTime;
            float t = shakeTimer / shakeDuration;
            float intensity = shakeIntensity * t;

            Vector3 shakeOffset = Random.insideUnitSphere * intensity;
            transform.position += shakeOffset;
        }

        private void EnforcePanBounds()
        {
            Vector3 offset = targetLookPoint - gridCenter;
            offset.y = 0f;
            if (offset.magnitude > currentMaxPanDistance)
            {
                offset = offset.normalized * currentMaxPanDistance;
                targetLookPoint = new Vector3(
                    gridCenter.x + offset.x,
                    targetLookPoint.y,
                    gridCenter.z + offset.z
                );
            }
        }

        // --- ICameraSystem API ---

        public void SetAutoRotate(bool enabled, float speed = -1f)
        {
            isAutoRotating = enabled;
            if (speed >= 0f) autoRotateSpeed = speed;
        }

        public void ZoomTo(float distance)
        {
            targetDistance = Mathf.Clamp(distance, minDistance, currentMaxDistance);
        }

        public void SetTarget(Vector3 worldPos)
        {
            targetLookPoint = worldPos;
        }

        public void FocusOnCell(int gridX, int gridY)
        {
            GridManager gm = GridManager.Instance;
            if (gm == null) return;

            Vector3 pos = gm.GridToWorldPosition(gridX, gridY);
            pos.y = 0f;
            targetLookPoint = pos;
            centerOfInterest = pos;
            panOverridesCOI = false; // Re-anchor to this new focus point
        }

        public void FocusOnPosition(Vector3 worldPos)
        {
            worldPos.y = 0f;
            targetLookPoint = worldPos;
            centerOfInterest = worldPos;
            panOverridesCOI = false; // Re-anchor to this new focus point
        }

        public void Shake(float intensity = 0.15f, float duration = 0.25f)
        {
            shakeIntensity = intensity;
            shakeDuration = duration;
            shakeTimer = duration;
        }

        // --- Additional API ---

        public void SetCenterOfInterest(Vector3 pos)
        {
            centerOfInterest = pos;
        }

        public void ResetView()
        {
            targetLookPoint = gridCenter;
            centerOfInterest = gridCenter;
            panOverridesCOI = false;
            targetYaw = 45f;
            targetPitch = 35f;

            RecalculateZoomLevels();
            targetDistance = currentDefaultDistance;
        }
    }
}
