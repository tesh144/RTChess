#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System.Collections;
using ClockworkGrid;
using ClockworkCraft;

namespace LittleCafe
{
    /// <summary>
    /// Partial: output resolution, draw helpers, and animation coroutines.
    /// </summary>
    public partial class DrawButtonController
    {
        // ── Output Resolution ───────────────────────────────────────

        /// <summary>
        /// Resolve the output for a draw level entry and add it to the dock.
        /// Returns true if a card was successfully added.
        /// </summary>
        private bool ResolveOutput(DrawButtonEntry entry, DockBarManager dock)
        {
            string output = entry.outputName;
            if (string.IsNullOrEmpty(output)) output = "None";

            // ── None → RandomBuilding (unfiltered) ──
            if (output.Equals("None", System.StringComparison.OrdinalIgnoreCase))
            {
                return DrawRandomAndAdd(dock);
            }

            // ── TierXBuilding / TierXUnit → tier-filtered draw by source type ──
            if (output.StartsWith("Tier", System.StringComparison.OrdinalIgnoreCase) &&
                (output.EndsWith("Building", System.StringComparison.OrdinalIgnoreCase) ||
                 output.EndsWith("Unit", System.StringComparison.OrdinalIgnoreCase)))
            {
                bool isBuilding = output.EndsWith("Building", System.StringComparison.OrdinalIgnoreCase);
                string tierStr = output.Substring(4, 1); // "Tier0Building" → "0"
                if (int.TryParse(tierStr, out int tier) && CardPool.Instance != null)
                {
                    UnitStats card = isBuilding
                        ? CardPool.Instance.DrawRandomBuildingByTier(tier)
                        : CardPool.Instance.DrawRandomUnitByTier(tier);
                    if (card != null)
                    {
                        UnitStats clone = Instantiate(card);
                        clone.name = card.unitName;
                        dock.AddCard(clone, markAsNew: true, animateFromDraw: true);
                        if (GameSFXManager.Instance != null)
                            GameSFXManager.Instance.PlayCardDraw();
                        return true;
                    }
                }
                return DrawRandomAndAdd(dock);
            }

            // ── RandomTier0-3 (legacy) → cumulative tier draw ──
            if (output.StartsWith("RandomTier", System.StringComparison.OrdinalIgnoreCase))
            {
                string tierStr = output.Substring("RandomTier".Length);
                if (int.TryParse(tierStr, out int maxTier))
                {
                    if (CardPool.Instance == null) return false;
                    UnitStats card = CardPool.Instance.DrawRandomUnitUpToTier(maxTier);
                    if (card != null)
                    {
                        UnitStats clone = Instantiate(card);
                        clone.name = card.unitName;
                        dock.AddCard(clone, markAsNew: true, animateFromDraw: true);
                        if (GameSFXManager.Instance != null)
                            GameSFXManager.Instance.PlayCardDraw();
                        return true;
                    }
                }
                return DrawRandomAndAdd(dock);
            }

            // ── RandomBuilding (legacy explicit) ──
            if (output.Equals("RandomBuilding", System.StringComparison.OrdinalIgnoreCase))
            {
                return DrawRandomAndAdd(dock);
            }

            // ── Worker → from WorkerDatabase ──
            if (output.Equals("Worker", System.StringComparison.OrdinalIgnoreCase))
            {
                WorkerData wd = workerDatabase != null ? workerDatabase.GetByName("Worker") : null;
                if (wd != null && wd.prefab != null)
                {
                    dock.AddWorkerCard(wd, animateFromDraw: true);
                    if (GameSFXManager.Instance != null)
                        GameSFXManager.Instance.PlayCardDraw();
                    return true;
                }
                Debug.LogWarning("[DrawButton] Worker not found in WorkerDatabase");
                return false;
            }

            // ── Fighter → from WorkerDatabase ──
            if (output.Equals("Fighter", System.StringComparison.OrdinalIgnoreCase))
            {
                // Fighter is registered in CardPool (created in SetupDeck from WorkerDatabase)
                if (CardPool.Instance != null)
                {
                    UnitStats fighter = CardPool.Instance.FindByName("Fighter");
                    if (fighter != null)
                    {
                        UnitStats clone = Instantiate(fighter);
                        clone.name = fighter.unitName;
                        dock.AddCard(clone, markAsNew: true, animateFromDraw: true);
                        if (GameSFXManager.Instance != null)
                            GameSFXManager.Instance.PlayCardDraw();
                        return true;
                    }
                }
                Debug.LogWarning("[DrawButton] Fighter card not found in CardPool");
                return false;
            }

            // ── Specific building/card name → find in CardPool ──
            if (CardPool.Instance != null)
            {
                UnitStats card = CardPool.Instance.FindByName(output);
                if (card != null)
                {
                    UnitStats clone = Instantiate(card);
                    clone.name = card.unitName;
                    dock.AddCard(clone, markAsNew: true, animateFromDraw: true);
                    if (GameSFXManager.Instance != null)
                        GameSFXManager.Instance.PlayCardDraw();
                    return true;
                }
            }

            Debug.LogWarning($"[DrawButton] Output '{output}' not found — falling back to random");
            return DrawRandomAndAdd(dock);
        }

