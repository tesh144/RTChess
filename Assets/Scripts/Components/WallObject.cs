#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;

namespace LittleCafe
{
    /// <summary>
    /// Wall furniture - blocks movement and pathfinding.
    /// Simple component with no additional behavior beyond FurnitureObject base.
    /// </summary>
    public class WallObject : FurnitureObject
    {
        /// <summary>
        /// Walls are always non-walkable.
        /// </summary>
        public override bool IsWalkable => false;

        private void Awake()
        {
            // Ensure wall properties are correct
            // (isWalkableDefault should already be false by default)
        }
    }
}
