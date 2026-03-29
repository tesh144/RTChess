#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System.Collections.Generic;

namespace ClockworkCraft
{
    /// <summary>
    /// Plans corruption entity placement on the map grid.
    /// Extracted from MapGeneratorV2.
    /// </summary>
    public class CorruptionPlanner
    {
        private readonly string[,] planGrid;
        private readonly System.Random rng;
        private readonly int width, height;
        private readonly Vector2Int center;
        private readonly int clearCenterCardinal;

        public CorruptionPlanner(string[,] planGrid, System.Random rng,
            int width, int height, Vector2Int center, int clearCenterCardinal)
        {
            this.planGrid = planGrid;
            this.rng = rng;
            this.width = width;
            this.height = height;
            this.center = center;
            this.clearCenterCardinal = clearCenterCardinal;
        }

        private bool IsInClearing(int x, int y) => MapGenHelpers.IsInClearing(x, y, center, clearCenterCardinal);
        private void ShuffleList<T>(List<T> list) => MapGenHelpers.ShuffleList(list, rng);

        /// <summary>
        /// Plans corruption entity placement into planGrid.
        /// Called after PlaceAllEntries() so corruption entities respect
        /// already-placed environment and unit cells.
        /// </summary>
        public void PlaceCorruptionEntities(List<CorruptionSpawnEntry> entries)
        {
            if (entries == null) return;

            foreach (var entry in entries)
            {
                if (entry.spawnCount <= 0) continue;

                if (entry.prefab == null)
                {
                    Debug.LogWarning($"[CorruptionPlanner] Entry '{entry.entityName}' has no prefab — skipping.");
                    continue;
                }

                string planName = MapGenHelpers.CORRUPTION_PREFIX + entry.entityName;

                switch (entry.spawnMode)
                {
                    case SpawnMode.Scattered:
                        PlaceCorruptionScattered(entry, planName);
                        break;
                    case SpawnMode.Clustered:
                        PlaceCorruptionClustered(entry, planName);
                        break;
                    case SpawnMode.Edge:
                        PlaceCorruptionEdge(entry, planName);
                        break;
                }
            }
        }

        private void PlaceCorruptionScattered(CorruptionSpawnEntry entry, string planName)
        {
            // Build candidate list: empty cells, outside clearing, beyond clearFromCenter
            var candidates = new List<Vector2Int>();
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (planGrid[x, y] != null) continue;
                if (IsInClearing(x, y)) continue;

                int dx = Mathf.Abs(x - center.x);
                int dy = Mathf.Abs(y - center.y);
                if (Mathf.Max(dx, dy) < entry.clearFromCenter) continue;

                candidates.Add(new Vector2Int(x, y));
            }

            // Fisher-Yates shuffle for determinism with the map's seeded RNG
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = candidates[i]; candidates[i] = candidates[j]; candidates[j] = tmp;
            }

            // Place with Chebyshev spacing constraint
            var placed = new List<Vector2Int>();
            foreach (var pos in candidates)
            {
                if (placed.Count >= entry.spawnCount) break;

                bool tooClose = false;
                foreach (var p in placed)
                {
                    int dx = Mathf.Abs(pos.x - p.x);
                    int dy = Mathf.Abs(pos.y - p.y);
                    if (Mathf.Max(dx, dy) < entry.minSpacing) { tooClose = true; break; }
                }
                if (tooClose) continue;

                planGrid[pos.x, pos.y] = planName;
                placed.Add(pos);
            }

