namespace ClockworkGrid
{
    public enum Facing
    {
        North = 0,   // Y rotation = 0°   (facing +Z)
        East = 90,   // Y rotation = 90°  (facing +X)
        South = 180, // Y rotation = 180° (facing -Z)
        West = 270   // Y rotation = 270° (facing -X)
    }

    public static class FacingExtensions
    {
        public static Facing RotateClockwise(this Facing facing)
        {
            return facing switch
            {
                Facing.North => Facing.East,
                Facing.East => Facing.South,
                Facing.South => Facing.West,
                Facing.West => Facing.North,
                _ => Facing.North
            };
        }

        public static Facing RotateCounterClockwise(this Facing facing)
        {
            return facing switch
            {
                Facing.North => Facing.West,
                Facing.West => Facing.South,
                Facing.South => Facing.East,
                Facing.East => Facing.North,
                _ => Facing.North
            };
        }

        public static float ToYRotation(this Facing facing)
        {
            return (float)facing;
        }

        /// <summary>
        /// Returns the grid coordinate offset (dx, dy) for this facing direction.
        /// North = +Z = (0, +1), East = +X = (+1, 0), etc.
        /// </summary>
        public static void ToGridOffset(this Facing facing, out int dx, out int dy)
        {
            switch (facing)
            {
                case Facing.North: dx = 0; dy = 1; break;
                case Facing.East:  dx = 1; dy = 0; break;
                case Facing.South: dx = 0; dy = -1; break;
                case Facing.West:  dx = -1; dy = 0; break;
                default: dx = 0; dy = 0; break;
            }
        }
    }
}
