#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;

namespace LittleCafe
{
    /// <summary>
    /// Marker component attached to placed Meal objects.
    /// Workers that directly interact with this object (via PerformStrongInteraction in GridEntityActor)
    /// receive a meal buff. No passive aura — buff is granted only on direct contact.
    /// </summary>
    public class MealBuffSource : MonoBehaviour
    {
        /// <summary>
        /// Icon sprite shown flying from this Feast to a worker when the buff is granted.
        /// Assign the food/meat sprite in the Inspector on the Feast prefab.
        /// </summary>
        public Sprite icon;
    }
}
