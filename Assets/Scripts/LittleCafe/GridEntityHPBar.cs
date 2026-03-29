#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using TMPro;

namespace LittleCafe
{
    /// <summary>
    /// Two-part HP display system for grid entities:
    ///
    /// 1. PERSISTENT HP LABEL — Shows current HP above the entity.
    ///    Fades in when hit, stays visible while taking damage, then fades out
    ///    5 seconds after the last interaction. Color is tinted by entity type
    ///    (green for trees/water, warm yellow for gold, neutral gray for others)
    ///    and shifts toward a dim red as HP drops.
    ///
    /// 2. QUICK DAMAGE NUMBER — A small "-N" popup that appears on each hit,
    ///    floats upward, and vanishes quickly (~0.8s). Kept subtle so it doesn't
    ///    dominate the scene.
    ///
    /// Billboarding runs in LateUpdate so it stays in sync with the orbiting camera.
    /// </summary>
    public class GridEntityHPBar : MonoBehaviour
    {
        [Header("Persistent HP Label")]
        [Tooltip("When false, the white HP number above entities is hidden. Red damage popups still show.")]
        [SerializeField] private bool showHPLabel = true;

        [Header("Damage Popup Toggle")]
        [Tooltip("When false, the red -N damage popups are disabled. Loot particles provide enough feedback.")]
        [SerializeField] private bool showDamagePopup = false; // Legacy — kept to avoid serialization warnings

        [SerializeField] private float labelHeight = 2.0f;
        [SerializeField] private float labelFontSize = 3.5f;
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float lingerDuration = 5.0f;
        [SerializeField] private float fadeOutDuration = 1.0f;

        [Header("Damage Popup")]
        [SerializeField] private float damageFloatDistance = 1.0f;
        [SerializeField] private float damageDuration = 0.8f;
        [SerializeField] private float damageFontSize = 3.5f;
        [SerializeField] private float randomSpreadX = 0.3f;

        [Header("Colors")]
        [SerializeField] private Color lowHPColor = new Color(0.8f, 0.35f, 0.3f, 1f);
        [SerializeField] private Color outlineColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);

        [Header("Font Override")]
        [SerializeField] private TMP_FontAsset overrideFont;

        // Entity-type tint — resolved once at Initialize
        // These should be almost white with just a hint of color
        private Color entityTintColor = new Color(0.92f, 0.92f, 0.90f, 1f); // neutral near-white default
        private Color entityDamageColor = new Color(0.75f, 0.4f, 0.35f, 1f); // soft red default

        // Internal — persistent label
        private GridEntityHealth entityHealth;
        private GameObject labelObj;
        private TextMeshPro labelTMP;
        private float lastHitTime = -100f;
        private float currentAlpha = 0f;
        private float actualLabelHeight; // Calculated from model bounds
        private enum LabelState { Hidden, FadingIn, Visible, FadingOut }
        private LabelState labelState = LabelState.Hidden;

        // Static font caches — separate for neutral (HP labels) and red (damage popups)
        private static TMP_FontAsset cachedNeutralFont;
        private static bool neutralFontSearchDone = false;
        private static TMP_FontAsset cachedRedFont;
        private static bool redFontSearchDone = false;
        private bool labelFontSupportsOutline = true;
        private bool damageFontSupportsOutline = true;

        // ---------------------------------------------------------------
        // Setup
        // ---------------------------------------------------------------

        public void Initialize(GridEntityHealth health)
        {
            entityHealth = health;
            entityHealth.OnDamaged += OnEntityDamaged;
            entityHealth.OnDamagedBy += OnEntityDamagedBy;

            // Force soft colors — overrides any stale serialized values from older prefabs
            lowHPColor = new Color(0.85f, 0.55f, 0.5f, 1f);  // Warm muted rose, not screaming red
            outlineColor = new Color(0.1f, 0.1f, 0.1f, 0.35f); // Very soft outline

            // HP labels only show on allied entities (buildings, workers).
            // Environment objects (trees, rocks, gold) don't need HP labels —
            // the loot particles flying out provide enough feedback.
            if (!health.IsAllied)
                showHPLabel = false;

            // Resolve entity-type tint from ResourceNode (if present)
            ResolveEntityTint();

            // Calculate label height from actual model bounds so the label
            // always appears above tall objects like trees, not inside them.
            actualLabelHeight = CalculateLabelHeight();

            if (showHPLabel)
                CreatePersistentLabel();
        }

