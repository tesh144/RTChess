#pragma warning disable CS0414, CS0219, CS0618
namespace ClockworkGrid
{
    /// <summary>
    /// Defines the clockwork behavior pattern for active grid entities.
    /// Selected per-entity in its database entry and applied by GridEntityActor.
    /// </summary>
    public enum BehaviorType
    {
        /// <summary>
        /// Default worker behavior: rotate → scan facing direction → interact with target.
        /// Workers attack/interact with whatever they find in their line of sight.
        /// </summary>
        RotateAndInteract = 0,

        /// <summary>
        /// Animal/beast behavior: rotate → attempt to move one cell forward.
        /// If the cell ahead is empty, move into it. If blocked, idle until next tick.
        /// </summary>
        RotateAndMove = 1,

        /// <summary>
        /// Heavy beast behavior: rotate → rotate again → attempt to move one cell forward.
        /// Two rotations per tick give a more deliberate, lumbering movement pattern.
        /// </summary>
        RotateRotateMove = 2,

        /// <summary>
        /// Corrupted mover behavior: rotate → attempt to move one cell forward, but ONLY
        /// if the target tile is corrupted. If the tile ahead is empty but not corrupted,
        /// the move action is silently skipped (no bump). Attack logic is identical to RotateAndMove.
        /// </summary>
        RotateAndMoveCorrupted = 3
    }
}
