---
title: Kitchen Resource Cost
date: 2026-03-23
status: approved
---

## Summary

The Kitchen building requires 10 Food to activate its production cycle. It consumes the Food upfront (pre-pay) before the timer starts. This is data-driven — any building can be given an activation resource cost via two new fields on `BuildingData`.

## Behaviour

1. Kitchen idles silently (timer hidden) after placement or after each collection.
2. Each global tick, it checks whether `ResourceManager` has ≥ 10 Food.
3. When the check passes, 10 Food is immediately spent and the production timer starts.
4. Timer runs normally; popup appears on completion; player collects Meal card.
5. After collection the building returns to idle and the cycle repeats.

If food never reaches 10, the building stays idle indefinitely with no visual feedback beyond the hidden timer (consistent with existing idle behaviour).

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

## Asset Layer — `BuildingDatabase.asset`

Kitchen entry updated:
- `productionCostResourceType: 3` (Food)
- `productionCostAmount: 10`

All other buildings: `productionCostResourceType: 0` (None), `productionCostAmount: 0`.

## Sheet Layer — `Buildings & Production`

Two new columns added: **Cost Resource** and **Cost Amount**.
- Kitchen row: `🍄 Food`, `10`
- All other rows: `None`, `0`

`SheetCache.json` headers and Kitchen row updated.
`SheetSyncEditor.SyncBuildings()` updated to parse and apply these two columns.

## Logic Layer — `BuildingProductionManager`

### `ProductionEntry` (inner class)

New field:
```csharp
public bool waitingForResources; // true when building needs to spend resources before starting
```

Initialised to `true` on entry creation if `productionCostAmount > 0`, otherwise `false`.

### `OnIntervalTick()`

After the existing `waitingForInput` guard, insert:

```csharp
if (entry.waitingForResources)
{
    var rm = ResourceManager.Instance;
    if (rm != null && rm.GetResource(entry.productionCostResourceType) >= entry.productionCostAmount)
    {
        rm.SpendResources(new Dictionary<ResourceType, int>
            { { entry.productionCostResourceType, entry.productionCostAmount } });
        entry.waitingForResources = false;
    }
    else
    {
        continue; // not enough resources — skip tick
    }
}
```

### After collection (in `CollectReward`)

Reset the flag for the next cycle:

```csharp
if (entry.productionCostAmount > 0)
    entry.waitingForResources = true;
```

This is added alongside the existing `waitingForInput` reset.

### `ProductionEntry` creation (`RegisterBuilding`)

```csharp
productionCostResourceType = stats.productionCostResourceType,
productionCostAmount       = stats.productionCostAmount,
waitingForResources        = stats.productionCostAmount > 0,
```

## Out of Scope

- No UI indicator for "waiting for food" state (idle-hidden timer is sufficient for v1).
- No partial resource reservation.
- No multi-resource costs (single resource type only).
