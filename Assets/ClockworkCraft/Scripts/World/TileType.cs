namespace ClockworkCraft
{
    public enum TileType
    {
        Empty,          // Grass — passable, buildable
        Tree,           // Wood resource — interactable, chain-clearable
        GoldMine,       // Gold resource — interactable, chain-clearable
        WildFarm,       // Food resource — interactable, chain-clearable
        Rock,           // Stone resource — LOCKED (not interactable at start)
        Water,          // Barrier — LOCKED (not interactable at start)
        TownHall,       // Pre-placed anchor building
        Building,       // Player-placed building (set at runtime)
    }

    public enum ResourceType
    {
        None,
        Gold,
        Wood,
        Food,
        Stone
    }
}
