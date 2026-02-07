namespace ClockworkGrid
{
    /// <summary>
    /// Interface for anything that can take damage (Units, ResourceNodes, etc.)
    /// </summary>
    public interface IDamageable
    {
        int CurrentHP { get; }
        int MaxHP { get; }

        /// <summary>
        /// Deal damage to this entity. Returns actual damage dealt.
        /// </summary>
        int TakeDamage(int damage);

        bool IsDestroyed { get; }
    }
}
