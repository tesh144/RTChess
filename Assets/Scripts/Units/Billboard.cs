using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Makes an object always face the camera (billboard effect).
    /// Used for HP bars, floating text, and other UI elements in world space.
    /// </summary>
    public class Billboard : MonoBehaviour
    {
        [Tooltip("Subtle bobbing amplitude (world units)")]
        [SerializeField] private float bobAmplitude = 0.04f;
        [Tooltip("Bobbing speed (cycles per second)")]
        [SerializeField] private float bobSpeed = 1.2f;

        private Camera mainCam;
        private float bobOffset; // Random phase offset so nearby texts don't sync

        private void Start()
        {
            mainCam = Camera.main;
            bobOffset = Random.Range(0f, Mathf.PI * 2f);
        }

        private void LateUpdate()
        {
            if (mainCam == null)
            {
                mainCam = Camera.main;
                if (mainCam == null) return;
            }

            // Always face the camera
            transform.LookAt(transform.position + mainCam.transform.forward);

            // Subtle bobbing animation
            float bob = Mathf.Sin(Time.time * bobSpeed * Mathf.PI * 2f + bobOffset) * bobAmplitude;
            transform.localPosition = new Vector3(
                transform.localPosition.x,
                transform.localPosition.y - _lastBob + bob,
                transform.localPosition.z
            );
            _lastBob = bob;
        }

        private float _lastBob;
    }
}
