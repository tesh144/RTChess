#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using LittleCafe;

namespace ClockworkGrid
{
    /// <summary>
    /// Simple camera panning with WASD/arrow keys and middle-mouse drag.
    /// Moves along the camera's local right and up axes for intuitive isometric control.
    /// Pan speed scales with revealed tile count so traversal stays comfortable as the map grows.
    /// </summary>
    public class CameraPan : MonoBehaviour
    {
        [SerializeField] private float basePanSpeed = 5f;
        [SerializeField] private float dragSpeed = 0.5f;
        [SerializeField] private float maxPanSpeedMultiplier = 3f;

        private Vector3 lastMousePos;
        private bool isDragging = false;

        private float EffectivePanSpeed
        {
            get
            {
                if (FogManager.Instance == null || GridManager.Instance == null) return basePanSpeed;
                int revealed = FogManager.Instance.GetRevealedCount();
                int total = GridManager.Instance.Width * GridManager.Instance.Height;
                if (total <= 0) return basePanSpeed;
                float ratio = (float)revealed / total;
                float multiplier = 1f + ratio * (maxPanSpeedMultiplier - 1f);
                return basePanSpeed * multiplier;
            }
        }

        private void Update()
        {
            float panSpeed = EffectivePanSpeed;

            // Keyboard panning (WASD / Arrow keys)
            float horizontal = 0f;
            float vertical = 0f;

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) vertical += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) vertical -= 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) horizontal -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) horizontal += 1f;

            if (horizontal != 0f || vertical != 0f)
            {
                Vector3 move = transform.right * horizontal + transform.up * vertical;
                transform.position += move * panSpeed * Time.deltaTime;
            }

            // Middle-mouse drag panning
            if (Input.GetMouseButtonDown(2))
            {
                isDragging = true;
                lastMousePos = Input.mousePosition;
            }
            if (Input.GetMouseButtonUp(2))
            {
                isDragging = false;
            }

            if (isDragging)
            {
                Vector3 delta = Input.mousePosition - lastMousePos;
                Vector3 move = -transform.right * delta.x - transform.up * delta.y;
                transform.position += move * dragSpeed * Time.deltaTime;
                lastMousePos = Input.mousePosition;
            }
        }
    }
}
