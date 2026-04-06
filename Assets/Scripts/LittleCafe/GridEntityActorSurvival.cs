#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System.Collections;
using ClockworkGrid;
using TMPro;

namespace LittleCafe
{
    /// <summary>
    /// GridEntityActor partial — starvation system, meal buff, and corruption pause.
    /// Workers that idle too long starve to death with a visible countdown.
    /// Meal buff doubles action rate by subscribing to OnHalfBar.
    /// </summary>
    public partial class GridEntityActor
    {
        // ── Corruption Pause ──────────────────────────────────────────────

        /// <summary>Pause this actor — stops all tick behavior. Called by CorruptionOverlay.</summary>
        public void PauseForCorruption()
        {
            isCorruptionPaused = true;
            if (rotationCoroutine != null) { StopCoroutine(rotationCoroutine); rotationCoroutine = null; }
            if (interactionCoroutine != null) { StopCoroutine(interactionCoroutine); interactionCoroutine = null; }

            // Tint renderers purple
            var renderers = GetComponentsInChildren<Renderer>();
            var tintable = new System.Collections.Generic.List<Renderer>();
            foreach (var r in renderers)
            {
                if (r is LineRenderer || r is TrailRenderer) continue;
                tintable.Add(r);
            }
            corruptionTintedMaterials = new Material[tintable.Count];
            originalColors = new Color[tintable.Count];
            for (int i = 0; i < tintable.Count; i++)
            {
                corruptionTintedMaterials[i] = tintable[i].material;
                originalColors[i] = corruptionTintedMaterials[i].color;
                corruptionTintedMaterials[i].color = Color.Lerp(originalColors[i], CorruptionTint, 0.5f);
            }
        }

        /// <summary>Resume this actor after corruption is cleared.</summary>
        public void ResumeFromCorruption()
        {
            isCorruptionPaused = false;

            if (corruptionTintedMaterials != null)
            {
                for (int i = 0; i < corruptionTintedMaterials.Length; i++)
                {
                    if (corruptionTintedMaterials[i] != null)
                        corruptionTintedMaterials[i].color = originalColors[i];
                }
                corruptionTintedMaterials = null;
                originalColors = null;
            }
        }

        // ── Meal Buff ────────────────────────────────────────────────────

        /// <summary>
        /// Converts mealBuffDurationSeconds to bar ticks using the current IntervalTimer bar duration.
        /// </summary>
        private int ConvertDurationToTicks()
        {
            if (IntervalTimer.Instance == null)
            {
                const float FallbackBarDuration = 2f;
                Debug.LogWarning("[GridEntityActor] IntervalTimer.Instance is null — using fallback bar duration");
                return Mathf.Max(1, Mathf.RoundToInt(mealBuffDurationSeconds / FallbackBarDuration));
            }
            float barDuration = Mathf.Max(float.Epsilon, IntervalTimer.Instance.IntervalDuration);
            return Mathf.Max(1, Mathf.RoundToInt(mealBuffDurationSeconds / barDuration));
        }

        public void GrantMealBuff(int durationTicks)
        {
            if (hasMealBuff) return;

            hasMealBuff = true;
            mealBuffTicksRemaining = durationTicks;

            if (IntervalTimer.Instance != null)
                IntervalTimer.Instance.OnHalfBar += OnHalfBarTick;
        }

        private void ExpireMealBuff()
        {
            hasMealBuff = false;
            mealBuffTicksRemaining = 0;

            if (IntervalTimer.Instance != null)
                IntervalTimer.Instance.OnHalfBar -= OnHalfBarTick;

            if (verboseLogging)
                Debug.Log($"[GridEntityActor] {gameObject.name} meal buff expired");
        }

