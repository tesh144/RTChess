# Meal Buff System — Design Spec
**Date:** 2026-03-23
**Card:** Trello #80 — Meal Buff System: Duration, Consumption & Worker Visuals
**Status:** Awaiting implementation

---

## Overview

Full redo of the meal buff system. The core tick timer was working; everything else needed redesign. This spec covers all three pillars: duration/timer, consumption mechanics, and worker visual.

A bug was fixed as part of this work: `MealBuffSource` had a passive aura that scanned workers within 3 cells every second and buffed them all automatically. That aura has been removed. `MealBuffSource` is now a plain marker component holding only the `icon` sprite. Buff is granted only via direct worker interaction.

---

## 1. Duration & Timer

No changes to the timer logic. The buff lasts `mealBuffDurationSeconds` (default 30s) converted to interval ticks via `ConvertDurationToTicks()`. While buffed, the worker subscribes to `OnHalfBar`, doubling their action rate. The buff decrements on each full bar tick and expires cleanly at 0.

---

## 2. Consumption Mechanics

### Feast HP depletion
Workers deal their normal `atkPower` damage to the feast on each interaction — this is unchanged. `FeastVisualDegradation` already handles the colour fade as HP drops. When the feast reaches 0 HP it is destroyed through the standard `GridEntityHealth` path. No changes needed here.

### Damage expiry
**No damage expiry.** The buff runs its full duration regardless of combat. This keeps the mechanic simple and readable while the system is being balanced.

### Re-eating
Workers may return to the feast before the buff expires and eat again. Doing so **resets the timer to full duration** — it does not stack or extend beyond one full duration. The `OnHalfBar` subscription is left untouched (already active), so no double-subscription can occur.

**Changes required — three locations:**

**Location 1 — `GridEntityActor` attack loop skip guard (~line 638):**
Remove this line entirely:
```csharp
if (hasMealBuff && occupant.GetComponent<MealBuffSource>() != null)
    continue;
```
Workers now treat the feast as a normal interactable target regardless of buff state.

**Location 2 — `GridEntityActor.PerformStrongInteraction` buff grant (~line 745):**
Change the condition from:
```csharp
if (mealSource != null && !hasMealBuff)
```
to:
```csharp
if (mealSource != null)
```
This allows `GrantMealBuff` to be called on re-eat. `GrantMealBuff` itself handles the already-buffed case (see Location 3).

**Location 3 — `GridEntityActor.GrantMealBuff(int durationTicks)`:**
Replace the early-return guard with a reset path:
```csharp
public void GrantMealBuff(int durationTicks)
{
    if (hasMealBuff)
    {
        // Re-eat while already buffed: reset timer and visual, don't re-subscribe
        mealBuffTicksRemaining = durationTicks;
        GetComponent<MealBuffVisual>()?.Restart();
        return;
    }

    hasMealBuff = true;
    mealBuffTicksRemaining = durationTicks;

    if (IntervalTimer.Instance != null)
        IntervalTimer.Instance.OnHalfBar += OnHalfBarTick;
}
```

---

## 3. Worker Visual

### Existing: Floating food icon (`MealBuffVisual`)
The existing icon behaviour is kept as-is: golden food sprite floats above the worker, bobs gently, shrinks proportionally from full size to zero over the buff duration, pulses on each tick, and fades out on expiry. `Restart()` already resets `pulseLerp = 0f` and `currentBaseScale` — the tint additions to `Restart()` are additive only.

### New: Body renderer tint

While buffed, all body renderers on the worker are tinted warm gold. The tint pulses in sync with the interval tick alongside the icon. As the buff runs down, the tint baseline decays linearly. In the last 3 ticks (`FAST_FADE_TICKS`) the baseline decays faster via a quadratic multiplier, so the glow visibly leaves the worker before the final `FadeOut()` fires. On expiry the renderer colours fade back to their originals over the same `FADE_DURATION` as the icon.

---

### Implementation — additions to `MealBuffVisual`

**New fields:**
```csharp
private Renderer[]  bodyRenderers;   // MeshRenderer/SkinnedMeshRenderer on worker (not icon child)
private Material[]  sharedMats;      // cached shared materials — used to restore on OnDestroy
private Material[]  bodyMaterials;   // instantiated copies — modified for tint, destroyed in OnDestroy
private Color[]     originalColors;  // colours from shared mats before buff applied
private float       tintAlpha = 1f;  // 0–1 current tint intensity, decays each tick
private static readonly Color BODY_TINT   = new Color(1f, 0.85f, 0.4f, 1f); // warm gold
private const int   FAST_FADE_TICKS = 3;  // last N ticks use accelerated decay
```

