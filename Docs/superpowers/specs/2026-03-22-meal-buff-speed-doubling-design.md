# Meal Buff Speed Doubling — Design Spec

**Date:** 2026-03-22
**Status:** Approved

## Summary

When a worker interacts with a Meal (MealBuffSource), they receive a timed buff that doubles their interaction speed by switching their clockwork subscription from `OnBar` to `OnHalfBar` for the buff duration. The buff duration is inspector-configurable with a 30-second placeholder default.

## Requirements

- Worker interacts with food → receives buff for a configurable duration (default 30s)
- While buffed, worker acts on every half-bar (beats 1 and 3) instead of every bar (beat 1 only)
- Buff duration counts down in bar ticks (one decrement per bar, regardless of the doubled action rate)
- Buff duration is exposed as `[SerializeField] float mealBuffDurationSeconds = 30f` on `GridEntityActor`
- On buff expiry, worker seamlessly returns to bar-based timing

## Architecture

### Affected File

`Assets/Scripts/LittleCafe/GridEntityActor.cs` — only file changed.

### New Inspector Field

```csharp
[Header("Meal Buff")]
[Tooltip("How long the meal buff lasts in real seconds. Converted to bar ticks on grant.")]
[SerializeField] private float mealBuffDurationSeconds = 30f;
```

### Tick Handler Split

Current single handler `OnIntervalTick(int bar)` is split into two:

| Handler | Subscribed | Responsibility |
|---|---|---|
| `OnBarTick(int bar)` | Always (OnEnable/OnDisable) | Buff decay; worker actions when NOT buffed |
| `OnHalfBarTick(int bar)` | Only while buffed | Worker actions at double speed |

### Subscription Lifecycle

**`GrantMealBuff(int durationTicks)`** (updated):
1. `hasMealBuff = true`
2. `mealBuffTicksRemaining = durationTicks`
3. Subscribe `OnHalfBarTick` to `IntervalTimer.Instance.OnHalfBar`

**`ExpireMealBuff()`** (new private method):
1. `hasMealBuff = false`
2. `mealBuffTicksRemaining = 0`
3. Unsubscribe `OnHalfBarTick` from `IntervalTimer.Instance.OnHalfBar`

**`OnDisable`** (updated): always unsubscribes both `OnBarTick` and `OnHalfBarTick` to prevent leaks.

**`OnEnable`** (unchanged): subscribes `OnBarTick` to `OnBar`. Does NOT re-subscribe `OnHalfBarTick` — the buff was not active before disable.

### Duration Conversion

```csharp
private int ConvertDurationToTicks()
{
    float barDuration = IntervalTimer.Instance != null
        ? IntervalTimer.Instance.IntervalDuration
        : 2f; // fallback
    return Mathf.Max(1, Mathf.RoundToInt(mealBuffDurationSeconds / barDuration));
}
```

Called from the `GrantMealBuff` call site (line ~698), replacing `GrantMealBuff(8)`.

### OnBarTick Logic

```
OnBarTick(bar):
  if !isInitialized || health.IsDestroyed → return
  if hasMealBuff:
    decrement mealBuffTicksRemaining
    if mealBuffTicksRemaining <= 0: ExpireMealBuff()
    return  ← no action; OnHalfBarTick handles it
  // Not buffed — normal worker action
  respect intervalMultiplier
  dispatch ClockworkTick coroutine
```

### OnHalfBarTick Logic

```
OnHalfBarTick(bar):
  if !isInitialized || health.IsDestroyed → return
  // Only called while buffed (subscription managed by GrantMealBuff/ExpireMealBuff)
  respect intervalMultiplier
  dispatch ClockworkTick coroutine
```

### Beat 1 Overlap

On beat 1, `IntervalTimer` fires `OnBar` then `OnHalfBar` (in that order). While buffed:
1. `OnBarTick` → decrement buff, return early (no action)
2. `OnHalfBarTick` → worker acts

On beat 3, only `OnHalfBar` fires:
1. `OnHalfBarTick` → worker acts

Result: 2 actions per bar while buffed, buff duration decrements once per bar.

On expiry tick: `OnBarTick` decrements to 0, calls `ExpireMealBuff()` (unsubscribes half-bar), `hasMealBuff` is now false → worker acts on that same bar tick. Clean handoff.

## What Does NOT Change

- `IntervalTimer.cs` — no changes
- `MealBuffSource.cs` — no changes
- `MealBuffVisual.cs` — no changes; it reads `HasMealBuff` and `MealBuffTicksRemaining` which remain accurate
- The starvation system — unchanged; starvation resets on any interaction regardless of buff state
- The "skip meals when already buffed" scan logic — unchanged

## Out of Scope

- Different foods granting different buff durations (duration lives on GridEntityActor, not MealBuffSource)
- Visual changes to indicate the speed increase
- Stacking or refreshing the buff while active (existing guard `!hasMealBuff` is unchanged)