        /// <summary>Draw a random building and add to dock. Returns true on success.</summary>
        private bool DrawRandomAndAdd(DockBarManager dock)
        {
            if (CardPool.Instance == null) return false;
            UnitStats card = CardPool.Instance.DrawRandomUnit();
            if (card != null)
            {
                UnitStats clone = Instantiate(card);
                clone.name = card.unitName;
                dock.AddCard(clone, markAsNew: true, animateFromDraw: true);
                if (GameSFXManager.Instance != null)
                    GameSFXManager.Instance.PlayCardDraw();
                return true;
            }
            return false;
        }

        // ── Animation Coroutines ────────────────────────────────────

        /// <summary>Quick punch-scale animation on a transform (press feedback).</summary>
        private IEnumerator ButtonPunchAnimation(Transform target)
        {
            Vector3 originalScale = target.localScale;
            Vector3 punchScale = originalScale * 0.85f;

            float duration = 0.12f;
            float half = duration * 0.5f;

            // Shrink
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                target.localScale = Vector3.Lerp(originalScale, punchScale, t / half);
                yield return null;
            }

            // Expand back
            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                target.localScale = Vector3.Lerp(punchScale, originalScale, t / half);
                yield return null;
            }

            target.localScale = originalScale;
        }

        /// <summary>Quick bounce when cooldown ends to draw attention.</summary>
        private IEnumerator ReadyBounceAnimation(Transform target)
        {
            Vector3 original = target.localScale;
            Vector3 big = original * 1.2f;
            float duration = 0.25f;
            float half = duration * 0.5f;

            // Scale up
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                target.localScale = Vector3.Lerp(original, big, t / half);
                yield return null;
            }

            // Settle back with slight overshoot
            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                float p = t / half;
                float ease = 1f + 2.7f * Mathf.Pow(p - 1f, 3f) + 1.7f * Mathf.Pow(p - 1f, 2f);
                target.localScale = Vector3.Lerp(big, original, ease);
                yield return null;
            }
            target.localScale = original;
        }

        /// <summary>Show the draw button with a pop-in scale animation.</summary>
        private IEnumerator PopInAnimation(Transform target)
        {
            Vector3 fullScale = Vector3.one;
            target.localScale = Vector3.zero;

            float duration = 0.3f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = t / duration;
                // Overshoot ease-out for a bouncy pop
                float ease = 1f + 2.7f * Mathf.Pow(p - 1f, 3f) + 1.7f * Mathf.Pow(p - 1f, 2f);
                target.localScale = fullScale * ease;
                yield return null;
            }
            target.localScale = fullScale;
        }

        // ── Cooldown Coroutine ──────────────────────────────────────

        private IEnumerator CooldownRoutine(float duration)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (LittleCafe.DevCheatMenu.InstantProduction) duration = 1f;
#endif
            isOnCooldown = true;
            cooldownRemaining = duration;

            if (drawButton != null)
                drawButton.interactable = false;

            // Show timer tag, hide buy tag
            RefreshTagVisibility();
            SetCooldownVisuals(true);

            // Reset timer text color to white for countdown display
            if (timerText != null)
                timerText.color = Color.white;

            while (cooldownRemaining > 0f)
            {
                if (timerText != null)
                    timerText.text = Mathf.CeilToInt(cooldownRemaining).ToString();

                yield return null;
                cooldownRemaining -= Time.deltaTime;
            }

            isOnCooldown = false;
            cooldownRemaining = 0f;

            if (drawButton != null)
            {
                drawButton.interactable = true;
                StartCoroutine(ReadyBounceAnimation(drawButton.transform));
            }

            // Switch from cooldown → ready state
            RefreshTagVisibility();
            SetCooldownVisuals(false);

            if (GameSFXManager.Instance != null)
                GameSFXManager.Instance.PlayDrawReady();

            cooldownCoroutine = null;
            Debug.Log("[DrawButton] Cooldown complete — draw available");
        }
    }
}
