#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System.Collections.Generic;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// Marker component attached to placed Meal objects.
    /// Provides two buff mechanics:
    /// 1. Direct interaction: Workers that interact with this Meal receive a buff
    /// 2. Aura: Periodic scan within buffRadius grants buff to nearby workers
    /// </summary>
    public class MealBuffSource : MonoBehaviour
    {
        /// <summary>
        /// Icon sprite shown flying from this Feast to a worker when the buff is granted.
        /// Assign the food/meat sprite in the Inspector on the Feast prefab.
        /// </summary>
        public Sprite icon;

        [Tooltip("Radius (in grid cells) within which workers receive the buff aura")]
        public float buffRadius = 3f;

        [Tooltip("How often (in seconds) to scan for nearby workers")]
        public float scanInterval = 1f;

        private float timeSinceLastScan = 0f;
        private FurnitureObject furnitureObject;
        private GridEntityHealth health;
        private HashSet<GridEntityActor> buffedWorkers = new HashSet<GridEntityActor>();

        private void Start()
        {
            furnitureObject = GetComponent<FurnitureObject>();
            health = GetComponent<GridEntityHealth>();
            timeSinceLastScan = Random.value * scanInterval; // Offset scan timing
        }

        private void Update()
        {
            // Only scan while meal is alive
            if (health != null && health.IsDestroyed)
                return;

            timeSinceLastScan += Time.deltaTime;
            if (timeSinceLastScan >= scanInterval)
            {
                timeSinceLastScan = 0f;
                ScanAndBuffWorkers();
            }
        }

        private void ScanAndBuffWorkers()
        {
            if (furnitureObject == null || GridManager.Instance == null)
                return;

            int mealX = furnitureObject.GridX;
            int mealY = furnitureObject.GridY;
            GridManager gm = GridManager.Instance;

            // Clear workers that are no longer in range
            buffedWorkers.RemoveWhere(w => {
                if (w == null) return true;
                var health = w.GetComponent<GridEntityHealth>();
                if (health != null && health.IsDestroyed) return true;
                var furniture = w.GetComponent<FurnitureObject>();
                return !IsInRange(furniture, mealX, mealY);
            });

            // Scan nearby cells for workers
            int radiusCells = Mathf.RoundToInt(buffRadius);
            for (int dx = -radiusCells; dx <= radiusCells; dx++)
            {
                for (int dy = -radiusCells; dy <= radiusCells; dy++)
                {
                    int checkX = mealX + dx;
                    int checkY = mealY + dy;

                    // Distance check
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > buffRadius)
                        continue;

                    GameObject cellOccupant = gm.GetCellOccupant(checkX, checkY);
                    if (cellOccupant == null)
                        continue;

                    GridEntityActor actor = cellOccupant.GetComponent<GridEntityActor>();
                    if (actor == null)
                        continue;

                    GridEntityHealth actorHealth = cellOccupant.GetComponent<GridEntityHealth>();
                    if (actorHealth == null || !actorHealth.IsAllied || actorHealth.IsDestroyed)
                        continue;

                    // Grant buff if not already buffed by this meal
                    if (!buffedWorkers.Contains(actor) && !actor.HasMealBuff)
                    {
                        actor.GrantMealBuff(10);
                        buffedWorkers.Add(actor);
                    }
                }
            }
        }

        private bool IsInRange(FurnitureObject worker, int mealX, int mealY)
        {
            if (worker == null)
                return false;

            int dx = worker.GridX - mealX;
            int dy = worker.GridY - mealY;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            return dist <= buffRadius;
        }

        /// <summary>
        /// Called when the meal is destroyed to clear buff tracking.
        /// </summary>
        public void OnMealDestroyed()
        {
            buffedWorkers.Clear();
        }
    }
}
