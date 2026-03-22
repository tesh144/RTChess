#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;

namespace ClockworkCraft
{
    /// <summary>
    /// Configuration data for a single currency type.
    /// Stored in CurrencyDatabase ScriptableObject.
    ///
    /// Each entry defines a resource currency players can earn:
    /// its name, icon sprite, fallback emoji, and associated ResourceType enum.
    /// </summary>
    [System.Serializable]
    public class CurrencyData
    {
        [Header("Identity")]
        [Tooltip("Display name shown in UI (e.g. 'Gold', 'Wood', 'Clay').")]
        public string currencyName;

        [Tooltip("Which ResourceType enum value this maps to. Must be unique per entry.")]
        public ResourceType resourceType = ResourceType.None;

        [Header("Visuals")]
        [Tooltip("Sprite icon for this currency (used in UI bars, loot particles when available).")]
        public Sprite icon;

        [Tooltip("Fallback emoji character used when no sprite is assigned (e.g. for TextMeshPro display).")]
        public string fallbackEmoji = "\u2728"; // ✨ default sparkle

        [Header("Economy")]
        [Tooltip("Starting amount of this currency when a new game begins.")]
        public int startingAmount = 0;

        [Tooltip("Whether workers can interact with nodes of this type from the start.")]
        public bool unlockedAtStart = false;

        /// <summary>
        /// Returns the icon sprite if assigned, otherwise null.
        /// Callers should fall back to fallbackEmoji for text-based display.
        /// </summary>
        public bool HasIcon => icon != null;
    }
}
