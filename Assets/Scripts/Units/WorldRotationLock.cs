using UnityEngine;

public class WorldRotationLock : MonoBehaviour
{
    [Tooltip("The world rotation to lock to")]
    public Vector3 worldEulerRotation;

    private Quaternion targetRotation;

    void Start()
    {
        // Store the desired world rotation
        targetRotation = Quaternion.Euler(worldEulerRotation);
    }

    void LateUpdate()
    {
        // Force world rotation every frame
        transform.rotation = targetRotation;
    }
}

