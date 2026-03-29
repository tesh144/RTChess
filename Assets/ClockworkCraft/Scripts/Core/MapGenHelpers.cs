#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System.Collections.Generic;

namespace ClockworkCraft
{
    /// <summary>
    /// Static helpers shared by MapPlanner, CorruptionPlanner, MapSpawner, and MapGeneratorV2.
    /// Extracted from MapGeneratorV2 to reduce file size.
    /// </summary>
    public static class MapGenHelpers
    {
        // Unit plan entries are stored with a "unit:" prefix in planGrid
        // so they don't collide with environment names.
        public const string UNIT_PREFIX = "unit:";

        // Corruption entities use a "corruption:" prefix in planGrid so they
        // don't collide with environment names or the "unit:" prefix.
        public const string CORRUPTION_PREFIX = "corruption:";

        public static bool IsInClearing(int x, int y, Vector2Int center, int clearCenterCardinal)
        {
            int dx = Mathf.Abs(x - center.x);
            int dy = Mathf.Abs(y - center.y);

            // Center tile itself
            if (dx == 0 && dy == 0) return true;

            // Plus/cross shape — cardinal directions only, no diagonals
            bool onCardinal = (dx == 0 || dy == 0);
            return onCardinal && (dx + dy) <= clearCenterCardinal;
        }

        public static bool IsTooClose(int x, int y, List<Vector2Int> placed, int minSpacing, System.Random rng)
        {
            // Spacing is a SOFT guideline, not a hard rule.
            // Higher override chance = more natural clumping + variation.
            // Closer tiles have slightly lower override chance to prevent
            // everything piling on top of each other.
            int sqThreshold = minSpacing * minSpacing;
            foreach (var p in placed)
            {
                int dx = x - p.x;
                int dy = y - p.y;
                int sqDist = dx * dx + dy * dy;
                if (sqDist <= sqThreshold)
                {
                    // Scale override chance by how close we are:
                    // Adjacent (dist=1): 30% chance to allow
                    // At spacing boundary: 45% chance to allow
                    float t = sqThreshold > 1 ? (float)sqDist / sqThreshold : 0f;
                    float overrideChance = Mathf.Lerp(0.30f, 0.45f, t);
                    if ((float)rng.NextDouble() < overrideChance)
                        continue; // break the rule this time
                    return true;
                }
            }
            return false;
        }

        public static void ShuffleList<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        public static ResourceType GuessResourceType(string envName)
        {
            if (envName == null) return ResourceType.None;
            string lower = envName.ToLowerInvariant();
            if (lower.Contains("tree") || lower.Contains("wood"))  return ResourceType.Wood;
            if (lower.Contains("gold") || lower.Contains("mine"))  return ResourceType.Gold;
            if (lower.Contains("farm") || lower.Contains("food"))  return ResourceType.Food;
            if (lower.Contains("rock") || lower.Contains("stone")) return ResourceType.Stone;
            if (lower.Contains("water") || lower.Contains("lake") || lower.Contains("river")) return ResourceType.Water;
            if (lower.Contains("clay") || lower.Contains("mud"))     return ResourceType.Clay;
            if (lower.Contains("flower") || lower.Contains("flora")) return ResourceType.Flowers;
            return ResourceType.None;
        }
    }
}
