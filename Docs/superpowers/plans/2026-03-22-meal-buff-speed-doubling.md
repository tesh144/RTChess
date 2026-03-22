# Meal Buff Speed Doubling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When a worker eats a meal, they subscribe to `OnHalfBar` instead of `OnBar` for the buff duration, doubling their action rate. Buff duration is inspector-configurable (default 30s).

**Architecture:** Split `GridEntityActor.OnIntervalTick` into `OnBarTick` (always subscribed — handles decay + unbuffed actions) and `OnHalfBarTick` (subscribed only while buffed — handles double-speed actions). `GrantMealBuff` subscribes `OnHalfBarTick`; `ExpireMealBuff` unsubscribes it. Duration is converted from seconds to bar ticks at grant time.

**Tech Stack:** Unity 2022.3 / C# / `IntervalTimer` event system (`OnBar`, `OnHalfBar`)

---

## File Map

| File | Change |
|------|--------|
| `Assets/Scripts/LittleCafe/GridEntityActor.cs` | All changes — add inspector field, split tick handler, add `ExpireMealBuff`, update `GrantMealBuff`, update `OnEnable`/`OnDisable` |

No other files change.

---

### Task 1: Add inspector field and duration conversion helper

**Files:**
- Modify: `Assets/Scripts/LittleCafe/GridEntityActor.cs`

The existing `[Header("Starvation")]` block is around line 40. Add the new `[Header("Meal Buff")]` inspector field directly after it. Add the `ConvertDurationToTicks()` private method near the existing `GrantMealBuff` method (around line 860).

- [ ] **Step 1: Add the inspector field**

In `GridEntityActor.cs`, find the `[Header("Debug")]` block (around line 46) and insert the new header block before it:

```csharp
[Header("Meal Buff")]
[Tooltip("How long the meal buff lasts in real seconds. Converted to bar ticks on grant.")]
[SerializeField] private float mealBuffDurationSeconds = 30f;

[Header("Debug")]
```

- [ ] **Step 2: Add the duration conversion helper**

Find the `GrantMealBuff` method (around line 860). Add `ConvertDurationToTicks()` immediately above it:

```csharp
private int ConvertDurationToTicks()
{
    if (IntervalTimer.Instance == null)
    {
        // IntervalTimer not ready — should not happen in normal play.
        // FallbackBarDuration must match IntervalTimer.baseIntervalDuration inspector default (2.0f).
        // If that default changes, update this constant to match.
        const float FallbackBarDuration = 2f;
        Debug.LogWarning("[GridEntityActor] IntervalTimer.Instance is null — using fallback bar duration");
        return Mathf.Max(1, Mathf.RoundToInt(mealBuffDurationSeconds / FallbackBarDuration));
    }
    return Mathf.Max(1, Mathf.RoundToInt(mealBuffDurationSeconds / IntervalTimer.Instance.IntervalDuration));
}
```

- [ ] **Step 3: Verify the project compiles**

