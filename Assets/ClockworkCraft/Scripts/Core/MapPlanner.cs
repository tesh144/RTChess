#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using LittleCafe;

namespace ClockworkCraft
{
    /// <summary>
    /// Plans environment and unit placement on the map grid.
    /// Extracted from MapGeneratorV2 — contains all placement algorithms.
    /// </summary>
    public class MapPlanner
    {
        private readonly string[,] planGrid;
        private readonly System.Random rng;
        private readonly int width, height;
        private readonly Vector2Int center;
        private readonly int clearCenterCardinal;

        public MapPlanner(string[,] planGrid, System.Random rng,
            int width, int height, Vector2Int center, int clearCenterCardinal)
        {
            this.planGrid = planGrid;
            this.rng = rng;
            this.width = width;
            this.height = height;
            this.center = center;
            this.clearCenterCardinal = clearCenterCardinal;
        }

        // ─────────────────────────────────────────────────────────────────
        // Helper wrappers (keep call sites clean)
        // ─────────────────────────────────────────────────────────────────

        private bool IsInClearing(int x, int y) => MapGenHelpers.IsInClearing(x, y, center, clearCenterCardinal);
        private bool IsTooClose(int x, int y, List<Vector2Int> placed, int minSpacing) => MapGenHelpers.IsTooClose(x, y, placed, minSpacing, rng);
        private void ShuffleList<T>(List<T> list) => MapGenHelpers.ShuffleList(list, rng);

