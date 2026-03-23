# Corruption System — Design Spec
**Date:** 2026-03-23
**Status:** Approved
**Trello card:** https://trello.com/c/EoyoPrZd/22-corruption-system

---

## Overview

A hostile environmental mechanic built around corruption hearts that spread a damaging layer across the map. Hearts are dormant threats that activate when the player explores nearby, spread guaranteed corruption on a slow timer, and can be destroyed to instantly clear their owned tiles. The system is a layer on top of the existing grid — it does not replace tile types or occupants.

---

## Core Rules (Confirmed)

- Corruption is a **layer on top of tiles**, not a tile type. It coexists with whatever is already on a tile (empty, tree, building, etc.).
- Each heart **owns its cluster** of corrupted tiles. Destroying a heart clears exactly its owned tiles — no bleed-over into other hearts' territory.
- Hearts are **dormant on spawn** and own zero tiles until activated. They do not spread until the player reveals a tile within the **activation radius** (default 5 tiles). Dormancy is one-time and irreversible — once activated, a heart stays active.
- On activation, the heart's own grid tile becomes its first corrupted tile and the heart begins spreading on the global tick.
- Spread is **100% guaranteed** — no random chance. On each tick, every tile in a heart's owned set spreads to all orthogonal neighbours not already corrupted. Newly corrupted tiles are added to that heart's owned set.
- **Spread interval:** 30 seconds (Inspector-configurable on `CorruptionManager`).
- **Multiple hearts** can be active simultaneously. Each operates independently.
- Corruption can spread onto **player-owned buildings**, which are **disabled** (production paused mid-timer, not reset) while corrupted and re-enabled when cleared.
- When the player reveals **any corrupted tile**, the **entire connected cluster** for that heart is immediately revealed through the fog of war, including the heart's own tile.
- Each heart has a **floating world-space billboard indicator** that renders above the fog layer at all times, giving the player a hint before contact.

---

## Architecture

### CorruptionManager
Singleton. Created via `EnsureManagers()` at scene start. Needs no constructor parameters — hearts are spawned separately by map generation code.

**Inspector fields:**
- `[SerializeField] float spreadInterval = 30f`
- `[SerializeField] int heartActivationRadius = 5`

**Internal state:**
- `List<CorruptionHeart> allHearts`
- `Dictionary<CorruptionHeart, HashSet<Vector2Int>> heartTiles` — per-heart ownership
- `HashSet<Vector2Int> allCorruptedTiles` — flat set for O(1) `IsCorrupted()` lookups; always kept in sync with `heartTiles` within the same method call (never updated independently)

**Spread mechanism:**
`CorruptionManager` uses a countdown float in `Update()` (or `InvokeRepeating` set up in `Start()`). On each tick, it iterates all active hearts and spreads their tiles. Spread ticks are not fired for dormant hearts.

**Key methods:**
- `RegisterHeart(CorruptionHeart heart)` — called by `CorruptionHeart.Start()`; adds to `allHearts`, initialises empty entry in `heartTiles`
- `CorruptTile(int x, int y, CorruptionHeart owner)` — adds `CorruptionOverlay` to tile GameObject, adds to `heartTiles[owner]` and `allCorruptedTiles`, pauses any building occupant
- `ClearTile(int x, int y, CorruptionHeart owner)` — removes overlay, removes from `heartTiles[owner]` and `allCorruptedTiles`, resumes any paused building
- `ClearHeartCluster(CorruptionHeart heart)` — called on heart death; iterates `heartTiles[heart]`, calls `ClearTile` for each, removes heart from `allHearts`
- `IsCorrupted(int x, int y)` — `allCorruptedTiles.Contains(new Vector2Int(x, y))`
- Subscribes to `FogManager.OnCellRevealed` for dormancy checks and connected-reveal BFS

**Dormancy check (on `FogManager.OnCellRevealed`):**
```
foreach dormant heart in allHearts:
    if Manhattan distance (revealed cell → heart.gridPosition) <= heartActivationRadius:
        heart.isActive = true
        CorruptTile(heart.gridPosition, heart)   // seed first tile
```

**Spread tick:**
```
foreach active heart in allHearts:
    foreach tile in heartTiles[heart] (copy to avoid mutation during iteration):
        foreach orthogonal neighbour:
            if IsValidCell and not IsCorrupted:
                CorruptTile(neighbour, heart)
```

---

### CorruptionOverlay
MonoBehaviour added to a **tile's** `GameObject` (not the building's `GameObject`) at runtime via `AddComponent`. The tile and the cell occupant are separate objects — `GridManager.GetGridTile()` returns the tile, `GridManager.GetCellOccupant()` returns the occupant. These are always distinct.

**Inspector fields:**
- `[SerializeField] int maxHP = 3`

**Fields:**
- `GridEntityHealth health` — `CorruptionOverlay.Awake()` calls `AddComponent<GridEntityHealth>()` and sets its `maxHP` field before `Start()` runs, ensuring the subscription to `OnEntityDestroyed` in `Start()` is always against a valid component
- `CorruptionHeart ownerHeart` — set by `CorruptionManager` immediately after `AddComponent`
- `Vector2Int gridPosition` — set by `CorruptionManager` immediately after `AddComponent`
- `GameObject visualChild` — spawned in `Start()` as a child of the tile; the corruption renderer/particles
- `GameObject pausedOccupant` — cached reference to the building that was paused (null if none)

