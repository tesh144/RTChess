#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace ClockworkCraft
{
    /// <summary>
    /// Controls a single currency slot inside the Currency Bar.
    ///
    /// Each slot is a clone of the CurrencyHolder prefab (a TMP text object).
    /// This component adds a child Image for the currency icon (from CurrencyDatabase)
    /// and sets the TMP text to just the numeric amount, tinted to match the resource.
    ///
    /// Icon is resolved directly from CurrencyDatabase sprites — no TMP_SpriteAsset needed.
    /// </summary>
    public class CurrencySlotUI : MonoBehaviour
    {
        private TextMeshProUGUI amountText;
        private Image iconImage;
        private ResourceType resourceType;

        // Icon sizing — match the text's font size so they feel balanced
        private const float ICON_SCALE = 1.5f; // ~30% larger than before to match text prominence

        public ResourceType ResourceType => resourceType;

        /// <summary>
        /// Set up this slot for a specific resource type.
        /// Creates a child Image for the icon, gets the sprite from CurrencyDatabase.
        /// </summary>
        public void Initialize(ResourceType type)
        {
            resourceType = type;

            // Find TMP text component (already on the cloned CurrencyHolder prefab)
            amountText = GetComponent<TextMeshProUGUI>();
            if (amountText == null)
                amountText = GetComponentInChildren<TextMeshProUGUI>();

            if (amountText == null)
            {
                Debug.LogWarning($"[CurrencySlotUI] No TextMeshProUGUI found on {name}");
                return;
            }

            // Disable TMP sprite asset — we use a real Image instead
            amountText.spriteAsset = null;
            amountText.richText = false;

            // Tint text to match resource type
            amountText.color = GetResourceColor(type);

            // Icon size derived from font size
            float iconPx = amountText.fontSize * ICON_SCALE;
            float leftMargin = iconPx + 6f; // icon + small gap

            // Add left margin to make room for the icon
            var rt = amountText.GetComponent<RectTransform>();
            if (rt != null)
            {
                Vector4 margin = amountText.margin;
                margin.x = leftMargin;
                amountText.margin = margin;
            }

            // Create the icon Image as a child of this slot
            CreateIconImage(type, iconPx);

            UpdateAmount(0);
        }

        /// <summary>
        /// Creates a child GameObject with an Image component for the currency icon.
        /// Icon sprite comes from CurrencyDatabase — each currency has its own unique icon.
        /// </summary>
        private void CreateIconImage(ResourceType type, float size)
        {
            // Get icon from CurrencyDatabase via ResourceDisplayUI
            Sprite iconSprite = ResourceDisplayUI.GetIconForResource(type);

            GameObject iconObj = new GameObject($"Icon_{type}");
            iconObj.transform.SetParent(transform, false);

            // Anchor to left side, centered vertically, sized to match text
            RectTransform iconRT = iconObj.AddComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0f, 0.5f);
            iconRT.anchorMax = new Vector2(0f, 0.5f);
            iconRT.pivot = new Vector2(0.5f, 0.5f); // Center pivot for proper vertical centering
            iconRT.anchoredPosition = new Vector2(size * 0.5f + 2f, 0f); // Offset by half-size + margin since pivot is centered
            iconRT.sizeDelta = new Vector2(size, size);

            // Add Image component
            iconImage = iconObj.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            if (iconSprite != null)
            {
                iconImage.sprite = iconSprite;
            }
            else
            {
                // No icon — hide the image, remove the left margin
                iconImage.enabled = false;
                if (amountText != null)
                {
                    Vector4 margin = amountText.margin;
                    margin.x = 0f;
                    amountText.margin = margin;
                }
                Debug.LogWarning($"[CurrencySlotUI] No icon found for {type} in CurrencyDatabase");
            }

            // Ensure icon renders behind text (first sibling)
            iconObj.transform.SetAsFirstSibling();
        }

        /// <summary>
        /// Update the displayed amount (plain number, no inline sprites).
        /// </summary>
        public void UpdateAmount(int amount)
        {
            if (amountText == null) return;
            amountText.text = amount.ToString();
        }

        /// <summary>
        /// Pop-in animation for first appearance. Scale from zero with OutBack ease + fade in.
        /// </summary>
        public void PlayAppearAnimation()
        {
            StartCoroutine(AppearCoroutine());
        }

        private IEnumerator AppearCoroutine()
        {
            float duration = 0.35f;
            CanvasGroup cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

            transform.localScale = Vector3.zero;
            cg.alpha = 0f;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // OutBack ease for scale
                float s = 1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f);
                transform.localScale = Vector3.one * s;

                // Alpha fades in over first 60%
                cg.alpha = Mathf.Clamp01(t / 0.6f);

                yield return null;
            }

            transform.localScale = Vector3.one;
            cg.alpha = 1f;
        }

        /// <summary>
        /// Returns a tint color appropriate for the resource type.
        /// Warm tones for precious resources, earthy for natural ones, etc.
        /// </summary>
        private static Color GetResourceColor(ResourceType type)
        {
            switch (type)
            {
                // Precious / currency
                case ResourceType.Gold:        return new Color(1.00f, 0.84f, 0.25f); // Rich gold
                case ResourceType.GoldBag:     return new Color(1.00f, 0.84f, 0.25f);
                case ResourceType.Gem:         return new Color(0.40f, 0.85f, 1.00f); // Bright cyan
                case ResourceType.Copper:      return new Color(0.85f, 0.55f, 0.30f); // Warm copper
                case ResourceType.Ore:         return new Color(0.65f, 0.65f, 0.70f); // Steel gray
                case ResourceType.Moonstone:   return new Color(0.80f, 0.85f, 1.00f); // Pale blue
                case ResourceType.WhiteMarble: return new Color(0.92f, 0.92f, 0.95f); // Off-white

                // Wood / plant
                case ResourceType.Wood:        return new Color(0.72f, 0.48f, 0.25f); // Warm brown
                case ResourceType.Bark:        return new Color(0.60f, 0.40f, 0.22f); // Dark bark
                case ResourceType.Twig:        return new Color(0.65f, 0.50f, 0.30f); // Light brown
                case ResourceType.Acorn:       return new Color(0.70f, 0.50f, 0.25f); // Acorn tan
                case ResourceType.Leaf:        return new Color(0.35f, 0.75f, 0.30f); // Bright green
                case ResourceType.Grass:       return new Color(0.40f, 0.80f, 0.35f); // Grass green
                case ResourceType.Petal:       return new Color(1.00f, 0.55f, 0.70f); // Pink
                case ResourceType.Flowers:     return new Color(0.90f, 0.50f, 0.65f); // Rose

                // Food
                case ResourceType.Food:        return new Color(0.95f, 0.65f, 0.20f); // Warm orange
                case ResourceType.Carrot:      return new Color(1.00f, 0.60f, 0.15f); // Orange
                case ResourceType.Tomato:      return new Color(0.95f, 0.25f, 0.20f); // Red
                case ResourceType.Pumpkin:     return new Color(0.95f, 0.60f, 0.15f); // Pumpkin
                case ResourceType.Rice:        return new Color(0.95f, 0.92f, 0.80f); // Cream
                case ResourceType.Coconut:     return new Color(0.85f, 0.75f, 0.60f); // Tan
                case ResourceType.Fish:        return new Color(0.45f, 0.70f, 0.90f); // Sea blue
                case ResourceType.Meat:        return new Color(0.85f, 0.35f, 0.30f); // Red meat
                case ResourceType.Meat2:       return new Color(0.80f, 0.30f, 0.25f);
                case ResourceType.Meat3:       return new Color(0.75f, 0.28f, 0.22f);
                case ResourceType.Boar:        return new Color(0.65f, 0.45f, 0.30f); // Dark brown

                // Mineral / earth
                case ResourceType.Stone:       return new Color(0.70f, 0.70f, 0.72f); // Gray
                case ResourceType.Clay:        return new Color(0.80f, 0.55f, 0.35f); // Terracotta
                case ResourceType.Water:       return new Color(0.30f, 0.65f, 0.95f); // Blue

                // Special
                case ResourceType.Approval:    return new Color(0.30f, 0.80f, 0.40f); // Thumbs-up green
                case ResourceType.Heart:       return new Color(1.00f, 0.40f, 0.55f); // Heart pink

                default:                       return new Color(0.85f, 0.85f, 0.90f); // Neutral light
            }
        }
    }
}