        // ─────────────────────────────────────────────────────────────────
        // Public entry point
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Budget-based placement. mapDensity controls total tiles filled.
        /// Each entry's spawnWeight (1-20) determines its share of the budget.
        ///
        /// Order: Clustered → Edge → Scattered (so edges can reference clusters).
        /// Each entry gets a tile budget = (weight / totalWeight) * totalBudget.
        /// </summary>
        public void PlaceAllEntries(
            List<EnvironmentSpawnEntry> envEntries, EnvironmentDatabase envDB,
            List<UnitSpawnEntry> unitEntries, UnitDatabase unitDB,
            float mapDensity)
        {
            // Validate environment entries
            var valid = new List<(EnvironmentSpawnEntry entry, EnvironmentData data)>();
            if (envDB != null)
            {
                foreach (var entry in envEntries)
                {
                    if (entry.spawnWeight <= 0f) continue;
                    if (entry.spawnMode == SpawnMode.Edge && string.IsNullOrEmpty(entry.edgeBorderOf)) continue;
                    if (entry.spawnMode == SpawnMode.OnTop) continue; // handled in separate pass
                    var data = envDB.GetByName(entry.environmentName);
                    if (data == null || data.prefab == null) continue;
                    valid.Add((entry, data));
                }
            }

            // Validate unit entries
            var validUnits = new List<(UnitSpawnEntry entry, UnitData data)>();
            if (unitDB != null)
            {
                foreach (var entry in unitEntries)
                {
                    if (entry.spawnWeight <= 0f) continue;
                    if (entry.spawnMode == SpawnMode.Edge && string.IsNullOrEmpty(entry.edgeBorderOf)) continue;
                    var data = unitDB.GetByName(entry.unitName);
                    if (data == null || data.prefab == null) continue;
                    validUnits.Add((entry, data));
                }
            }

            if (valid.Count == 0 && validUnits.Count == 0)
            {
                Debug.LogWarning("[MapPlanner] No valid spawn entries.");
                return;
            }

            // Calculate total tile budget from mapDensity (shared by env + units)
            int clearingSize = (2 * clearCenterCardinal + 1) * (2 * clearCenterCardinal + 1);
            int availableTiles = (width * height) - clearingSize;
            int totalBudget = Mathf.RoundToInt(availableTiles * mapDensity);

            // Combined weight pool: environment + unit entries share the same budget
            float totalWeight = valid.Sum(v => v.entry.spawnWeight)
                              + validUnits.Sum(v => v.entry.spawnWeight);

            // Per-entry budgets (environment)
            var entryBudgets = new Dictionary<string, int>();
            foreach (var (entry, _) in valid)
            {
                int budget = Mathf.RoundToInt((entry.spawnWeight / totalWeight) * totalBudget);
                entryBudgets[entry.environmentName] = budget;
            }

            // Per-entry budgets (units) — stored with UNIT_PREFIX key
            var unitBudgets = new Dictionary<string, int>();
            foreach (var (entry, _) in validUnits)
            {
                int budget = Mathf.RoundToInt((entry.spawnWeight / totalWeight) * totalBudget);
                unitBudgets[entry.unitName] = budget;
            }

            Debug.Log($"[MapPlanner] Density {mapDensity:P0}: total budget={totalBudget} tiles, {valid.Count} env + {validUnits.Count} units, weights sum={totalWeight:F1}");

            // ── Pass 1: Clustered ─────────────────────────────────────
            foreach (var (entry, _) in valid.Where(v => v.entry.spawnMode == SpawnMode.Clustered))
            {
                int budget = entryBudgets[entry.environmentName];
                PlaceClusters(entry, budget);
            }

            // ── Pass 2: Edge ──────────────────────────────────────────
            foreach (var (entry, _) in valid.Where(v => v.entry.spawnMode == SpawnMode.Edge))
            {
                int budget = entryBudgets[entry.environmentName];
                PlaceEdges(entry, budget);
            }

            // ── Pass 3: Scattered ─────────────────────────────────────
            var scattered = valid.Where(v => v.entry.spawnMode == SpawnMode.Scattered).ToList();
            if (scattered.Count > 0)
            {
                var scatteredBudgets = new Dictionary<string, int>();
                foreach (var (entry, _) in scattered)
                    scatteredBudgets[entry.environmentName] = entryBudgets[entry.environmentName];
                PlaceScattered(scattered, scatteredBudgets);
            }

            // ── Pass 4: Clustered units ──────────────────────────────
            foreach (var (entry, _) in validUnits.Where(v => v.entry.spawnMode == SpawnMode.Clustered))
            {
                int budget = unitBudgets[entry.unitName];
                PlaceUnitClusters(entry, budget);
            }

            // ── Pass 5: Edge units ───────────────────────────────────
            foreach (var (entry, _) in validUnits.Where(v => v.entry.spawnMode == SpawnMode.Edge))
            {
                int budget = unitBudgets[entry.unitName];
                PlaceUnitEdges(entry, budget);
            }

            // ── Pass 6: Scattered units ──────────────────────────────
            var scatteredUnits = validUnits.Where(v => v.entry.spawnMode == SpawnMode.Scattered).ToList();
            if (scatteredUnits.Count > 0)
            {
                PlaceUnitScattered(scatteredUnits, unitBudgets);
            }

            // ── Pass 7: Fill remaining budget ─────────────────────────
            // If spacing or BFS limits prevented entries from filling their
            // budgets, redistribute remaining tiles proportionally.
            // Excludes Clustered entries to prevent orphan singles.
            // Respects spacing constraints for Scattered entries.
            int placedSoFar = 0;
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (planGrid[x, y] != null && planGrid[x, y] != "__center__") placedSoFar++;

            int remainingBudget = totalBudget - placedSoFar;
            if (remainingBudget > 0)
            {
                var emptyForFill = new List<Vector2Int>();
                for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    if (planGrid[x, y] == null && !IsInClearing(x, y))
                        emptyForFill.Add(new Vector2Int(x, y));

                ShuffleList(emptyForFill);

                var fillable = valid.Where(v => v.entry.spawnMode != SpawnMode.Clustered).ToList();
                if (fillable.Count == 0) fillable = valid.ToList();

                // Build existing placement map for spacing checks
                // (scan planGrid for positions of each fillable entry)
                var fillPositions = new Dictionary<string, List<Vector2Int>>();
                foreach (var (entry, _) in fillable)
                    fillPositions[entry.environmentName] = new List<Vector2Int>();

                for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    string name = planGrid[x, y];
                    if (name != null && fillPositions.ContainsKey(name))
                        fillPositions[name].Add(new Vector2Int(x, y));
                }

                int filled = 0;
                foreach (var pos in emptyForFill)
                {
                    if (filled >= remainingBudget) break;

                    // Weighted shuffle so we try alternatives if spacing rejects first pick
                    var candidates = new List<(EnvironmentSpawnEntry entry, EnvironmentData data)>(fillable);
                    // Simple weighted shuffle inline
                    for (int i = 0; i < candidates.Count - 1; i++)
                    {
                        float tw = 0f;
                        for (int j = i; j < candidates.Count; j++) tw += candidates[j].entry.spawnWeight;
                        float r = (float)(rng.NextDouble() * tw);
                        float c = 0f;
                        int p = i;
                        for (int j = i; j < candidates.Count; j++)
                        {
                            c += candidates[j].entry.spawnWeight;
                            if (r < c) { p = j; break; }
                        }
                        if (p != i) { var tmp = candidates[i]; candidates[i] = candidates[p]; candidates[p] = tmp; }
                    }

                    bool placed = false;
                    foreach (var (entry, _) in candidates)
                    {
                        // Respect spacing for scattered entries
                        if (entry.minSpacing > 0 &&
                            IsTooClose(pos.x, pos.y, fillPositions[entry.environmentName], entry.minSpacing))
                            continue;

                        planGrid[pos.x, pos.y] = entry.environmentName;
                        fillPositions[entry.environmentName].Add(pos);
                        filled++;
                        placed = true;
                        break;
                    }
                }
                Debug.Log($"[MapPlanner] Fill pass: placed {filled} extra tiles to meet density target (budget gap was {remainingBudget})");
            }

