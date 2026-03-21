# Meal Buff Visual — Glow + Icon Arc Transfer

**Date:** 2026-03-21
**Trello:** Card #42 — Meal Buff Visual — Glow + Particle Transfer
**Branch:** ClockworkWarcraft

---

## Overview

When a worker interacts with a Feast (MealBuffSource), two visuals fire:

1. **Icon arc transfer** — the food icon flies in a small arc from the Feast to the worker.
2. **Particle aura** — a continuous golden-white glow orbits the worker for the buff's duration, flickering in the final 3 ticks before expiring.

The buff mechanic already exists in `GridEntityActor` (`hasMealBuff`, `mealBuffTicksRemaining`, `GrantMealBuff`). This spec covers visuals only.

---

## Architecture

### New files

| File | Namespace | Purpose |
|------|-----------|---------|
| `Assets/ClockworkCraft/Scripts/UI/IconFlyFX.cs` | `ClockworkCraft` | General-purpose world-to-world icon arc. Singleton. |
| `Assets/Scripts/LittleCafe/MealBuffVisual.cs` | `LittleCafe` | Persistent particle aura component attached to the worker while buffed. Self-destructs on buff expiry. |

### Modified files

| File | Change |
|------|--------|
| `Assets/Scripts/LittleCafe/MealBuffSource.cs` | Add `[SerializeField] public Sprite icon` field. |
| `Assets/Scripts/LittleCafe/GridEntityActor.cs` | Call `IconFlyFX` and add `MealBuffVisual` inside `GrantMealBuff()`. |

---

## IconFlyFX

**Location:** `Assets/ClockworkCraft/Scripts/UI/IconFlyFX.cs`
**Namespace:** `ClockworkCraft`
**Pattern:** Singleton (`Instance`), same canvas/Image pattern as `ResourceLootFX`.

### API

```csharp
IconFlyFX.Instance.SpawnArc(Sprite icon, Vector3 worldFrom, Vector3 worldTo)
```

### Animation

Three phases, total ~0.55s:

1. **Pop-in** (0.15s) — Icon spawns at `worldFrom` screen position, scales from 0 → 1 with ease-out.
2. **Arc** (0.4s) — Smooth lerp from screen-start to screen-end, with `Mathf.Sin(t * Mathf.PI) * arcHeight` added to Y for the arc curve. Arc height: ~60 screen pixels.
3. **Arrival** — Scale shrinks to zero in the final 20% of the arc phase. GameObject destroyed on completion.

### Implementation notes

- Converts world positions to screen space each frame (handles camera movement mid-flight).
- Uses a `Canvas` `Image` component with `sortingOrder = 100` (same as `ResourceLootFX`).
- Icon size: ~56px (smaller than loot icons — this is a single focused transfer, not a burst).
- No pooling needed for now (single icon per interaction).

### Future use

This is the canonical "unit picks up item from world" visual. Any future pickup interaction (workers collecting items from environment) calls `SpawnArc` with the appropriate icon sprite and world positions.

---

## MealBuffVisual

**Location:** `Assets/Scripts/LittleCafe/MealBuffVisual.cs`
**Namespace:** `LittleCafe`
**Lifecycle:** Added to the worker's GameObject by `GridEntityActor.GrantMealBuff()`. Self-destructs when buff expires.

### Aura particles

- **Color:** Golden-white — `new Color(1f, 0.92f, 0.45f)` (Unlit/Color, no transparency).
- **Spawn rate:** ~1.5 particles/second in normal mode.
- **Per-particle behavior:** Spawns at worker position + random XZ offset within 0.35 units. Drifts upward over ~1.2s. Shrinks from start size to zero over its lifetime (same quadratic ease-in as `PoofEffect`).
- **Particle size:** 0.05–0.10 units (slightly smaller than PoofEffect).

### Flicker mode (last 3 ticks)

Activated when `GridEntityActor.mealBuffTicksRemaining <= 3` on a tick event:

- Spawn rate triples (~4.5/sec).
- Particle lifetime halves (~0.6s).
- Rapid appearance and disappearance reads as an unstable flicker, telegraphing buff expiry.

### Expiry

- `MealBuffVisual` subscribes to `IntervalTimer.OnIntervalTick` and holds a reference to its parent `GridEntityActor`.
- Each tick: check `actor.HasMealBuff`. When false, stop spawning. Wait for in-flight particles to finish their lifetimes, then `Destroy(gameObject)` on self.
- No cleanup required in `GridEntityActor`.

---

## GridEntityActor changes

Only `GrantMealBuff()` is modified. The call site already has `target` (the Feast GameObject) in scope:

```csharp
public void GrantMealBuff(int durationTicks)
{
    hasMealBuff = true;
    mealBuffTicksRemaining = durationTicks;

    // Icon arc: food sprite flies from feast to this worker
    MealBuffSource mealSource = target.GetComponent<MealBuffSource>();
    if (mealSource != null && mealSource.icon != null)
        IconFlyFX.Instance?.SpawnArc(mealSource.icon, target.transform.position, transform.position);

    // Aura: attach visual component (guard against duplicate on re-grant, though re-grant is blocked by scan logic)
    if (GetComponent<MealBuffVisual>() == null)
        gameObject.AddComponent<MealBuffVisual>();
}
```

No expiry code needed in `GridEntityActor` — `MealBuffVisual` polls `HasMealBuff` directly.

---

## Design decisions

| Decision | Choice | Reason |
|----------|--------|--------|
| Glow color | Golden-white `(1f, 0.92f, 0.45f)` | Bright/energetic; readable against PEPO art style |
| Glow technique | Runtime PoofSphere-style particles | Zero new assets; matches existing VFX language |
| Flicker style | Faster spawn + shorter lifetime | Distinct from normal aura; no shader changes needed |
| Buff expiry on re-eat | Do nothing (no reset) | Worker scan already skips meals while buffed; simplest behavior |
| Transfer animation | Single food icon arc (IconFlyFX) | Reusable for all future world pickups; matches existing loot fly pattern |
| MealBuffSource.icon | SerializeField Sprite, set in Inspector | Food sprite already exists in project from animal kill loot system |

---

## Human setup checklist

After implementation, a developer must do the following in the Unity Editor:

- [ ] Add `IconFlyFX` component to the scene's manager object (alongside `ResourceLootFX`).
- [ ] Assign the food/meat sprite to the `icon` field on the `MealBuffSource` component in the Feast prefab.
- [ ] Verify `IconFlyFX` finds the scene Canvas correctly at runtime (check for null warnings in Play mode).
- [ ] Play-test: worker eats Feast → icon arc appears → golden aura activates → flicker in last 3 ticks → aura disappears cleanly.
- [ ] Tune `arcHeight`, particle spawn rate, and particle size if the visual feels too subtle or too noisy.
