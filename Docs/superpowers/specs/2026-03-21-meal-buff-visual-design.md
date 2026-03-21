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
| `Assets/Scripts/LittleCafe/MealBuffVisual.cs` | `LittleCafe` | Persistent particle aura component attached to the worker while buffed. Self-destructs (component only) on buff expiry. |

### Modified files

| File | Change |
|------|--------|
| `Assets/Scripts/LittleCafe/MealBuffSource.cs` | Add `public Sprite icon` field (public so `GridEntityActor` can read it; visible in Inspector by default for public fields). |
| `Assets/Scripts/LittleCafe/GridEntityActor.cs` | Add `public int MealBuffTicksRemaining => mealBuffTicksRemaining;` property. Add visual calls at the existing `MealBuffSource` check in `ScanAndInteract`. `GrantMealBuff()` itself is not modified. |

---

## IconFlyFX

**Location:** `Assets/ClockworkCraft/Scripts/UI/IconFlyFX.cs`
**Namespace:** `ClockworkCraft`
**Pattern:** Scene-placed singleton (same as `ResourceLootFX`) — must be added as a component to a manager object in the scene. Not self-initializing. `Instance` is assigned in `Awake()` with the standard `if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this;` pattern.

### API

```csharp
IconFlyFX.Instance.SpawnArc(Sprite icon, Vector3 worldFrom, Vector3 worldTo)
```

