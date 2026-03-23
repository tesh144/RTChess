# Kitchen Resource Cost Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Gate the Kitchen's production timer behind a 10 Food pre-pay — any building can optionally require a resource spend before its timer starts, via two new data-driven fields.

**Architecture:** Two new fields (`productionCostResourceType`, `productionCostAmount`) are added to `BuildingData` and `UnitStats`, copied in `MapGeneratorV2.SetupDeck()`, stored on `ProductionEntry`, and checked each tick via a `waitingForResources` flag that mirrors the existing `waitingForInput` pattern. `SheetSyncEditor` and `SheetCache.json` are updated so the values round-trip through the sheet sync tool.

**Tech Stack:** Unity 2022.3 · C# · YAML .asset files · JSON (SheetCache)

---

## File Map

| File | Change |
|---|---|
| `Assets/Scripts/Data/BuildingData.cs` | Add 2 fields under `[Header("Production")]` |
| `Assets/Scripts/Data/UnitStats.cs` | Add 2 matching fields under `[Header("Building Production")]` |
| `Assets/ClockworkCraft/Scripts/Core/MapGeneratorV2.cs` | Copy 2 fields in the BuildingData→UnitStats block (~line 410) |
| `Assets/Scripts/LittleCafe/BuildingProductionManager.cs` | `ProductionEntry` (+3 fields), `RegisterBuilding` (+3 init lines), `OnIntervalTick` (+resource guard block), `CollectReward` (+reset line) |
| `Assets/Scripts/Editor/SheetSyncEditor.cs` | `SyncBuildings()` — add parsing for "Cost Resource" and "Cost Amount" columns |
| `Assets/Scripts/Editor/SheetCache.json` | Add column headers + Kitchen row values |
| `Assets/Scripts/Data/BuildingDatabase.asset` | Kitchen entry: set `productionCostResourceType` and `productionCostAmount` |

---

### Task 1: Add fields to `BuildingData.cs`

**Files:**
- Modify: `Assets/Scripts/Data/BuildingData.cs:132`

- [ ] Open `BuildingData.cs`. Find the line `public int productionAmount = 1;` (line 132). Insert the two new fields immediately after it:

```csharp
        [Header("Production Resource Cost")]
        [Tooltip("Resource type required to start each production cycle. None = no cost.")]
        public ResourceType productionCostResourceType = ResourceType.None;

        [Tooltip("Amount of productionCostResourceType consumed when the cycle starts. 0 = no cost.")]
        public int productionCostAmount = 0;
```

  Note: the new `[Header("Production Resource Cost")]` will appear as a separate header group in the Unity Inspector below `[Header("Production")]`. This is intentional — it is a distinct sub-section, not a mistake.

- [ ] Verify the file compiles (no errors in Unity Console after saving).

- [ ] Commit:
```bash
git add Assets/Scripts/Data/BuildingData.cs
git commit -m "feat: add productionCostResourceType/Amount fields to BuildingData"
```

---

### Task 2: Add fields to `UnitStats.cs`

**Files:**
- Modify: `Assets/Scripts/Data/UnitStats.cs:135`

- [ ] Open `UnitStats.cs`. Find the line `public int productionAmount = 1;` (line 135). Insert the two new fields immediately after it, inside the `[Header("Building Production")]` section:

```csharp
        [Tooltip("Resource type required to start each production cycle. None = no cost.")]
        public ResourceType productionCostResourceType = ResourceType.None;

        [Tooltip("Amount of productionCostResourceType consumed when the cycle starts. 0 = no cost.")]
        public int productionCostAmount = 0;
```

- [ ] Verify no compile errors.

- [ ] Commit:
```bash
git add Assets/Scripts/Data/UnitStats.cs
git commit -m "feat: add productionCostResourceType/Amount fields to UnitStats"
```

---

### Task 3: Copy fields in `MapGeneratorV2.SetupDeck()`

**Files:**
- Modify: `Assets/ClockworkCraft/Scripts/Core/MapGeneratorV2.cs:410`

- [ ] Open `MapGeneratorV2.cs`. Find the existing copy block (around line 410):
```csharp
                    stats.killerAdvances          = data.killerAdvances;
```
Add two lines immediately after it:
```csharp
                    stats.productionCostResourceType = data.productionCostResourceType;
                    stats.productionCostAmount       = data.productionCostAmount;
```

- [ ] Verify no compile errors.

- [ ] Commit:
```bash
git add Assets/ClockworkCraft/Scripts/Core/MapGeneratorV2.cs
git commit -m "feat: copy productionCost fields from BuildingData to UnitStats in SetupDeck"
```

