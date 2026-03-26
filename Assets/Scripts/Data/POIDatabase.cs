using System.Collections.Generic;
using UnityEngine;

namespace ClockworkCraft
{
    [CreateAssetMenu(fileName = "POIDatabase", menuName = "RTChess/POI Database")]
    public class POIDatabase : ScriptableObject
    {
        [SerializeField] private List<POITypeData> entries = new List<POITypeData>();

        /// <summary>All entries (for editor sync).</summary>
        public List<POITypeData> Entries => entries;

        /// <summary>
        /// Find the entry whose typeName exactly matches the given assetName (case-insensitive).
        /// typeName stores the real database assetName (e.g. "Tree", "CorruptedHeart").
        /// Returns null if no match.
        /// </summary>
        public POITypeData GetByTypeName(string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) return null;
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.typeName)) continue;
                if (string.Equals(entry.typeName, assetName, System.StringComparison.OrdinalIgnoreCase))
                    return entry;
            }
            return null;
        }
    }
}
