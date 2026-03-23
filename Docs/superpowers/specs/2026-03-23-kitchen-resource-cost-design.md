---
title: Kitchen Resource Cost
date: 2026-03-23
status: draft
---

## Summary

The Kitchen building requires 10 Food to activate its production cycle. It consumes the Food upfront (pre-pay) before the timer starts. This is data-driven — any building can be given an activation resource cost via two new fields on `BuildingData` and `UnitStats`.

## Behaviour

1. Kitchen idles silently (timer hidden) after placement or after each collection.
2. Each global tick, it checks whether `ResourceManager` has ≥ 10 Food.
3. When the check passes, 10 Food is immediately spent and the production timer starts running.
4. On the same tick that resources are spent, the timer canvas is revealed (consistent with how the first-tick reveal already works — `waitingForResources` clears and execution falls through to the reveal block).
5. Timer runs normally; popup appears on completion; player collects Meal card.
6. After collection the building returns to idle and the cycle repeats.

If food never reaches 10, the building stays idle indefinitely. The timer remains hidden. No additional visual feedback is needed for v1. The `IsResourceUnlocked` state is intentionally not checked — the cost check is amount-only.

Building destroyed while waiting: the existing null-guard in `OnIntervalTick` (lines 529–533) removes the entry before the resource check is reached, so no special handling is needed.

## Data Layer — `BuildingData.cs`

Two new fields added to `BuildingData`:

```csharp
[Header("Production Resource Cost")]
[Tooltip("Resource type required to start each production cycle. None = no cost.")]
public ResourceType productionCostResourceType = ResourceType.None;

[Tooltip("Amount of productionCostResourceType consumed when the cycle starts. 0 = no cost.")]
public int productionCostAmount = 0;
```

Defaults to `None / 0` so all existing buildings are unaffected.

## Data Layer — `UnitStats.cs`

Two matching fields added to `UnitStats`, under the `Building Production` header:

```csharp
[Tooltip("Resource type required to start each production cycle. None = no cost.")]
public ResourceType productionCostResourceType = ResourceType.None;

[Tooltip("Amount of productionCostResourceType consumed when the cycle starts. 0 = no cost.")]
public int productionCostAmount = 0;
```

## Runtime Copy — `MapGeneratorV2.SetupDeck()`

In the `BuildingData → UnitStats` copy block (around line 403), add:

```csharp
stats.productionCostResourceType = data.productionCostResourceType;
stats.productionCostAmount       = data.productionCostAmount;
```

This is the only place `BuildingData` flows into `UnitStats` at runtime.

## Asset Layer — `BuildingDatabase.asset`

Kitchen entry updated via the Unity Inspector:
- `productionCostResourceType` → set to `Food` (do not hardcode the integer ordinal — use the enum by name)
- `productionCostAmount` → `10`

All other buildings: leave at defaults (`None`, `0`).

## Sheet Layer — `Buildings & Production`

Two new columns added to the sheet: **Cost Resource** and **Cost Amount**.
- Kitchen row: `🍄 Food`, `10`
- All other rows: `None`, `0`

`SheetCache.json` headers and Kitchen row must be updated as part of implementation. The header key strings used in `GetValue(row, ...)` must match the `SheetCache.json` keys exactly — use `"Cost Resource"` and `"Cost Amount"`.

`SheetSyncEditor.SyncBuildings()` must be updated to parse and apply these two columns, following the existing precedent from the DrawButton sync block:

```csharp
// Cost Resource
string costResStr = StripEmoji(GetValue(row, "Cost Resource")).Replace(" ", "");
if (!string.IsNullOrEmpty(costResStr) && costResStr != "None")
{
    if (Enum.TryParse<ResourceType>(costResStr, true, out var costRes))
        if (existing.productionCostResourceType != costRes) { existing.productionCostResourceType = costRes; changed = true; }
}
// Cost Amount
changed |= TrySetInt(ref existing.productionCostAmount, GetValue(row, "Cost Amount"));
```

## Logic Layer — `BuildingProductionManager`

### `ProductionEntry` (inner class)

Three new fields:

```csharp
public ResourceType productionCostResourceType;
public int          productionCostAmount;
public bool         waitingForResources; // true when building needs to spend resources before starting timer
```

### `RegisterBuilding()`

Set fields from `stats`:

```csharp
productionCostResourceType = stats.productionCostResourceType,
productionCostAmount       = stats.productionCostAmount,
waitingForResources        = stats.productionCostAmount > 0,
```

### `OnIntervalTick()`

After the existing `waitingForInput` guard, insert:

```csharp
if (entry.waitingForResources)
{
    var rm = ResourceManager.Instance;
    bool spent = rm != null && rm.SpendResources(
        new Dictionary<ResourceType, int> { { entry.productionCostResourceType, entry.productionCostAmount } });
    if (spent)
        entry.waitingForResources = false;
    else
        continue; // not enough resources — skip tick
}
```

`SpendResources` returns `false` without spending if `CanAfford` fails, so its return value is the sole gating condition — no redundant pre-check needed.

### After collection (`CollectReward`)

Reset the flag alongside the existing `waitingForInput` reset:

```csharp
if (entry.productionCostAmount > 0)
    entry.waitingForResources = true;
```

## Out of Scope

- No UI indicator for "waiting for food" state (idle-hidden timer is sufficient for v1).
- No partial resource reservation.
- No multi-resource activation costs (single resource type only).
- `IsResourceUnlocked` is not checked — buildings can consume any resource regardless of unlock state.
- Pause/resume interaction: if a building is paused (corrupted tile) while `waitingForResources = true`, `timerRevealed` is `false` and the timer canvas stays hidden on resume. This is correct — the building simply continues waiting for resources after unpause. No special handling needed; the existing `isPaused` guard runs before the `waitingForResources` check.
