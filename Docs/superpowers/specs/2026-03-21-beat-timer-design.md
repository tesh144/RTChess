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
- `int currentBeat` — ranges 1–4, resets to 1 after beat 4
- `int currentBar` — increments each time `currentBeat` resets (replaces `currentInterval`)

**`currentInterval` is kept as a public alias for `currentBar`** to avoid breaking any external references outside the three known subscribers.

### Update Loop

```
timer += Time.deltaTime
if timer >= beatDuration:
    timer -= beatDuration
    currentBeat++
    if currentBeat > 4:
        currentBeat = 1
        currentBar++

    fire OnBeat(currentBeat, currentBar)
    if currentBeat == 1 or currentBeat == 3:
        fire OnHalfBar(currentBar)
    if currentBeat == 1:
        fire OnBar(currentBar)
        fire OnIntervalTick(currentBar)   ← backward-compat alias
```

### Public Properties

| Property | Value | Notes |
|---|---|---|
| `BeatDuration` | `baseIntervalDuration / 4` | New |
| `IntervalDuration` | `baseIntervalDuration` | Unchanged (= bar duration) |
| `CurrentInterval` | alias for `currentBar` | Backward compat |
| `CurrentBar` | bar count | New |
| `CurrentBeat` | 1–4 | New |
| `IntervalProgress` | `timer / baseIntervalDuration` | Stays as bar-level progress |

---

## New Events

```csharp
public event Action<int, int> OnBeat;         // (beat 1–4, barNumber)
public event Action<int>      OnHalfBar;      // (barNumber)
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

Three existing subscribers hook into `OnIntervalTick`. Each is remapped to `OnBar` — subscribe/unsubscribe call sites updated, handler method names unchanged. No logic changes required.

| File | Subscribe site | Unsubscribe site |
|---|---|---|
| `Assets/Scripts/LittleCafe/GridEntityActor.cs` | ~L225 | ~L233 |
| `Assets/Scripts/LittleCafe/BuildingProductionManager.cs` | ~L134 | ~L140 |
| `Assets/Scripts/Analytics/GameplayRecorder.cs` | ~L140 | ~L158 |

`OnIntervalTick` remains on the class so any other external subscribers (editor scripts, future systems) don't break at compile time.

---

## Pause/Resume

No changes to `Pause()` / `Resume()` behavior. When paused, the beat accumulator simply stops — no partial beat is fired on resume.

---

## Out of Scope

- No changes to `attackIntervalMultiplier` logic in `GridEntityActor`
- No changes to `BuildingProductionManager` production logic (it reads `IntervalDuration`, which is unchanged)
- No UI progress bar changes (bar-level `IntervalProgress` remains)
- No `DontDestroyOnLoad` behavior added
