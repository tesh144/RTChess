# Hold-to-Fill Production System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a new `HoldToFill` production input type where the player holds left-click on a building to drain resources into it at an accelerating rate, then the building starts its normal production timer.

**Architecture:** New `ProductionInputType.HoldToFill` enum value gates production behind a player-driven hold interaction. `HoldToFillHandler` (new singleton MonoBehaviour) manages input, drain scheduling, UI fill bar, VFX, and audio. It communicates with `BuildingProductionManager` through a small public API. The Kitchen is the first building to use this type.

**Tech Stack:** Unity C#, world-space Canvas UI, ResourceManager integration, GameSFXManager for audio.

**Spec:** `Docs/superpowers/specs/2026-03-24-hold-to-fill-production-design.md`

**Note on field naming:** The spec discusses renaming `productionCostAmount` → `resourcesRequired`. To avoid Unity serialization breakage across .asset files, this plan keeps the existing `productionCostAmount` field name. It serves as `resourcesRequired`. Only the new `resourcesRequiredIncrement` field is added. A cosmetic rename can be done later with `[FormerlySerializedAs]` if desired.

---

## File Structure

| File | Action | Responsibility |
|------|--------|----------------|
| `Assets/Scripts/Data/BuildingData.cs` | Modify | Add `HoldToFill` enum value, add `resourcesRequiredIncrement` field |
| `Assets/Scripts/Data/UnitStats.cs` | Modify | Add `resourcesRequiredIncrement` field |
| `Assets/Scripts/LittleCafe/BuildingProductionManager.cs` | Modify | New gate state, public API for handler, registration/tick/collect changes |
| `Assets/Scripts/LittleCafe/HoldToFillHandler.cs` | Create | Input detection, accelerating drain, fill bar UI, resource stream VFX, audio |
| `Assets/ClockworkCraft/Scripts/Core/MapGeneratorV2.cs` | Modify | Copy new field in SetupDeck() |
| `Assets/Scripts/Editor/SheetSyncEditor.cs` | Modify | Sync new field from Google Sheets |
| `Assets/Scripts/Data/BuildingDatabase.asset` | Modify | Kitchen: productionInputType=HoldToFill, productionCostAmount=3 |

---

### Task 1: Data Layer — Enum + New Field

**Files:**
- Modify: `Assets/Scripts/Data/BuildingData.cs:26-31` (ProductionInputType enum)
- Modify: `Assets/Scripts/Data/BuildingData.cs:141-144` (cost fields)
- Modify: `Assets/Scripts/Data/UnitStats.cs:153-156` (cost fields)

- [ ] **Step 1: Add HoldToFill to ProductionInputType enum**

In `BuildingData.cs` at the `ProductionInputType` enum (line 26-31), add `HoldToFill` after `Fighter`:

```csharp
public enum ProductionInputType
{
    None,
    Worker,
    Fighter,
    HoldToFill
}
```

- [ ] **Step 2: Add resourcesRequiredIncrement field to BuildingData**

After the `productionCostAmount` field (line 144), add:

```csharp
public int resourcesRequiredIncrement = 0;
```

- [ ] **Step 3: Add resourcesRequiredIncrement field to UnitStats**

After the `productionCostAmount` field (line 156), add:

```csharp
public int resourcesRequiredIncrement = 0;
```

- [ ] **Step 4: Copy new field in MapGeneratorV2.SetupDeck()**

In `MapGeneratorV2.cs` after the `productionCostAmount` copy (line 448), add:

