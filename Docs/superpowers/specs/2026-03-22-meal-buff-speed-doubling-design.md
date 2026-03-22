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
1. Guard: `if (hasMealBuff) return;` — prevents double-subscription since C# `+=` does not deduplicate
2. `hasMealBuff = true`
3. `mealBuffTicksRemaining = durationTicks`
4. Subscribe `OnHalfBarTick` to `IntervalTimer.Instance.OnHalfBar`

Note: `GrantMealBuff` is only reachable from the `RotateAndInteract` scan path (existing call-site guard). No additional `behaviorType` check is needed inside `GrantMealBuff`, but the guard above makes it safe regardless.

**`ExpireMealBuff()`** (new private method):
1. `hasMealBuff = false`
2. `mealBuffTicksRemaining = 0`
3. Unsubscribe `OnHalfBarTick` from `IntervalTimer.Instance.OnHalfBar`

**`OnEnable`** (updated): subscribes `OnBarTick` to `OnBar`. If `hasMealBuff == true` (worker was disabled while buffed), also re-subscribes `OnHalfBarTick` to `OnHalfBar` to restore the doubled speed.

**`OnDisable`** (updated): always unsubscribes both `OnBarTick` and `OnHalfBarTick` inside the existing `if (IntervalTimer.Instance != null)` null guard (retained from current code). Unsubscribing a delegate that was never subscribed is a safe C# no-op — no `if (hasMealBuff)` guard needed.

### Duration Conversion

```csharp
private int ConvertDurationToTicks()
{
    if (IntervalTimer.Instance == null)
    {
        // IntervalTimer not ready — this should not happen in normal play.
        // Fallback matches IntervalTimer.baseIntervalDuration inspector default (2.0f).
        // If that default changes, update this constant to match.
        const float FallbackBarDuration = 2f;
        Debug.LogWarning("[GridEntityActor] IntervalTimer.Instance is null — using fallback bar duration");
        return Mathf.Max(1, Mathf.RoundToInt(mealBuffDurationSeconds / FallbackBarDuration));
    }
    return Mathf.Max(1, Mathf.RoundToInt(mealBuffDurationSeconds / IntervalTimer.Instance.IntervalDuration));
}
```

Called from the `GrantMealBuff` call site (~line 690 in current source), replacing `GrantMealBuff(8)`.

Note: tick count is computed once at grant time. If `baseIntervalDuration` changes at runtime, buff duration in wall-clock seconds will drift — this is acceptable and out of scope.

### OnBarTick Logic

```
OnBarTick(bar):
  if !isInitialized || health.IsDestroyed → return
  if hasMealBuff:
    decrement mealBuffTicksRemaining
    if mealBuffTicksRemaining <= 0: ExpireMealBuff()
    return  ← deliberate: no bar-tick action while buffed (OnHalfBarTick handles it);
              on the expiry beat, the last buffed action already fired via OnHalfBarTick
  // Not buffed — normal worker action
  respect intervalMultiplier
  dispatch ClockworkTick coroutine
```

### OnHalfBarTick Logic

```
OnHalfBarTick(bar):
  if !isInitialized || health.IsDestroyed → return
  // Only subscribed while buffed (managed by GrantMealBuff/ExpireMealBuff)
  respect intervalMultiplier
  dispatch ClockworkTick coroutine
```

### Beat 1 Overlap — Actual Firing Order

On beat 1, `IntervalTimer` fires `OnHalfBar` **before** `OnBar` (verified in IntervalTimer.cs lines 75–82). While buffed on beat 1:
1. `OnHalfBarTick` fires → worker acts
2. `OnBarTick` fires → decrement buff, return early (no second action)

On beat 3, only `OnHalfBar` fires:
1. `OnHalfBarTick` fires → worker acts

Result: 2 actions per bar while buffed, buff duration decrements once per bar.

On expiry beat 1: `OnHalfBarTick` fires first → worker acts. Then `OnBarTick` fires → decrements to 0, calls `ExpireMealBuff()` (unsubscribes `OnHalfBar`), `hasMealBuff = false` → returns early (no redundant action). Worker had their last buffed action at beat 1, then transitions to bar timing from the next bar.

### `attackIntervalMultiplier` Interaction

The `intervalMultiplier` check uses `barNumber % attackIntervalMultiplier`. `OnHalfBar` passes the same `barNumber` for both beat 1 and beat 3 of a bar, so both half-bar ticks of a given bar will either both fire or both skip.

Policy: the multiplier check applies identically to half-bar ticks. A worker with `attackIntervalMultiplier = 2` acts twice on even bars and zero times on odd bars — a net rate of 1 action/bar average, matching an unbuffed worker. This is correct: the buff doubles their *relative* rate (2× their normal multiplied cadence), not necessarily 2× an unbuffed worker's rate.

Buff decay still decrements once per bar regardless of the multiplier. This is intentional — duration in wall-clock seconds should be consistent across all workers.

## What Does NOT Change

- `IntervalTimer.cs` — no changes
- `MealBuffSource.cs` — no changes
- `MealBuffVisual.cs` — no changes; it reads `HasMealBuff` and `MealBuffTicksRemaining` which remain accurate
- The starvation system — unchanged; starvation resets on any interaction regardless of buff state
- The "skip meals when already buffed" scan logic — unchanged

## Out of Scope

- Different foods granting different buff durations (duration lives on GridEntityActor, not MealBuffSource)
- Visual changes to indicate the speed increase
- Stacking or refreshing the buff while active (existing guard in `GrantMealBuff` prevents this)
