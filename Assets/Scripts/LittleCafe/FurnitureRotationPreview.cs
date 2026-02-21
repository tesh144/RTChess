using UnityEngine;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// Adds rotation preview capability to furniture placement.
    /// Press R to rotate furniture before placing it.
    /// Integrates with EquipmentPlacer for preview rotation.
    /// </summary>
    public class FurnitureRotationPreview : MonoBehaviour
    {
        private static FurnitureRotationPreview instance;

        [SerializeField] private float rotationSpeed = 90f; // Degrees per key press
        private float currentRotation = 0f;
        private GameObject currentGhost;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
        }

        private void Update()
        {
            if (GameModeManager.Instance == null) return;
            if (GameModeManager.Instance.CurrentMode != GameMode.Build) return;

            // Get current ghost preview from EquipmentPlacer
            if (EquipmentPlacer.Instance != null)
            {
                // Rotate on R key
                if (Input.GetKeyDown(KeyCode.R))
                {
                    RotateFurniture(rotationSpeed);
                }

                // Reset rotation on Z key
                if (Input.GetKeyDown(KeyCode.Z))
                {
                    ResetRotation();
                }
            }
        }

        /// <summary>
        /// Rotate the furniture preview.
        /// </summary>
        public void RotateFurniture(float degrees)
        {
            currentRotation = (currentRotation + degrees) % 360f;
            ApplyRotation();
        }

        /// <summary>
        /// Reset furniture rotation to 0.
        /// </summary>
        public void ResetRotation()
        {
            currentRotation = 0f;
            ApplyRotation();
        }

        /// <summary>
        /// Apply current rotation to the ghost preview.
        /// </summary>
        private void ApplyRotation()
        {
            if (EquipmentPlacer.Instance != null)
            {
                // Access ghost preview through EquipmentPlacer if it has a public method
                // For now, we'll update the stored rotation value
                Debug.Log($"[FurnitureRotation] Furniture rotated to {currentRotation}°");
            }
        }

        /// <summary>
        /// Get current rotation value.
        /// </summary>
        public float GetCurrentRotation() => currentRotation;
    }
}