```csharp
stats.resourcesRequiredIncrement = data.resourcesRequiredIncrement;
```

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Data/BuildingData.cs Assets/Scripts/Data/UnitStats.cs Assets/ClockworkCraft/Scripts/Core/MapGeneratorV2.cs
git commit -m "feat: add HoldToFill enum value and resourcesRequiredIncrement field"
```

---

### Task 2: BuildingProductionManager — Gate State + Public API

**Files:**
- Modify: `Assets/Scripts/LittleCafe/BuildingProductionManager.cs:76-119` (ProductionEntry)
- Modify: `Assets/Scripts/LittleCafe/BuildingProductionManager.cs:166-208` (RegisterBuilding)
- Modify: `Assets/Scripts/LittleCafe/BuildingProductionManager.cs:529-625` (OnIntervalTick)
- Modify: `Assets/Scripts/LittleCafe/BuildingProductionManager.cs:938-1018` (CollectReward)

- [ ] **Step 1: Add new fields to ProductionEntry**

In the `ProductionEntry` class (after line 93 `waitingForResources`), add:

```csharp
public bool waitingForHoldFill;
public int holdFillProgress;
public int resourcesRequiredIncrement;
```

Add a computed property after `EffectiveInterval` (line 116):

```csharp
public int EffectiveFillCost => productionCostAmount + (resourcesRequiredIncrement * collectCount);
```

- [ ] **Step 2: Update RegisterBuilding() for HoldToFill**

In `RegisterBuilding()` (around line 183-186), modify the gate initialization. After the existing `waitingForInput` and `waitingForResources` lines, add HoldToFill handling:

```csharp
// Existing lines:
waitingForInput = (stats.productionInputType != ProductionInputType.None && stats.productionInputType != ProductionInputType.HoldToFill),

// Add new fields:
waitingForHoldFill = (stats.productionInputType == ProductionInputType.HoldToFill),
holdFillProgress = 0,
resourcesRequiredIncrement = stats.resourcesRequiredIncrement,

// Modify waitingForResources to exclude HoldToFill:
waitingForResources = (stats.productionCostAmount > 0 && stats.productionInputType != ProductionInputType.HoldToFill),
```

- [ ] **Step 3: Update OnIntervalTick() to skip HoldToFill buildings**

In `OnIntervalTick()` (around line 535), add a skip for `waitingForHoldFill` alongside the existing `waitingForInput` check:

```csharp
if (entry.waitingForHoldFill)
    continue;
```

Also guard the `waitingForResources` block (line 555) to skip HoldToFill entries as a safety measure:

```csharp
if (entry.waitingForResources && entry.inputType != ProductionInputType.HoldToFill)
```

- [ ] **Step 4: Update CollectReward() to reset HoldToFill state**

In `CollectReward()` (around line 1010-1015), add HoldToFill reset alongside existing resets:

```csharp
if (entry.inputType == ProductionInputType.HoldToFill)
{
    entry.waitingForHoldFill = true;
    entry.holdFillProgress = 0;
}
```

Ensure `waitingForResources` is NOT set for HoldToFill entries. Modify the existing reset (line 1014):

```csharp
if (entry.productionCostAmount > 0 && entry.inputType != ProductionInputType.HoldToFill)
    entry.waitingForResources = true;
```

Also fix the `waitingForInput` reset to exclude HoldToFill (line 1010-1011). Without this, HoldToFill buildings get double-gated after first collection and permanently stuck:

```csharp
if (entry.inputType != ProductionInputType.None && entry.inputType != ProductionInputType.HoldToFill)
    entry.waitingForInput = true;
```

- [ ] **Step 5: Add public API methods for HoldToFillHandler**

Add these public methods to `BuildingProductionManager`:

```csharp
public struct HoldFillInfo
{
    public int progress;
    public int effectiveCost;
    public ResourceType resourceType;
    public GameObject buildingObj;
}

public bool IsWaitingForHoldFill(GameObject building)
{
    var entry = entries.Find(e => e.buildingObj == building);
    return entry != null && entry.waitingForHoldFill;
}

public HoldFillInfo GetHoldFillInfo(GameObject building)
{
    var entry = entries.Find(e => e.buildingObj == building);
    if (entry == null) return default;
    return new HoldFillInfo
    {
        progress = entry.holdFillProgress,
        effectiveCost = entry.EffectiveFillCost,
        resourceType = entry.productionCostResourceType,
        buildingObj = entry.buildingObj
    };
}

/// <summary>
/// Increments hold fill progress by 1. Returns true if fill just completed.
/// </summary>
public bool IncrementHoldFill(GameObject building)
{
    var entry = entries.Find(e => e.buildingObj == building);
    if (entry == null || !entry.waitingForHoldFill) return false;

    entry.holdFillProgress++;
    if (entry.holdFillProgress >= entry.EffectiveFillCost)
    {
        entry.waitingForHoldFill = false;
        // Keep holdFillProgress at max (shows 100% bar briefly). Reset to 0 happens in CollectReward().
        entry.elapsedTime = 0f;
        entry.timerRevealed = false;
        return true; // Fill complete, timer starts
    }
    return false;
}

