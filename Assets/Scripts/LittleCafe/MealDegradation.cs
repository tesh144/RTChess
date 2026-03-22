#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;

namespace LittleCafe
{
    /// <summary>
    /// Handles Meal HP depletion over time.
    /// Meals naturally degrade as they sit on the map, simulating consumption/spoilage.
    /// This incentivizes workers to collect them before they disappear.
    /// </summary>
    public class MealDegradation : MonoBehaviour
    {
        [Header("Degradation")]
        [Tooltip("Damage per depletion interval (lower = slower degradation)")]
        [SerializeField] private int damagePerInterval = 1;

        [Tooltip("Time between damage applications (in seconds)")]
        [SerializeField] private float depletionInterval = 5f;

        private GridEntityHealth healthComponent;
        private float timeSinceLastDamage = 0f;

        private void Start()
        {
            healthComponent = GetComponent<GridEntityHealth>();
            if (healthComponent == null)
            {
                Debug.LogWarning($"[MealDegradation] {gameObject.name} has no GridEntityHealth component");
                enabled = false;
                return;
            }

            // Randomize first damage to avoid all meals degrading at the same time
            timeSinceLastDamage = Random.value * depletionInterval;
        }

        private void Update()
        {
            if (healthComponent == null || healthComponent.IsDestroyed)
                return;

            timeSinceLastDamage += Time.deltaTime;
            if (timeSinceLastDamage >= depletionInterval)
            {
                timeSinceLastDamage = 0f;
                ApplyDegradation();
            }
        }

        private void ApplyDegradation()
        {
            if (healthComponent != null && !healthComponent.IsDestroyed)
            {
                healthComponent.TakeDamage(damagePerInterval);
            }
        }
    }
}
