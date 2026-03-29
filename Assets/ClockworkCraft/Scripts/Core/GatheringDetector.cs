#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System.Collections.Generic;

namespace ClockworkCraft
{
    /// <summary>
    /// Flood-fills a planGrid to find all connected groups of same-type environment objects.
    /// Extracted from MapGeneratorV2.DetectGatherings().
    /// </summary>
    public static class GatheringDetector
    {
        /// <summary>
        /// Finds all connected groups of same-type environment objects (4-connected).
        /// Skips center, footprint, unit-prefix, and corruption-prefix cells.
        /// </summary>
        public static List<EnvironmentGathering> DetectGatherings(
            string[,] planGrid, int width, int height)
        {
            var gatherings = new List<EnvironmentGathering>();
            bool[,] visited = new bool[width, height];

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (visited[x, y]) continue;
                string cellName = planGrid[x, y];
                if (string.IsNullOrEmpty(cellName)) continue;
                if (cellName == "__center__" || cellName == "__footprint__") continue;
                if (cellName.StartsWith(MapGenHelpers.UNIT_PREFIX)) continue;
                if (cellName.StartsWith(MapGenHelpers.CORRUPTION_PREFIX)) continue;

                var gathering = FloodFillGathering(x, y, cellName, visited, planGrid, width, height);
                gatherings.Add(gathering);
            }

            Debug.Log($"[GatheringDetector] {gatherings.Count} gatherings found.");
            return gatherings;
        }

        /// <summary>
        /// BFS flood-fill from (startX, startY), collecting all 4-connected cells
        /// sharing the same assetName. Marks cells as visited.
        /// </summary>
        static EnvironmentGathering FloodFillGathering(
            int startX, int startY, string assetName, bool[,] visited,
            string[,] planGrid, int width, int height)
        {
            var cells = new List<Vector2Int>();
            var queue = new Queue<Vector2Int>();

            queue.Enqueue(new Vector2Int(startX, startY));
            visited[startX, startY] = true;

            int sumX = 0, sumY = 0;

            while (queue.Count > 0)
            {
                var pos = queue.Dequeue();
                cells.Add(pos);
                sumX += pos.x;
                sumY += pos.y;

                // 4-connected neighbors (cardinal directions only)
                int nx, ny;

                nx = pos.x + 1; ny = pos.y;
                if (nx < width && !visited[nx, ny] && planGrid[nx, ny] == assetName)
                { visited[nx, ny] = true; queue.Enqueue(new Vector2Int(nx, ny)); }

                nx = pos.x - 1; ny = pos.y;
                if (nx >= 0 && !visited[nx, ny] && planGrid[nx, ny] == assetName)
                { visited[nx, ny] = true; queue.Enqueue(new Vector2Int(nx, ny)); }

                nx = pos.x; ny = pos.y + 1;
                if (ny < height && !visited[nx, ny] && planGrid[nx, ny] == assetName)
                { visited[nx, ny] = true; queue.Enqueue(new Vector2Int(nx, ny)); }

                nx = pos.x; ny = pos.y - 1;
                if (ny >= 0 && !visited[nx, ny] && planGrid[nx, ny] == assetName)
                { visited[nx, ny] = true; queue.Enqueue(new Vector2Int(nx, ny)); }
            }

            // Snap centroid to the nearest actual cell in the cluster
            Vector2Int avgCenter = new Vector2Int(sumX / cells.Count, sumY / cells.Count);
            Vector2Int bestCell = cells[0];
            float bestDist = float.MaxValue;
            for (int i = 0; i < cells.Count; i++)
            {
                float dist = (cells[i] - avgCenter).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestCell = cells[i];
                }
            }

            return new EnvironmentGathering
            {
                assetName = assetName,
                cells = cells,
                centroid = bestCell,
                size = cells.Count
            };
        }
    }
}