**Lifecycle:**
- Added by `CorruptionManager.CorruptTile()` via `tile.AddComponent<CorruptionOverlay>()`
- In `Start()`, subscribes to `health.OnEntityDestroyed`
- On `OnEntityDestroyed`: calls `CorruptionManager.Instance.ClearTile(gridPosition.x, gridPosition.y, ownerHeart)`, which destroys this component and the visual child

---

### CorruptionHeart
MonoBehaviour on a GameObject spawned by map generation code. Map gen is responsible for ensuring hearts are placed at valid, unoccupied grid positions, and that no two hearts share a cell.

**Inspector fields:**
- `[SerializeField] int maxHP = 10`

**Fields:**
- `GridEntityHealth health` — attached in `Awake()`; this is what workers and other combat units attack
- `bool isActive` — starts false; set by `CorruptionManager` on activation
- `Vector2Int gridPosition` — set by map gen before the heart is active
- `GameObject floatingIndicator` — world-space billboard sprite; always rendered above fog (dedicated layer/sorting order above fog visuals); assigned via Inspector or spawned in `Start()`

**Lifecycle:**
- `Start()` → calls `CorruptionManager.Instance.RegisterHeart(this)`; subscribes to `health.OnEntityDestroyed`
- `OnEntityDestroyed` → `CorruptionManager.Instance.ClearHeartCluster(this)` → `Destroy(gameObject)`

---

## Fog Integration — Connected Reveal

`CorruptionManager` subscribes to `FogManager.OnCellRevealed`. When a newly revealed cell is corrupted:

1. Look up the owning heart via `CorruptionOverlay.ownerHeart` on the tile's overlay component
2. Iterate `heartTiles[owner]` — every tile is already enumerated, no open-ended BFS needed
3. Call `FogManager.RevealCell(x, y)` for each tile in the set
4. Also reveal the heart's `gridPosition` tile

This reveals the full cluster instantly the moment the player touches its edge.

---

## Combat Priority

Workers currently resolve targets via:
```csharp
GameObject occupant = gm.GetCellOccupant(x, y);
GridEntityHealth targetHealth = occupant.GetComponent<GridEntityHealth>();
```

**Change:** At each targeting callsite in `GridEntityActor`, add a prior check against the tile:
```csharp
GameObject tile = gm.GetGridTile(x, y);
CorruptionOverlay overlay = tile != null ? tile.GetComponent<CorruptionOverlay>() : null;
if (overlay != null && overlay.health != null && !overlay.health.IsDestroyed)
{
    // target overlay.health instead of occupant health
}
```

Because Unity is single-threaded, there are no race conditions. If the overlay is destroyed between targeting and attacking, `IsDestroyed` will be true and the worker re-targets on its next cycle — exactly the same pattern used for all other entity deaths.

Workers can also target the `CorruptionHeart` directly via its `GridEntityHealth` once the heart's tile is revealed.

---

## Building Disabling

**New API on `BuildingProductionManager`:**
- `PauseBuilding(GameObject building)` — sets `isPaused = true` on that building's production entry. The existing countdown timer is preserved in place (not reset). While paused, the tick handler skips the entry entirely. Calling `PauseBuilding` on an already-paused building is a no-op.
- `ResumeBuilding(GameObject building)` — clears `isPaused`. Timer resumes from where it was.

**Cleanup if building is destroyed while paused:** `CorruptionOverlay` subscribes to the occupant's `GridEntityHealth.OnEntityDestroyed` event (if a building was paused). On destruction, the overlay clears `pausedOccupant` without calling `ResumeBuilding`, preventing a dangling reference in `BuildingProductionManager`. `ResumeBuilding` is idempotent — calling it on a non-paused or already-resumed building is a no-op. The building reference cached in `pausedOccupant` is the building's `GameObject`; `BuildingProductionManager` looks up the production entry by that reference at resume time.

**Flow:**
- `CorruptionManager.CorruptTile()` → `GridManager.GetCellOccupant(x, y)` → if occupant is non-null and has a production entry → `BuildingProductionManager.Instance.PauseBuilding(occupant)` → overlay caches `pausedOccupant`
- `CorruptionManager.ClearTile()` → if `overlay.pausedOccupant != null` → `BuildingProductionManager.Instance.ResumeBuilding(pausedOccupant)`

---

## Files to Create / Modify

| File | Change |
|------|--------|
| `Assets/Scripts/Systems/CorruptionManager.cs` | **New** — singleton manager |
| `Assets/Scripts/LittleCafe/CorruptionOverlay.cs` | **New** — per-tile component |
| `Assets/Scripts/LittleCafe/CorruptionHeart.cs` | **New** — heart entity |
| `Assets/Scripts/LittleCafe/GridEntityActor.cs` | **Modify** — add corruption overlay priority check at targeting callsites |
| `Assets/Scripts/LittleCafe/BuildingProductionManager.cs` | **Modify** — add `PauseBuilding` / `ResumeBuilding` with `isPaused` flag on production entries |
| `Assets/Scripts/LittleCafe/CafeSceneSetupV2.cs` (EnsureManagers host) | **Modify** — auto-create CorruptionManager |

---

## Out of Scope (Human Tasks)

- Assigning the corruption visual prefab / particle system in Inspector
- Assigning the floating heart indicator sprite/prefab in Inspector
- Art for corrupted tile overlay
- Map generation code for placing hearts at valid spawn positions
- Balance tuning of `spreadInterval`, `maxHP`, `heartActivationRadius` beyond defaults
- Audio / SFX for corruption spread and clearing