            if (placed.Count < entry.spawnCount)
                Debug.LogWarning($"[CorruptionPlanner] '{entry.entityName}': only placed {placed.Count}/{entry.spawnCount} (Scattered) — not enough valid candidates.");
            else
                Debug.Log($"[CorruptionPlanner] '{entry.entityName}': planned {placed.Count} (Scattered).");
        }

        private void PlaceCorruptionClustered(CorruptionSpawnEntry entry, string planName)
        {
            Vector2Int[] dirs = {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1)
            };

            int tileBudget = entry.spawnCount;
            if (tileBudget <= 0) return;

            float frag   = Mathf.Clamp01(entry.fragmentation);
            float spread = Mathf.Clamp01(entry.clusterSpread);
            int clusterCount = Mathf.Max(2, Mathf.RoundToInt(Mathf.Lerp(2f, 8f, frag)));
            int tilesPerCluster = Mathf.Max(1, tileBudget / clusterCount);
            int placed = 0;

            for (int c = 0; c < clusterCount && placed < tileBudget; c++)
            {
                Vector2Int seed = Vector2Int.zero;
                bool found = false;
                for (int a = 0; a < 80; a++)
                {
                    int sx = rng.Next(width), sy = rng.Next(height);
                    int ddx = Mathf.Abs(sx - center.x);
                    int ddy = Mathf.Abs(sy - center.y);
                    if (planGrid[sx, sy] == null && !IsInClearing(sx, sy)
                        && Mathf.Max(ddx, ddy) >= entry.clearFromCenter)
                    {
                        seed = new Vector2Int(sx, sy);
                        found = true;
                        break;
                    }
                }
                if (!found) continue;

                // BFS blob
                var queue   = new Queue<Vector2Int>();
                var visited = new HashSet<Vector2Int>();
                queue.Enqueue(seed);
                visited.Add(seed);
                int grown = 0;

                while (queue.Count > 0 && grown < tilesPerCluster && placed < tileBudget)
                {
                    var current = queue.Dequeue();
                    if (planGrid[current.x, current.y] == null && !IsInClearing(current.x, current.y))
                    {
                        planGrid[current.x, current.y] = planName;
                        placed++;
                        grown++;
                    }
                    foreach (var d in dirs)
                    {
                        var nb = current + d;
                        if (nb.x < 0 || nb.x >= width || nb.y < 0 || nb.y >= height) continue;
                        if (visited.Contains(nb)) continue;
                        visited.Add(nb);
                        if (planGrid[nb.x, nb.y] != null || IsInClearing(nb.x, nb.y)) continue;
                        if ((float)rng.NextDouble() > spread) continue;
                        queue.Enqueue(nb);
                    }
                }
            }

            Debug.Log($"[CorruptionPlanner] '{entry.entityName}': planned {placed}/{tileBudget} (Clustered).");
        }

        private void PlaceCorruptionEdge(CorruptionSpawnEntry entry, string planName)
        {
            if (string.IsNullOrEmpty(entry.edgeBorderOf))
            {
                Debug.LogWarning($"[CorruptionPlanner] '{entry.entityName}' uses Edge mode but edgeBorderOf is empty — falling back to Scattered.");
                PlaceCorruptionScattered(entry, planName);
                return;
            }

            Vector2Int[] dirs = {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1)
            };

            var edgeCells = new List<Vector2Int>();
            string borderTarget      = entry.edgeBorderOf;
            string unitBorderTarget  = MapGenHelpers.UNIT_PREFIX + borderTarget;
            string corrBorderTarget  = MapGenHelpers.CORRUPTION_PREFIX + borderTarget;

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (planGrid[x, y] != null) continue;
                if (IsInClearing(x, y)) continue;
                int ddx = Mathf.Abs(x - center.x);
                int ddy = Mathf.Abs(y - center.y);
                if (Mathf.Max(ddx, ddy) < entry.clearFromCenter) continue;

                bool adjacent = false;
                foreach (var d in dirs)
                {
                    int nx = x + d.x, ny = y + d.y;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                    string nb = planGrid[nx, ny];
                    if (nb == borderTarget || nb == unitBorderTarget || nb == corrBorderTarget)
                    {
                        adjacent = true;
                        break;
                    }
                }
                if (adjacent) edgeCells.Add(new Vector2Int(x, y));
            }

            ShuffleList(edgeCells);

            var placed = new List<Vector2Int>();
            foreach (var pos in edgeCells)
            {
                if (placed.Count >= entry.spawnCount) break;

                bool tooClose = false;
                foreach (var p in placed)
                {
                    int ddx = Mathf.Abs(pos.x - p.x);
                    int ddy = Mathf.Abs(pos.y - p.y);
                    if (Mathf.Max(ddx, ddy) < entry.minSpacing) { tooClose = true; break; }
                }
                if (tooClose) continue;

                planGrid[pos.x, pos.y] = planName;
                placed.Add(pos);
            }

            Debug.Log($"[CorruptionPlanner] '{entry.entityName}': planned {placed.Count}/{entry.spawnCount} (Edge near '{borderTarget}').");
        }
    }
}