        /// <summary>
        /// Only subscribed while the meal buff is active.
        /// Fires at beats 1 and 3, doubling the worker's action rate.
        /// </summary>
        private void OnHalfBarTick(int bar)
        {
            if (!isInitialized) return;
            if (health != null && health.IsDestroyed) return;
            if (isCorruptionPaused) return;

            if (attackIntervalMultiplier > 1 && bar % attackIntervalMultiplier != 0)
                return;

            if (interactionCoroutine != null)
                StopCoroutine(interactionCoroutine);

            switch (behaviorType)
            {
                case BehaviorType.RotateAndInteract:
                    interactionCoroutine = StartCoroutine(ClockworkTickInteract());
                    break;
                case BehaviorType.RotateAndMove:
                    interactionCoroutine = StartCoroutine(ClockworkTickMove());
                    break;
                case BehaviorType.RotateAndMoveCorrupted:
                    interactionCoroutine = StartCoroutine(ClockworkTickMoveCorrupted());
                    break;
                case BehaviorType.RotateRotateMove:
                    interactionCoroutine = StartCoroutine(ClockworkTickRotateRotateMove());
                    break;
                default:
                    interactionCoroutine = StartCoroutine(ClockworkTickInteract());
                    break;
            }
        }

        // ── Starvation ──────────────────────────────────────────────────

        /// <summary>
        /// Reset the idle counter completely. Called on any successful interaction.
        /// </summary>
        private void ResetIdleCounter()
        {
            bool wasCountingDown = idleTickCount > graceThreshold;
            if (idleTickCount > 0 && verboseLogging)
                Debug.Log($"[GridEntityActor] {gameObject.name} idle counter reset (was {idleTickCount})");

            idleTickCount = 0;

            if (wasCountingDown)
                SetStarvationTint(false);
        }

        // ── Starvation Tint ─────────────────────────────────────────────

        private static readonly Color starvationColor = new Color(0.9f, 0.15f, 0.15f);
        private bool hasStarvationTint;

        private void SetStarvationTint(bool tinted)
        {
            if (tinted == hasStarvationTint) return;
            hasStarvationTint = tinted;

            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            var mpb = new MaterialPropertyBlock();
            int colorID = Shader.PropertyToID("_TintColor");
            int strengthID = Shader.PropertyToID("_TintStrength");

            foreach (var rend in renderers)
            {
                rend.GetPropertyBlock(mpb);
                mpb.SetColor(colorID, tinted ? starvationColor : Color.white);
                mpb.SetFloat(strengthID, tinted ? 0.5f : 0f);
                rend.SetPropertyBlock(mpb);
            }
        }

        /// <summary>
        /// Increment the idle counter and check for starvation death.
        /// Called when ScanAndInteract finds nothing.
        /// </summary>
        private void IncrementIdleCounter()
        {
            if (behaviorType != BehaviorType.RotateAndInteract) return;
            if (health != null && !health.IsAllied) return;

            idleTickCount++;

            int totalThreshold = graceThreshold + countdownThreshold;

            if (idleTickCount > graceThreshold && idleTickCount <= totalThreshold)
            {
                if (!hasStarvationTint)
                    SetStarvationTint(true);

                // displayNumber counts down: countdownThreshold, countdownThreshold-1, ... 1
                int displayNumber = totalThreshold - idleTickCount + 1;

                if (verboseLogging)
                    Debug.Log($"[GridEntityActor] {gameObject.name} starving! Countdown: {displayNumber} (idle ticks: {idleTickCount}/{totalThreshold})");
                SpawnCountdownPopup(displayNumber);

                if (GameSFXManager.Instance != null)
                    GameSFXManager.Instance.PlayClockTick();
            }
            else if (idleTickCount > totalThreshold)
            {
                if (verboseLogging)
                    Debug.Log($"[GridEntityActor] {gameObject.name} STARVED TO DEATH after {idleTickCount} idle ticks!");
                isStarving = true;

                if (health != null && !health.IsDestroyed)
                    health.TakeDamage(health.CurrentHP);
            }
            else
            {
                if (verboseLogging)
                    Debug.Log($"[GridEntityActor] {gameObject.name} idle tick {idleTickCount}/{graceThreshold} (grace period)");
            }
        }

