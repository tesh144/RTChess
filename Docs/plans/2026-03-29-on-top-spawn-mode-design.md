# On Top Spawn Mode Design

**Date:** 2026-03-29
**Status:** Approved

## Summary

Add a fourth spawn mode `OnTop` to the map generator that places objects on the Object layer above existing Surface-layer tiles. First use case: water lilies spawning on water tiles.

## Data Model Changes

### SpawnMode Enum

Add `OnTop` as a fourth value:

```csharp
public enum SpawnMode
{
    Scattered,
    Clustered,
    Edge,
    OnTop
}
```

### EnvironmentSpawnEntry — New Fields

Two new fields, only used when `spawnMode == OnTop`:

- `requiredSurface` (`SurfaceType`) — which surface the object spawns on (e.g. `Water`)
- `coveragePercent` (`float`, 0-1) — fraction of qualifying tiles to cover

Existing `minSpacing` is reused for spacing control. Other mode-specific fields (`spawnWeight`, `fragmentation`, `clusterSpread`, `edgeBorderOf`) are ignored for OnTop mode.

## Planning Phase

### Second-Pass Planning

MapPlanner gets a new method called after `PlaceAllEntries()`:

**`PlaceOnTopEntries(string[,] planGrid, List<EnvironmentSpawnEntry> onTopEntries)`**

For each OnTop entry:

1. Scan planGrid for tiles planned as the matching surface type (e.g. all cells where the planned asset has `SurfaceType.Water`)
2. Filter out tiles that already have an object-layer entry
3. Calculate target count = `qualifyingTiles.Count * coveragePercent`
4. Shuffle qualifying tiles, place entries respecting `minSpacing` until target count is reached
5. Write the asset name into a **separate `string[,] onTopPlanGrid`** (not the main planGrid)

### Why a Separate Grid

The main planGrid cell holds the surface entry name (e.g. "Water"). Overwriting it with "WaterLily" would lose the surface entry, preventing the surface from being spawned. A parallel `onTopPlanGrid` keeps both layers independent.

## Spawning Phase

### New Coroutine: SpawnAllOnTopStaggered

Runs **after** `SpawnAllStaggered()` so surface GameObjects already exist:

1. Iterate `onTopPlanGrid`
2. For each non-null cell, look up the prefab from EnvironmentDatabase
3. Instantiate at the tile's world position
4. Register on Object layer via `GridManager.PlaceUnit(x, y, obj, CellState.Resource)`
5. Attach standard components (FogHideable, ResourceNode if applicable)
6. Stagger at ~25 per frame

### Updated Pipeline Order

1. InitPlanGrid
2. PlaceAllEntries (normal env + units)
3. **PlaceOnTopEntries** (second pass, fills onTopPlanGrid)
4. PlaceCorruptionEntities
5. DetectGatherings
6. SpawnCenter
7. SpawnAllStaggered (surfaces + normal objects)
8. **SpawnAllOnTopStaggered** (on-top objects)
9. SpawnAllUnitsStaggered
10. SpawnAllCorruptionEntitiesStaggered

## First Object: Water Lily

- Environment entry name: "WaterLily"
- `spawnMode = OnTop`
- `requiredSurface = SurfaceType.Water`
- `coveragePercent` ~ 0.2-0.3 (tunable)
- `minSpacing = 2`
- `layerType = Object`
- Prefab: water lily GameObject (sits at same world position as water tile, visually on top)

## Decisions Made

| Decision | Choice | Reasoning |
|----------|--------|-----------|
| Which layer for on-top objects | Object layer | Lily wouldn't share space with a tree; keeps grid model at two layers |
| When to plan on-top entries | Second pass after all normal entries | Guarantees prerequisite surfaces exist |
| How many to spawn | Coverage percentage of qualifying tiles | Scales naturally with map size and surface abundance |
| Distribution control | Min spacing (reuse existing field) | Prevents unnatural clumping without overcomplicating |
| Prerequisite reference | SurfaceType enum | On-top only makes sense on surface tiles; enum is direct and type-safe |
| planGrid strategy | Separate onTopPlanGrid | Avoids overwriting surface entries; no string parsing needed |