        /// <summary>
        /// Pick a subtle tint color based on what kind of entity this is.
        /// Trees/water → soft green, gold → warm yellow, rock → cool gray, etc.
        /// </summary>
        private void ResolveEntityTint()
        {
            var resourceNode = GetComponent<ClockworkCraft.ResourceNode>();
            if (resourceNode != null)
            {
                // Almost white with just a subtle tint — blends into the scene
                switch (resourceNode.resourceType)
                {
                    case ClockworkCraft.ResourceType.Wood:
                        entityTintColor = new Color(0.88f, 0.95f, 0.87f, 1f);    // near-white, hint of green
                        entityDamageColor = new Color(0.65f, 0.5f, 0.35f, 1f);   // brownish
                        break;
                    case ClockworkCraft.ResourceType.Water:
                        entityTintColor = new Color(0.87f, 0.93f, 0.96f, 1f);    // near-white, hint of blue
                        entityDamageColor = new Color(0.45f, 0.55f, 0.7f, 1f);   // muted blue
                        break;
                    case ClockworkCraft.ResourceType.Gold:
                        entityTintColor = new Color(0.96f, 0.94f, 0.85f, 1f);    // near-white, hint of yellow
                        entityDamageColor = new Color(0.85f, 0.65f, 0.35f, 1f);  // amber
                        break;
                    case ClockworkCraft.ResourceType.Stone:
                        entityTintColor = new Color(0.90f, 0.90f, 0.92f, 1f);    // near-white, hint of cool
                        entityDamageColor = new Color(0.6f, 0.5f, 0.48f, 1f);    // warm gray
                        break;
                    case ClockworkCraft.ResourceType.WhiteMarble:
                        entityTintColor = new Color(0.94f, 0.94f, 0.92f, 1f);    // near-white, warm
                        entityDamageColor = new Color(0.7f, 0.55f, 0.5f, 1f);    // dusty rose
                        break;
                    default:
                        // Neutral near-white
                        break;
                }
            }
        }

        /// <summary>
        /// Determine the height above the entity where labels/popups should appear.
        ///
        /// Priority:
        /// 1. RefHeight child transform — manually placed reference point on each prefab
        /// 2. Renderer bounds calculation — auto-measured from the model
        /// 3. Serialized labelHeight fallback
        /// </summary>
        private float CalculateLabelHeight()
        {
            // 1. Try RefHeight — the manually placed reference point
            Transform refHeight = FindRefHeight(transform);
            if (refHeight != null)
            {
                float height = refHeight.position.y - transform.position.y;
                return Mathf.Max(height, 0.5f);
            }

            // 2. Fall back to renderer bounds
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return labelHeight;

            // Combine all renderer bounds to get total bounding box
            Bounds combinedBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                combinedBounds.Encapsulate(renderers[i].bounds);
            }

            // Label goes just above the top of the model
            float topY = combinedBounds.max.y - transform.position.y;
            // Small proportional padding: 15% of object height, clamped 0.15–0.4
            float padding = Mathf.Clamp(topY * 0.15f, 0.15f, 0.4f);
            float calculatedHeight = topY + padding;

            return Mathf.Max(calculatedHeight, 0.5f); // Absolute minimum so it's not underground
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
            labelTMP.richText = false;

            TMP_FontAsset font = GetNeutralFont();
            if (font != null)
            {
                labelTMP.font = font;
                labelFontSupportsOutline = font.material != null &&
                    font.material.HasProperty("_OutlineColor");
            }

