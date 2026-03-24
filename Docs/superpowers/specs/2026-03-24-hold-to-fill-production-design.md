# Hold-to-Fill Production System

**Trello card:** https://trello.com/c/gDTxdu2K
**Date:** 2026-03-24
**Status:** Design approved, pending implementation

## Summary

New building activation type where the player holds left-click on a building to gradually fill it with resources. Once full, the building starts its normal production timer. Replaces the auto-deducting resource gate for buildings that use this type. The Kitchen is the first building to use this system, but it is designed as a generic mechanism for any future building.

## Motivation

The Kitchen currently auto-consumes Food every tick with no player agency. The hold-to-fill mechanic gives the player direct control over when and how resources are spent, creating a more intentional interaction loop.

## Data Model

### New enum value

`ProductionInputType.HoldToFill` added alongside `None`, `Worker`, `Fighter`.

### Field changes

- Keep existing `productionCostAmount` field but add `[FormerlySerializedAs("productionCostAmount")]` when renaming to `resourcesRequired`. This preserves serialized data in .asset files.
- Alternatively, keep `productionCostAmount` as-is and just add `resourcesRequiredIncrement`. The rename is cosmetic — implementation should decide based on risk. If renaming, re-sync all .asset files from Google Sheets after.
- New field: `resourcesRequiredIncrement` (int, default 0) — added to cost each collect cycle

Both fields on `BuildingData` and `UnitStats`.

### Effective fill cost

```
EffectiveFillCost = resourcesRequired + (resourcesRequiredIncrement * collectCount)
```

Kitchen: 3, 4, 5, 6... (base=3, increment=1).

### ProductionEntry new fields

- `waitingForHoldFill` (bool) — true when building needs player to fill it
- `holdFillProgress` (int) — resource units deposited so far (0 to EffectiveFillCost)

Buildings with `productionInputType == HoldToFill` use `waitingForHoldFill` instead of `waitingForResources`. They never auto-deduct.

### State flow

```
Building placed → waitingForHoldFill=true, holdFillProgress=0
Player holds → chunks drain from resource pool, holdFillProgress increments
Player releases → progress retained
holdFillProgress reaches EffectiveFillCost → waitingForHoldFill=false, timer starts
Timer completes → popup appears
Player collects → waitingForHoldFill=true, holdFillProgress=0, collectCount++
```

## Hold Input & Accelerating Drain

### Input detection

New `HoldToFillHandler` MonoBehaviour (singleton, on a manager object). On left-click-down, raycasts to check if hit a building with `waitingForHoldFill=true`. While held:

### Accelerating chunk schedule

Serialized fields on `HoldToFillHandler` for designer tuning:
- `baseChunkInterval`: starting seconds between chunks (default 0.5s)
- `chunkDecayFactor`: multiplier per successive chunk (default 0.85)
- `minChunkInterval`: floor to prevent instant drain (default 0.08s)
- Result: first chunks are deliberate, ramps up quickly for large fill amounts

### Per chunk

1. Check `ResourceManager.GetResource(type) >= 1`
2. If affordable: `SpendResources(1)`, increment `holdFillProgress`, trigger VFX/SFX
3. If not affordable: hold pauses (no drain, no progress), resumes if resources appear while still holding

### On release or resource exhaustion

Stop draining. Progress retained. Player can re-click to continue filling.

### Camera interaction

Hold-to-fill does not block camera pan/zoom. Only the fill interaction is consumed.

### Input priority

Multiple systems respond to left-click. Priority order:
1. **Popup collection** (`HandlePopupTap`) — if building has `isReady=true`, collect the reward. Hold-to-fill does NOT activate.
2. **Drag-drop** (`DragDropHandler`) — if dragging a card, hold-to-fill does NOT activate.
3. **Hold-to-fill** (`HoldToFillHandler`) — only activates if raycast hits a building in `waitingForHoldFill` state and no higher-priority interaction claims the click.

`HoldToFillHandler` should check `DragDropHandler.IsDragging` and `BuildingProductionManager.HasReadyPopupAt(position)` before starting a fill session.

## Visuals

### Fill bar

