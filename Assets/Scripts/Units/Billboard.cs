using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Makes an object always face the camera (billboard effect).
    /// Used for HP bars, floating text, and other UI elements in world space.
    /// </summary>
    public class Billboard : MonoBehaviour
    {
        private Camera mainCam;

        private void Start()
        {
            mainCam = Camera.main;
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
        }
    }
}