public bool HasReadyPopupAt(GameObject building)
{
    var entry = entries.Find(e => e.buildingObj == building);
    return entry != null && entry.isReady;
}

/// <summary>
/// Set to true in HandlePopupTap when a click is consumed by popup collection.
/// Reset at start of Update(). HoldToFillHandler checks this in LateUpdate().
/// </summary>
public bool ClickConsumedThisFrame { get; set; }

/// <summary>
/// Check if a building is paused (corrupted).
/// </summary>
public bool IsBuildingPaused(GameObject building)
{
    var entry = entries.Find(e => e.buildingObj == building);
    return entry != null && entry.isPaused;
}

/// <summary>
/// Event fired when a building enters or exits waitingForHoldFill state.
/// Listeners: HoldToFillHandler (fill bar management).
/// </summary>
public event System.Action<GameObject, bool> OnHoldFillStateChanged;
```

- [ ] **Step 6: Add click-consumed flag for input priority**

In `BuildingProductionManager.Update()`, add at the top: `ClickConsumedThisFrame = false;`

In `HandlePopupTap()`, when a popup is successfully collected, set `ClickConsumedThisFrame = true;`. This prevents `HoldToFillHandler.LateUpdate()` from also processing the same click.

Also fire `OnHoldFillStateChanged` events from `RegisterBuilding()` (when `waitingForHoldFill` is set true), `IncrementHoldFill()` (when fill completes, set false), and `CollectReward()` (when reset to true).

- [ ] **Step 7: Verify compilation in Unity**

Open Unity, check Console for compile errors. All existing behavior should be unchanged since no buildings use HoldToFill yet.

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/LittleCafe/BuildingProductionManager.cs
git commit -m "feat: add HoldToFill gate state and public API to BuildingProductionManager"
```

---

### Task 3: HoldToFillHandler — Input + Accelerating Drain Logic

**Files:**
- Create: `Assets/Scripts/LittleCafe/HoldToFillHandler.cs`

**References:**
- `BuildingProductionManager.cs` — public API from Task 2
- `ResourceManager.cs:207-210` — `GetResource(ResourceType)`
- `ResourceManager.cs:236-246` — `SpendResources(Dictionary<ResourceType, int>)`
- `DragDropHandler.cs:19-20` — `isDragging` / `IsDragging`
- `HandlePopupTap()` at `BuildingProductionManager.cs:872` — input priority

- [ ] **Step 1: Create HoldToFillHandler.cs with core input + drain**

