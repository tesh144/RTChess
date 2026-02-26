using UnityEngine;
using TMPro;

namespace LittleCafe
{
    /// <summary>
    /// Two-part HP display system for grid entities:
    ///
    /// 1. PERSISTENT HP LABEL — Shows current HP above the entity.
    ///    Fades in when hit, stays visible while taking damage, then fades out
    ///    5 seconds after the last interaction. Color shifts white→red as HP drops.
    ///
    /// 2. QUICK DAMAGE NUMBER — A "-N" popup that appears on each hit,
    ///    floats upward, and vanishes quickly (~0.8s).
    ///
    /// Billboarding runs in LateUpdate so it stays in sync with the orbiting camera.
    /// </summary>
    public class GridEntityHPBar : MonoBehaviour
    {
        [Header("Persistent HP Label")]
        [SerializeField] private float labelHeight = 2.0f;
        [SerializeField] private float labelFontSize = 5f;
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float lingerDuration = 5.0f;
        [SerializeField] private float fadeOutDuration = 1.0f;

        [Header("Damage Popup")]
        [SerializeField] private float damageHeight = 2.2f;
        [SerializeField] private float damageFloatDistance = 1.2f;
        [SerializeField] private float damageDuration = 0.8f;
        [SerializeField] private float damageFontSize = 5f;
        [SerializeField] private float randomSpreadX = 0.4f;

        [Header("Colors")]
        [SerializeField] private Color healthyColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color lowHPColor = new Color(1f, 0.3f, 0.2f, 1f);
        [SerializeField] private Color damageNumberColor = new Color(1f, 0.25f, 0.15f, 1f);
        [SerializeField] private Color outlineColor = new Color(0.15f, 0.05f, 0f, 1f);

        [Header("Font Override")]
        [SerializeField] private TMP_FontAsset overrideFont;

        // Internal — persistent label
        private GridEntityHealth entityHealth;
        private GameObject labelObj;
        private TextMeshPro labelTMP;
        private float lastHitTime = -100f;
        private float currentAlpha = 0f;
        private float actualLabelHeight; // Calculated from model bounds
        private enum LabelState { Hidden, FadingIn, Visible, FadingOut }
        private LabelState labelState = LabelState.Hidden;

        // Static font cache
        private static TMP_FontAsset cachedFont;
        private static bool fontSearchDone = false;

        // ---------------------------------------------------------------
        // Setup
        // ---------------------------------------------------------------

        public void Initialize(GridEntityHealth health)
        {
            entityHealth = health;
            entityHealth.OnDamaged += OnEntityDamaged;

            // Calculate label height from actual model bounds so the label
            // always appears above tall objects like trees, not inside them.
            actualLabelHeight = CalculateLabelHeight();

            CreatePersistentLabel();
        }

        /// <summary>
        /// Measure the entity's actual rendered height and place the label above it.
        /// Falls back to the serialized labelHeight if no renderers are found.
        /// </summary>
        private float CalculateLabelHeight()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return labelHeight;

            // Combine all renderer bounds to get total bounding box
            Bounds combinedBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                combinedBounds.Encapsulate(renderers[i].bounds);
            }

            // Label goes above the top of the model + small padding
            float topY = combinedBounds.max.y - transform.position.y;
            float calculatedHeight = topY + 0.4f; // 0.4 units above the model top