            if (labelFontSupportsOutline)
            {
                labelTMP.outlineWidth = 0.15f;
                labelTMP.outlineColor = new Color32(
                    (byte)(outlineColor.r * 255),
                    (byte)(outlineColor.g * 255),
                    (byte)(outlineColor.b * 255),
                    (byte)(outlineColor.a * 255));
            }

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
            if (showHPLabel)
                UpdateHPLabel(currentHP, maxHP);
            // Damage popup is now handled by OnEntityDamagedBy (relationship-based)
        }

        /// <summary>
        /// Show damage popup only when attacker and target are enemies.
        /// Neutral resources (trees, rocks, goldmines) have no actor — no popup.
        /// Active enemies (dinos, mammoths, spikes) have an actor — popup shows.
        ///   - Worker (allied) attacks Dino (not allied, has actor) → popup
        ///   - Spike (not allied) attacks Worker (allied) → popup
        ///   - Worker (allied) attacks Tree (not allied, no actor = neutral) → no popup
        /// </summary>
        private void OnEntityDamagedBy(GridEntityHealth attacker, int damageDealt)
        {
            if (entityHealth == null || attacker == null) return;

            // Only show when attacker and target are on opposite sides
            bool opposingSides = entityHealth.IsAllied != attacker.IsAllied;
            if (!opposingSides) return;

            // Non-allied targets without an actor are neutral resources (trees, goldmine, rocks).
            // They don't show damage popups — loot particles provide the feedback instead.
            if (!entityHealth.IsAllied && GetComponent<GridEntityActor>() == null) return;

            SpawnDamagePopup(damageDealt, entityHealth.CurrentHP, entityHealth.MaxHP);
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
            Color textColor = Color.Lerp(lowHPColor, entityTintColor, hpFraction);
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

            if (labelFontSupportsOutline)
            {
                Color32 oc = labelTMP.outlineColor;
                oc.a = (byte)(200 * alpha);
                labelTMP.outlineColor = oc;
            }
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
            // Spawn damage popup just above the HP label — tight to the object
            float popupY = actualLabelHeight + 0.15f;
            Vector3 spawnPos = transform.position + new Vector3(offsetX, popupY, 0f);

            GameObject popupObj = new GameObject("DamagePopup");
            popupObj.transform.position = spawnPos;

            // Buildings (allied entities) show remaining HP in starvation-countdown style.
            // Everything else shows damage dealt as "-N".
            bool isBuilding = entityHealth != null && entityHealth.IsAllied;
            string popupText = isBuilding ? currentHP.ToString() : $"-{damageDealt}";

            TextMeshPro tmp = popupObj.AddComponent<TextMeshPro>();
            tmp.text = popupText;
            tmp.fontSize = isBuilding ? damageFontSize * 1.2f : damageFontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.sortingOrder = 200;
            tmp.enableWordWrapping = false;
            tmp.richText = false; // Prevent underline glyph lookup on bitmap fonts

            TMP_FontAsset font = GetRedFont();
            if (font != null)
            {
                tmp.font = font;
                damageFontSupportsOutline = font.material != null &&
                    font.material.HasProperty("_OutlineColor");
            }

            // Buildings use the aggressive starvation-countdown red; others use softer damage color
            tmp.color = isBuilding ? new Color(0.9f, 0.15f, 0.15f, 1f) : entityDamageColor;

            if (damageFontSupportsOutline)
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
            animator.Initialize(damageFloatDistance, damageDuration, isCritical);
        }

        // ---------------------------------------------------------------
        // Font Resolution
        // ---------------------------------------------------------------

        /// <summary>
        /// Proper SDF font for HP labels — fully tintable via tmp.color.
        /// Uses Quicksand Bold (GUI Pro Kit's number font), NOT the MuseoModerno
        /// transparent variant which is an outline-only overlay font.
        /// </summary>
        private TMP_FontAsset GetNeutralFont()
        {
            if (overrideFont != null) return overrideFont;
            if (neutralFontSearchDone) return cachedNeutralFont;

            neutralFontSearchDone = true;

            GUIProKitAssets guiKit = GUIProKitAssets.Instance;
            // Use uiNumberFont (Quicksand Bold) — proper SDF, fully tintable
            if (guiKit != null && guiKit.uiNumberFont != null)
            {
                cachedNeutralFont = guiKit.uiNumberFont;
                return cachedNeutralFont;
            }

            // Fall back to TMP default
            cachedNeutralFont = GetFallbackFont();
            return cachedNeutralFont;
        }

        /// <summary>
        /// Red pre-baked font for damage popups — bold and eye-catching.
        /// Falls back to TMP default.
        /// </summary>
        private TMP_FontAsset GetRedFont()
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

            // Fall back to TMP default
            cachedRedFont = GetFallbackFont();
            return cachedRedFont;
        }

        private static TMP_FontAsset GetFallbackFont()
        {
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null) return font;
            return TMP_Settings.defaultFontAsset;
        }

        // ---------------------------------------------------------------
        // RefHeight Utilities
        // ---------------------------------------------------------------

        /// <summary>
        /// Searches the hierarchy for a child named "RefHeight".
        /// Checks immediate children first, then does a recursive search.
        /// </summary>
        private static Transform FindRefHeight(Transform root)
        {
            // Fast path: check immediate children
            for (int i = 0; i < root.childCount; i++)
            {
                if (root.GetChild(i).name == "RefHeight")
                    return root.GetChild(i);
            }

            // Recursive: check deeper children (e.g. inside AnimatorHolder/Recenter)
            return FindChildRecursive(root, "RefHeight");
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// Public static utility: get the world-space Y position of the top of an object.
        /// Uses RefHeight if available, falls back to renderer bounds, then a default.
        ///
        /// Call from anywhere:
        ///   float topY = GridEntityHPBar.GetTopOfObject(someTransform);
        ///   Vector3 topPos = someTransform.position + Vector3.up * topY;
        /// </summary>
        public static float GetTopOfObject(Transform objectRoot, float fallback = 1.5f)
        {
            // 1. RefHeight
            Transform refHeight = FindRefHeight(objectRoot);
            if (refHeight != null)
                return Mathf.Max(refHeight.position.y - objectRoot.position.y, 0.3f);

            // 2. Renderer bounds
            Renderer[] renderers = objectRoot.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds combinedBounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    combinedBounds.Encapsulate(renderers[i].bounds);

                float topY = combinedBounds.max.y - objectRoot.position.y;
                return Mathf.Max(topY, 0.3f);
            }

            // 3. Fallback
            return fallback;
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
