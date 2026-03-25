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
        /// Find the first entry whose typeName appears (case-insensitive) inside the given assetName.
        /// E.g. assetName "PineTree_01" matches typeName "Tree".
        /// Returns null if no match.
        /// </summary>
        public POITypeData GetByTypeName(string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) return null;
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.typeName)) continue;
                if (assetName.IndexOf(entry.typeName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return entry;
            }
            return null;
        }
    }
}
