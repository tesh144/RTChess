using UnityEngine;

namespace LittleCafe
{
    /// <summary>
    /// Marker component attached to placed Meal objects.
    /// Workers that interact with a MealBuffSource receive a temporary meal buff.
    /// Workers with an active buff skip meals during target scanning.
    /// </summary>
    public class MealBuffSource : MonoBehaviour
    {
        // Marker only — no additional data needed.
        // Buff duration and effects are managed on GridEntityActor.
    }
}
