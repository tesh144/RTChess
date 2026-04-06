#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;

namespace LittleCafe
{
    /// <summary>
    /// Small directional bump on hit. Pushes the object away from the attacker
    /// then eases it back. Applies offset additively so it doesn't fight with
    /// grid positioning.
    /// </summary>
    public class HitBump : MonoBehaviour
    {
        private const float BumpDistance = 0.12f;
        private const float BumpDuration = 0.25f;

        private Vector3 bumpDirection;
        private Vector3 currentOffset;
        private float timer;
        private bool active;

        public void Trigger(Vector3 direction)
        {
            // Undo any existing offset before starting new bump
            transform.position -= currentOffset;

            bumpDirection = direction.normalized * BumpDistance;
            timer = 0f;
            active = true;
            currentOffset = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (!active) return;

            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / BumpDuration);

            // Quick out, slow return (quadratic ease-out)
            float ease = 1f - t;
            ease = ease * ease;

            Vector3 newOffset = bumpDirection * ease;
            transform.position += (newOffset - currentOffset);
            currentOffset = newOffset;

            if (t >= 1f)
            {
                transform.position -= currentOffset;
                currentOffset = Vector3.zero;
                active = false;
            }
        }
    }
}