**In `Start()` — renderer cache MUST come before the icon child is created:**
```csharp
// ── Body tint setup (must run before icon child is created) ──────────────
bodyRenderers = GetComponentsInChildren<Renderer>();
sharedMats    = new Material[bodyRenderers.Length];
bodyMaterials = new Material[bodyRenderers.Length];
originalColors = new Color[bodyRenderers.Length];
for (int i = 0; i < bodyRenderers.Length; i++)
{
    sharedMats[i]    = bodyRenderers[i].sharedMaterial;
    bodyMaterials[i] = bodyRenderers[i].material;      // auto-instantiates a per-instance copy
    originalColors[i] = sharedMats[i].color;
}
tintAlpha = 1f;
ApplyTint(tintAlpha);

// ── Icon child (created after renderer cache so GetComponentsInChildren above won't capture it) ──
iconObject = new GameObject("MealBuffIcon");
// ... rest of existing icon setup unchanged
```

**In `OnDestroy()` — restore shared materials and destroy instances:**
```csharp
// Restore each renderer to its shared material, then destroy the instances
for (int i = 0; i < bodyRenderers.Length; i++)
{
    if (bodyRenderers[i] != null && sharedMats[i] != null)
        bodyRenderers[i].sharedMaterial = sharedMats[i];  // restores original appearance
    if (bodyMaterials[i] != null)
        Object.Destroy(bodyMaterials[i]);
}
// ... existing OnDestroy code (icon + tick unsubscribe) unchanged
```

**In `OnTick(int intervalCount)` — after updating `currentBaseScale`:**
```csharp
// Decay tint baseline — linear with quadratic acceleration in last FAST_FADE_TICKS ticks
float ticksLeft       = Mathf.Max(0, actor.MealBuffTicksRemaining);
float linearDecay     = initialTicks > 0 ? ticksLeft / (float)initialTicks : 0f;
float fastMultiplier  = ticksLeft <= FAST_FADE_TICKS
    ? (ticksLeft / (float)FAST_FADE_TICKS)
    : 1f;
tintAlpha = linearDecay * fastMultiplier;
// pulseLerp reset is already on the next line in existing code — no change needed
```

**In `Update()` — alongside existing icon pulse, add tint pulse:**
```csharp
if (bodyMaterials != null && bodyMaterials.Length > 0)
{
    float displayAlpha = Mathf.Lerp(Mathf.Min(tintAlpha * PULSE_PEAK, 1f), tintAlpha, pulseLerp);
    ApplyTint(displayAlpha);
}
```
> `PULSE_PEAK` multiplication is clamped to 1f before `Lerp`, so `displayAlpha` never exceeds 1f and `Color.Lerp` receives only valid 0–1 values.

**`FadeOut()` coroutine — add tint restoration alongside existing icon fade:**
```csharp
// Inside the while (elapsed < FADE_DURATION) loop:
float tintAtFadeStart = tintAlpha; // capture at fade entry (before the loop)
// ... per-frame inside loop:
ApplyTint(Mathf.Lerp(tintAtFadeStart, 0f, t));

// After the loop, before Destroy(this):
ApplyTint(0f); // ensure fully restored
```

**`Restart()` — add tint reset (existing icon/pulseLerp resets are unchanged):**
```csharp
// Add to existing Restart() body:
tintAlpha = 1f;
ApplyTint(1f);
```

**`ApplyTint(float alpha)` — new private helper:**
```csharp
private void ApplyTint(float alpha)
{
    if (bodyMaterials == null) return;
    for (int i = 0; i < bodyMaterials.Length; i++)
    {
        if (bodyMaterials[i] == null) continue;
        bodyMaterials[i].color = Color.Lerp(originalColors[i], BODY_TINT, alpha);
    }
}
```

---

## 4. File Change Summary

| File | Change |
|------|--------|
| `MealBuffSource.cs` | **Done.** Stripped to marker component with `icon` field only. |
| `GridEntityActor.cs` | 3 changes: remove attack loop skip guard; remove `!hasMealBuff` from buff-grant condition in `PerformStrongInteraction`; update `GrantMealBuff` to reset timer on re-eat. |
| `MealBuffVisual.cs` | Add body renderer tint: cache shared mats + instantiate copies in `Start()` (before icon child); `ApplyTint()` helper; pulse in `Update()`; decay in `OnTick()`; restore + destroy in `OnDestroy()`; reset in `Restart()`; fade in `FadeOut()`. |

No other files require changes.

---

## 5. Out of Scope

- Glow particle effects
- Damage expiry mechanic
- Changes to feast HP values or `FeastVisualDegradation`
- Changes to the `OnHalfBar` double-speed logic