Open Unity or run a script compilation check. Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/LittleCafe/GridEntityActor.cs
git commit -m "feat: add mealBuffDurationSeconds inspector field and ConvertDurationToTicks helper"
```

---

### Task 2: Add `ExpireMealBuff` and update `GrantMealBuff`

**Files:**
- Modify: `Assets/Scripts/LittleCafe/GridEntityActor.cs`

`GrantMealBuff` is around line 860. The call site `GrantMealBuff(8)` is around line 690.

- [ ] **Step 1: Replace `GrantMealBuff` with the updated version**

Find the existing `GrantMealBuff` method:

```csharp
public void GrantMealBuff(int durationTicks)
{
    hasMealBuff = true;
    mealBuffTicksRemaining = durationTicks;
}
```

Replace it with:

```csharp
public void GrantMealBuff(int durationTicks)
{
    if (hasMealBuff) return; // already buffed — prevents double-subscription

    hasMealBuff = true;
    mealBuffTicksRemaining = durationTicks;

    if (IntervalTimer.Instance != null)
        IntervalTimer.Instance.OnHalfBar += OnHalfBarTick;
}
```

- [ ] **Step 2: Add `ExpireMealBuff` immediately after `GrantMealBuff`**

```csharp
private void ExpireMealBuff()
{
    hasMealBuff = false;
    mealBuffTicksRemaining = 0;

    if (IntervalTimer.Instance != null)
        IntervalTimer.Instance.OnHalfBar -= OnHalfBarTick;

    if (verboseLogging)
        Debug.Log($"[GridEntityActor] {gameObject.name} meal buff expired");
}
```

- [ ] **Step 3: Update the `GrantMealBuff` call site**

Find around line 690:

```csharp
GrantMealBuff(8); // 8 interval ticks
```

Replace with:

```csharp
GrantMealBuff(ConvertDurationToTicks());
```

- [ ] **Step 4: Verify the project compiles**

Expected: no errors. `OnHalfBarTick` doesn't exist yet — Unity will not error at compile time for a missing event subscriber (it's an `Action<int>` delegate, so this step may produce an error). If so, add a temporary stub:

```csharp
private void OnHalfBarTick(int bar) { }
```

Then continue to Task 3 where it gets its real implementation.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/LittleCafe/GridEntityActor.cs
git commit -m "feat: update GrantMealBuff to subscribe OnHalfBar, add ExpireMealBuff"
```

---

### Task 3: Split `OnIntervalTick` into `OnBarTick` and `OnHalfBarTick`

**Files:**
- Modify: `Assets/Scripts/LittleCafe/GridEntityActor.cs`

The existing `OnIntervalTick` method is around line 256. `OnEnable` and `OnDisable` are around lines 224 and 232.

The existing `OnIntervalTick` body:
1. Guards (`isInitialized`, `IsDestroyed`)
2. Buff decay block
3. `attackIntervalMultiplier` check
4. Coroutine dispatch switch

This splits into `OnBarTick` (steps 1–4 when not buffed; steps 1–2 when buffed) and `OnHalfBarTick` (steps 1, 3–4 when buffed).

- [ ] **Step 1: Replace `OnIntervalTick` with `OnBarTick`**

Delete the existing `OnIntervalTick` method entirely and replace it with `OnBarTick`:

```csharp
private void OnBarTick(int bar)
{
    if (!isInitialized) return;
    if (health != null && health.IsDestroyed) return;

    // Decay meal buff on every bar tick
    if (hasMealBuff)
    {
        mealBuffTicksRemaining--;
        if (mealBuffTicksRemaining <= 0)
            ExpireMealBuff();

        // Deliberate: no bar-tick action while buffed.
        // OnHalfBarTick handles actions; last buffed action already fired via OnHalfBarTick.
        return;
    }

    // Not buffed — normal action cadence
    if (attackIntervalMultiplier > 1 && bar % attackIntervalMultiplier != 0)
        return;

    if (interactionCoroutine != null)
        StopCoroutine(interactionCoroutine);

    switch (behaviorType)
    {
        case BehaviorType.RotateAndInteract:
            interactionCoroutine = StartCoroutine(ClockworkTickInteract());
            break;
        case BehaviorType.RotateAndMove:
            interactionCoroutine = StartCoroutine(ClockworkTickMove());
            break;
        case BehaviorType.RotateRotateMove:
            interactionCoroutine = StartCoroutine(ClockworkTickRotateRotateMove());
            break;
        default:
            interactionCoroutine = StartCoroutine(ClockworkTickInteract());
            break;
    }
}
```

- [ ] **Step 2: Add `OnHalfBarTick` immediately after `OnBarTick`**

Remove the temporary stub from Task 2 Step 4 if it exists, and replace with:

```csharp
/// <summary>
/// Only subscribed while the meal buff is active (see GrantMealBuff/ExpireMealBuff).
/// Fires at beats 1 and 3, doubling the worker's action rate.
/// </summary>
private void OnHalfBarTick(int bar)
{
    if (!isInitialized) return;
    if (health != null && health.IsDestroyed) return;

    // Respect interval multiplier — same barNumber check as OnBarTick.
    // Both beat-1 and beat-3 of a given bar share the same barNumber,
    // so both fire or both skip together on multiplier workers.
    if (attackIntervalMultiplier > 1 && bar % attackIntervalMultiplier != 0)
        return;

    if (interactionCoroutine != null)
        StopCoroutine(interactionCoroutine);

    switch (behaviorType)
    {
        case BehaviorType.RotateAndInteract:
            interactionCoroutine = StartCoroutine(ClockworkTickInteract());
            break;
        case BehaviorType.RotateAndMove:
            interactionCoroutine = StartCoroutine(ClockworkTickMove());
            break;
        case BehaviorType.RotateRotateMove:
            interactionCoroutine = StartCoroutine(ClockworkTickRotateRotateMove());
            break;
        default:
            interactionCoroutine = StartCoroutine(ClockworkTickInteract());
            break;
    }
}
```

- [ ] **Step 3: Update `OnEnable` to use `OnBarTick` and re-subscribe `OnHalfBarTick` if buffed**

Find the existing `OnEnable`:

```csharp
private void OnEnable()
{
    if (IntervalTimer.Instance != null)
    {
        IntervalTimer.Instance.OnBar += OnIntervalTick;
    }
}
```

Replace with:

```csharp
private void OnEnable()
{
    if (IntervalTimer.Instance != null)
    {
        IntervalTimer.Instance.OnBar += OnBarTick;

        // Re-subscribe half-bar if the worker was disabled while buffed
        if (hasMealBuff)
            IntervalTimer.Instance.OnHalfBar += OnHalfBarTick;
    }
}
```

- [ ] **Step 4: Update `OnDisable` to unsubscribe both handlers**

Find the existing `OnDisable` unsubscribe line:

```csharp
IntervalTimer.Instance.OnBar -= OnIntervalTick;
```

Replace it with both unsubscribes (keep it inside the existing `if (IntervalTimer.Instance != null)` null guard):

```csharp
IntervalTimer.Instance.OnBar -= OnBarTick;
IntervalTimer.Instance.OnHalfBar -= OnHalfBarTick; // safe no-op if not subscribed
```

- [ ] **Step 5: Verify the project compiles with zero errors**

Open Unity. Expected: no compile errors. Fix any if found before continuing.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/LittleCafe/GridEntityActor.cs
git commit -m "feat: split OnIntervalTick into OnBarTick/OnHalfBarTick for meal buff speed doubling"
```

---

### Task 4: Manual play test

No automated tests exist for this system (Unity game — tick-based). Verify the feature works in the editor.

- [ ] **Step 1: Enter Play Mode with a Feast placed near a worker**

Place a worker and a Feast (MealBuffSource) adjacent. Observe the worker eat the meal.

Expected:
- Food icon flies from Feast to worker (existing FX — unchanged)
- Aura appears on worker (existing MealBuffVisual — unchanged)
- Worker now visibly acts twice as often (rotates and interacts at half-bar cadence)

- [ ] **Step 2: Verify buff duration in Inspector**

Select the worker in the hierarchy while in Play Mode. In the Inspector:
- `Meal Buff Duration Seconds` field is visible and shows `30`
- `Meal Buff Ticks Remaining` (if exposed via a debug view) counts down

Change `Meal Buff Duration Seconds` to `4` (at 2s/bar = 2 bar ticks). Eat a meal. Worker should return to normal speed after ~4 seconds.

- [ ] **Step 3: Verify buff expiry is clean**

Watch for the worker's action rate returning to one-per-bar after expiry. No errors in the console. Aura flickers and disappears (existing MealBuffVisual behavior — unchanged).

- [ ] **Step 4: Verify worker skips re-eating while buffed**

While the worker has an active buff, place another Feast nearby. Worker should ignore it until the buff expires (existing `!hasMealBuff` guard — unchanged).

- [ ] **Step 5: Commit if any minor fixes were made during testing**

```bash
git add Assets/Scripts/LittleCafe/GridEntityActor.cs
git commit -m "fix: <describe any fix found during play test>"
```

If no fixes needed, skip this step.