---

### Task 4: Add `waitingForResources` to `ProductionEntry` and wire up `RegisterBuilding`

**Files:**
- Modify: `Assets/Scripts/LittleCafe/BuildingProductionManager.cs:90` (ProductionEntry fields)
- Modify: `Assets/Scripts/LittleCafe/BuildingProductionManager.cs:168` (RegisterBuilding initializer)

- [ ] Open `BuildingProductionManager.cs`. Find the `ProductionEntry` inner class. After the line:
```csharp
            public bool waitingForInput; // Input-triggered buildings idle until fed
```
Add three new fields:
```csharp
            public ResourceType productionCostResourceType;
            public int          productionCostAmount;
            public bool         waitingForResources; // true when building needs to spend resources before starting timer
```

- [ ] In `RegisterBuilding()`, find the `new ProductionEntry { ... }` initializer (around line 168). After the line:
```csharp
                waitingForInput = stats.productionInputType != ProductionInputType.None,
```
Add:
```csharp
                productionCostResourceType = stats.productionCostResourceType,
                productionCostAmount       = stats.productionCostAmount,
                waitingForResources        = stats.productionCostAmount > 0,
```

- [ ] Verify no compile errors.

- [ ] Commit:
```bash
git add Assets/Scripts/LittleCafe/BuildingProductionManager.cs
git commit -m "feat: add waitingForResources fields to ProductionEntry and RegisterBuilding"
```

---

### Task 5: Add resource guard in `OnIntervalTick`

**Files:**
- Modify: `Assets/Scripts/LittleCafe/BuildingProductionManager.cs:542`

- [ ] In `OnIntervalTick()`, find the existing `waitingForInput` guard (line 542):
```csharp
                if (entry.waitingForInput) continue;
```
Add the resource guard **immediately after** it:
```csharp
                // Resource-cost buildings wait until they can afford the activation cost
                if (entry.waitingForResources)
                {
                    var rm = ResourceManager.Instance;
                    bool spent = rm != null && rm.SpendResources(
                        new Dictionary<ResourceType, int> { { entry.productionCostResourceType, entry.productionCostAmount } });
                    if (spent)
                        entry.waitingForResources = false;
                    else
                        continue; // not enough resources — skip tick
                }
```

- [ ] Verify no compile errors. Check that `using ClockworkCraft;` (for `ResourceType`) is already present at the top of the file — it is.

- [ ] Commit:
```bash
git add Assets/Scripts/LittleCafe/BuildingProductionManager.cs
git commit -m "feat: gate production timer behind resource cost check in OnIntervalTick"
```

---

### Task 6: Reset `waitingForResources` after collection

**Files:**
- Modify: `Assets/Scripts/LittleCafe/BuildingProductionManager.cs:985`

- [ ] In `CollectReward()`, find the existing `waitingForInput` reset (line 984–985):
```csharp
            // Input-triggered buildings return to waiting state after collection
            if (entry.inputType != ProductionInputType.None)
                entry.waitingForInput = true;
```
Add the resource-cost reset immediately after:
```csharp
            // Resource-cost buildings return to waiting state after collection
            if (entry.productionCostAmount > 0)
                entry.waitingForResources = true;
```

- [ ] Verify no compile errors.

- [ ] Commit:
```bash
git add Assets/Scripts/LittleCafe/BuildingProductionManager.cs
git commit -m "feat: reset waitingForResources after collection in CollectReward"
```

---

### Task 7: Update `SheetCache.json` with new columns

**Files:**
- Modify: `Assets/Scripts/Editor/SheetCache.json`

- [ ] Open `SheetCache.json`. In the `"Buildings & Production"` section:

  1. Add `"Cost Resource"` and `"Cost Amount"` to the `"headers"` array (append to end of the array).

  2. Add those keys to each row:
     - Home: `"Cost Resource": "None", "Cost Amount": "0"`
     - Torch: `"Cost Resource": "None", "Cost Amount": "0"`
     - Statue: `"Cost Resource": "None", "Cost Amount": "0"`
     - Barracks: `"Cost Resource": "None", "Cost Amount": "0"`
     - **Kitchen**: `"Cost Resource": "🍄 Food", "Cost Amount": "10"`
     - Feast: `"Cost Resource": "None", "Cost Amount": "0"`

- [ ] Verify the JSON is valid (no parse errors — check with any JSON validator or Unity console on import).

- [ ] Commit:
```bash
git add Assets/Scripts/Editor/SheetCache.json
git commit -m "data: add Cost Resource/Amount columns to SheetCache Buildings sheet"
```