            // Use whichever is higher — the calculated value or the default
            return Mathf.Max(calculatedHeight, labelHeight);
        }

        private void CreatePersistentLabel()
        {
            // Don't parent to the entity — position in world space via LateUpdate.
            // This avoids issues with entity scale/rotation affecting the label,
            // and keeps the label above tall objects like trees.
            labelObj = new GameObject("HPLabel");
            labelObj.transform.position = transform.position + new Vector3(0f, actualLabelHeight, 0f);

            labelTMP = labelObj.AddComponent<TextMeshPro>();
            labelTMP.fontSize = labelFontSize;
            labelTMP.alignment = TextAlignmentOptions.Center;
            labelTMP.sortingOrder = 150;
            labelTMP.enableWordWrapping = false;

            TMP_FontAsset font = GetFont();
            if (font != null)
                labelTMP.font = font;

            labelTMP.outlineWidth = 0.2f;
            labelTMP.outlineColor = new Color32(
                (byte)(outlineColor.r * 255),
                (byte)(outlineColor.g * 255),
                (byte)(outlineColor.b * 255),
                200);

            RectTransform rect = labelTMP.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(5f, 2f);

            SetLabelAlpha(0f);
            labelObj.SetActive(false);
        }

        // ---------------------------------------------------------------
        // Damage Callback
        // ---------------------------------------------------------------

        private void OnEntityDamaged(int damageDealt, int currentHP, int maxHP)
        {
            UpdateHPLabel(currentHP, maxHP);
            SpawnDamagePopup(damageDealt, currentHP, maxHP);
        }

        // ---------------------------------------------------------------
        // Persistent HP Label
        // ---------------------------------------------------------------

        private void UpdateHPLabel(int currentHP, int maxHP)
        {
            if (labelTMP == null) return;

            lastHitTime = Time.time;

            // Show only current HP
            labelTMP.text = currentHP.ToString();

            float hpFraction = maxHP > 0 ? (float)currentHP / maxHP : 0f;
            Color textColor = Color.Lerp(lowHPColor, healthyColor, hpFraction);
            labelTMP.color = new Color(textColor.r, textColor.g, textColor.b, currentAlpha);

            if (labelState == LabelState.Hidden || labelState == LabelState.FadingOut)
            {
                labelObj.SetActive(true);
                labelState = LabelState.FadingIn;
            }
        }

        private void SetLabelAlpha(float alpha)
        {
            currentAlpha = alpha;
            if (labelTMP == null) return;

            Color c = labelTMP.color;
            c.a = alpha;
            labelTMP.color = c;

            Color32 oc = labelTMP.outlineColor;
            oc.a = (byte)(200 * alpha);
            labelTMP.outlineColor = oc;
        }

        /// <summary>
        /// All billboarding + fade state runs in LateUpdate, AFTER the camera
        /// has finalized its position. This prevents the one-frame-behind jitter.
        /// </summary>
        private void LateUpdate()
        {
            // Position + billboard the persistent label
            if (labelObj != null && labelObj.activeSelf)
            {
                // Track entity position in world space
                labelObj.transform.position = transform.position + new Vector3(0f, actualLabelHeight, 0f);

                Camera cam = Camera.main;
                if (cam != null)
                    labelObj.transform.forward = cam.transform.forward;
            }

            // Fade state machine
            switch (labelState)
            {
                case LabelState.FadingIn:
                    currentAlpha += Time.deltaTime / fadeInDuration;
                    if (currentAlpha >= 1f)
                    {
                        currentAlpha = 1f;
                        labelState = LabelState.Visible;
                    }
                    SetLabelAlpha(currentAlpha);
                    break;

                case LabelState.Visible:
                    if (Time.time - lastHitTime > lingerDuration)
                        labelState = LabelState.FadingOut;
                    break;

                case LabelState.FadingOut:
                    currentAlpha -= Time.deltaTime / fadeOutDuration;
                    if (currentAlpha <= 0f)
                    {
                        currentAlpha = 0f;
                        labelState = LabelState.Hidden;
                        labelObj.SetActive(false);
                    }
                    SetLabelAlpha(currentAlpha);
                    break;
            }
        }

        // ---------------------------------------------------------------
        // Quick Damage Popup (-N)
        // ---------------------------------------------------------------

        private void SpawnDamagePopup(int damageDealt, int currentHP, int maxHP)
        {
            float offsetX = Random.Range(-randomSpreadX, randomSpreadX);
            // Use actual label height + small offset so damage numbers appear above the model
            float popupY = Mathf.Max(actualLabelHeight + 0.2f, damageHeight);
            Vector3 spawnPos = transform.position + new Vector3(offsetX, popupY, 0f);

            GameObject popupObj = new GameObject("DamagePopup");
            popupObj.transform.position = spawnPos;

            TextMeshPro tmp = popupObj.AddComponent<TextMeshPro>();
            tmp.text = $"-{damageDealt}";
            tmp.fontSize = damageFontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.sortingOrder = 200;
            tmp.enableWordWrapping = false;

            TMP_FontAsset font = GetFont();
            if (font != null)
                tmp.font = font;

            tmp.color = damageNumberColor;

            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = new Color32(
                (byte)(outlineColor.r * 255),
                (byte)(outlineColor.g * 255),
                (byte)(outlineColor.b * 255),
                220);

            RectTransform rect = popupObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(4f, 2f);

            float hpFraction = maxHP > 0 ? (float)damageDealt / maxHP : 0f;
            bool isCritical = hpFraction >= 0.4f;

            DamagePopupAnimator animator = popupObj.AddComponent<DamagePopupAnimator>();
            animator.Initialize(damageFloatDistance, damageDuration, isCritical);
        }

        // ---------------------------------------------------------------
        // Font Resolution
        // ---------------------------------------------------------------

        private TMP_FontAsset GetFont()
        {
            if (overrideFont != null) return overrideFont;
            if (fontSearchDone) return cachedFont;

            fontSearchDone = true;

            // TMP's built-in default font (in TextMesh Pro/Resources/)
            cachedFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (cachedFont != null) return cachedFont;

            // TMP_Settings fallback
            if (TMP_Settings.defaultFontAsset != null)
            {
                cachedFont = TMP_Settings.defaultFontAsset;
                return cachedFont;
            }

            return null;
        }

        // ---------------------------------------------------------------
        // Cleanup
        // ---------------------------------------------------------------

        private void OnDestroy()
        {
            if (entityHealth != null)
                entityHealth.OnDamaged -= OnEntityDamaged;

            if (labelObj != null)
                Destroy(labelObj);
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

        public void Initialize(float distance, float totalDuration, bool critical)
        {
            floatDistance = distance;
            duration = totalDuration;
            isCritical = critical;
            startPos = transform.position;
            tmp = GetComponent<TextMeshPro>();
            if (tmp != null)
                startColor = tmp.color;
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
                float overshoot = isCritical ? 1.8f : 1.5f;
                scale = Mathf.Lerp(0f, overshoot, popT);
            }
            else if (t < 0.25f)
            {
                float settleT = (t - 0.12f) / 0.13f;
                float overshoot = isCritical ? 1.8f : 1.5f;
                scale = Mathf.Lerp(overshoot, 1f, settleT);
            }

            if (isCritical) scale *= 1.3f;
            transform.localScale = Vector3.one * scale;

            // Fade out in the last 40%
            if (tmp != null && t > 0.6f)
            {
                float fadeT = (t - 0.6f) / 0.4f;
                Color c = startColor;
                c.a = 1f - fadeT;
                tmp.color = c;

                byte outlineAlpha = (byte)(220 * (1f - fadeT));
                tmp.outlineColor = new Color32(
                    tmp.outlineColor.r,
                    tmp.outlineColor.g,
                    tmp.outlineColor.b,
                    outlineAlpha);
            }

            if (elapsed >= duration)
                Destroy(gameObject);
        }

        /// <summary>
        /// Billboard runs in LateUpdate — after the orbiting camera has settled.
        /// Uses transform.forward = cam.forward which is the standard Unity billboard
        /// approach: one assignment, no trig, no LookAt, GPU-friendly.
        /// </summary>
        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam != null)
                transform.forward = cam.transform.forward;
        }
    }
}
