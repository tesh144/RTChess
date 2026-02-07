using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Creates a 3D HP bar overlay above a unit.
    /// Shows health visually with color-coded fill (green → yellow → red).
    /// </summary>
    public class HPBarOverlay : MonoBehaviour
    {
        [Header("HP Bar Settings")]
        [SerializeField] private float barWidth = 0.8f;
        [SerializeField] private float barHeight = 0.1f;
        [SerializeField] private float yOffset = 0.7f; // Above unit

        [Header("Colors")]
        [SerializeField] private Color fullHealthColor = new Color(0.2f, 0.9f, 0.2f); // Bright green
        [SerializeField] private Color midHealthColor = new Color(0.9f, 0.9f, 0.2f); // Yellow
        [SerializeField] private Color lowHealthColor = new Color(0.9f, 0.2f, 0.2f); // Red
        [SerializeField] private Color backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f); // Dark gray

        // Bar objects
        private GameObject barContainer;
        private GameObject backgroundBar;
        private GameObject fillBar;

        private Renderer fillRenderer;
        private IDamageable unit;

        private void Start()
        {
            unit = GetComponent<IDamageable>();
            if (unit == null)
            {
                Debug.LogWarning("HPBarOverlay requires a component implementing IDamageable");
                enabled = false;
                return;
            }

            CreateHPBar();
        }

        private void Update()
        {
            if (unit == null || barContainer == null) return;

            UpdateHPBar();
        }

        /// <summary>
        /// Create the 3D HP bar geometry
        /// </summary>
        private void CreateHPBar()
        {
            // Container (positioned above unit)
            barContainer = new GameObject("HPBarContainer");
            barContainer.transform.SetParent(transform);
            barContainer.transform.localPosition = new Vector3(0f, yOffset, 0f);
            barContainer.transform.localRotation = Quaternion.identity;

            // Background bar (dark)
            backgroundBar = CreateBar("HPBarBackground", barWidth, barHeight);
            backgroundBar.transform.SetParent(barContainer.transform);
            backgroundBar.transform.localPosition = Vector3.zero;
            backgroundBar.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Face upward

            Renderer bgRenderer = backgroundBar.GetComponent<Renderer>();
            if (bgRenderer != null)
            {
                bgRenderer.material.color = backgroundColor;
            }

            // Fill bar (colored, scales with HP)
            fillBar = CreateBar("HPBarFill", barWidth, barHeight);
            fillBar.transform.SetParent(barContainer.transform);
            fillBar.transform.localPosition = Vector3.zero;
            fillBar.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Face upward
            fillBar.transform.localScale = new Vector3(1f, 1f, 1f); // Will be scaled based on HP

            fillRenderer = fillBar.GetComponent<Renderer>();

            // Add Billboard component to make bars face camera
            Billboard billboard = barContainer.AddComponent<Billboard>();
        }

        /// <summary>
        /// Create a quad for the HP bar
        /// </summary>
        private GameObject CreateBar(string name, float width, float height)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bar.name = name;

            // Remove collider (we don't need raycasting on HP bars)
            Collider col = bar.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Scale to desired size
            bar.transform.localScale = new Vector3(width, height, 1f);

            // Use unlit shader for consistent visibility
            Renderer renderer = bar.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Unlit/Color"));
                renderer.material = mat;
            }

            return bar;
        }

        /// <summary>
        /// Update HP bar fill and color based on current health
        /// </summary>
        private void UpdateHPBar()
        {
            if (unit == null || fillBar == null || fillRenderer == null) return;

            // Calculate HP ratio
            float ratio = (float)unit.CurrentHP / unit.MaxHP;
            ratio = Mathf.Clamp01(ratio);

            // Scale fill bar horizontally based on HP ratio
            Vector3 scale = fillBar.transform.localScale;
            scale.x = ratio;
            fillBar.transform.localScale = scale;

            // Offset fill bar to align with left edge
            Vector3 localPos = fillBar.transform.localPosition;
            localPos.x = -barWidth * 0.5f * (1f - ratio); // Offset left as it shrinks
            fillBar.transform.localPosition = localPos;

            // Color-code based on HP ratio
            Color targetColor;
            if (ratio > 0.6f)
            {
                // High HP: Green
                targetColor = fullHealthColor;
            }
            else if (ratio > 0.3f)
            {
                // Mid HP: Interpolate green → yellow
                float t = (ratio - 0.3f) / 0.3f; // 0.3-0.6 → 0-1
                targetColor = Color.Lerp(midHealthColor, fullHealthColor, t);
            }
            else
            {
                // Low HP: Interpolate yellow → red
                float t = ratio / 0.3f; // 0-0.3 → 0-1
                targetColor = Color.Lerp(lowHealthColor, midHealthColor, t);
            }

            fillRenderer.material.color = targetColor;

            // Hide bar if unit is destroyed
            if (unit.IsDestroyed && barContainer != null)
            {
                barContainer.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            // Clean up bar objects
            if (barContainer != null)
            {
                Destroy(barContainer);
            }
        }
    }
}
