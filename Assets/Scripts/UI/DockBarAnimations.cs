#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

namespace ClockworkGrid
{
    /// <summary>
    /// Card fly-in animations and hand-full popup for DockBarManager.
    /// Extracted to keep DockBarManager under 500 lines.
    /// </summary>
    public static class DockBarAnimations
    {
        /// <summary>
        /// Animate a card flying from a start position to its dock slot with an upward arc.
        /// Must be run as a coroutine on DockBarManager (or any MonoBehaviour).
        /// </summary>
        public static IEnumerator CardFlyIn(
            GameCardUI card, RectTransform cardRect,
            Vector3? overrideStartScreenPos,
            RectTransform drawButtonRect)
        {
            if (cardRect == null) yield break;

            // Determine start position
            Vector3 startPos;
            if (overrideStartScreenPos.HasValue)
            {
                startPos = overrideStartScreenPos.Value;
            }
            else if (drawButtonRect != null)
            {
                startPos = drawButtonRect.position;
            }
            else
            {
                cardRect.localScale = Vector3.one;
                card.PlayAppearAnimation();
                yield break;
            }

            // Yield one frame so HorizontalLayoutGroup recalculates slot positions
            yield return null;
            if (cardRect == null) yield break;

            // Temporarily restore scale so layout computes correct slot size
            cardRect.localScale = Vector3.one;
            LayoutRebuilder.ForceRebuildLayoutImmediate(cardRect.parent as RectTransform);
            Vector3 finalPos = cardRect.position;
            Vector3 finalScale = Vector3.one;

            // Snap back to start before the next frame renders
            cardRect.position = startPos;
            cardRect.localScale = finalScale * 0.2f;

            float arcHeight = Mathf.Abs(finalPos.y - startPos.y) * 0.5f + 80f;
            float duration = 0.45f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (cardRect == null) yield break;

                float easeT = t * t * (3f - 2f * t);

                Vector3 pos = Vector3.Lerp(startPos, finalPos, easeT);
                pos.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
                cardRect.position = pos;

                float scaleT;
                if (t < 0.7f)
                {
                    scaleT = Mathf.Lerp(0.2f, 1.1f, t / 0.7f);
                }
                else
                {
                    float settleT = (t - 0.7f) / 0.3f;
                    scaleT = Mathf.Lerp(1.1f, 1f, settleT);
                }
                cardRect.localScale = finalScale * scaleT;

                yield return null;
            }

            if (cardRect == null) yield break;
            cardRect.position = finalPos;
            cardRect.localScale = finalScale;

            if (card != null)
                card.StartIdleAnimation();
        }

        /// <summary>
        /// Show a brief "Hand Full!" floating text at the given screen position.
        /// Must be run as a coroutine on a MonoBehaviour.
        /// </summary>
        public static IEnumerator ShowHandFullPopup(Vector2 screenPos)
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) yield break;

            GameObject popupObj = new GameObject("HandFullPopup");
            popupObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = popupObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(200f, 40f);

            Camera canvasCam = (canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(), screenPos, canvasCam, out Vector2 localPoint);
            rt.anchoredPosition = localPoint;

            Canvas overrideCanvas = popupObj.AddComponent<Canvas>();
            overrideCanvas.overrideSorting = true;
            overrideCanvas.sortingOrder = 100;

            TextMeshProUGUI tmp = popupObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "Hand Full!";
            tmp.fontSize = 24f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.4f, 0.4f, 1f);
            tmp.raycastTarget = false;
            tmp.enableAutoSizing = false;
            tmp.fontStyle = FontStyles.Bold;

            // Float upward and fade out
            float duration = 1f;
            float elapsed = 0f;
            Vector2 startPos = rt.anchoredPosition;

            while (elapsed < duration)
            {
                if (rt == null || popupObj == null)
                    yield break;

                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                rt.anchoredPosition = startPos + new Vector2(0f, 40f * t);

                float alpha = t < 0.5f ? 1f : 1f - ((t - 0.5f) * 2f);
                if (tmp != null)
                    tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, alpha);

                yield return null;
            }

            if (popupObj != null)
                Object.Destroy(popupObj);
        }
    }
}
