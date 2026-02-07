using UnityEngine;

namespace ClockworkGrid
{
    public class FixWorldRotation : MonoBehaviour
    {
        private Quaternion fixedRotation;

        private void Awake()
        {
            fixedRotation = transform.rotation;
        }

        private void LateUpdate()
        {
            transform.rotation = fixedRotation;
        }
    }
}
