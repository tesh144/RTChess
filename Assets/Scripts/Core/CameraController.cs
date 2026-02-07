using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Orbit camera controller that targets the grid center.
    /// Supports zoom (scroll), auto-rotation, smooth transitions,
    /// and public API for gameplay-driven camera effects.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        public static CameraController Instance { get; private set; }

        [Header("Orbit")]
        [Tooltip("Horizontal angle around the target (degrees)")]
        [SerializeField] private float yaw = 45f;
        [Tooltip("Vertical angle above the target (degrees)")]
        [SerializeField] private float pitch = 50f;
        [Tooltip("Distance from the target point")]
        [SerializeField] private float distance = 45f;

        [Header("Zoom")]
        [SerializeField] private float minDistance = 8f;
        [SerializeField] private float maxDistance = 100f;
        [SerializeField] private float zoomSpeed = 5f;
        [SerializeField] private float zoomSmoothing = 8f;

        [Header("Auto Rotation")]
        [SerializeField] private bool autoRotate = false;
        [Tooltip("Degrees per second of slow rotation around the board")]
        [SerializeField] private float autoRotateSpeed = 3f;

        [Header("Manual Rotation")]
        [Tooltip("Hold right-click and drag to rotate")]
        [SerializeField] private float manualRotateSpeed = 0.3f;

        [Header("Pan")]
        [SerializeField] private float panSpeed = 10f;
        [Tooltip("Middle-click drag pan speed")]
        [SerializeField] private float dragPanSpeed = 0.3f;

        [Header("Smoothing")]
        [SerializeField] private float moveSmoothTime = 0.15f;
        [SerializeField] private float orbitSmoothTime = 0.1f;

        // Internal state
        private Vector3 targetPoint;
        private float targetYaw;
        private float targetPitch;
        private float targetDistance;

        // Smoothing velocities
        private float yawVelocity;
        private float pitchVelocity;
        private float distanceVelocity;
        private Vector3 targetPointVelocity;

        // Input state
        private bool isMiddleDragging;
        private bool isRightDragging;
        private Vector3 lastMousePos;

        // Effect state
        private float shakeIntensity;
        private float shakeDuration;
        private float shakeTimer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            // Initialize targets from serialized values
            targetYaw = yaw;
            targetPitch = pitch;
            targetDistance = distance;
        }

        /// <summary>
        /// Call this after GridManager is ready to center the camera on the grid.
        /// </summary>
        public void CenterOnGrid()
        {
            if (GridManager.Instance != null)
            {
                // Grid is centered at origin by design (GridToWorldPosition uses symmetric offsets)
                targetPoint = Vector3.zero;
                Debug.Log("[CameraController] Centered on grid at origin");
            }
            else
            {
                targetPoint = Vector3.zero;
                Debug.LogWarning("[CameraController] No GridManager found, defaulting to origin");
            }

            // Snap immediately on first center (no smooth transition)
            ApplyOrbitPosition(immediate: true);
        }

        private void LateUpdate()
        {
            HandleInput();

            if (autoRotate && !isRightDragging)
            {
                targetYaw += autoRotateSpeed * Time.deltaTime;
            }

            ApplyOrbitPosition(immediate: false);
            ApplyShake();
        }

        private void HandleInput()
        {
            // Scroll wheel zoom
            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0f)
            {
                targetDistance -= scroll * zoomSpeed;
                targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
            }

            // Right-click drag to rotate
            if (Input.GetMouseButtonDown(1))
            {
                isRightDragging = true;
                lastMousePos = Input.mousePosition;
            }
            if (Input.GetMouseButtonUp(1))
            {
                isRightDragging = false;
            }
            if (isRightDragging)
            {
                Vector3 delta = Input.mousePosition - lastMousePos;
                targetYaw += delta.x * manualRotateSpeed;
                targetPitch -= delta.y * manualRotateSpeed;
                targetPitch = Mathf.Clamp(targetPitch, 10f, 85f);
                lastMousePos = Input.mousePosition;
            }

            // Middle-click drag to pan target point
            if (Input.GetMouseButtonDown(2))
            {
                isMiddleDragging = true;
                lastMousePos = Input.mousePosition;
            }
            if (Input.GetMouseButtonUp(2))
            {
                isMiddleDragging = false;
            }
            if (isMiddleDragging)
            {
                Vector3 delta = Input.mousePosition - lastMousePos;
                // Pan along the ground plane relative to camera orientation
                Vector3 right = transform.right;
                Vector3 forward = Vector3.Cross(right, Vector3.up).normalized;
                targetPoint -= (right * delta.x + forward * delta.y) * dragPanSpeed * Time.deltaTime;
                lastMousePos = Input.mousePosition;
            }

            // WASD/arrow key pan
            float h = 0f, v = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v -= 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;

            if (h != 0f || v != 0f)
            {
                Vector3 right = transform.right;
                Vector3 forward = Vector3.Cross(right, Vector3.up).normalized;
                targetPoint += (right * h + forward * v) * panSpeed * Time.deltaTime;
            }
        }

        private void ApplyOrbitPosition(bool immediate)
        {
            if (immediate)
            {
                yaw = targetYaw;
                pitch = targetPitch;
                distance = targetDistance;
            }
            else
            {
                yaw = Mathf.SmoothDamp(yaw, targetYaw, ref yawVelocity, orbitSmoothTime);
                pitch = Mathf.SmoothDamp(pitch, targetPitch, ref pitchVelocity, orbitSmoothTime);
                distance = Mathf.SmoothDamp(distance, targetDistance, ref distanceVelocity, 1f / zoomSmoothing);
            }

            // Convert spherical to cartesian
            float yawRad = yaw * Mathf.Deg2Rad;
            float pitchRad = pitch * Mathf.Deg2Rad;

            Vector3 offset = new Vector3(
                Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
                Mathf.Sin(pitchRad),
                Mathf.Cos(yawRad) * Mathf.Cos(pitchRad)
            ) * distance;

            if (immediate)
            {
                transform.position = targetPoint + offset;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(
                    transform.position,
                    targetPoint + offset,
                    ref targetPointVelocity,
                    moveSmoothTime);
            }

            transform.LookAt(targetPoint);
        }

        private void ApplyShake()
        {
            if (shakeTimer <= 0f) return;

            shakeTimer -= Time.deltaTime;
            float t = shakeTimer / shakeDuration;
            float currentIntensity = shakeIntensity * t; // Fade out

            Vector3 shakeOffset = Random.insideUnitSphere * currentIntensity;
            transform.position += shakeOffset;
        }

        // ─── Public API for gameplay-driven camera effects ───

        /// <summary>
        /// Smoothly move the camera focus to a world position.
        /// </summary>
        public void SetTarget(Vector3 worldPos)
        {
            targetPoint = worldPos;
        }

        /// <summary>
        /// Smoothly move the camera focus to a grid cell.
        /// </summary>
        public void FocusOnCell(int gridX, int gridY)
        {
            if (GridManager.Instance != null)
            {
                targetPoint = GridManager.Instance.GridToWorldPosition(gridX, gridY);
            }
        }

        /// <summary>
        /// Smoothly zoom to a specific distance.
        /// </summary>
        public void ZoomTo(float newDistance)
        {
            targetDistance = Mathf.Clamp(newDistance, minDistance, maxDistance);
        }

        /// <summary>
        /// Quick zoom punch (zoom in then back out) for impact feedback.
        /// </summary>
        public void ZoomPunch(float amount = 2f, float duration = 0.3f)
        {
            // Temporarily reduce distance, then restore
            float original = targetDistance;
            targetDistance = Mathf.Clamp(targetDistance - amount, minDistance, maxDistance);
            // Schedule restore (simple approach using Invoke)
            CancelInvoke(nameof(RestoreZoom));
            _savedDistance = original;
            Invoke(nameof(RestoreZoom), duration);
        }

        private float _savedDistance;
        private void RestoreZoom()
        {
            targetDistance = _savedDistance;
        }

        /// <summary>
        /// Camera shake effect for impacts/destruction.
        /// </summary>
        public void Shake(float intensity = 0.15f, float duration = 0.25f)
        {
            shakeIntensity = intensity;
            shakeDuration = duration;
            shakeTimer = duration;
        }

        /// <summary>
        /// Smoothly rotate to a specific yaw angle.
        /// </summary>
        public void SetYaw(float newYaw)
        {
            targetYaw = newYaw;
        }

        /// <summary>
        /// Enable/disable auto-rotation.
        /// </summary>
        public void SetAutoRotate(bool enabled, float speed = -1f)
        {
            autoRotate = enabled;
            if (speed >= 0f) autoRotateSpeed = speed;
        }

        /// <summary>
        /// Reset camera to default grid-centered view.
        /// </summary>
        public void ResetView()
        {
            targetPoint = Vector3.zero;
            targetYaw = 45f;
            targetPitch = 50f;
            targetDistance = 45f;
        }
    }
}
