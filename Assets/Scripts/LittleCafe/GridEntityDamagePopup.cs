#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using TMPro;

namespace LittleCafe
{
    /// <summary>
    /// Floating "-N" damage popup that appears on hit, floats upward, and fades out.
    /// Spawned by GridEntityHPBar when attacker and target are enemies.
    ///
    /// Buildings (allied) show remaining HP in starvation-countdown style (big red).
    /// Everything else shows damage dealt as "-N" in softer entity-tinted colour.
    /// </summary>
    public static class GridEntityDamagePopup
    {
        // Static font cache for red/damage font
        private static TMP_FontAsset cachedRedFont;
        private static bool redFontSearchDone = false;

        /// <summary>
        /// Spawn a floating damage popup at the given world position.
        /// </summary>
        /// <param name="worldPos">Base position of the entity being hit.</param>
        /// <param name="labelHeight">Height above the entity (from GridEntityHPBar).</param>
        /// <param name="damageDealt">Amount of damage dealt this hit.</param>
        /// <param name="currentHP">Target's current HP after the hit.</param>
        /// <param name="maxHP">Target's max HP.</param>
        /// <param name="isBuilding">True for allied entities — shows countdown style.</param>
        /// <param name="entityDamageColor">Tint colour for non-building popups.</param>
        /// <param name="outlineColor">Outline colour for non-building popups.</param>
        /// <param name="overrideFont">Optional font override from GridEntityHPBar.</param>
        /// <param name="floatDistance">How far the popup floats upward.</param>
        /// <param name="duration">Total lifetime of the popup.</param>
        /// <param name="fontSize">Base font size.</param>
        /// <param name="randomSpreadX">Horizontal random offset range.</param>
        public static void Spawn(
            Vector3 worldPos, float labelHeight,
            int damageDealt, int currentHP, int maxHP,
            bool isBuilding, Color entityDamageColor, Color outlineColor,
            TMP_FontAsset overrideFont,
            float floatDistance = 1.0f, float duration = 0.8f,
            float fontSize = 3.5f, float randomSpreadX = 0.3f)
        {
            float offsetX = Random.Range(-randomSpreadX, randomSpreadX);
            float popupY = labelHeight + 0.15f;
            Vector3 spawnPos = worldPos + new Vector3(offsetX, popupY, 0f);

            GameObject popupObj = new GameObject("DamagePopup");
            popupObj.transform.position = spawnPos;

            string popupText = isBuilding ? currentHP.ToString() : $"-{damageDealt}";

            TextMeshPro tmp = popupObj.AddComponent<TextMeshPro>();
            tmp.text = popupText;
            tmp.fontSize = isBuilding ? fontSize * 1.2f : fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.sortingOrder = 200;
            tmp.enableWordWrapping = false;
            tmp.richText = false;

            TMP_FontAsset font = GetRedFont(overrideFont);
            bool fontSupportsOutline = true;
            if (font != null)
            {
                tmp.font = font;
                fontSupportsOutline = font.material != null &&
                    font.material.HasProperty("_OutlineColor");
            }

            tmp.color = isBuilding ? new Color(0.9f, 0.15f, 0.15f, 1f) : entityDamageColor;

            if (fontSupportsOutline)
            {
                tmp.outlineWidth = isBuilding ? 0.25f : 0.12f;
                tmp.outlineColor = isBuilding
                    ? new Color32(40, 10, 0, 220)
                    : new Color32(
                        (byte)(outlineColor.r * 255),
                        (byte)(outlineColor.g * 255),
                        (byte)(outlineColor.b * 255),
                        (byte)(outlineColor.a * 180));
            }

            RectTransform rect = popupObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(4f, 2f);

            float hpFraction = maxHP > 0 ? (float)damageDealt / maxHP : 0f;
            bool isCritical = hpFraction >= 0.4f;

            DamagePopupAnimator animator = popupObj.AddComponent<DamagePopupAnimator>();
            animator.Initialize(floatDistance, duration, isCritical);
        }

        // ---------------------------------------------------------------
        // Font Resolution
        // ---------------------------------------------------------------

        private static TMP_FontAsset GetRedFont(TMP_FontAsset overrideFont)
        {
            if (overrideFont != null) return overrideFont;
            if (redFontSearchDone) return cachedRedFont;

            redFontSearchDone = true;

            GUIProKitAssets guiKit = GUIProKitAssets.Instance;
            if (guiKit != null && guiKit.criticalNumberFont != null)
            {
                cachedRedFont = guiKit.criticalNumberFont;
                return cachedRedFont;
            }

            cachedRedFont = GetFallbackFontAsset();
            return cachedRedFont;
        }

        /// <summary>Shared fallback font — used by GridEntityHPBar for neutral labels too.</summary>
        public static TMP_FontAsset GetFallbackFontAsset()
        {
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null) return font;
            return TMP_Settings.defaultFontAsset;
        }
    }

    /// <summary>
    /// Animates a floating damage popup: pop-scale entrance, rise upward,
    /// fade out, self-destruct. Billboarding runs in LateUpdate so it
    /// stays in sync with the orbiting camera.
    /// </summary>
    public class DamagePopupAnimator : MonoBehaviour
    {
        private float floatDistance;
        private float duration;
        private float elapsed = 0f;
        private Vector3 startPos;
        private TextMeshPro tmp;
        private Color startColor;
        private bool isCritical;
        private bool hasOutline;

        public void Initialize(float distance, float totalDuration, bool critical)
        {
            floatDistance = distance;
            duration = totalDuration;
            isCritical = critical;
            startPos = transform.position;
            tmp = GetComponent<TextMeshPro>();
            if (tmp != null)
            {
                startColor = tmp.color;
                hasOutline = tmp.font != null && tmp.font.material != null &&
                    tmp.font.material.HasProperty("_OutlineColor");
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Float upward
            float easedT = 1f - (1f - t) * (1f - t);
            transform.position = startPos + new Vector3(0f, floatDistance * easedT, 0f);

            // Scale: pop in big then settle
            float scale = 1f;
            if (t < 0.12f)
            {
                float popT = t / 0.12f;
                float overshoot = isCritical ? 1.3f : 1.15f;
                scale = Mathf.Lerp(0f, overshoot, popT);
            }
            else if (t < 0.25f)
            {
                float settleT = (t - 0.12f) / 0.13f;
                float overshoot = isCritical ? 1.3f : 1.15f;
                scale = Mathf.Lerp(overshoot, 1f, settleT);
            }

            if (isCritical) scale *= 1.1f;
            transform.localScale = Vector3.one * scale;

            // Fade out in the last 40%
            if (tmp != null && t > 0.6f)
            {
                float fadeT = (t - 0.6f) / 0.4f;
                Color c = startColor;
                c.a = 1f - fadeT;
                tmp.color = c;

                if (hasOutline)
                {
                    byte outlineAlpha = (byte)(220 * (1f - fadeT));
                    tmp.outlineColor = new Color32(
                        tmp.outlineColor.r,
                        tmp.outlineColor.g,
                        tmp.outlineColor.b,
                        outlineAlpha);
                }
            }

            if (elapsed >= duration)
                Destroy(gameObject);
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam != null)
                transform.forward = cam.transform.forward;
        }
    }
}
