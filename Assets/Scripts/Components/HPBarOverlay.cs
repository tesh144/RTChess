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
        [SerializeField] private float yOffset = 1.5f; // Above unit
        [SerializeField] private float overlayScale = 1f; // Overall scale of the HP overlay

        [Header("Colors")]
        [SerializeField] private Color fullHealthColor = new Color(0.2f, 0.9f, 0.2f); // Bright green
        [SerializeField] private Color midHealthColor = new Color(0.9f, 0.9f, 0.2f); // Yellow
        [SerializeField] private Color lowHealthColor = new Color(0.9f, 0.2f, 0.2f); // Red
        [SerializeField] private Color backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f); // Dark gray

        [Header("Team Tint")]
        [SerializeField] private Color allyTint = new Color(0.4f, 0.6f, 1f); // Blue
        [SerializeField] private Color enemyTint = new Color(1f, 0.4f, 0.4f); // Red
        [SerializeField] private Color resourceTint = new Color(1f, 0.9f, 0.3f); // Yellow

        [Header("Fade (Units Only)")]
        [SerializeField] private float fadeDelay = 3f; // Seconds before fading starts
        [SerializeField] private float fadeDuration = 1.5f; // Seconds to fully fade out

        // Bar objects
        private GameObject barContainer;
        private GameObject backgroundBar;
        private GameObject fillBar;

        private Renderer fillRenderer;
        private Renderer bgRenderer;
        private IDamageable unit;
        private Color teamTint = Color.white; // Cached tint (white = no tint)

        // Fade state (units only, not resource nodes)
        private bool shouldFade = false;
        private float lastInterestTime;
        private int lastHP;
        private float currentAlpha = 1f;

        // HP number text
        private TextMesh hpTextMesh;
        private TextMesh hpTextShadow;

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

            // Units fade; resource nodes stay visible
            shouldFade = GetComponent<ResourceNode>() == null;
            lastInterestTime = Time.time;
            lastHP = unit.CurrentHP;
        }

        private void Update()
        {
            if (unit == null || barContainer == null) return;

            // Follow unit position in world space
            barContainer.transform.position = transform.position + Vector3.up * yOffset;

            // Detect HP change to re-show bar
            if (shouldFade && unit.CurrentHP != lastHP)
            {
                lastHP = unit.CurrentHP;
                lastInterestTime = Time.time;
                currentAlpha = 1f;
            }

            // Fade logic for units
            if (shouldFade)
            {
                float elapsed = Time.time - lastInterestTime;
                if (elapsed > fadeDelay)
                {
                    float fadeProgress = (elapsed - fadeDelay) / fadeDuration;
                    currentAlpha = Mathf.Clamp01(1f - fadeProgress);
                }
                else
                {
                    currentAlpha = 1f;
                }
            }

            UpdateHPBar();
        }

        /// <summary>
        /// Create the 3D HP bar geometry
        /// </summary>
        private void CreateHPBar()
        {
            // Container (NOT parented to unit to avoid scale inheritance from FBX models)
            barContainer = new GameObject("HPBarContainer");
            barContainer.transform.position = transform.position + Vector3.up * yOffset;

            // Background bar (dark)
            backgroundBar = CreateBar("HPBarBackground", barWidth, barHeight);
            backgroundBar.transform.SetParent(barContainer.transform);
            backgroundBar.transform.localPosition = Vector3.zero;
            backgroundBar.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Face upward

            bgRenderer = backgroundBar.GetComponent<Renderer>();
            if (bgRenderer != null)
            {
                bgRenderer.material.color = backgroundColor;
            }

            // Cache team tint for fill bar coloring
            teamTint = GetTeamTint();

            // Fill bar (colored, scales with HP)
            fillBar = CreateBar("HPBarFill", barWidth, barHeight);
            fillBar.transform.SetParent(barContainer.transform);
            fillBar.transform.localPosition = Vector3.zero;
            fillBar.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Face upward
            fillBar.transform.localScale = new Vector3(1f, 1f, 1f); // Will be scaled based on HP

            fillRenderer = fillBar.GetComponent<Renderer>();

            // Add Billboard component to make bars face camera
            barContainer.AddComponent<Billboard>();

            // Apply overall scale
            barContainer.transform.localScale = Vector3.one * overlayScale;

            // Add HP number text if the owner doesn't already have one (avoids duplicate on resource nodes)
            bool hasExistingHPText = false;
            TextMesh[] existingTexts = GetComponentsInChildren<TextMesh>(true);
            foreach (TextMesh tm in existingTexts)
            {
                if (tm.gameObject.name == "HPText")
                {
                    hasExistingHPText = true;
                    break;
                }
            }

            if (!hasExistingHPText)
            {
                // Shadow text (behind main text)
                GameObject shadowObj = new GameObject("HPOverlayTextShadow");
                shadowObj.transform.SetParent(barContainer.transform);
                shadowObj.transform.localPosition = new Vector3(0.02f, 0.13f, 0.01f);

                hpTextShadow = shadowObj.AddComponent<TextMesh>();
                hpTextShadow.text = "";
                hpTextShadow.characterSize = 0.1f;
                hpTextShadow.fontSize = 48;
                hpTextShadow.anchor = TextAnchor.MiddleCenter;
                hpTextShadow.alignment = TextAlignment.Center;
                hpTextShadow.color = Color.black;
                hpTextShadow.fontStyle = FontStyle.Bold;

                // Main HP number text
                GameObject hpTextObj = new GameObject("HPOverlayText");
                hpTextObj.transform.SetParent(barContainer.transform);
                hpTextObj.transform.localPosition = new Vector3(0f, 0.15f, 0f);

                hpTextMesh = hpTextObj.AddComponent<TextMesh>();
                hpTextMesh.text = "";
                hpTextMesh.characterSize = 0.1f;
                hpTextMesh.fontSize = 48;
                hpTextMesh.anchor = TextAnchor.MiddleCenter;
                hpTextMesh.alignment = TextAlignment.Center;
                hpTextMesh.color = teamTint;
                hpTextMesh.fontStyle = FontStyle.Bold;
            }
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

            // Use Sprites/Default shader (supports alpha for fading)
            Renderer renderer = bar.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Sprites/Default"));
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

            // Apply fade alpha
            targetColor.a = currentAlpha;
            fillRenderer.material.color = targetColor;

            if (bgRenderer != null)
            {
                Color bgColor = backgroundColor;
                bgColor.a = backgroundColor.a * currentAlpha;
                bgRenderer.material.color = bgColor;
            }

            // Update HP number text
            if (hpTextMesh != null)
            {
                string hpStr = unit.CurrentHP.ToString();
                hpTextMesh.text = hpStr;
                Color textColor = teamTint;
                textColor.a = currentAlpha;
                hpTextMesh.color = textColor;
                if (hpTextShadow != null)
                {
                    hpTextShadow.text = hpStr;
                    hpTextShadow.color = new Color(0f, 0f, 0f, currentAlpha);
                }
            }

            // Hide bar if unit is destroyed
            if (unit.IsDestroyed && barContainer != null)
            {
                barContainer.SetActive(false);
            }
        }

        /// <summary>
        /// Get team tint color based on entity type (ally/enemy/resource).
        /// </summary>
        private Color GetTeamTint()
        {
            Unit unitComp = GetComponent<Unit>();
            if (unitComp != null)
            {
                return unitComp.Team == Team.Player ? allyTint : enemyTint;
            }

            if (GetComponent<ResourceNode>() != null)
            {
                return resourceTint;
            }

            return Color.white; // No tint
        }

        private void OnDestroy()
        {
            // Clean up bar objects - hide immediately to avoid 1-frame orphan visibility
            if (barContainer != null)
            {
                barContainer.SetActive(false);
                Destroy(barContainer);
            }
        }
    }
}
