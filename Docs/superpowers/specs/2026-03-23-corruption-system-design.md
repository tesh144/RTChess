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
- Hearts are **dormant on spawn** and do not spread until the player reveals a tile within a **5-tile radius** of the heart.
- Spread is **100% guaranteed** — no random chance. On each tick, every tile owned by an active heart spreads to all orthogonal neighbours that are not already corrupted.
- **Spread interval:** 30 seconds (configurable in Inspector on `CorruptionManager`).
- **Multiple hearts** can be active simultaneously. Each operates independently.
- Corruption can spread onto **player-owned buildings**, which are **disabled** (production paused) while corrupted and re-enabled when cleared.
- When the player reveals **any corrupted tile**, the **entire connected cluster** for that heart is immediately revealed through the fog of war, including the heart itself.
- Each heart has a **floating world-space billboard indicator** that is visible above the fog layer at all times, giving the player a hint before they make contact.

---

## Architecture

### CorruptionManager
Singleton. Created via `EnsureManagers()` following project convention.

**Owns:**
- `List<CorruptionHeart> allHearts` — all hearts, dormant and active
- `Dictionary<CorruptionHeart, HashSet<Vector2Int>> heartTiles` — per-heart tile ownership
- `[SerializeField] float spreadInterval = 30f` — inspector-configurable spread timer
- Spread timer logic (counts down, fires tick across all active hearts)

**Key methods:**
- `CorruptTile(int x, int y, CorruptionHeart owner)` — adds `CorruptionOverlay` to the tile GameObject, registers in `heartTiles[owner]`, pauses any building occupant
- `ClearTile(int x, int y, CorruptionHeart owner)` — removes overlay, unregisters from `heartTiles[owner]`, resumes any paused building
- `ClearHeartCluster(CorruptionHeart heart)` — called on heart death; iterates `heartTiles[heart]` and clears every owned tile
- `IsCorrupted(int x, int y)` — O(1) check via a flat `HashSet<Vector2Int> allCorruptedTiles` maintained in parallel
- `OnHeartActivated(CorruptionHeart heart)` — called when dormancy check passes; starts that heart participating in spread ticks
- Subscribes to `FogManager.OnCellRevealed` for dormancy checks and connected-reveal BFS

**Spread tick logic:**
```
foreach active heart:
    foreach tile in heartTiles[heart]:
        foreach orthogonal neighbour (up/down/left/right):
            if not corrupted and IsValidCell:
                CorruptTile(neighbour, heart)
```

---

### CorruptionOverlay
MonoBehaviour added directly to the tile's `GameObject` at runtime.

**Fields:**
- `[SerializeField] int maxHP = 3`
- `GridEntityHealth health` — component added at `Awake()`; implements `IDamageable` so existing worker targeting picks it up
- `GameObject visualChild` — spawned in `Start()`; the corruption renderer/particles above the tile
- `GameObject pausedOccupant` — cached reference to any building that was paused when this overlay was applied

**Lifecycle:**
- Added by `CorruptionManager.CorruptTile()` via `tile.AddComponent<CorruptionOverlay>()`
- On HP reaching zero → notifies `CorruptionManager.ClearTile()` → component and visual are destroyed

---

### CorruptionHeart
MonoBehaviour placed on a GameObject by `CorruptionManager` at spawn time.

**Fields:**
- `GridEntityHealth health` — existing combat system; workers can attack the heart directly
- `bool isActive` — false until dormancy check passes
- `Vector2Int gridPosition` — cached grid coords
- `GameObject floatingIndicator` — world-space billboard sprite, rendered above fog at all times (uses a layer/sorting order that renders above the fog visual)

**Lifecycle:**
- On `GridEntityHealth.OnEntityDestroyed` → `CorruptionManager.ClearHeartCluster(this)`, then destroy self
- `CorruptionManager` checks distance to this heart on every `FogManager.OnCellRevealed` event; when any revealed tile is within 5 tiles, sets `isActive = true` and calls `OnHeartActivated`

---

## Fog Integration — Connected Reveal

`CorruptionManager` subscribes to `FogManager.OnCellRevealed`.

When a newly revealed cell is corrupted:
1. Identify which heart owns it via reverse lookup (or `CorruptionOverlay` stores its owner)
2. BFS across `heartTiles[owner]` — every tile in that set is already known, so no open-ended search needed
3. Call `FogManager.RevealCell(x, y)` for each tile in the set
4. Also reveal the heart's own grid tile

This gives the player full information about a corruption cluster the instant they touch its edge.

---

## Combat Priority

Workers currently resolve targets via:
```csharp
GameObject occupant = gm.GetCellOccupant(x, y);
GridEntityHealth targetHealth = occupant.GetComponent<GridEntityHealth>();
```

**Change:** At each targeting callsite in `GridEntityActor`, add a prior check:
```csharp
GameObject tile = gm.GetGridTile(x, y);
CorruptionOverlay overlay = tile?.GetComponent<CorruptionOverlay>();
if (overlay != null && !overlay.Health.IsDestroyed)
    // attack overlay instead of occupant
```

The `CorruptionOverlay`'s `GridEntityHealth` implements `IDamageable`, so `TakeDamage` works identically. Once the overlay is gone, the next attack cycle falls through to the normal occupant.

Workers can also target the `CorruptionHeart` directly once it's revealed, via its `GridEntityHealth`.

---

## Building Disabling

**New API on `BuildingProductionManager`:**
- `PauseBuilding(GameObject building)` — sets `isPaused = true` on that building's production entry; tick handler skips paused entries
- `ResumeBuilding(GameObject building)` — clears the flag

**Flow:**
- `CorruptionManager.CorruptTile()` → if `GridManager.GetCellOccupant(x, y)` is a building → `BuildingProductionManager.Instance.PauseBuilding(occupant)` → `CorruptionOverlay` caches `pausedOccupant`
- `CorruptionManager.ClearTile()` → if overlay had a `pausedOccupant` → `BuildingProductionManager.Instance.ResumeBuilding(pausedOccupant)`

---

## Files to Create / Modify

| File | Change |
|------|--------|
| `Assets/Scripts/Systems/CorruptionManager.cs` | **New** — singleton manager |
| `Assets/Scripts/LittleCafe/CorruptionOverlay.cs` | **New** — per-tile component |
| `Assets/Scripts/LittleCafe/CorruptionHeart.cs` | **New** — heart entity |
| `Assets/Scripts/LittleCafe/GridEntityActor.cs` | **Modify** — add corruption priority check at targeting callsites |
| `Assets/Scripts/LittleCafe/BuildingProductionManager.cs` | **Modify** — add `PauseBuilding` / `ResumeBuilding` |
| `Assets/Scripts/LittleCafe/CafeSceneSetupV2.cs` (or equivalent EnsureManagers host) | **Modify** — create CorruptionManager in `EnsureManagers()` |

---

## Out of Scope (Human Tasks)

- Assigning the corruption visual prefab / particle system in Inspector
- Assigning the floating heart indicator sprite in Inspector
- Art for corrupted tile overlay
- Balance tuning of `spreadInterval`, `maxHP`, and activation radius beyond defaults
- Audio / SFX for corruption spread and clearing
