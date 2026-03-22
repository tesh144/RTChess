#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;

namespace LittleCafe
{
    /// <summary>
    /// Handles visual degradation of the Feast (meat) based on current HP.
    /// As HP decreases, the mesh renderer color fades from vibrant to nearly invisible.
    /// When HP reaches 0, the Feast is destroyed with particle effects.
    /// </summary>
    public class FeastVisualDegradation : MonoBehaviour
    {
        [Header("Visual Settings")]
        [Tooltip("Maximum HP when the Feast is first placed (defines full-color state)")]
        public int maxHP = 5;

        [Tooltip("Degradation stages: Color intensity at each HP threshold")]
        [SerializeField] private float[] degradationStages = { 1.0f, 0.7f, 0.4f, 0.1f };
        // Stage mapping: HP 5 → 1.0, HP 3-4 → 0.7, HP 1-2 → 0.4, HP 0 → destroyed

        [Tooltip("Base color of the meat (vibrant/fresh)")]
        [SerializeField] private Color baseColor = new Color(0.8f, 0.2f, 0.1f, 1f); // reddish meat color

        [Tooltip("Desaturation amount for faded stages (0 = no color, 1 = full color)")]
        [SerializeField] private float desaturationAmount = 0.6f;

        private MeshRenderer meshRenderer;
        private Material meshMaterial;
        private GridEntityHealth healthComponent;
        private int currentHP;

        private void Start()
        {
            // Get components
            meshRenderer = GetComponentInChildren<MeshRenderer>();
            healthComponent = GetComponent<GridEntityHealth>();

            if (meshRenderer == null)
            {
                Debug.LogWarning($"[FeastVisualDegradation] No MeshRenderer found on {gameObject.name}");
                return;
            }

            // Create unique material instance for this feast
            meshMaterial = meshRenderer.material;

            // Initialize appearance based on current HP
            if (healthComponent != null)
                currentHP = healthComponent.CurrentHP;
            else
                currentHP = maxHP;

            UpdateVisualState();
        }

        private void Update()
        {
            // Check if HP has changed
            if (healthComponent != null && healthComponent.CurrentHP != currentHP)
            {
                currentHP = healthComponent.CurrentHP;
                UpdateVisualState();
            }
        }

        /// <summary>
        /// Update the meat color based on current HP.
        /// Uses degradation stages to create discrete visual transitions.
        /// </summary>
        private void UpdateVisualState()
        {
            if (meshMaterial == null) return;

            // Determine color intensity based on HP
            float colorIntensity = GetColorIntensity(currentHP);

            // Create degraded color by desaturating the base color
            Color degradedColor = Color.Lerp(GetGrayScale(baseColor), baseColor, colorIntensity);
            degradedColor.a = 1f; // Keep alpha at 1

            // Apply to material
            meshMaterial.color = degradedColor;

            if (currentHP <= 0)
            {
                // Trigger destruction effect (handled by GridEntityHealth)
                // This script just manages visuals up to destruction
                meshMaterial.color = new Color(0.1f, 0.1f, 0.1f, 0.1f); // Very faded before destroy
            }
        }

        /// <summary>
        /// Map current HP to a color intensity value based on degradation stages.
        /// </summary>
        private float GetColorIntensity(int hp)
        {
            if (hp >= maxHP) return degradationStages[0]; // Full color
            if (hp >= Mathf.CeilToInt(maxHP * 0.6f)) return degradationStages[1]; // Slightly faded
            if (hp >= Mathf.CeilToInt(maxHP * 0.2f)) return degradationStages[2]; // Very faded
            return degradationStages[3]; // Nearly invisible
        }

        /// <summary>
        /// Convert color to grayscale for desaturation effect.
        /// </summary>
        private Color GetGrayScale(Color color)
        {
            float gray = (color.r + color.g + color.b) / 3f;
            return new Color(gray, gray, gray, color.a);
        }
    }
}