            // Log final placement counts
            int totalPlaced = 0;
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (planGrid[x, y] != null && planGrid[x, y] != "__center__") totalPlaced++;

            Debug.Log($"[MapPlanner] Plan complete: {totalPlaced} tiles placed (budget was {totalBudget}, {(float)totalPlaced / availableTiles:P1} actual coverage)");
        }

        /// <summary>
        /// Second-pass placement for OnTop entries. Scans planGrid for tiles
        /// matching each entry's requiredSurface and places objects into a
        /// separate onTopPlanGrid at the configured coverage percentage.
        /// Must be called AFTER PlaceAllEntries so surface tiles are already planned.
        /// </summary>
        public void PlaceOnTopEntries(
            List<EnvironmentSpawnEntry> envEntries, EnvironmentDatabase envDB,
            string[,] onTopPlanGrid)
        {
            if (envDB == null) return;

            var onTopEntries = new List<(EnvironmentSpawnEntry entry, EnvironmentData data)>();
            foreach (var entry in envEntries)
            {
                if (entry.spawnMode != SpawnMode.OnTop) continue;
                var data = envDB.GetByName(entry.environmentName);
                if (data == null || data.prefab == null) continue;
                onTopEntries.Add((entry, data));
            }

            if (onTopEntries.Count == 0) return;

            // Build a lookup: for each environment name, what SurfaceType does it map to?
            // Surface entries in planGrid are stored by their environment name (e.g. "Water").
            // We need to know which planGrid names correspond to which SurfaceType.
            // Mirrors the mapping logic in MapGeneratorV2.PlaceOnCorrectLayer().
            var nameToSurface = new Dictionary<string, ClockworkGrid.SurfaceType>();
            foreach (var envData in envDB.AllEnvironment)
            {
                if (envData.layerType != LittleCafe.EnvironmentLayerType.Surface) continue;
                string lower = envData.assetName.ToLowerInvariant();
                if (lower.Contains("corrupt"))
                    nameToSurface[envData.assetName] = ClockworkGrid.SurfaceType.Corruption;
                else if (lower.Contains("lava"))
                    nameToSurface[envData.assetName] = ClockworkGrid.SurfaceType.Lava;
                else
                    nameToSurface[envData.assetName] = ClockworkGrid.SurfaceType.Water;
            }

            foreach (var (entry, data) in onTopEntries)
            {
                // Find all planGrid tiles with a surface matching requiredSurface
                var qualifying = new List<Vector2Int>();
                for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    string cellName = planGrid[x, y];
                    if (cellName == null) continue;
                    if (!nameToSurface.TryGetValue(cellName, out var surfType)) continue;
                    if (surfType != entry.requiredSurface) continue;
                    // Skip if onTopPlanGrid already has something here
                    if (onTopPlanGrid[x, y] != null) continue;
                    qualifying.Add(new Vector2Int(x, y));
                }

                if (qualifying.Count == 0)
                {
                    Debug.Log($"[MapPlanner] OnTop '{entry.environmentName}': 0 qualifying {entry.requiredSurface} tiles found");
                    continue;
                }

                int targetCount = Mathf.RoundToInt(qualifying.Count * entry.coveragePercent);
                if (targetCount <= 0) continue;

                ShuffleList(qualifying);

                var placed = new List<Vector2Int>();
                foreach (var pos in qualifying)
                {
                    if (placed.Count >= targetCount) break;

                    if (entry.minSpacing > 0 && IsTooClose(pos.x, pos.y, placed, entry.minSpacing))
                        continue;

                    onTopPlanGrid[pos.x, pos.y] = entry.environmentName;
                    placed.Add(pos);
                }

                Debug.Log($"[MapPlanner] OnTop '{entry.environmentName}' on {entry.requiredSurface}: {placed.Count} tiles (target={targetCount}, qualifying={qualifying.Count})");
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Environment placement
        // ─────────────────────────────────────────────────────────────────

        // ── Clustered placement ──────────────────────────────────────
        //
        // Fragmentation (0-1) controls the feel:
        //   0.0 = 2 large cohesive blobs, no loose pieces
        //   0.5 = ~5 medium blobs + 15% loose break-off tiles nearby
        //   1.0 = ~12 small blobs + 40% loose scatter near edges
        //
        // Spread (0-1) controls blob shape only (round vs stringy).

        private void PlaceClusters(EnvironmentSpawnEntry entry, int tileBudget)
        {
            if (tileBudget <= 0) return;

            float frag = Mathf.Clamp01(entry.fragmentation);
            float spread = Mathf.Clamp01(entry.clusterSpread);

            // Derive cluster count from fragmentation: 3 at frag=0, up to 12 at frag=1
            // Minimum 3 ensures decent spatial coverage on a 40x40 map
            int clusterCount = Mathf.Max(3, Mathf.RoundToInt(Mathf.Lerp(3f, 12f, frag)));

            // Derive loose scatter fraction: 0% at frag<0.25, up to 40% at frag=1
            // Low frag entries (like water pools) get NO loose singles
            float looseFraction = frag < 0.25f ? 0f : Mathf.Lerp(0f, 0.4f, frag);
            int looseBudget = Mathf.RoundToInt(tileBudget * looseFraction);
            int blobBudget  = tileBudget - looseBudget;

            // ── Phase 1: BFS blobs with zone-based seeding ──────────
            int blobPlaced = 0;
            var allClusterCells = new List<Vector2Int>();

            int zonesPerSide = Mathf.Max(2, Mathf.CeilToInt(Mathf.Sqrt(clusterCount)));
            var zoneSeedCandidates = GetZoneSeeds(zonesPerSide);

            int maxAttempts = clusterCount + 10;
            for (int c = 0; c < maxAttempts && blobPlaced < blobBudget; c++)
            {
                int targetSize;
                if (c < clusterCount)
                {
                    int remainingClusters = clusterCount - c;
                    targetSize = Mathf.Max(1, Mathf.RoundToInt((float)(blobBudget - blobPlaced) / remainingClusters));
                    int minSize = Mathf.Max(1, Mathf.RoundToInt(targetSize * 0.7f));
                    int maxSize = Mathf.RoundToInt(targetSize * 1.3f);
                    targetSize = minSize + (maxSize > minSize ? rng.Next(maxSize - minSize + 1) : 0);
                }
                else
                {
                    targetSize = blobBudget - blobPlaced;
                }
                targetSize = Mathf.Min(targetSize, blobBudget - blobPlaced);

                // Pick seed: use zone-based for first seeds, random for overflow
                Vector2Int seed = Vector2Int.zero;
                bool foundSeed = false;

                if (c < zoneSeedCandidates.Count)
                {
                    var zoneList = zoneSeedCandidates[c];
                    foreach (var candidate in zoneList)
                    {
                        if (planGrid[candidate.x, candidate.y] == null && !IsInClearing(candidate.x, candidate.y))
                        {
                            seed = candidate;
                            foundSeed = true;
                            break;
                        }
                    }
                }

                if (!foundSeed)
                {
                    for (int attempt = 0; attempt < 80; attempt++)
                    {
                        int sx = rng.Next(width);
                        int sy = rng.Next(height);
                        if (planGrid[sx, sy] == null && !IsInClearing(sx, sy))
                        {
                            seed = new Vector2Int(sx, sy);
                            foundSeed = true;
                            break;
                        }
                    }
                }
                if (!foundSeed) break;

                // BFS expansion
                var frontier = new List<Vector2Int> { seed };
                var visited  = new HashSet<Vector2Int> { seed };
                var deferred = new List<Vector2Int>();
                planGrid[seed.x, seed.y] = entry.environmentName;
                allClusterCells.Add(seed);
                int grown = 1;

                Vector2Int[] dirs = {
                    new Vector2Int(1, 0), new Vector2Int(-1, 0),
                    new Vector2Int(0, 1), new Vector2Int(0, -1)
                };

                // Phase A: shaped growth
                while (grown < targetSize && frontier.Count > 0)
                {
                    int fi = rng.Next(frontier.Count);
                    var current = frontier[fi];
                    frontier.RemoveAt(fi);

                    foreach (var d in dirs)
                    {
                        if (grown >= targetSize) break;
                        var nb = current + d;
                        if (nb.x < 0 || nb.x >= width || nb.y < 0 || nb.y >= height) continue;
                        if (visited.Contains(nb)) continue;
                        visited.Add(nb);
                        if (planGrid[nb.x, nb.y] != null || IsInClearing(nb.x, nb.y)) continue;

                        if ((float)rng.NextDouble() > spread)
                        {
                            deferred.Add(nb);
                            continue;
                        }

                        planGrid[nb.x, nb.y] = entry.environmentName;
                        frontier.Add(nb);
                        allClusterCells.Add(nb);
                        grown++;
                    }
                }

                // Phase B: force-grow from deferred cells to hit target
                if (grown < targetSize && deferred.Count > 0)
                {
                    ShuffleList(deferred);
                    foreach (var nb in deferred)
                    {
                        if (grown >= targetSize) break;
                        if (planGrid[nb.x, nb.y] != null) continue;
                        planGrid[nb.x, nb.y] = entry.environmentName;
                        allClusterCells.Add(nb);
                        grown++;

                        foreach (var d in dirs)
                        {
                            if (grown >= targetSize) break;
                            var nb2 = nb + d;
                            if (nb2.x < 0 || nb2.x >= width || nb2.y < 0 || nb2.y >= height) continue;
                            if (visited.Contains(nb2)) continue;
                            visited.Add(nb2);
                            if (planGrid[nb2.x, nb2.y] != null || IsInClearing(nb2.x, nb2.y)) continue;
                            planGrid[nb2.x, nb2.y] = entry.environmentName;
                            allClusterCells.Add(nb2);
                            grown++;
                        }
                    }
                }

                blobPlaced += grown;
            }

            // ── Phase 2: Loose break-off pieces near clusters ─────────
            int loosePlaced = 0;
            if (looseBudget > 0 && allClusterCells.Count > 0)
            {
                var looseCandidates = new HashSet<Vector2Int>();
                var clusterSet = new HashSet<Vector2Int>(allClusterCells);

                foreach (var cell in allClusterCells)
                {
                    for (int dx = -3; dx <= 3; dx++)
                    for (int dy = -3; dy <= 3; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = cell.x + dx, ny = cell.y + dy;
                        if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                        var candidate = new Vector2Int(nx, ny);
                        if (planGrid[nx, ny] != null) continue;
                        if (IsInClearing(nx, ny)) continue;
                        if (clusterSet.Contains(candidate)) continue;
                        int dist = Mathf.Abs(dx) + Mathf.Abs(dy);
                        if (dist >= 2)
                            looseCandidates.Add(candidate);
                    }
                }

                var looseList = new List<Vector2Int>(looseCandidates);
                ShuffleList(looseList);

                int toPlace = Mathf.Min(looseBudget, looseList.Count);
                for (int i = 0; i < toPlace; i++)
                {
                    var pos = looseList[i];
                    if (planGrid[pos.x, pos.y] != null) continue;
                    planGrid[pos.x, pos.y] = entry.environmentName;
                    loosePlaced++;
                }
            }

            int totalPlaced = blobPlaced + loosePlaced;
            Debug.Log($"[MapPlanner] Clustered '{entry.environmentName}': {totalPlaced} tiles ({blobPlaced} in blobs + {loosePlaced} loose) [budget={tileBudget}, frag={frag:F1}]");
        }

        private List<List<Vector2Int>> GetZoneSeeds(int zonesPerSide)
        {
            int zoneW = Mathf.Max(1, width / zonesPerSide);
            int zoneH = Mathf.Max(1, height / zonesPerSide);

            var zones = new List<List<Vector2Int>>();

            for (int zy = 0; zy < zonesPerSide; zy++)
            for (int zx = 0; zx < zonesPerSide; zx++)
            {
                var candidates = new List<Vector2Int>();
                int startX = zx * zoneW;
                int startY = zy * zoneH;
                int endX = (zx == zonesPerSide - 1) ? width : startX + zoneW;
                int endY = (zy == zonesPerSide - 1) ? height : startY + zoneH;

                for (int x = startX; x < endX; x++)
                for (int y = startY; y < endY; y++)
                    candidates.Add(new Vector2Int(x, y));

                ShuffleList(candidates);
                zones.Add(candidates);
            }

            ShuffleList(zones);
            return zones;
        }

        private void PlaceEdges(EnvironmentSpawnEntry entry, int tileBudget)
        {
            if (tileBudget <= 0) return;

            var candidates = new List<Vector2Int>();
            Vector2Int[] dirs = {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1)
            };

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (planGrid[x, y] != null) continue;
                if (IsInClearing(x, y)) continue;

                bool adjacentToTarget = false;
                foreach (var d in dirs)
                {
                    int nx = x + d.x, ny = y + d.y;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                    if (planGrid[nx, ny] == entry.edgeBorderOf)
                    {
                        adjacentToTarget = true;
                        break;
                    }
                }

                if (adjacentToTarget)
                    candidates.Add(new Vector2Int(x, y));
            }

