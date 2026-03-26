using System.Collections.Generic;
using UnityEngine;

namespace ClockworkCraft
{
    /// <summary>
    /// A contiguous group of same-type environment objects detected on the planGrid
    /// after map generation. NOT the same as the SpawnMode.Clustered spawn pattern —
    /// a Gathering is any connected group found post-placement, regardless of how
    /// those tiles were originally spawned.
    ///
    /// MapGeneratorV2 detects all gatherings; POIManager filters which ones
    /// qualify as Points of Interest based on POIDatabase rules.
    /// </summary>
    public class EnvironmentGathering
    {
        /// <summary>Environment asset name shared by all cells in this gathering (planGrid key).</summary>
        public string assetName;

        /// <summary>All grid positions belonging to this gathering.</summary>
        public List<Vector2Int> cells;

        /// <summary>Approximate center of the gathering (integer average of cell positions).</summary>
        public Vector2Int centroid;

        /// <summary>Number of cells in the gathering (cells.Count cached for convenience).</summary>
        public int size;
    }
}
