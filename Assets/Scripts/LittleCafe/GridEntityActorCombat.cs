#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System.Collections;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// GridEntityActor partial — scan logic and interaction execution.
    /// Covers worker scan, wild animal scan, enemy scan, strong/weak interactions,
    /// and advance-into-cell after kill.
    /// </summary>
    public partial class GridEntityActor
    {
        // ===============================================================
        // Behavior Tick Coroutines
        // ===============================================================

        private IEnumerator ClockworkTickInteract()
        {
            if (isFirstTick)
            {
                isFirstTick = false;
                yield return new WaitForSeconds(INTERACTION_DELAY);
            }
            else
            {
                Rotate();
                yield return new WaitForSeconds(ROTATION_DURATION + INTERACTION_DELAY);
            }

            yield return StartCoroutine(ScanAndInteract());
            interactionCoroutine = null;
        }

        private IEnumerator ClockworkTickMove()
        {
            if (isMoving) yield break;

            if (isFirstTick)
            {
                isFirstTick = false;
                yield return new WaitForSeconds(INTERACTION_DELAY);
            }
            else
            {
                Rotate();
                yield return new WaitForSeconds(ROTATION_DURATION + INTERACTION_DELAY);
            }

            bool interacted = false;
            yield return StartCoroutine(ScanAndInteractWildAnimal(result => interacted = result));

            if (!interacted)
                yield return StartCoroutine(TryMoveForward());

            interactionCoroutine = null;
        }

        private IEnumerator ClockworkTickMoveCorrupted()
        {
            if (isMoving) yield break;

            if (isFirstTick)
            {
                isFirstTick = false;
                yield return new WaitForSeconds(INTERACTION_DELAY);
            }
            else
            {
                Rotate();
                yield return new WaitForSeconds(ROTATION_DURATION + INTERACTION_DELAY);
            }

            bool interacted = false;
            yield return StartCoroutine(ScanAndInteractEnemy(result => interacted = result));

            if (!interacted)
                yield return StartCoroutine(TryMoveForward());

            interactionCoroutine = null;
        }

        private IEnumerator ClockworkTickRotateRotateMove()
        {
            if (isMoving) yield break;

            if (isFirstTick)
            {
                isFirstTick = false;
                yield return new WaitForSeconds(INTERACTION_DELAY);
                bool interacted = false;
                yield return StartCoroutine(ScanAndInteractWildAnimal(result => interacted = result));
                if (!interacted)
                    yield return StartCoroutine(TryMoveForward());
                interactionCoroutine = null;
                yield break;
            }

            if (rrm_isRotateTick)
            {
                Rotate();
                yield return new WaitForSeconds(ROTATION_DURATION + INTERACTION_DELAY);
                yield return StartCoroutine(ScanAndInteractWildAnimal(_ => { }));
                rrm_isRotateTick = false;
            }
            else
            {
                Rotate();
                yield return new WaitForSeconds(ROTATION_DURATION + INTERACTION_DELAY);
                bool interacted = false;
                yield return StartCoroutine(ScanAndInteractWildAnimal(result => interacted = result));
                if (!interacted)
                    yield return StartCoroutine(TryMoveForward());
                rrm_isRotateTick = true;
            }

            interactionCoroutine = null;
        }

        // ===============================================================
        // Scan & Interact (RotateAndInteract behavior — workers)
        // ===============================================================

        /// <summary>
        /// Look in the facing direction, find the first occupant, and interact.
        /// Object layer is checked first, then surface layer (corruption, water).
        /// </summary>
        private IEnumerator ScanAndInteract()
        {
            if (furnitureObject == null) yield break;

            GridManager gm = GridManager.Instance;
            if (gm == null) yield break;

            currentFacing.ToGridOffset(out int dx, out int dy);

            int startX = furnitureObject.GridX;
            int startY = furnitureObject.GridY;

            for (int step = 1; step <= attackRange; step++)
            {
                int checkX = startX + (dx * step);
                int checkY = startY + (dy * step);

                if (checkX < 0 || checkX >= gm.Width || checkY < 0 || checkY >= gm.Height)
                    break;

                // Object layer first, then surface layer (corruption, water).
                // Workers interact with the object on a tile before touching the surface underneath.
                GameObject occupant = gm.GetCellOccupant(checkX, checkY);
                if (occupant == null)
                {
                    // No object — check for attackable surface (corruption, water)
                    CorruptionOverlay tileCorruption = CorruptionManager.Instance != null
                        ? CorruptionManager.Instance.GetOverlay(checkX, checkY) : null;
                    if (tileCorruption != null && tileCorruption.Health != null && !tileCorruption.Health.IsDestroyed)
                    {
                        ResetIdleCounter();
                        yield return PerformStrongInteraction(tileCorruption.Health, checkX, checkY);
                        yield break;
                    }

                    if (gm.HasSurface(checkX, checkY))
                    {
                        var surface = gm.GetSurface(checkX, checkY);
                        if (surface == SurfaceType.Water)
                        {
                            GameObject surfaceGO = gm.GetSurfaceOccupant(checkX, checkY);
                            if (surfaceGO != null)
                            {
                                GridEntityHealth surfaceHealth = surfaceGO.GetComponent<GridEntityHealth>();
                                if (surfaceHealth != null && !surfaceHealth.IsDestroyed)
                                {
                                    ResetIdleCounter();
                                    yield return PerformStrongInteraction(surfaceHealth, checkX, checkY);
                                    yield break;
                                }
                            }
                        }
                    }
                    continue;
                }
                if (occupant == gameObject) continue;

                if (hasMealBuff && occupant.GetComponent<MealBuffSource>() != null)
                    continue;

                GridEntityHealth targetHealth = occupant.GetComponent<GridEntityHealth>();

                if (targetHealth != null && !targetHealth.IsDestroyed)
                {
                    bool isAlliedActor = health != null && health.IsAllied;
                    bool isAlliedTarget = targetHealth.IsAllied;

                    if (isAlliedActor && isAlliedTarget)
                        continue;
                    if (!isAlliedActor && !isAlliedTarget)
                        continue;

                    bool canInteract;
                    if (isAlliedActor)
                    {
                        canInteract = targetHealth.WorkerCanInteract;
                    }
                    else
                    {
                        string targetName = occupant.name.Replace("(Clone)", "").Trim();
                        canInteract = ClockworkCraft.InteractionRegistry.Instance != null
                            && ClockworkCraft.InteractionRegistry.Instance.CanInteract(targetName, ClockworkCraft.InteractorType.Enemy);
                    }

                    if (!canInteract)
                    {
                        FaceTarget(checkX, checkY);
                        if (animator != null) animator.SetTrigger("interact_weak");
                        yield break;
                    }

                    ResetIdleCounter();
                    yield return PerformStrongInteraction(targetHealth, checkX, checkY);
                    yield break;
                }
                else
                {
                    ResetIdleCounter();
                    PerformWeakInteraction(checkX, checkY);
                    yield break;
                }
            }

            // Nothing found — idle bounce + starvation tracking
            if (animator != null) animator.SetTrigger("idle_bounce");
            IncrementIdleCounter();
        }

        // ---------------------------------------------------------------
        // Scan & Interact — Wild Animal (RotateAndMove / RotateRotateMove)
        // ---------------------------------------------------------------

        private IEnumerator ScanAndInteractWildAnimal(System.Action<bool> result)
        {
            result(false);

            if (furnitureObject == null) yield break;
            GridManager gm = GridManager.Instance;
            if (gm == null) yield break;

            currentFacing.ToGridOffset(out int dx, out int dy);
            int startX = furnitureObject.GridX;
            int startY = furnitureObject.GridY;

            for (int step = 1; step <= attackRange; step++)
            {
                int checkX = startX + (dx * step);
                int checkY = startY + (dy * step);

                if (checkX < 0 || checkX >= gm.Width || checkY < 0 || checkY >= gm.Height)
                    break;

                GameObject occupant = gm.GetCellOccupant(checkX, checkY);
                if (occupant == null) continue;
                if (occupant == gameObject) continue;

                GridEntityHealth targetHealth = occupant.GetComponent<GridEntityHealth>();
                if (targetHealth == null || targetHealth.IsDestroyed) continue;

                if (!targetHealth.IsAllied)
                    continue;

                string targetName = occupant.name.Replace("(Clone)", "").Trim();
                bool canInteract = ClockworkCraft.InteractionRegistry.Instance != null
                    && ClockworkCraft.InteractionRegistry.Instance.CanInteract(targetName, ClockworkCraft.InteractorType.WildAnimal);

                if (canInteract)
                {
                    yield return PerformStrongInteraction(targetHealth, checkX, checkY);
                    result(true);
                    yield break;
                }
            }
        }

        // ---------------------------------------------------------------
        // Scan & Interact — Enemy (Corruption spikes)
        // ---------------------------------------------------------------

        private IEnumerator ScanAndInteractEnemy(System.Action<bool> result)
        {
            result(false);

            if (furnitureObject == null) yield break;
            GridManager gm = GridManager.Instance;
            if (gm == null) yield break;

            currentFacing.ToGridOffset(out int dx, out int dy);
            int startX = furnitureObject.GridX;
            int startY = furnitureObject.GridY;

            for (int step = 1; step <= attackRange; step++)
            {
                int checkX = startX + (dx * step);
                int checkY = startY + (dy * step);

                if (checkX < 0 || checkX >= gm.Width || checkY < 0 || checkY >= gm.Height)
                    break;

                GameObject occupant = gm.GetCellOccupant(checkX, checkY);
                if (occupant == null) continue;
                if (occupant == gameObject) continue;

                GridEntityHealth targetHealth = occupant.GetComponent<GridEntityHealth>();
                if (targetHealth == null || targetHealth.IsDestroyed) continue;

                if (!targetHealth.IsAllied)
                    continue;

                string targetName = occupant.name.Replace("(Clone)", "").Trim();
                bool canInteract = ClockworkCraft.InteractionRegistry.Instance != null
                    && ClockworkCraft.InteractionRegistry.Instance.CanInteract(targetName, ClockworkCraft.InteractorType.Enemy);

                if (canInteract)
                {
                    yield return PerformStrongInteraction(targetHealth, checkX, checkY);
                    result(true);
                    yield break;
                }
            }
        }

        // ---------------------------------------------------------------
        // Interactions
        // ---------------------------------------------------------------

        private IEnumerator PerformStrongInteraction(GridEntityHealth target, int targetX, int targetY)
        {
            bool cachedSlotTakeable = target != null && target.IsSlotTakeable;

            FaceTarget(targetX, targetY);

            if (animator != null)
                animator.SetTrigger("interact_strong");

            if (GameSFXManager.Instance != null)
                GameSFXManager.Instance.PlayHitImpact();

            yield return new WaitForSeconds(ATTACK_CONTACT_DELAY);

            bool targetKilled = false;

            if (target != null && !target.IsDestroyed)
            {
                int attackPower = health != null ? health.AttackPower : 1;
                int damageDealt = target.TakeDamageFrom(attackPower, health);

                bool isAlliedAttacker = health != null && health.IsAllied;
                var resourceNode = target.GetComponent<ClockworkCraft.ResourceNode>();
                if (isAlliedAttacker && resourceNode != null && resourceNode.resourceType != ClockworkCraft.ResourceType.None)
                {
                    int lootCount = resourceNode.AccumulateDamage(damageDealt);
                    if (lootCount > 0)
                    {
                        float topY = GridEntityHPBar.GetTopOfObject(target.transform, 0.5f);
                        Vector3 hitPos = target.transform.position + Vector3.up * topY;
                        ClockworkCraft.ResourceType dropType = resourceNode.GetDropResourceType();

                        var lootFX = ClockworkCraft.ResourceLootFX.Instance;
                        if (lootFX != null)
                        {
                            lootFX.SpawnLoot(hitPos, dropType, lootCount);
                            if (GameSFXManager.Instance != null)
                                GameSFXManager.Instance.PlayLootBurst();
                        }
                        else
                        {
                            ClockworkCraft.ResourceManager.Instance?.AddResource(dropType, lootCount);
                        }
                    }
                }

                MealBuffSource mealSource = target.GetComponent<MealBuffSource>();
                if (mealSource != null && !hasMealBuff)
                {
                    GrantMealBuff(ConvertDurationToTicks());

                    if (mealSource.icon != null)
                        ClockworkCraft.IconFlyFX.Instance?.SpawnArc(mealSource.icon, target.transform.position, transform.position);

                    MealBuffVisual existingVisual = GetComponent<MealBuffVisual>();
                    if (existingVisual != null)
                    {
                        existingVisual.Restart();
                    }
                    else
                    {
                        MealBuffVisual newVisual = gameObject.AddComponent<MealBuffVisual>();
                        newVisual.buffIcon = mealSource.icon;
                    }

                    if (verboseLogging)
                        Debug.Log($"[GridEntityActor] {gameObject.name} received meal buff ({mealBuffTicksRemaining} ticks)");
                }

                if (health != null && health.IsAllied && FogManager.Instance != null)
                {
                    FogManager.Instance.RevealCell(targetX, targetY);
                    FogManager.Instance.RevealCell(targetX + 1, targetY);
                    FogManager.Instance.RevealCell(targetX - 1, targetY);
                    FogManager.Instance.RevealCell(targetX, targetY + 1);
                    FogManager.Instance.RevealCell(targetX, targetY - 1);
                }

                targetKilled = target.IsDestroyed;
                if (verboseLogging)
                    Debug.Log($"[GridEntityActor] {gameObject.name} → STRONG interact → {target.gameObject.name} for {damageDealt} damage (target HP: {target.CurrentHP}/{target.MaxHP}){(targetKilled ? " [KILLED]" : "")}");
            }

            if (targetKilled && furnitureObject != null && cachedSlotTakeable)
            {
                yield return StartCoroutine(AdvanceIntoCell(targetX, targetY));
            }
        }

        private void PerformWeakInteraction(int targetX, int targetY)
        {
            FaceTarget(targetX, targetY);

            if (animator != null)
                animator.SetTrigger("interact_weak");

            if (verboseLogging)
                Debug.Log($"[GridEntityActor] {gameObject.name} → WEAK interact → cell ({targetX},{targetY})");
        }

        /// <summary>
        /// Rotate the ROOT transform to face a specific grid cell.
        /// </summary>
        private void FaceTarget(int targetX, int targetY)
        {
            if (furnitureObject == null) return;

            GridManager gm = GridManager.Instance;
            if (gm == null) return;

            Vector3 myWorldPos = gm.GridToWorldPosition(furnitureObject.GridX, furnitureObject.GridY);
            Vector3 targetWorldPos = gm.GridToWorldPosition(targetX, targetY);

            Vector3 direction = (targetWorldPos - myWorldPos).normalized;
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
        }
    }
}