        private void SpawnCountdownPopup(int number)
        {
            float spawnHeight = GridEntityHPBar.GetTopOfObject(transform, 2.2f) + 0.3f;
            float spreadX = Random.Range(-0.3f, 0.3f);
            Vector3 spawnPos = transform.position + new Vector3(spreadX, spawnHeight, 0f);

            GameObject popupObj = new GameObject("StarvationCountdown");
            popupObj.transform.position = spawnPos;

            TextMeshPro tmp = popupObj.AddComponent<TextMeshPro>();
            tmp.text = number.ToString();
            tmp.fontSize = 7f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.9f, 0.15f, 0.15f, 1f);
            tmp.fontStyle = FontStyles.Bold;
            tmp.sortingOrder = 100;
            tmp.enableWordWrapping = false;
            tmp.richText = false;

            TMP_FontAsset font = null;
            GUIProKitAssets guiKit = GUIProKitAssets.Instance;
            if (guiKit != null && guiKit.criticalNumberFont != null)
                font = guiKit.criticalNumberFont;
            if (font == null)
                font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font == null && TMP_Settings.defaultFontAsset != null)
                font = TMP_Settings.defaultFontAsset;
            if (font != null)
                tmp.font = font;

            bool hasOutline = font != null && font.material != null &&
                font.material.HasProperty("_OutlineColor");
            if (hasOutline)
            {
                tmp.outlineWidth = 0.25f;
                tmp.outlineColor = new Color32(40, 10, 0, 220);
            }

            RectTransform rect = popupObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(3f, 2f);

            StarvationCountdownPopup animator = popupObj.AddComponent<StarvationCountdownPopup>();
            animator.Initialize(1.0f, 1.4f);
        }

        private void OnDestroy()
        {
            // Countdown popups are self-destructing — no cleanup needed
        }
    }

    /// <summary>
    /// Animates a starvation countdown popup: pop-scale entrance, float upward,
    /// fade out, self-destruct. Billboard in LateUpdate.
    /// </summary>
    public class StarvationCountdownPopup : MonoBehaviour
    {
        private float floatDistance;
        private float duration;
        private float elapsed = 0f;
        private Vector3 startPos;
        private TextMeshPro tmp;
        private Color startColor;
        private Color32 startOutlineColor;
        private bool hasOutline;

        public void Initialize(float distance, float totalDuration)
        {
            floatDistance = distance;
            duration = totalDuration;
            startPos = transform.position;
            tmp = GetComponent<TextMeshPro>();
            if (tmp != null)
            {
                startColor = tmp.color;
                hasOutline = tmp.font != null && tmp.font.material != null &&
                    tmp.font.material.HasProperty("_OutlineColor");
                if (hasOutline)
                    startOutlineColor = tmp.outlineColor;
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float easedT = 1f - (1f - t) * (1f - t);
            transform.position = startPos + new Vector3(0f, floatDistance * easedT, 0f);

            float scale = 1f;
            if (t < 0.1f)
            {
                float popT = t / 0.1f;
                scale = Mathf.Lerp(0f, 1.8f, popT);
            }
            else if (t < 0.25f)
            {
                float settleT = (t - 0.1f) / 0.15f;
                scale = Mathf.Lerp(1.8f, 1f, settleT);
            }
            transform.localScale = Vector3.one * scale;

            if (tmp != null && t > 0.6f)
            {
                float fadeT = (t - 0.6f) / 0.4f;
                Color c = startColor;
                c.a = 1f - fadeT;
                tmp.color = c;

                if (hasOutline)
                {
                    byte outlineAlpha = (byte)(startOutlineColor.a * (1f - fadeT));
                    tmp.outlineColor = new Color32(
                        startOutlineColor.r, startOutlineColor.g,
                        startOutlineColor.b, outlineAlpha);
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