---

### Task 8: Update `SheetSyncEditor.SyncBuildings()` to parse new columns

**Files:**
- Modify: `Assets/Scripts/Editor/SheetSyncEditor.cs:431` (inside `SyncBuildings`, after the wild-animal interactible block)

- [ ] Open `SheetSyncEditor.cs`. In `SyncBuildings()`, find the block that ends with:
```csharp
                string wildStr = GetValue(row, "Wild Animal Interactible");
                if (!string.IsNullOrEmpty(wildStr))
                {
                    bool newWild = wildStr.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || wildStr == "1";
                    if (existing.wildAnimalInteractible != newWild) { existing.wildAnimalInteractible = newWild; changed = true; }
                }
```
Add the following immediately after that block, before the `if (changed)` check:
```csharp
                // Production resource cost
                string costResStr = StripEmoji(GetValue(row, "Cost Resource")).Replace(" ", "");
                if (!string.IsNullOrEmpty(costResStr) && !costResStr.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    if (Enum.TryParse<ClockworkCraft.ResourceType>(costResStr, true, out var costRes))
                        if (existing.productionCostResourceType != costRes) { existing.productionCostResourceType = costRes; changed = true; }
                }
                changed |= TrySetInt(ref existing.productionCostAmount, GetValue(row, "Cost Amount"));
```

- [ ] Verify no compile errors in Unity.

- [ ] Commit:
```bash
git add Assets/Scripts/Editor/SheetSyncEditor.cs
git commit -m "feat: parse Cost Resource and Cost Amount in SheetSyncEditor.SyncBuildings"
```

---

### Task 9: Set Kitchen values in `BuildingDatabase.asset`

**Files:**
- Modify: `Assets/Scripts/Data/BuildingDatabase.asset` (Kitchen entry)

- [ ] Open Unity. In the Project window, navigate to `Assets/Scripts/Data/BuildingDatabase.asset` and select it.
- [ ] In the Inspector, expand the `buildingList` and find the **Kitchen** entry.
- [ ] Set `productionCostResourceType` to **Food** using the enum dropdown.
- [ ] Set `productionCostAmount` to **10**.
- [ ] Save (Ctrl+S). Unity will serialize the enum value correctly — do not manually edit the YAML integer.
- [ ] Verify the Inspector shows `productionCostResourceType = Food` and `productionCostAmount = 10`.

- [ ] Commit:
```bash
git add Assets/Scripts/Data/BuildingDatabase.asset
git commit -m "data: set Kitchen productionCostResourceType=Food, productionCostAmount=10"
```

---

### Task 10: Manual verification in Unity

- [ ] Open Unity and enter Play mode.
- [ ] Verify Kitchen is placed on the map. With 0 Food, its timer should stay hidden indefinitely.
- [ ] To test activation: in the Inspector while in Play mode, find `ResourceManager` and manually add Food via `SpendResources` in reverse (or temporarily set `productionCostAmount` to `1` in the BuildingDatabase Inspector while in Play mode and acquire 1 Food from a Food-dropping node if one exists on the map). The simplest approach is to **temporarily set `productionCostAmount` to `0`** on Kitchen in the Inspector during Play mode — the timer should immediately start, confirming the guard is the only thing blocking it.
- [ ] Set `productionCostAmount` back to `10`, then temporarily add Food by setting `startingAmount` to `10` in `CurrencyDatabase.asset` for Food and restarting Play mode. Verify the timer starts and 10 Food is deducted from the resource bar.
- [ ] Verify after collecting the Meal card, the Kitchen returns to idle (timer hidden) and waits for another 10 Food.
- [ ] Verify all other buildings (Home, Torch, Statue, Barracks, Feast) are completely unaffected — their timers run as before.
- [ ] Check Unity Console for no new errors or warnings related to `BuildingProductionManager` or `ResourceManager`.

---

### Task 11: Update `JAI_AI_SYNC.md`

**Files:**
- Modify: `JAI_AI_SYNC.md`

- [ ] Add an entry to the Completed Work table:
```
| 2026-03-23 | Claude Code | Kitchen resource cost: productionCostResourceType/Amount fields on BuildingData+UnitStats, waitingForResources gate in BuildingProductionManager, SheetSyncEditor + SheetCache updated. Kitchen = Food/10. |
```

- [ ] Commit:
```bash
git add JAI_AI_SYNC.md
git commit -m "docs: log kitchen resource cost implementation in JAI_AI_SYNC"
```
