#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System.Collections;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// GridEntityActor partial — movement logic.
    /// TryMoveForward (cell-by-cell movement for movers), CanWalkOnTile surface checks,
    /// and AdvanceIntoCell (lunge into killed target's cell).
    /// </summary>
    public partial class GridEntityActor
    {
        // ---------------------------------------------------------------
        // Movement — TryMoveForward
        // ---------------------------------------------------------------

        private bool CanWalkOnTile(GridManager gm, int x, int y)
        {
            string walkableStr = walkableSurfaces;
            if (string.IsNullOrEmpty(walkableStr)) walkableStr = "None";

            SurfaceType tileSurface = gm.GetSurface(x, y);

            string[] allowed = walkableStr.Split('+', ',');
            for (int i = 0; i < allowed.Length; i++)
            {
                string s = allowed[i].Trim();
                if (s.Equals("None", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (tileSurface == SurfaceType.None) return true;
                }
                else if (System.Enum.TryParse<SurfaceType>(s, true, out var requiredSurface))
                {
                    if (tileSurface == requiredSurface) return true;
                }
            }
            return false;
        }

        private IEnumerator TryMoveForward()
        {
            if (furnitureObject == null) yield break;

            if (furnitureObject.Shape != null && furnitureObject.Shape.Count > 1)
            {
                Debug.LogError($"[GridEntityActor] TryMoveForward called on multi-cell object '{gameObject.name}' — not supported.");
                yield break;
            }

            GridManager gm = GridManager.Instance;
            if (gm == null) yield break;

            currentFacing.ToGridOffset(out int dx, out int dy);

            int oldX = furnitureObject.GridX;
            int oldY = furnitureObject.GridY;
            int newX = oldX + dx;
            int newY = oldY + dy;

            if (!gm.IsValidCell(newX, newY))
            {
                if (animator != null) animator.SetTrigger("interact_weak");
                yield break;
            }

            if (!CanWalkOnTile(gm, newX, newY))
                yield break;

            if (!gm.IsCellEmpty(newX, newY))
            {
                GameObject occupant = gm.GetCellOccupant(newX, newY);
                if (occupant != null)
                {
                    GridEntityHealth targetHealth = occupant.GetComponent<GridEntityHealth>();
                    if (targetHealth != null && !targetHealth.IsDestroyed)
                    {
                        var interactorType = behaviorType == BehaviorType.RotateAndMoveCorrupted
                            ? ClockworkCraft.InteractorType.Enemy
                            : ClockworkCraft.InteractorType.WildAnimal;

                        bool sameTeam = (targetHealth.IsAllied == false && interactorType == ClockworkCraft.InteractorType.Enemy);
                        if (!sameTeam)
                        {
                            string targetName = occupant.name.Replace("(Clone)", "").Trim();
                            if (ClockworkCraft.InteractionRegistry.Instance != null
                                && ClockworkCraft.InteractionRegistry.Instance.CanInteract(targetName, interactorType))
                            {
                                yield return StartCoroutine(PerformStrongInteraction(targetHealth, newX, newY));
                                yield break;
                            }
                        }
                    }
                }

                if (animator != null) animator.SetTrigger("interact_weak");
                yield break;
            }

            // Execute the move
            isMoving = true;

            CellState myState = gm.GetCellState(oldX, oldY);
            gm.PlaceUnit(newX, newY, gameObject, myState);
            gm.RemoveUnit(oldX, oldY);

            furnitureObject.GridX = newX;
            furnitureObject.GridY = newY;

            if (FogManager.Instance != null)
            {
                FogManager.Instance.RevealCell(newX, newY);
                FogManager.Instance.RevealCell(newX + 1, newY);
                FogManager.Instance.RevealCell(newX - 1, newY);
                FogManager.Instance.RevealCell(newX, newY + 1);
                FogManager.Instance.RevealCell(newX, newY - 1);
            }

            Vector3 startPos = gm.GridToWorldPosition(oldX, oldY);
            Vector3 endPos = gm.GridToWorldPosition(newX, newY);
            startPos.y += 0.01f;
            endPos.y += 0.01f;

            float elapsed = 0f;
            while (elapsed < MOVE_DURATION)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / MOVE_DURATION);
                float easedT = t * t * (3f - 2f * t);
                transform.position = Vector3.Lerp(startPos, endPos, easedT);
                yield return null;
            }

            transform.position = endPos;
            isMoving = false;
        }

        // ---------------------------------------------------------------
        // AdvanceIntoCell — lunge into killed target's cell
        // ---------------------------------------------------------------

        private IEnumerator AdvanceIntoCell(int targetX, int targetY)
        {
            GridManager gm = GridManager.Instance;
            if (gm == null) yield break;

            yield return new WaitForSeconds(0.3f);
            yield return null;

            // Don't advance onto tiles with surfaces (corruption/water/lava)
            if (gm.HasSurface(targetX, targetY)) yield break;

            GameObject occupant = gm.GetCellOccupant(targetX, targetY);
            if (occupant != null && occupant != gameObject)
            {
                GridEntityHealth occupantHealth = occupant.GetComponent<GridEntityHealth>();
                if (occupantHealth != null && occupantHealth.IsDestroyed)
                {
                    gm.RemoveUnit(targetX, targetY);
                }
                else
                {
                    yield break;
                }
            }

            int oldX = furnitureObject.GridX;
            int oldY = furnitureObject.GridY;

            CellState myState = gm.GetCellState(oldX, oldY);
            gm.PlaceUnit(targetX, targetY, gameObject, myState);
            gm.RemoveUnit(oldX, oldY);

            furnitureObject.GridX = targetX;
            furnitureObject.GridY = targetY;

            if (FogManager.Instance != null)
            {
                FogManager.Instance.RevealCell(targetX, targetY);
                FogManager.Instance.RevealCell(targetX + 1, targetY);
                FogManager.Instance.RevealCell(targetX - 1, targetY);
                FogManager.Instance.RevealCell(targetX, targetY + 1);
                FogManager.Instance.RevealCell(targetX, targetY - 1);
            }

            Vector3 startPos = transform.position;
            Vector3 endPos = gm.GridToWorldPosition(targetX, targetY);
            endPos.y += 0.01f;

            float elapsed = 0f;
            while (elapsed < MOVE_DURATION)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / MOVE_DURATION);
                float easedT = t * t * (3f - 2f * t);
                transform.position = Vector3.Lerp(startPos, endPos, easedT);
                yield return null;
            }
            transform.position = endPos;

            if (verboseLogging)
                Debug.Log($"[GridEntityActor] {gameObject.name} advanced into killed target's cell ({targetX},{targetY})");
        }
    }
}