- Small horizontal bar at the base of the building (world-space canvas)
- Shows `holdFillProgress / EffectiveFillCost` as fill amount
- Always visible when `waitingForHoldFill=true` — shows at 0% (empty bar) when no progress yet, so the player has an affordance that the building needs filling
- Disappears when full and production timer starts
- Reappears at 0% when production cycle resets after collection

### Resource stream VFX

- On each chunk: spawn a particle/icon that flies from the resource bar (top-right UI) to the building
- Reverse of the normal resource gain animation (ResourceLootFX inverted)
- One particle per chunk — they visually accelerate with the drain speed

## Audio

### Chunk SFX

- One SFX hit per chunk (same clip, pitch-shifted)
- Pitch starts at base (~0.8) and rises proportionally with `holdFillProgress / EffectiveFillCost`
- Capped at ceiling (~1.4) to avoid shrillness

### Completion SFX

- Distinct sound when fill completes
- Fill bar disappears, normal production timer appears

## Integration

### BuildingProductionManager

- `RegisterBuilding()`: when `productionInputType == HoldToFill`, set `waitingForHoldFill=true`, `holdFillProgress=0`. Do NOT set `waitingForResources`.
- `OnIntervalTick()`: if `entry.waitingForHoldFill == true`, skip the tick. Also guard the `waitingForResources` block to skip HoldToFill entries — they must never auto-deduct.
- `CollectReward()`: on collection, reset `waitingForHoldFill=true`, `holdFillProgress=0`. Do NOT set `waitingForResources=true` for HoldToFill entries.
- New property on ProductionEntry: `EffectiveFillCost` = `resourcesRequired + (resourcesRequiredIncrement * collectCount)`.

### BuildingProductionManager public API for HoldToFillHandler

Since `ProductionEntry` is private, expose these methods:
- `bool IsWaitingForHoldFill(GameObject building)` — check if building is in fill state
- `HoldFillInfo GetHoldFillInfo(GameObject building)` — returns current progress, effective cost, resource type (simple struct)
- `bool IncrementHoldFill(GameObject building)` — adds 1 to progress, returns true if fill just completed (clears gate)
- `bool HasReadyPopupAt(Vector2Int gridPos)` — for input priority check

### HoldToFillHandler (new file)

- Singleton MonoBehaviour on a manager object
- Handles mouse input: raycast on click-down, drain loop while held
- Queries BuildingProductionManager for hold-fill buildings at hit position
- Updates `holdFillProgress` and clears gate when full
- Owns fill bar UI, resource stream VFX, and chunk audio

### DragDropHandler

No changes. Hold-to-fill buildings don't accept dragged cards.

### Google Sheets / SheetSyncEditor

- Rename code field `productionCostAmount` → `resourcesRequired`
- Add `resourcesRequiredIncrement` field to BuildingData + UnitStats
- Map from sheet columns (existing "Cost Amount" → `resourcesRequired`, increment column as needed)

### Data values

| Building | productionInputType | resourcesRequired | resourcesRequiredIncrement | productionCostResourceType |
|----------|--------------------|--------------------|---------------------------|---------------------------|
| Kitchen  | HoldToFill         | 3                  | 1                         | Food                      |
| All others | (unchanged)     | 0                  | 0                         | None                      |

## Edge cases

- **Building destroyed mid-fill:** Progress is lost. Entry removed from BuildingProductionManager as normal.
- **Building corrupted mid-fill:** Hold is interrupted (handler releases). Progress is retained. Player can resume after corruption clears.
- **Multiple HoldToFill buildings:** Only one can be held at a time. Starting a hold on building B while holding A releases A.

## Files affected

- `BuildingData.cs` — new enum value, new `resourcesRequiredIncrement` field (+ optional rename with FormerlySerializedAs)
- `UnitStats.cs` — new `resourcesRequiredIncrement` field (+ optional rename)
- `BuildingProductionManager.cs` — new gate state, registration, tick skip, collection reset, public API for handler
- `HoldToFillHandler.cs` — **new file**, input handling, drain logic, UI, VFX, audio
- `SheetSyncEditor.cs` — new field sync
- `BuildingDatabase.asset` — Kitchen data update
- `MapGeneratorV2.cs` — new field copy in SetupDeck