```csharp
using UnityEngine;
using System.Collections.Generic;

public class HoldToFillHandler : MonoBehaviour
{
    public static HoldToFillHandler Instance { get; private set; }

    [Header("Drain Timing")]
    [SerializeField] private float baseChunkInterval = 0.5f;
    [SerializeField] private float chunkDecayFactor = 0.85f;
    [SerializeField] private float minChunkInterval = 0.08f;

    // State
    private GameObject activeBuilding;
    private float chunkTimer;
    private float currentChunkInterval;
    private int chunksThisSession; // resets on each new hold

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void LateUpdate()
    {
        // LateUpdate ensures HandlePopupTap (in Update) runs first and consumes clicks.
        // Check clickConsumedThisFrame to avoid double-handling.
        if (Input.GetMouseButtonDown(0) && !BuildingProductionManager.Instance.ClickConsumedThisFrame)
        {
            TryStartHold();
        }

        if (Input.GetMouseButton(0) && activeBuilding != null)
        {
            UpdateHold();
        }

        if (Input.GetMouseButtonUp(0))
        {
            StopHold();
        }
    }

    private void TryStartHold()
    {
        // Input priority: don't activate if dragging or popup is ready
        if (DragDropHandler.Instance != null && DragDropHandler.Instance.IsDragging)
            return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 100f))
            return;

        GameObject hitObj = hit.collider.gameObject;
        // Walk up to find the building root if needed
        var bpm = BuildingProductionManager.Instance;
        if (bpm == null) return;

        if (bpm.HasReadyPopupAt(hitObj))
            return; // Let HandlePopupTap handle this click

        if (!bpm.IsWaitingForHoldFill(hitObj))
            return;

        if (bpm.IsBuildingPaused(hitObj))
            return; // Corrupted — can't fill

        activeBuilding = hitObj;
        chunksThisSession = 0;
        currentChunkInterval = baseChunkInterval;
        chunkTimer = 0f; // First chunk fires after first interval
    }

    private void UpdateHold()
    {
        if (activeBuilding == null) return;

        var bpm = BuildingProductionManager.Instance;
        if (bpm == null || !bpm.IsWaitingForHoldFill(activeBuilding))
        {
            StopHold();
            return;
        }

        chunkTimer += Time.deltaTime;
        if (chunkTimer >= currentChunkInterval)
        {
            chunkTimer -= currentChunkInterval;
            TryDrainChunk();
        }
    }

    private void TryDrainChunk()
    {
        var bpm = BuildingProductionManager.Instance;
        var info = bpm.GetHoldFillInfo(activeBuilding);

        // Check if player can afford 1 unit
        var rm = ResourceManager.Instance;
        if (rm == null || rm.GetResource(info.resourceType) < 1)
            return; // Pause — no resources, but don't stop hold

        // Spend 1 resource
        rm.SpendResources(new Dictionary<ResourceType, int>
        {
            { info.resourceType, 1 }
        });

        // Increment fill
        bool fillComplete = bpm.IncrementHoldFill(activeBuilding);

        chunksThisSession++;

        // TODO Task 5: trigger VFX per chunk
        // TODO Task 4: update fill bar UI
        // TODO Task 6: play chunk SFX

        // Accelerate
        currentChunkInterval = Mathf.Max(
            minChunkInterval,
            currentChunkInterval * chunkDecayFactor
        );

        if (fillComplete)
        {
            // TODO Task 6: play completion SFX
            StopHold();
        }
    }

    private void StopHold()
    {
        activeBuilding = null;
        chunksThisSession = 0;
    }

    /// <summary>
    /// Called externally when a building is destroyed or corrupted mid-fill.
    /// </summary>
    public void InterruptIfActive(GameObject building)
    {
        if (activeBuilding == building)
            StopHold();
    }
}
```

- [ ] **Step 2: Verify DragDropHandler.IsDragging is accessible**

Check `DragDropHandler.cs:19-20` — `IsDragging` should be a public property or field. If it's private, make it public. The handler checks this for input priority.

- [ ] **Step 3: Add HoldToFillHandler to the scene**

The handler needs to exist as a singleton in the scene. Either attach it to an existing manager GameObject or create a new one. Check how other singletons (like `BuildingProductionManager`, `ResourceManager`) are set up in the scene — follow the same pattern.

- [ ] **Step 4: Test basic hold-to-drain in Unity**

Temporarily set Kitchen to `HoldToFill` in BuildingDatabase.asset (Task 7 does this properly). Place a Kitchen in-game, hold click on it, and verify via Debug.Log that chunks drain and the building eventually starts its timer. Check:
- Resources decrement by 1 per chunk
- Chunks accelerate
- Releasing pauses progress
- Re-clicking resumes
- Fill completes and production timer starts

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/LittleCafe/HoldToFillHandler.cs
git commit -m "feat: add HoldToFillHandler with input detection and accelerating drain"
```

---

### Task 4: Fill Bar UI

**Files:**
- Modify: `Assets/Scripts/LittleCafe/HoldToFillHandler.cs`

**References:**
- `GridEntityHPBar.cs:489-510` — `GetTopOfObject()` for positioning world-space UI
- `BuildingProductionManager.cs` timer canvas creation (around line 104, 380+) — for reference on world-space canvas setup

- [ ] **Step 1: Add fill bar creation and update methods to HoldToFillHandler**

Add fields and methods for managing per-building fill bar canvases:

```csharp
[Header("Fill Bar")]
[SerializeField] private Color fillBarColor = new Color(0.3f, 0.85f, 0.4f, 1f);
[SerializeField] private Color fillBarBgColor = new Color(0.15f, 0.15f, 0.2f, 0.6f);
[SerializeField] private float fillBarWidth = 1.2f;
[SerializeField] private float fillBarHeight = 0.15f;
[SerializeField] private float fillBarYOffset = -0.3f; // Below the building base

