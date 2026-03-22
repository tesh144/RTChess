#pragma warning disable CS0414, CS0219, CS0618
namespace ClockworkCraft
{
    public enum TileType
    {
        Empty,          // Grass — passable, buildable
        Tree,           // Wood resource — interactable, chain-clearable
        GoldMine,       // Gold resource — interactable, chain-clearable
        WildFarm,       // Food resource — interactable, chain-clearable
        Rock,           // Stone resource — LOCKED (not interactable at start)
        Water,          // Water resource — interactable if worker has capability
        TownHall,       // Pre-placed anchor building
        Building,       // Player-placed building (set at runtime)
    }

    /// <summary>
    /// All currency/resource types in the game.
    /// Each entry maps to a CurrencyDatabase entry which defines its name, icon, and emoji.
    /// Add new entries here and in the CurrencyDatabase to create new currencies.
    /// </summary>
    public enum ResourceType
    {
        None,
        // ── Core Resources ──────────────────────
        Gold,
        Wood,
        Food,
        Stone,
        Water,
        Clay,
        Flowers,
        // ── Gathered Resources ──────────────────
        Gem,
        Copper,
        Ore,
        WhiteMarble,
        Moonstone,
        Bark,
        Twig,
        Acorn,
        Leaf,
        Grass,
        Petal,
        Rice,
        Coconut,
        // ── Food & Animal Resources ─────────────
        Carrot,
        Tomato,
        Meat,
        Meat2,
        Meat3,
        Boar,
        Fish,
        Pumpkin,
        // ── Special / Abstract ──────────────────
        GoldBag,
        Approval,       // Thumbs up
        Heart,          // Pink heart
    }
}
