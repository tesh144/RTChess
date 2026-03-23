#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;

namespace LittleCafe
{
    /// <summary>
    /// Time-based expiry for placed Feast objects.
    /// After <see cref="lifetimeSeconds"/> the feast deals itself fatal damage,
    /// triggering the standard GridEntityHealth death path.
    /// Added at runtime by DragDropHandler when isMealSource is true.
    /// </summary>
    public class FeastVisualDegradation : MonoBehaviour
    {
        [Header("Lifetime")]
        [Tooltip("How long (seconds) before the Feast expires and destroys itself.")]
        public float lifetimeSeconds = 60f;

        private GridEntityHealth health;
        private float            elapsed = 0f;
        private bool             expired = false;

        private void Start()
        {
            health = GetComponent<GridEntityHealth>();
        }

        private void Update()
        {
            if (expired) return;
            if (health != null && health.IsDestroyed) return;

            elapsed += Time.deltaTime;

            if (elapsed >= lifetimeSeconds)
            {
                expired = true;
                if (health != null)
                    health.TakeDamage(health.MaxHP);
            }
        }
    }
}