private Dictionary<GameObject, GameObject> fillBarCanvases = new Dictionary<GameObject, GameObject>();
```

Create world-space canvas with a background Image and a foreground fill Image. Position at the base of the building (below it, using `GetTopOfObject` inverted or a fixed offset from the building's transform). The fill image uses `Image.fillAmount` set to `progress / effectiveCost`.

- [ ] **Step 2: Subscribe to OnHoldFillStateChanged event**

Subscribe to `BuildingProductionManager.OnHoldFillStateChanged` to create/destroy fill bars on state transitions (avoids per-frame List allocation). Create fill bar when state becomes `true`, destroy when `false`. Update `fillAmount` in `LateUpdate()` only for buildings in the `fillBarCanvases` dictionary (cheap — no allocation).

- [ ] **Step 3: Hide fill bar when fill completes**

When `IncrementHoldFill()` returns true (fill complete), fire `OnHoldFillStateChanged(building, false)` which destroys the fill bar canvas.

- [ ] **Step 4: Recreate fill bar on production cycle reset**

After collection, `CollectReward()` fires `OnHoldFillStateChanged(building, true)` which recreates the bar at 0%.

- [ ] **Step 5: Test in Unity**

Verify: bar appears at 0% on Kitchen when placed, fills as you hold, disappears when full, reappears after collecting the Meal.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/LittleCafe/HoldToFillHandler.cs
git commit -m "feat: add world-space fill bar UI for hold-to-fill buildings"
```

---

### Task 5: Resource Stream VFX

**Files:**
- Modify: `Assets/Scripts/LittleCafe/HoldToFillHandler.cs`

**References:**
- `ResourceLootFX.cs:79-240` — existing particle fly system (we reverse the direction)
- Resource bar UI position — top-right of screen

- [ ] **Step 1: Add resource stream VFX method**

Add a method that spawns a small icon/particle at the resource bar's screen position, converts to world space, and flies it to the building. This is the reverse of `ResourceLootFX.LootParticleCoroutine()` which flies FROM world TO the UI bar.

```csharp
[Header("Resource Stream VFX")]
[SerializeField] private GameObject resourceIconPrefab; // Small food/resource icon
[SerializeField] private float streamFlyDuration = 0.4f;
[SerializeField] private float streamArcHeight = 1.5f;
```

Use a coroutine: spawn icon at resource bar screen position (converted to world), lerp to building position over `streamFlyDuration` with a sine arc, destroy on arrival.

- [ ] **Step 2: Call VFX per chunk in TryDrainChunk()**

Replace the `// TODO Task 5` comment with a call to spawn the resource stream particle.

- [ ] **Step 3: Test in Unity**

Verify particles fly from the resource UI bar down to the Kitchen, one per chunk, accelerating with the drain.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/LittleCafe/HoldToFillHandler.cs
git commit -m "feat: add resource stream VFX for hold-to-fill drain"
```

---

### Task 6: Audio — Chunk SFX + Completion

**Files:**
- Modify: `Assets/Scripts/LittleCafe/HoldToFillHandler.cs`

**References:**
- `GameSFXManager.cs:236-264` — `PlayCoinCollect()` ascending pitch pattern
- `GameSFXManager.cs:332-340` — `PlaySFX()` core playback

- [ ] **Step 1: Add audio fields and chunk SFX method**

```csharp
[Header("Audio")]
[SerializeField] private AudioClip chunkSFX;
[SerializeField] private AudioClip completionSFX;
[SerializeField] private float basePitch = 0.8f;
[SerializeField] private float maxPitch = 1.4f;
private AudioSource audioSource;
```

Initialize AudioSource in `Awake()`: `audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();`

On each chunk, play `chunkSFX` with pitch interpolated from `basePitch` to `maxPitch` based on `progress / effectiveCost`:

```csharp
private void PlayChunkSound(float fillRatio)
{
    if (chunkSFX == null || audioSource == null) return;
    audioSource.pitch = Mathf.Lerp(basePitch, maxPitch, fillRatio);
    audioSource.PlayOneShot(chunkSFX);
}
```

- [ ] **Step 2: Play completion SFX**

When fill completes, play `completionSFX` at normal pitch (1.0).

- [ ] **Step 3: Wire into TryDrainChunk()**

Replace the `// TODO Task 6` comments with calls to `PlayChunkSound()` and completion SFX.

