namespace ClockworkCraft
{
    /// <summary>
    /// Selects which visual child is active on a BubblePopup prefab.
    /// Child GameObjects must be named exactly as the enum values.
    /// </summary>
    public enum BubbleType
    {
        POI_Gold,       // Valuable POI (goldmine, treasure)
        POI_Grey,       // Neutral POI (rocks, bones, flowers)
        POI_Red,        // Danger POI (corruption hearts)
        Bubble_Insert,  // HoldToFill resource input (future)
        Bubble_Collect, // Output ready to collect (future)
        Bubble_Alert    // Attention/problem state (future)
    }
}