`SpawnArc` is a no-op with a null-check guard if `canvas == null` (handles scenes where the canvas isn't ready yet).

### Animation

Three phases, total ~0.55s, driven by a **coroutine** (same update strategy as `ResourceLootFX`):

1. **Pop-in** (0.15s) — Snapshot `worldFrom` to screen space once at coroutine start. Icon spawns at that position, scales from 0 → 1 with ease-out. Position does not update during pop-in.
2. **Arc** (0.4s) — Each coroutine frame: re-converts `worldFrom`/`worldTo` to screen space (handles moving camera), lerps from screen-start to screen-end. `Mathf.Sin(t * Mathf.PI) * arcHeight` added to Y for the curve. Arc height: ~60 screen pixels.
3. **Arrival** — Scale shrinks to zero in the final 20% of the arc phase. `Destroy(iconGameObject)` on completion.

### Implementation notes

- **Canvas reference:** In `Start()`, use `FindObjectOfType<Canvas>()` — same as `ResourceLootFX`. The ClockworkCraft scene has a single overlay Canvas used by all UI systems; both `ResourceLootFX` and `IconFlyFX` will find it. If `canvas` is null at `SpawnArc` call time, the call is silently skipped.
- **Sorting:** When creating the icon GameObject at runtime, `AddComponent<Canvas>()` on it with `overrideSorting = true` and `sortingOrder = 100` — same as `ResourceLootFX.CreateSpriteParticle()`. Created at runtime per-icon, not scene-placed. Works regardless of scene canvas render mode.
- **Icon size:** ~56px.
- **No pooling** needed for now (single icon per interaction).

### Future use

This is the canonical "unit picks up item from world" visual. Any future world-to-world pickup calls `SpawnArc` with the appropriate icon and positions.

---

## MealBuffVisual

**Location:** `Assets/Scripts/LittleCafe/MealBuffVisual.cs`
**Namespace:** `LittleCafe`
**Lifecycle:** Added to the worker's root GameObject (the same GameObject `GridEntityActor` lives on) via `gameObject.AddComponent<MealBuffVisual>()`. On buff expiry, destroys **only itself** (`Destroy(this)`) — not the parent GameObject.

### Initialization

In `Start()`, call `GetComponent<GridEntityActor>()` on the same GameObject. `GridEntityActor` is confirmed to live on the worker's root node. Subscribe to `IntervalTimer.Instance.OnIntervalTick` with a handler of signature `void OnTick(int intervalCount)`. The `intervalCount` parameter is not used. If `IntervalTimer.Instance` is null, log a warning and return early — `MealBuffVisual` will be inert but won't throw.

**Required `GridEntityActor` accessor:** `mealBuffTicksRemaining` is currently a private field. Add a public read-only property:
```csharp
public int MealBuffTicksRemaining => mealBuffTicksRemaining;
```
`MealBuffVisual` reads `actor.MealBuffTicksRemaining` for the flicker check.

### Aura particles

- **Color:** `new Color(1f, 0.92f, 0.45f)`, `Shader.Find("Unlit/Color")`, no transparency.
- **Spawn rate:** Use a spawn interval threshold: `spawnInterval = 0.667f` seconds (= 1/1.5) in normal mode, `spawnInterval = 0.222f` (= 1/4.5) in flicker mode. In `Update`, accumulate `timeSinceLastSpawn += Time.deltaTime`; when `timeSinceLastSpawn >= spawnInterval`, reset to zero and spawn one particle.
- **Per-particle behavior:** Spawns at worker position + random XZ offset within 0.35 units. Drifts upward-only (no gravity), lifetime ~1.2s. Shrinks to zero over lifetime using quadratic ease-in (same curve as `PoofEffect`).
- **Particle size:** 0.05–0.10 units.
- **Implementation pattern:** Duplicate `PoofEffect`'s `GameObject.CreatePrimitive(PrimitiveType.Sphere)` + `Shader.Find("Unlit/Color")` pattern inline. Do **not** call `PoofEffect.Spawn()` — its burst behavior (radial spread, gravity, upward bias) is wrong for a continuous aura. Each sphere is its own GameObject, parented to the world root (not the worker), tracking its own lifetime in `Update`. Colliders removed (same as `PoofEffect`).

### Flicker mode (last 3 ticks)

On a tick event, when `actor.mealBuffTicksRemaining <= 3`, set a **one-way `bool isFlickering` flag** (never reset back to false). On transition, reset `timeSinceLastSpawn = 0` so the first flicker particle spawns immediately.

In flicker mode:
- Spawn rate triples (~4.5/sec via a lower `spawnInterval` threshold).
- Per-particle lifetime halves (~0.6s).

### State fields

```csharp
private bool isFlickering = false; // one-way latch, set when ticksRemaining <= 3
private bool isExpiring = false;   // set when HasMealBuff becomes false
private float timeSinceLastSpawn = 0f;
```

### Tick evaluation order

On each tick, evaluate in this order:
1. **Expiry first:** if `actor.HasMealBuff == false` and `!isExpiring`, set `isExpiring = true`, stop spawning, start the `WaitForSeconds(1.2f)` coroutine → `Destroy(this)`.
2. **Flicker (only if not expiring):** if `actor.mealBuffTicksRemaining <= 3` and `!isFlickering`, set `isFlickering = true`, reset `timeSinceLastSpawn = 0`.

Expiry takes full priority; flicker and expiry never fire on the same tick.

### Expiry

- `isExpiring = true` stops all new particle spawns in `Update`.
- `WaitForSeconds(1.2f)` coroutine lets in-flight spheres finish before `Destroy(this)` fires. Destroys only this component, not the worker GameObject.
- **`OnDestroy()`:** Unsubscribes from `IntervalTimer.Instance.OnIntervalTick`. This fires whether the coroutine completes normally or is cancelled externally (e.g. worker death), guaranteeing cleanup.
- **Orphaned particles on worker death:** Sphere GameObjects are parented to scene root and run independently. If the worker is destroyed mid-buff, in-flight spheres continue animating and self-destruct within 1.2s. Acceptable cosmetic behaviour.
- No cleanup required in `GridEntityActor`.

---

## GridEntityActor changes

`GrantMealBuff()` is **not** modified. The visual calls are added at the **existing `MealBuffSource` check** in `ScanAndInteract` (around line 691), where `target` and `mealSource` are already local variables:

```csharp
MealBuffSource mealSource = target.GetComponent<MealBuffSource>();
if (mealSource != null && !hasMealBuff)
{
    GrantMealBuff(8); // unchanged

    // NEW: icon arc — food sprite flies from feast to this worker
    if (mealSource.icon != null)
        IconFlyFX.Instance?.SpawnArc(mealSource.icon, target.transform.position, transform.position);

    // NEW: aura — attach visual component (safety guard; re-grant is blocked by scan logic)
    if (GetComponent<MealBuffVisual>() == null)
        gameObject.AddComponent<MealBuffVisual>();
}
```

`GrantMealBuff` remains a pure mechanic method. No expiry code needed in `GridEntityActor` — `MealBuffVisual` polls `HasMealBuff` directly each tick.

---

## Design decisions

| Decision | Choice | Reason |
|----------|--------|--------|
| Glow color | Golden-white `(1f, 0.92f, 0.45f)` | Bright/energetic; readable against PEPO art style |
| Glow technique | Runtime sphere particles (inline PoofEffect pattern) | Zero new assets; matches existing VFX language |
| Flicker | Faster spawn + shorter lifetime; one-way `isFlickering` flag | Distinct from normal aura; no shader changes needed; can't un-flicker |
| Buff expiry on re-eat | Do nothing | Worker scan skips `MealBuffSource` while `hasMealBuff` is true; `GrantMealBuff` is never re-called during active buff. Guard is safety net only. |
| Transfer animation | Single food icon arc (IconFlyFX) | Reusable for all future world pickups; matches existing loot fly pattern |
| MealBuffSource.icon | `public Sprite icon`, set in Inspector | Public so GridEntityActor can access it; food sprite already exists in project |
| Destroy on expiry | `Destroy(this)` not `Destroy(gameObject)` | Component only — must not destroy the worker |
| Sorting order | Child Canvas with `overrideSorting = true` | Works in both Overlay and Camera canvas modes; matches ResourceLootFX |

---

## Human setup checklist

After implementation, a developer must do the following in the Unity Editor:

- [ ] Add `IconFlyFX` component to the scene's manager object (alongside `ResourceLootFX`).
- [ ] Assign the food/meat sprite to the `icon` field on the `MealBuffSource` component in the Feast prefab.
- [ ] Verify `IconFlyFX` finds the scene Canvas correctly at runtime (check for null warnings in Play mode).
- [ ] Play-test: worker eats Feast → icon arc appears → golden aura activates → flicker in last 3 ticks → aura disappears cleanly (worker remains alive).
- [ ] Tune `arcHeight`, spawn rate, and particle size if the visual feels too subtle or too noisy.