- [ ] **Step 4: Test in Unity**

Verify: rising pitch as you hold, capped before shrillness, distinct completion sound.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/LittleCafe/HoldToFillHandler.cs
git commit -m "feat: add chunk and completion SFX for hold-to-fill"
```

---

### Task 7: Data — Kitchen Configuration + Sheet Sync

**Files:**
- Modify: `Assets/Scripts/Data/BuildingDatabase.asset` (Kitchen entry)
- Modify: `Assets/Scripts/Editor/SheetSyncEditor.cs:791-803` (sync new field)

- [ ] **Step 1: Update Kitchen in BuildingDatabase.asset**

Set on the Kitchen entry:
- `productionInputType` = `3` (HoldToFill enum index: None=0, Worker=1, Fighter=2, HoldToFill=3)
- `productionCostAmount` = `3`
- `resourcesRequiredIncrement` = `1`
- `productionCostResourceType` = Food (should already be set)

- [ ] **Step 2: Update SheetSyncEditor to sync resourcesRequiredIncrement**

In `SyncBuildings()` (around line 800-803 where "Cost Amount" is parsed), add parsing for the increment column. Check the Google Sheet for the exact column name (likely "Cost Increment" or similar). Add:

```csharp
var incrementStr = GetValue(row, "Cost Increment");
if (!string.IsNullOrEmpty(incrementStr) && int.TryParse(incrementStr, out int increment))
    building.resourcesRequiredIncrement = increment;
```

Also sync the `productionInputType` if the sheet has "Input Type" column — ensure `HoldToFill` is a valid value in the dropdown.

- [ ] **Step 3: Verify in Unity**

Place a Kitchen in-game. It should:
- Show the fill bar at 0%
- Respond to hold-click with accelerating Food drain
- Start production timer after 3 Food consumed
- After collecting the Meal, reset fill bar, now requiring 4 Food
- Third cycle requires 5 Food, etc.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Data/BuildingDatabase.asset Assets/Scripts/Editor/SheetSyncEditor.cs
git commit -m "data: configure Kitchen as HoldToFill building, sync increment field"
```

---

### Task 8: Edge Cases + Polish

**Files:**
- Modify: `Assets/Scripts/LittleCafe/HoldToFillHandler.cs`
- Modify: `Assets/Scripts/LittleCafe/BuildingProductionManager.cs`

- [ ] **Step 1: Handle building destruction mid-fill**

In `BuildingProductionManager` where entries are removed for destroyed buildings (around line 541), call:

```csharp
HoldToFillHandler.Instance?.InterruptIfActive(entry.buildingObj);
```

- [ ] **Step 2: Handle corruption mid-fill**

When `isPaused` is set to true on a ProductionEntry (corruption), interrupt the hold if active:

```csharp
HoldToFillHandler.Instance?.InterruptIfActive(entry.buildingObj);
```

Progress is retained — player can resume after corruption clears.

- [ ] **Step 3: Prevent multiple simultaneous holds**

Already handled in `TryStartHold()` — setting `activeBuilding` to a new building implicitly releases the old one. But add an explicit `StopHold()` call at the start of `TryStartHold()` before assigning the new building, so any cleanup (UI, audio) happens properly.

- [ ] **Step 4: Test edge cases**

- Place Kitchen, start filling, destroy it (via enemy) → hold stops, no errors
- Place Kitchen, start filling, release, fill more → progress retained
- Place two Kitchens, start filling one, click the other → switches cleanly
- Fill Kitchen with exactly 0 Food available → nothing happens, no errors
- Fill Kitchen, run out of Food mid-fill → pauses, get more Food, resume

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/LittleCafe/HoldToFillHandler.cs Assets/Scripts/LittleCafe/BuildingProductionManager.cs
git commit -m "fix: handle edge cases for hold-to-fill (destruction, corruption, multi-building)"
```
