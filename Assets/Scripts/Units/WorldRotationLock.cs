using UnityEngine;

public class WorldRotationLock : MonoBehaviour
{
    [Tooltip("The world rotation to lock to (X and Z only — Y is inherited from parent for facing)")]
    public Vector3 worldEulerRotation;

    void LateUpdate()
    {
        // Get parent's Y rotation (facing direction set by Unit.Rotate)
        float parentY = transform.parent != null ? transform.parent.eulerAngles.y : 0f;

        // Lock X/Z for FBX correction, inherit Y from parent for facing
        transform.rotation = Quaternion.Euler(worldEulerRotation.x, parentY + worldEulerRotation.y, worldEulerRotation.z);
    }
}