            ShuffleList(candidates);
            int placed = Mathf.Min(tileBudget, candidates.Count);
            for (int i = 0; i < placed; i++)
            {
                var pos = candidates[i];
                planGrid[pos.x, pos.y] = entry.environmentName;
            }

            Debug.Log($"[MapPlanner] Edge '{entry.environmentName}' bordering '{entry.edgeBorderOf}': {placed} tiles (budget was {tileBudget}, {candidates.Count} candidates)");
        }

        private void PlaceScattered(List<(EnvironmentSpawnEntry entry, EnvironmentData data)> scattered, Dictionary<string, int> budgets)
        {
            int totalScatteredBudget = budgets.Values.Sum();
            if (totalScatteredBudget <= 0) return;

            var emptyTiles = new List<Vector2Int>();
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (planGrid[x, y] == null && !IsInClearing(x, y))
                    emptyTiles.Add(new Vector2Int(x, y));

            ShuffleList(emptyTiles);

            var placedPositions = new Dictionary<string, List<Vector2Int>>();
            var remaining = new Dictionary<string, int>();
            foreach (var (entry, _) in scattered)
            {
                placedPositions[entry.environmentName] = new List<Vector2Int>();
                remaining[entry.environmentName] = budgets[entry.environmentName];
            }

            int totalPlaced = 0;

            foreach (var pos in emptyTiles)
            {
                if (totalPlaced >= totalScatteredBudget) break;

                var candidates = new List<EnvironmentSpawnEntry>();
                foreach (var (entry, _) in scattered)
                    if (remaining[entry.environmentName] > 0)
                        candidates.Add(entry);

                if (candidates.Count == 0) break;

                WeightedShuffle(candidates);

                bool placed = false;
                foreach (var candidate in candidates)
                {
                    if (candidate.clearFromCenter > 0 &&
                        Mathf.Max(Mathf.Abs(pos.x - center.x), Mathf.Abs(pos.y - center.y)) < candidate.clearFromCenter)
                        continue;

                    if (candidate.minSpacing > 0 &&
                        IsTooClose(pos.x, pos.y, placedPositions[candidate.environmentName], candidate.minSpacing))
                        continue;

                    planGrid[pos.x, pos.y] = candidate.environmentName;
                    placedPositions[candidate.environmentName].Add(pos);
                    remaining[candidate.environmentName]--;
                    totalPlaced++;
                    placed = true;
                    break;
                }
            }

            foreach (var (entry, _) in scattered)
            {
                int budget = budgets[entry.environmentName];
                int placed = placedPositions[entry.environmentName].Count;
                Debug.Log($"[MapPlanner] Scattered '{entry.environmentName}': {placed} tiles (budget was {budget})");
            }
        }

        private void WeightedShuffle(List<EnvironmentSpawnEntry> list)
        {
            for (int i = 0; i < list.Count - 1; i++)
            {
                float totalW = 0f;
                for (int j = i; j < list.Count; j++)
                    totalW += list[j].spawnWeight;

                float roll = (float)(rng.NextDouble() * totalW);
                float cumulative = 0f;
                int picked = i;
                for (int j = i; j < list.Count; j++)
                {
                    cumulative += list[j].spawnWeight;
                    if (roll < cumulative) { picked = j; break; }
                }

                if (picked != i)
                {
                    var tmp = list[i];
                    list[i] = list[picked];
                    list[picked] = tmp;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Unit placement
        // ─────────────────────────────────────────────────────────────────

        private void PlaceUnitClusters(UnitSpawnEntry entry, int tileBudget)
        {
            if (tileBudget <= 0) return;
            string planName = MapGenHelpers.UNIT_PREFIX + entry.unitName;
            float frag = Mathf.Clamp01(entry.fragmentation);
            float spread = Mathf.Clamp01(entry.clusterSpread);
            int clusterCount = Mathf.Max(2, Mathf.RoundToInt(Mathf.Lerp(2f, 8f, frag)));

            int placed = 0;
            Vector2Int[] dirs = {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1)
            };

            for (int c = 0; c < clusterCount + 5 && placed < tileBudget; c++)
            {
                int targetSize = Mathf.Max(1, (tileBudget - placed) / Mathf.Max(1, clusterCount - c));

                Vector2Int seed = Vector2Int.zero;
                bool found = false;
                for (int a = 0; a < 80; a++)
                {
                    int sx = rng.Next(width), sy = rng.Next(height);
                    if (planGrid[sx, sy] == null && !IsInClearing(sx, sy))
                    {
                        seed = new Vector2Int(sx, sy);
                        found = true;
                        break;
                    }
                }
                if (!found) break;

                var frontier = new List<Vector2Int> { seed };
                var visited = new HashSet<Vector2Int> { seed };
                planGrid[seed.x, seed.y] = planName;
                int grown = 1;
                placed++;

                while (grown < targetSize && frontier.Count > 0)
                {
                    int fi = rng.Next(frontier.Count);
                    var current = frontier[fi];
                    frontier.RemoveAt(fi);

                    foreach (var d in dirs)
                    {
                        if (grown >= targetSize || placed >= tileBudget) break;
                        var nb = current + d;
                        if (nb.x < 0 || nb.x >= width || nb.y < 0 || nb.y >= height) continue;
                        if (visited.Contains(nb)) continue;
                        visited.Add(nb);
                        if (planGrid[nb.x, nb.y] != null || IsInClearing(nb.x, nb.y)) continue;

                        if ((float)rng.NextDouble() > spread) continue;

                        planGrid[nb.x, nb.y] = planName;
                        frontier.Add(nb);
                        grown++;
                        placed++;
                    }
                }
            }

            Debug.Log($"[MapPlanner] Clustered unit '{entry.unitName}': {placed} tiles (budget was {tileBudget})");
        }

        private void PlaceUnitEdges(UnitSpawnEntry entry, int tileBudget)
        {
            if (tileBudget <= 0) return;
            string planName = MapGenHelpers.UNIT_PREFIX + entry.unitName;

            string borderTarget = entry.edgeBorderOf;
            string unitBorderTarget = MapGenHelpers.UNIT_PREFIX + borderTarget;

            var candidates = new List<Vector2Int>();
            Vector2Int[] dirs = {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1)
            };

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (planGrid[x, y] != null) continue;
                if (IsInClearing(x, y)) continue;

                bool adjacent = false;
                foreach (var d in dirs)
                {
                    int nx = x + d.x, ny = y + d.y;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                    string cell = planGrid[nx, ny];
                    if (cell == borderTarget || cell == unitBorderTarget)
                    {
                        adjacent = true;
                        break;
                    }
                }
                if (adjacent) candidates.Add(new Vector2Int(x, y));
            }

            ShuffleList(candidates);
            int placed = Mathf.Min(tileBudget, candidates.Count);
            for (int i = 0; i < placed; i++)
                planGrid[candidates[i].x, candidates[i].y] = planName;

            Debug.Log($"[MapPlanner] Edge unit '{entry.unitName}' near '{borderTarget}': {placed} tiles (budget was {tileBudget})");
        }

        private void PlaceUnitScattered(List<(UnitSpawnEntry entry, UnitData data)> scattered, Dictionary<string, int> budgets)
        {
            int totalBudget = 0;
            foreach (var (entry, _) in scattered)
                totalBudget += budgets.ContainsKey(entry.unitName) ? budgets[entry.unitName] : 0;
            if (totalBudget <= 0) return;

            var emptyTiles = new List<Vector2Int>();
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (planGrid[x, y] == null && !IsInClearing(x, y))
                    emptyTiles.Add(new Vector2Int(x, y));

            ShuffleList(emptyTiles);

            var placedPositions = new Dictionary<string, List<Vector2Int>>();
            var remaining = new Dictionary<string, int>();
            foreach (var (entry, _) in scattered)
            {
                placedPositions[entry.unitName] = new List<Vector2Int>();
                remaining[entry.unitName] = budgets.ContainsKey(entry.unitName) ? budgets[entry.unitName] : 0;
            }

            int totalPlaced = 0;
            foreach (var pos in emptyTiles)
            {
                if (totalPlaced >= totalBudget) break;

                var candidates = scattered.Where(s => remaining[s.entry.unitName] > 0).ToList();
                if (candidates.Count == 0) break;

                float tw = candidates.Sum(c => c.entry.spawnWeight);
                float roll = (float)(rng.NextDouble() * tw);
                float cum = 0f;
                (UnitSpawnEntry entry, UnitData data) picked = candidates[0];
                foreach (var c in candidates)
                {
                    cum += c.entry.spawnWeight;
                    if (roll < cum) { picked = c; break; }
                }

                if (picked.entry.clearFromCenter > 0 &&
                    Mathf.Max(Mathf.Abs(pos.x - center.x), Mathf.Abs(pos.y - center.y)) < picked.entry.clearFromCenter)
                    continue;

                if (picked.entry.minSpacing > 0 &&
                    IsTooClose(pos.x, pos.y, placedPositions[picked.entry.unitName], picked.entry.minSpacing))
                    continue;

                planGrid[pos.x, pos.y] = MapGenHelpers.UNIT_PREFIX + picked.entry.unitName;
                placedPositions[picked.entry.unitName].Add(pos);
                remaining[picked.entry.unitName]--;
                totalPlaced++;
            }

            foreach (var (entry, _) in scattered)
            {
                int budget = budgets.ContainsKey(entry.unitName) ? budgets[entry.unitName] : 0;
                int placed = placedPositions[entry.unitName].Count;
                Debug.Log($"[MapPlanner] Scattered unit '{entry.unitName}': {placed} tiles (budget was {budget})");
            }
        }
    }
}
