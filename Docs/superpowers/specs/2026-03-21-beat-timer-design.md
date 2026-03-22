# Beat Timer System — Design Spec
**Date:** 2026-03-21
**Status:** Approved

---

## Overview

Extend the existing `IntervalTimer` singleton to subdivide each bar into beats, exposing separate events for each rhythmic subdivision. This allows game systems to hook into finer-grained time increments (every beat, every half-bar, or every bar) without polling or building their own timers.

---

## Terminology

| Term | Duration | Description |
|---|---|---|
| Beat | 0.5s | Smallest subdivision. 4 per bar. |
| Half-bar | 1.0s | Every 2 beats (beats 1 and 3). |
| Bar | 2.0s | Full cycle. Current behavior. |

---

## IntervalTimer Changes

### Internal State

Replace the single `timer` accumulator (accumulating against `baseIntervalDuration`) with a beat-level accumulator against `beatDuration = baseIntervalDuration / 4`.

**New fields:**
- `float beatDuration` — computed as `baseIntervalDuration / 4`, read-only
- `int currentBeat` — **initialized to `0`**; incremented before each tick fires, so the first fired beat is 1. After beat 4, resets to 1.
- `int currentBar` — replaces the private `currentInterval` field entirely. The old private field `currentInterval` is removed; `CurrentInterval` becomes a property returning `currentBar`.

**`CurrentInterval` is kept as a public property returning `currentBar`** to avoid breaking any external references outside the three known subscribers.

### Update Loop

```
// Initial values: timer = 0, currentBeat = 0, currentBar = 0

timer += Time.deltaTime
if timer >= beatDuration:
    timer -= beatDuration
    currentBeat++
    if currentBeat > 4:
        currentBeat = 1
        currentBar++

    fire OnBeat(currentBeat, currentBar)
    if currentBeat == 1 or currentBeat == 3:
        fire OnHalfBar(currentBar)   // passes enclosing bar number, NOT a monotonic half-bar index
    if currentBeat == 1:
        fire OnBar(currentBar)
        fire OnIntervalTick(currentBar)   // backward-compat alias, fires same moment as OnBar
```

### Public Properties

| Property | Value | Notes |
|---|---|---|
| `BeatDuration` | `baseIntervalDuration / 4` | New |
| `IntervalDuration` | `baseIntervalDuration` | Unchanged (= bar duration) |
| `CurrentInterval` | property returning `currentBar` | Backward compat — old private field `currentInterval` is removed |
| `CurrentBar` | `currentBar` | New |
| `CurrentBeat` | `currentBeat` (1–4, or 0 before first tick) | New |
| `IntervalProgress` | `((currentBeat - 1) * beatDuration + timer) / baseIntervalDuration` | Updated formula — still returns 0–1 bar-level progress. Use `Math.Max(0, currentBeat - 1)` to handle pre-first-tick state where `currentBeat == 0`. |

---

## New Events

```csharp
public event Action<int, int> OnBeat;         // (beat 1–4, barNumber)
public event Action<int>      OnHalfBar;      // (barNumber) — bar number, not half-bar index
public event Action<int>      OnBar;          // (barNumber)
public event Action<int>      OnIntervalTick; // backward-compat alias → fires same time as OnBar
```

### Firing Pattern Per Bar

| Beat | OnBeat | OnHalfBar | OnBar | OnIntervalTick |
|---|---|---|---|---|
| 1 | ✓ | ✓ | ✓ | ✓ |
| 2 | ✓ | | | |
| 3 | ✓ | ✓ | | |
| 4 | ✓ | | | |

---

## Subscriber Migration

Three existing subscribers hook into `OnIntervalTick`. Each must have its subscription **removed from `OnIntervalTick` and added to `OnBar`** — both the subscribe and unsubscribe call sites. Do not add to both events; `OnIntervalTick` will still fire at bar time and would cause double-execution.

The local handler method names (e.g. `private void OnIntervalTick(int intervalCount)`) are unchanged — they are compatible with `OnBar`'s `Action<int>` signature.

| File | Subscribe site | Unsubscribe site |
|---|---|---|
| `Assets/Scripts/LittleCafe/GridEntityActor.cs` | ~L225 | ~L233 |
| `Assets/Scripts/LittleCafe/BuildingProductionManager.cs` | ~L134 | ~L140 |
| `Assets/Scripts/Analytics/GameplayRecorder.cs` | ~L140 | ~L158 |

`OnIntervalTick` remains declared on the class so any other external subscribers don't break at compile time.

---

## Pause/Resume

No changes to `Pause()` / `Resume()` behavior. When paused, the beat accumulator simply stops — no partial beat is fired on resume.

---

## Scene Reload Behavior

`IntervalTimer` has no `DontDestroyOnLoad` (unchanged). On scene reload the singleton is destroyed and a fresh instance is created with `currentBeat = 0`, `currentBar = 0`, `timer = 0`. This matches existing behavior where `currentInterval` reset to 0. `GameplayRecorder` handles a null `Instance` at stop-time gracefully (falls back to `-1` for the interval argument).

---

## Out of Scope

- No changes to `attackIntervalMultiplier` logic in `GridEntityActor`
- No changes to `BuildingProductionManager` production logic (it reads `IntervalDuration`, which is unchanged)
- No `DontDestroyOnLoad` behavior added
