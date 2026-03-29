# ClockworkCraft — Project Documentation

## Standing Rules

### Google Sheets — Formatting & Structure Rules

**Spreadsheet ID:** `1UvfldgEvr3dM_OqHfNyDHi_8qGoiO72CwTDrCRbUNy0`

**Sheet IDs (needed for batch_update):**
| Sheet | sheetId |
|-------|---------|
| Currencies | 0 |
| Environment & Loot | 1027353443 |
| Buildings & Production | 2122729009 |
| Placement Costs | 1854940026 |
| Workers & Entities | 1256997970 |
| Cards & Deck | 1675160473 |
| Map Generation | 1697855788 |
| Timers & Animations | 1150895612 |
| DrawButton | 1441998793 |
| PointsOfInterest | 764607241 |
| Ref | 1089629018 |

**Sheets that must be synced every session (pull from Google Sheets → SheetCache.json → .asset files):**
Buildings & Production, Workers & Entities, Environment & Loot, DrawButton, PointsOfInterest. All five are read by `SheetSyncEditor.cs`. POI data flows: Sheet → SheetCache.json → POIDatabase.asset → POIManager (via "Sync from Database" button).

**Visual Style (MUST follow for all sheets):**
- **Header row:** Dark bg `rgb(0.157, 0.157, 0.2)`, white text, Arial 11, bold, center-aligned. Frozen.
- **Data rows:** White background, black text. Do NOT apply dark backgrounds to data rows — it makes emojis and text illegible.
- **Alternating row stripes:** Use conditional formatting (`=MOD(ROW(),2)=0` → light purple `rgb(0.949, 0.949, 0.969)`, odd rows → white). These MUST cover ALL columns including any newly added ones.
- **Booleans:** ALWAYS use checkboxes (`dataValidation` with `BOOLEAN` type + `boolValue`). Never use TRUE/FALSE text.
- **Enum columns:** Use `ONE_OF_LIST` data validation dropdowns matching code enum values.
- **No borders/outlines** on any sheet. Stripped as of 2026-03-21.

**Critical Gotchas (hard-won from painful 2026-03-21 audit session):**

1. **Conditional formatting overrides cell formatting.** If a sheet has alternating row rules, your `repeatCell` background changes will be invisible. Always check for and update existing conditional format rules rather than fighting them with cell formatting.

2. **When inserting columns, ALL existing data validations shift.** If column C has a Type dropdown and you insert a new column B, that dropdown now lives on column D — but the validation ALSO gets copied to the new column B. You MUST re-apply `setDataValidation` with `rule: null` on the inserted column AND re-verify all other columns' validations are on the correct indices. This caused entity names to show as "invalid" because the Type dropdown leaked onto the Entity column.

3. **When inserting columns, update conditional formatting ranges.** The `endColumnIndex` on alternating stripe rules must be extended to include the new column, or it'll have no stripes.

4. **`TEXT_EQ` condition type DOES NOT WORK for conditional formatting text color.** Use `CUSTOM_FORMULA` instead. Example: to grey out "None" values, use `=A2="None"` as a CUSTOM_FORMULA (the cell reference is relative and shifts for each cell in the range). TEXT_EQ silently does nothing — the API accepts it without error but the format never applies.

5. **Conditional format rule INDEX controls priority.** Index 0 = highest priority. If you add a text-color-only rule at index 0, it can prevent the stripe rules (now at index 1, 2) from applying their backgrounds. Always add text-color rules AFTER stripe rules (use a high index like 99 to append to the end).

6. **Emoji-prefixed dropdown values** (e.g. "💰 Gold", "👷 Worker") are used throughout. The sync tool has a `StripEmoji()` helper that strips everything before the first ASCII letter. Always use this when parsing sheet values to code enums.

7. **NEVER use `add_rows`/`add_columns`** without explicit start positions — they default to inserting at the BEGINNING, pushing all data down/right. Use `batch_update` → `insertDimension` with explicit `startIndex`.

8. **Before ANY sheet write, read the current data first** (`include_grid_data: true`) to verify row/column positions, check for conditional formatting rules, and inspect existing data validations. Reading values-only is not enough — you need the grid data to see validations and conditional formats.

9. **ALWAYS verify after making changes.** Re-fetch the affected range with `include_grid_data: true` and check `effectiveFormat` to confirm your changes actually applied. Never claim a fix is done based on a successful API response alone — the API returns success even when formatting silently fails to render (see TEXT_EQ above). This is the single most important rule.

10. **Watch for stray BOOLEAN validations.** When applying checkbox validation to a column, double-check the column index. A BOOLEAN validation on a numeric column (like Start Amt) creates a broken checkbox overlay that changes text color and cell appearance unpredictably. If you see weird formatting on a cell, check for accidental data validation first.

**Column-to-Code Mappings (Workers & Entities):**
| Sheet Column | Code Field | Notes |
|---|---|---|
| Entity | assetName | Strip parenthetical e.g. "Worker (Generic)" → "Worker" |
| Type | WorkerType / GameUnitType | Dropdown: Worker, Wild Animal |
| HP | hp | int |
| Attack Power | attackPower | int |
| Movement Behavior | behaviorType | Dropdown: RotateAndInteract, RotateAndMove, RotateRotateMove |
| Attack Behavior | → isEnemy | Hostile = isEnemy:true, Peaceful = isEnemy:false. NOT a code enum yet. |
| Killer's Behavior | killerAdvances | Advance = true, Stay = false. Controls whether attacker moves into this entity's cell on kill. |
| Draw Weight | drawWeight | float |
| Slot Takeable | isSlotTakeable | Checkbox boolean |

**Column-to-Code Mappings (Buildings & Production):**
| Sheet Column | Code Field | Notes |
|---|---|---|
| Building | assetName | |
| Prod. Interval (s) | productionInterval | float |
| Interval Bonus (s) | productionIntervalBonus | float |
| Input | productionInputType | Dropdown with emoji: None, 👷 Worker, ⚔️ Fighter |
| Output | productionOutputType | Dropdown with emoji: None, 👷 Worker, 💰 Currency, 🏠 RandomBuilding, ⚔️ Fighter, 🍖 Meal |
| Output Amt | productionAmount | int |
| Killer's Behavior | killerAdvances | Advance = true, Stay = false. Controls whether attacker moves into cell on kill. |
| Reveal Radius | fogRevealRadius | int |
| Ally Interactible | isMealSource | Checkbox boolean |
| HP | hp | int |
| Attack | attackPower | int |

**Column-to-Code Mappings (Environment & Loot):**
| Sheet Column | Code Field | Notes |
|---|---|---|
| Object | assetName | |
| Type | layerType | `EnvironmentLayerType` enum: Object (Tier 1) or Surface (Tier 2). Surfaces coexist with Objects on the same tile. |
| Drops | lootResourceType | Dropdown with emoji (e.g. "💰 Gold"). StripEmoji → Enum.TryParse |
| Loot per Hit | lootYield | int |
| HP | hp | int |
| Total Yield | (formula) | =Loot per Hit × HP. Not a code field. |
| Killer's Behavior | killerAdvances | Advance = true, Stay = false. Controls whether attacker moves into cell on kill. |

**BuildOn (Buildings & Production, column E):**
Dropdown populated from all Environment & Loot entry names + "Empty". Controls which tile surface type is valid for placement when the player drags a building. "Empty" = standard unoccupied tile. Any surface name (e.g. "Water") = only valid on a tile bearing that surface. Synced from sheet via SheetSyncEditor → `buildOn` string field on `BuildingData`.

**Sync Tool:** `Assets/Scripts/Editor/SheetSyncEditor.cs` reads `Assets/Scripts/Editor/SheetCache.json` (written by Claude via MCP) and updates ScriptableObject .asset files. Open via ClockworkCraft → Sheet Sync in Unity.

## Project Overview
A Unity-based grid strategy game that evolved from a real-time chess prototype (RTChess) through a cafe builder (LittleCafe) into a world-building simulation (ClockworkCraft). All three share a common core of grid, camera, and placement systems.

**Unity Version:** 2022.3.62f3
**Last Updated:** 2026-03-29

## Current Status

### Active Development: ClockworkCraft (World Builder)
The primary focus is ClockworkCraft — a procedurally generated grid world where players draw worker cards from a deck, drag-and-drop them onto the map, and workers autonomously interact with environment objects on a clockwork tick. The map is generated with clustered forests, edge-spawned water, scattered rocks and gold mines, all hidden by fog of war until the player reveals them.

**Core systems implemented:** Procedural map generation, card-based drag-and-drop placement, fog of war, clockwork tick interaction loop, grid entity HP/death system, building production timers, loot drops, 31-currency economy, ScriptableObject databases (Furniture/Building/Worker/Unit/Environment), POI system.

### Secondary Mode: LittleCafe (Cafe Builder)
The cafe builder scene is still functional (CafeSceneSetupV2). It uses the FurnitureDatabase to populate the deck instead of WorkerDatabase, and doesn't do procedural map generation — the player builds freely on a flat grid.

## Architecture

### Namespaces
- `ClockworkGrid` — Core shared systems (GridManager, IntervalTimer, DragDropHandler, RaritySystem, etc.)
- `LittleCafe` — Cafe/world builder systems (FurnitureObject, GridCamera, FogManager, connectivity, entity system, etc.)
- `ClockworkCraft` — Procedural world generation (MapGeneratorV2, ResourceNode, NodeManager, FogHideable, etc.)

### Singleton Managers
All managers use the `Instance` singleton pattern:
- `GridManager.Instance` — Grid state, cell occupants, coordinate conversion
- `IntervalTimer.Instance` — Global clock driving gameplay ticks
- `ResourceTokenManager.Instance` — Economy (token tracking)
- `DragDropHandler.Instance` — Drag state, arc line, cell highlights, placement validation
- `DockBarManager.Instance` — Bottom dock bar with draggable cards
- `RaritySystem.Instance` — Draw pool and weighted random selection
- `FurnitureConnectivityManager.Instance` — Groups adjacent furniture
- `FogManager.Instance` — Fog of war, tile reveal tracking
- `GridCamera.Instance` — Orbiting camera with adaptive zoom
- `FurnitureRemovalHandler.Instance` — Tap/hold removal
- `TableSeatingManager.Instance` — Chair-to-table seat assignment
- `GridEntityManager.Instance` — Entity component factory + registry
- `MapGeneratorV2.Instance` — Procedural map generation authority
- `NodeManager.Instance` — Resource node registry (ClockworkCraft)
- `ResourceManager.Instance` — Resource economy (ClockworkCraft)
- `SFXManager.Instance` — Sound effects
- `GameStateManager.Instance` — Title/Playing state machine
- `MusicSystem.Instance` — Procedural music generation
- `BuildingProductionManager.Instance` — Building production timers + rewards
- `ResourceDisplayUI.Instance` — Currency bar UI (uses Lobby UIPanel slots)
- `ResourceLootFX.Instance` — Loot particle fly-to-bar effects
- `WorkerCardFlyFX.Instance` — Card fly animation (worker + building cards)
- `GameSFXManager.Instance` — Game sound effects with procedural fallbacks
- `DrawButtonController.Instance` — Draw button state + animation

### Camera System
`ICameraSystem` interface with `CameraSystemLocator` service locator. `GridCamera` implements it and is the only camera system. Code references `CameraSystemLocator.Current` for scene-agnostic camera access.

**GridCamera features:**
- Orbiting around a pivot point
- Scroll-wheel zoom clamped to [minDistance, currentMaxDistance]
- Adaptive zoom: default and max distance grow with `FogManager.GetRevealedCount()`
- Auto-rotate (slow orbit)
- Camera shake
- Focus on position / cell
- `ZoomToDefault()` — recalculates adaptive zoom and zooms to it (called after placement)

**CameraPan:** WASD/arrow key and middle-mouse drag movement. Works alongside GridCamera.

### Event System
- `IntervalTimer.OnIntervalTick` — Subscribe for interval-based actions
- `ResourceTokenManager.OnTokensChanged` — UI updates on token change
- `FurnitureConnectivityManager.ConnectivityChanged` — Fires when groups are rebuilt
- `FogManager.OnCellRevealed` — Fires when a fog cell is revealed (canonical fog event)
- `GridEntityHealth.OnEntityDestroyed` — Per-entity death event
- `GridEntityHealth.OnAnyEntityDestroyed` — Static global death event
- `ResourceManager.OnResourceChanged` — Fires when any resource amount changes
- `GameStateManager.OnStateChanged` — Title/Playing transitions

## Core Systems

### Grid System (`Assets/Scripts/Core/GridManager.cs`)
- Configurable grid size (default 11x11, ClockworkCraft uses 80x80)
- Cell states: Empty, PlayerUnit, EnemyUnit, Resource
- `cellOccupants[,]` — GameObject array tracking what's on each cell
- `GetOccupant(x, y)` — Public accessor for cell lookup
- `PlaceMultiCell()` / `RemoveMultiCell()` — Multi-cell footprint support
- Checkerboard tile prefabs (A/B pattern)
- `WorldToGridPosition()` / `GridToWorldPosition()` — Coordinate conversion
- `InitializeGrid(width, height)` — Called by MapGeneratorV2 or CafeSceneSetupV2
- Cell size: 1.5 units

**Dual-Layer Grid (Tier 1 = Object, Tier 2 = Surface):**
A tile can hold one Object AND one Surface simultaneously. `IsCellEmpty()` only checks the Object layer — Surface tiles are always buildable.
```csharp
public enum SurfaceType { None, Water, Corruption, Lava }
```
Surface API on GridManager:
- `PlaceSurface(x, y, SurfaceType, GameObject)` — registers a surface (does NOT touch cellStates)
- `RemoveSurface(x, y)` — clears surface entry
- `GetSurface(x, y)` → SurfaceType
- `HasSurface(x, y)` → bool
- `GetSurfaceOccupant(x, y)` → GameObject

CorruptionManager calls `PlaceSurface(SurfaceType.Corruption)` on spread and `RemoveSurface()` on clear, so corruption tiles are queryable via `GetSurface()` without touching the Object layer.

### Placement Flow (Dock → Grid)
1. `DockBarManager` shows cards, player drags a `UnitIcon`
2. `UnitIcon.OnBeginDrag()` calls `DragDropHandler.StartDrag()`
3. Camera zooms in (50% of current distance), auto-rotate off
4. `DragDropHandler.UpdateDrag()` raycasts to ground plane, validates cells, shows arc line + cell highlights
5. On release: `DragDropHandler.EndDrag()` instantiates prefab, routes to either:
   - **Furniture path:** `FurnitureObject.OnPlaced()` handles grid registration + fog reveal + connectivity
   - **Non-furniture path:** Direct grid registration + manual fog reveal + GridEntity attachment
6. Camera calls `ZoomToDefault()` (adaptive zoom out) and `FocusOnPosition()` on placed object

### Fog of War
- `FogManager` (LittleCafe namespace) tracks revealed cells in a bool grid
- `TileFog` component on each tile handles visual state (lowered + transparent when fogged)
- `FogHideable` (ClockworkCraft namespace) hides spawned objects until their cell is revealed
- **Canonical reveal path:** ALL fog reveals must go through `FogManager.Instance.RevealCell()` which fires `OnCellRevealed`. Both `TileFog` (tile visuals) and `FogHideable` (object visibility) listen to this event.
- `GetRevealedCount()` — Used by GridCamera for adaptive zoom calculation
- `enableFog` toggle on MapGeneratorV2 — when false, reveals entire grid for debugging

### Furniture System

**FurnitureObject** (`Assets/Scripts/Components/FurnitureObject.cs`)
- Base component for all placeable objects
- Serialized fields: furnitureType, isFunctional, isWalkable, gridSize
- `SetType()` — Runtime type override (fixes variant prefab inheritance issues)
- `OnPlaced()` / `OnRemoved()` — Lifecycle hooks (handles grid registration, fog reveal, connectivity)
- `RevealSurroundingTiles()` — Routes through `FogManager.RevealCell()` (not GridManager directly)
- Subclasses: `ChairObject`, `TableObject`, `WallObject`

**FurnitureType enum:**
Decoration, Table, Chair, Wall, Countertop, Sink, Cooker

**Prefab Hierarchy:** Root → AnimatorHolder → Recenter → [3D Model]
- Animator controls AnimatorHolder
- Recenter transform is free to move (used by ChairPositionController for tuck)

**ChairPositionController:**
- Moves via Recenter transform's localPosition (NOT root)
- 0.9s delay after placement animation before tuck begins
- States: Idle, Stored (tucked toward table), InUse
- `storedOffset = 0.75f` on local Z axis


### Animation System

**ObjectAnimController** (`Assets/Animations/ObjectAnimController.controller`)
This is the animator controller used by all PEPO furniture prefabs.

States: Idle (default), Appear, Interact, Remove, InteractStrong, InteractWeak
Parameters (all triggers, lowercase): `appear`, `interact`, `remove`, `interact_strong`, `interact_weak`
Transitions:
- Idle → Appear (on `appear` trigger)
- AnyState → Interact (on `interact` trigger) → auto-returns to Idle
- AnyState → Remove (on `remove` trigger) → no return (object destroyed)
- AnyState → InteractStrong (on `interact_strong` trigger) → auto-returns to Idle
- AnyState → InteractWeak (on `interact_weak` trigger) → auto-returns to Idle
- Appear → Idle (on exit time)

Animation clips:
- `Furniture_Appear.anim` — Drop/wobble spawn animation
- `Furniture_Interact_Weak.anim` — Light jiggle on tap (furniture)
- `Furniture_Remove.anim` — Disappear/hide animation
- `Character_Interact_Strong.anim` — Push forward along local Z (0.85 units), brief hold, return. Duration: 0.5s
- `Character_Interact_Weak.anim` — Hesitant nudge forward along local Z (0.25 units), then settle back. Duration: 0.5s

**Direction convention:** All objects have a "face" along their local Z axis. Before playing an interaction animation, the object should be rotated to face its target.

### Grid Entity System (Clockwork Interaction Loop)

The clockwork interaction loop brings auto-rotate-and-interact behavior to the world builder. Any placed object with `isActive=true` in its database entry participates.

**Components (LittleCafe namespace):**

**GridEntityHealth** (`Assets/Scripts/LittleCafe/GridEntityHealth.cs`)
- Implements `IDamageable` (from ClockworkGrid namespace)
- Fields: `currentHP`, `maxHP`, `attackPower`
- `TakeDamage(int damage)` — Reduces HP, triggers red damage flash (0.15s), fires events
- `OnEntityDestroyed` event — HP reached 0. Listener decides what happens.
- `OnAnyEntityDestroyed` static event — GridEntityManager listens globally
- Initialized by `GridEntityManager.AttachComponents()` with database values

**GridEntityActor** (`Assets/Scripts/LittleCafe/GridEntityActor.cs`)
- The clockwork brain. Subscribes to `IntervalTimer.OnIntervalTick`
- Each tick sequence:
  1. **Rotate** — Advances facing (clockwise by default, uses `Facing.RotateClockwise()`)
  2. **Wait** — 0.25s rotation animation (ease-in-out on AnimatorHolder Y rotation)
  3. **Scan** — Walks facing direction cell-by-cell up to `attackRange` via `GridManager.GetCellOccupant()`
  4. **Interact** — If target has `GridEntityHealth` → `interact_strong` + `TakeDamage()`. If occupant but no health → `interact_weak`. If empty → idle.
- Faces target before animation (rotates AnimatorHolder to look at target cell)
- Respects `attackIntervalMultiplier` (e.g., only act every 2nd tick)
- `walkableSurfaces` (string) — surface type(s) this unit can step on. `"None"` = only tiles with no surface. `"Corruption"` = only corrupted tiles. Use `"+"` for multiple (e.g. `"None+Water"`). Parsed by `CanWalkOnTile()` — called in `TryMoveForward()` after bounds check, before occupancy check.

**BehaviorType enum** (`Assets/Scripts/Units/BehaviorType.cs`):
```csharp
RotateAndInteract = 0   // Stays in place; rotates and attacks adjacent targets
RotateAndMove = 1       // Walks forward each tick; bumps on occupied/invalid cells
RotateRotateMove = 2    // Two rotate ticks then one move tick
RotateAndMoveCorrupted = 3  // Like RotateAndMove but only steps onto Corruption surface tiles; silent skip otherwise
```
`RotateAndMoveCorrupted` is used by corruption spikes — they patrol within the corruption cluster but never leave it.

**GridEntityManager** (`Assets/Scripts/LittleCafe/GridEntityManager.cs`)
- Singleton registry and factory
- `AttachComponents(go, hp, attackPower, isActive)` — Adds GridEntityHealth and/or GridEntityActor based on stats
- Overloads: `AttachFromWorkerData()`, `AttachFromUnitData()`, `AttachFromEnvironmentData()`, etc.
- Listens to `GridEntityHealth.OnAnyEntityDestroyed` — cleans up grid, destroys object after remove animation

### Building Production System

**BuildingProductionManager** (`Assets/Scripts/LittleCafe/BuildingProductionManager.cs`)
Singleton that manages per-building production timers and reward collection.

**ProductionEntry** (internal per-building state):
- `baseInterval` / `intervalBonus` — From BuildingDatabase
- `collectCount` — How many times this building has been collected
- `EffectiveInterval = baseInterval + (intervalBonus × collectCount)` — Gets slower each collection
- `pendingWorker` / `pendingCard` — Queued reward waiting for player tap
- `timerCanvasObj` / `timerFillImage` / `timerCountText` — World-space donut timer UI
- `objectTopHeight` — Cached from RefHeight system for positioning

**ProductionOutputType enum** (BuildingData.cs):
- `None` (0) — No production
- `Worker` (1) — Produces a worker card (ConeTent uses this)
- `Currency` (2) — Produces currency that flies to the currency bar
- `RandomBuilding` (3) — Draws random building card from deck (Statue uses this)

**Flow:** IntervalTimer tick → elapsed time accumulates → when >= EffectiveInterval → draw reward + show popup → player taps → collect reward (fly animation) → increment collectCount → next interval is longer

**InstantProduction cheat flag:** When `DevCheatMenu.InstantProduction` is true (editor/dev builds only), `effectiveInterval` is clamped to `1f` on every tick — buildings fire almost immediately. `DrawButtonController.CooldownRoutine` also clamps its duration to `1f` so the draw button cooldown is equally bypassed.

**Current Building Stats (BuildingDatabase.asset):**
- ConeTent: interval=30s, bonus=6s, output=Worker
- Statue: interval=45s, bonus=10s, output=RandomBuilding
- Torch: no production (decoration)

### Dev Cheat Menu (`Assets/Scripts/UI/DevCheatMenu.cs`, namespace: `LittleCafe`)

Editor-only overlay (IMGUI) that exposes gameplay cheats. Only compiled in `DEVELOPMENT_BUILD || UNITY_EDITOR` builds. Toggle via the hidden debug button or keyboard shortcut.

**Static cheat flags** (read by game systems under `#if DEVELOPMENT_BUILD || UNITY_EDITOR`):

| Flag | Default | Effect |
|------|---------|--------|
| `DevCheatMenu.FreeCosts` | false | Bypasses all placement costs: token cost (DragDropHandler), EconomyManager.SpendForPlacement, EconomyManager.CanAfford, draw button token cost (DockBarManager), and draw button upgrade cost (DrawButtonController). Green toggle button labeled "💰 Free Costs [ON/OFF]". |
| `DevCheatMenu.InstantProduction` | false | Forces all building effectiveIntervals to 1s (BuildingProductionManager update loop) and the draw button cooldown to 1s (DrawButtonController.CooldownRoutine). Green toggle button labeled "⚡ Skip Timers [ON/OFF]". |

**Files that read these flags:**
- `DragDropHandler.cs` — FreeCosts bypass on token spend + EconomyManager spend + CanAfford gate
- `DockBarManager.cs` — FreeCosts bypass on draw button token cost
- `DrawButtonController.cs` — FreeCosts bypass on upgrade cost + InstantProduction clamp on cooldown duration
- `BuildingProductionManager.cs` — InstantProduction override of effectiveInterval

### Currency Display System

**ResourceDisplayUI** (`Assets/ClockworkCraft/Scripts/UI/ResourceDisplayUI.cs`)
Uses the Lobby UIPanel's existing white StatusBar slots for currency display:
- Finds the light Lobby UIPanel via `FindObjectsOfType<UIPanel>(true)`
- Gets `StatusBar_Group_ColorButton` via `UIPanel.Get()` as the container
- Repurposes 3 built-in slots: Status_Life → Gold, Status_Coin → Wood, Status_Gem → Stone
- Each slot's Icon Image gets CurrencyDatabase sprite, Text TMP gets amount, Button_Add is hidden
- Additional currencies get new slots (from currencyHolderPrefab or minimal fallback)
- CurrencySlotUI.InitializeWithExisting() for pre-existing UI references

**CurrencyDatabase** — 31 currency types with icons from PEPO StatusIcon assets. Auto-populated by PopulateCurrencyDatabase editor script.

**ResourceManager** — Tracks all 31 currencies, fires OnResourceChanged events, grants starting resources (20 Gold).

### Loot System

**GridEntityLootDrop** — Component attached to environment objects. On entity death, drops resources.
**ResourceLootFX** — Singleton that spawns particle icons that fly from world position to the currency bar.

### Draw & Economy System
- `RaritySystem` manages the draw pool of available items
- `UnitStats` holds per-item data including `drawWeight`, `furnitureTypeOverride`, `isActive`, `maxHP`, `attackDamage`
- `GetEffectiveDrawWeight()` — Uses explicit drawWeight if > 0, else falls back to rarity-based weight
- Draw cost starts at 0 in ClockworkCraft mode
- `UnitIcon` shows weight badge (green "xN") on dock cards

## Map Generation (ClockworkCraft)

### MapGeneratorV2 (`Assets/ClockworkCraft/Scripts/Core/MapGeneratorV2.cs`)
Single authority for map creation. Owns the full pipeline:
1. Ensure singletons (GridManager, FogManager, NodeManager, ResourceManager)
2. Initialize GridManager with grid size
3. Setup worker deck (WorkerDatabase → RaritySystem → DockBarManager)
4. Plan scatter using spawnEntries
5. Initialize fog (full reveal if `enableFog=false`)
6. Spawn center object + all scattered environment

**Execution order:** `ClockworkCraftSceneSetup [-100]` → `MapGeneratorV2 [-10]` → `GridManager [0]`

**Center calculation:** `Mathf.RoundToInt((width - 1) * 0.5f)` to match `GridCamera.PointAtGrid()`

### Spawn Modes
```csharp
public enum SpawnMode
{
    Scattered,  // Random per-tile — direct percentage probability
    Clustered,  // BFS blobs from random seed points
    Edge        // Spawns along the border of existing clusters of a specific type
}
```

**Multi-pass generation:**
- Pass 1 — **Clustered** entries (BFS blob expansion from random seed points)
- Pass 2 — **Edge** entries (border tiles around specified cluster types)
- Pass 3 — **Scattered** entries (random per-tile with direct % weights)

Each pass respects tiles claimed by earlier passes.

### Spawn Weight System
Each entry's `spawnWeight` (0–100) is a **direct per-tile percentage** for Scattered mode. Weight 5 = 5% of tiles get that type. Lower weight = fewer tiles. The sum of all scattered weights is the total chance anything spawns on a given empty tile (capped at 100%). Within that total, each entry gets its proportional slice.

### EnvironmentSpawnEntry (inline on MapGeneratorV2)
```csharp
[System.Serializable]
public class EnvironmentSpawnEntry
{
    public string environmentName;
    public SpawnMode spawnMode;
    [Range(0f, 100f)] public float spawnWeight = 5f;
    [Min(0)] public int minSpacing = 0;
    // Cluster settings
    [Min(1)] public int clusterCount = 3;
    [Min(1)] public int clusterSizeMin = 3;
    [Min(1)] public int clusterSizeMax = 12;
    [Range(0f, 1f)] public float clusterSpread = 0.6f;
    // Edge settings
    public string edgeBorderOf = "";
    [Range(0f, 1f)] public float edgeDensity = 0.5f;
}
```

### Key Inspector Fields (MapGeneratorV2)
- `mapWidth` / `mapHeight` (default 80×80)
- `seed` (0 = random each run)
- `enableFog` (toggle for debugging)
- `startingRevealRadius` (cells revealed around center at start)
- `environmentDatabase` / `workerDatabase` / `buildingDatabase`
- `centerEnvironmentName` (dropdown from EnvironmentDatabase)
- `clearingRadius` (Chebyshev distance — 1 = 3×3 clearing)
- `spawnEntries` (list, one per EnvironmentDatabase item, use "Sync from Database" button)

## Data Layer — ScriptableObject Databases

All databases follow the same pattern: a `[Serializable]` data class + a `ScriptableObject` database with list, query methods, and an editor "Scan PEPO Folder" context menu.

**Universal Fields** — All five databases include:
- `isActive` (bool) — Whether this object acts each interval tick. Workers/Units default `true`; others default `false`.
- `hp` (int) — Health points. When 0, triggers death event.
- `attackPower` (int) — Damage dealt per successful interaction.
- `drawWeight` — Controls card draw probability in RaritySystem.
- `gridSize` — Multi-cell footprint (Vector2Int).
- `visualScale` — Prefab scale override.
- `prefab` / `icon` — Reference to prefab and UI sprite.

### FurnitureDatabase (`LittleCafe/Furniture Database`)
Types: Decoration, Table, Chair, Wall, Countertop, Sink, Cooker

### BuildingDatabase (`LittleCafe/Building Database`)
Types: Generic, House, Shop, Workshop, Storage, Civic, Military, Religious

### WorkerDatabase (`LittleCafe/Worker Database`)
Types: Generic, Villager, Farmer, Miner, Builder, Merchant, Guard, Crafter
**Used by:** MapGeneratorV2.SetupWorkerDeck() to populate the draw pool

### UnitDatabase (`LittleCafe/Unit Database`)
Types: Generic, Villager, Farmer, Miner, Builder, Merchant, Guard, Crafter, Soldier, Archer, Beast, Boss
Query helpers: `GetEnemyUnits()`, `GetAlliedUnits()`, `GetByType()`, `GetFunctionalUnits()`

### EnvironmentDatabase (`LittleCafe/Environment Database`)
Types: Generic, Tree, Rock, Water, Path, Fence, Terrain, Flora
**Used by:** MapGeneratorV2 for procedural world generation

## File Structure

Use Glob/Grep to discover file locations. Key roots:
- `Assets/Scripts/` — Shared systems (Core/, Components/, Data/, LittleCafe/, Systems/, UI/, Editor/)
- `Assets/ClockworkCraft/Scripts/` — ClockworkCraft-specific (Core/, UI/, World/, Editor/)
- `Assets/Animations/` — `ObjectAnimController.controller` + all .anim clips
- `Assets/Scripts/Editor/SheetSyncEditor.cs` + `SheetCache.json` — Sheet sync tool

## Scene Setup Flow

### ClockworkCraft Scene (BOTH scripts exist in this scene)
Both CafeSceneSetupV2 and MapGeneratorV2 run. They cooperate via `deferToMapGen` flag:

**CafeSceneSetupV2** [-100] (shared infrastructure):
- Awake: detects MapGeneratorV2, sets `deferToMapGen=true`
- Awake: runs SetupGrid (tile prefabs), SetupCamera, SetupTokenManager, SetupLighting, SetupFurnitureSystems
- Awake: SKIPS SetupRaritySystem when deferring
- Start: SKIPS InitializeDockBar when deferring
- Start: creates GameStartGate (always)
- OnGameStarted (deferring): only shows dock UI + resumes timer

**MapGeneratorV2** [-10] (grid, deck, fog, map):
- Awake: singleton
- Start: EnsureManagers, SetupDeck (Worker+Building DBs merged into RaritySystem)
- Start: finds GameStartGate, hooks RunGenerate
- RunGenerate (after gate fires): InitializeGridManager → GenerateMap (fog, center goldmine, scattered env)

**Execution order:** CafeSceneSetupV2.Awake → MapGeneratorV2.Awake → CafeSceneSetupV2.Start → MapGeneratorV2.Start → [player clicks] → both OnGameStart callbacks fire

### LittleCafe Scene (standalone cafe builder)
1. `CafeSceneSetupV2` [-100] — `deferToMapGen=false`, full setup with FurnitureDatabase
2. On game start: initializes grid, reveals starting tiles, shows dock bar

## Known Issues & Gotchas

### Variant Prefab Type Inheritance
PEPO variant prefabs may inherit incorrect `furnitureType` from their base prefab. The runtime fix: `UnitStats.furnitureTypeOverride` is set from `FurnitureData.type` in CafeSceneSetupV2, then applied via `FurnitureObject.SetType()` before `OnPlaced()` in DragDropHandler. If adding new prefab variants, either regenerate via PEPOPrefabGenerator or ensure the runtime override is set.

### Chair Tuck & Selection
Chairs visually slide under tables via the Recenter transform, but their grid cell occupancy stays at the original position. The removal system uses grid-based detection (raycast → ground plane → grid cell → occupant) so chairs are always selectable by tapping their original square.

### Fog Reveal Path
ALL fog reveals MUST go through `FogManager.Instance.RevealCell()`. Going directly to `GridManager.RevealTile()` will update tile visuals but NOT notify `FogHideable` objects, leaving spawned environment invisible.

### Center Tile Calculation
Both MapGeneratorV2 and GridCamera use `Mathf.RoundToInt((width - 1) * 0.5f)` for center. Using `width / 2` gives a different result for even-width grids and causes misalignment.

### Shadow Clipping
Spawned objects need `worldPos.y += 0.01f` to avoid shadow z-fighting with the ground plane.

### Clearing Radius
Uses Chebyshev distance (`Mathf.Max(|dx|, |dy|) <= clearingRadius`) for square clearing zone, not Euclidean.

## Design Principles
1. **Grid-centric** — All objects positioned relative to 1.5-unit grid cells
2. **Data-driven** — Stats in ScriptableObject databases for easy tweaking
3. **Animator-first** — Unity Animator for all visual effects
4. **Component composition** — FurnitureObject + GridObject on any prefab makes it placeable
5. **Event-driven** — Loose coupling via events (IntervalTimer, TokenManager, Connectivity, Fog)
6. **Runtime type safety** — furnitureTypeOverride corrects variant prefab inheritance
7. **Recenter transform** — Visual positioning layer free from Animator control
8. **Single authority** — MapGeneratorV2 owns the full map generation pipeline
9. **Canonical paths** — FogManager.RevealCell() is THE way to reveal fog
10. **RefHeight system** — Use GridEntityHPBar.GetTopOfObject() for positioning above objects (checks RefHeight child → renderer bounds → default)

## Standing Rules (MUST FOLLOW)
- **Serialized over runtime** — NEVER use runtime `AddListener()` for button onClick events. Always use persistent serialized listeners visible in Inspector.
- **All objects face along local +Z axis** — Direction convention for interaction animations.
- **Button onClick events MUST be persistent serialized listeners** visible in Inspector.
- **UIPanel.Get() for UI access** — Use UIPanel's indexed Get() method to access serialized UI elements, not FindChildRecursive or manual hierarchy traversal.
- **Bitmap font caution** — MuseoModerno CriticalNum fonts are bitmap-only. Set `richText = false` on any TextMeshPro using these fonts to avoid underline glyph warnings.
- **Console logs** — Check `Logs/Unity_Console_Latest.log` proactively.

## Notion Task List
**Page ID:** `31439a10-1010-8195-97b6-d2956b3326e9`

See CLAUDE_USER.md for the full Notion workflow process.

## Forward-Looking Rules (from 2026-03-29 Post-Mortem)

Rules derived from 12 agent sessions of hard-won lessons. All agents must follow these.

### File Integrity
1. **Verify every edit** — `wc -l` after every file modification, restore from git if count drops.
2. **Keep files small** — target 200–400 lines, hard limit at 600. Design for modularity from day one. MapGeneratorV2.cs at 2695 lines is the highest-priority decomposition target.
3. **Use proper parsers** — Python for JSON, never sed/awk on structured data.

### Data Integrity
4. **Document data formats immediately** — any shared data file (JSON cache, config) gets its format spec in this file before the second session touches it.
5. **Always fetch live data** — never treat caches as source of truth, never hardcode data from memory.
6. **Validate the full pipeline** — parsing cleanly isn't enough; cross-reference column names, enum values, and entry names across all systems in the chain.

### Code Discipline
7. **Read before writing** — understand the consumer before modifying the producer.
8. **Inspector values override code defaults** — check serialized data first when debugging runtime behavior. Before any new Inspector field, answer: is this component scene-placed or AddComponent'd?
9. **Before any change, verify 3 things** — (1) Does the target exist as I think (read it now, don't trust memory)? (2) How is this component created at runtime? (3) Who calls or references this (grep)?

### Session Management
10. **End sessions cleanly** — NEXT STEPS in the log, conventions in RTChess.md, code in a known-good state.
11. **Docs are part of the task, not cleanup** — update JAI_AI_SYNC.md and RTChess.md immediately after each change, not at session end.
12. **Defer Unity-specific operations** — asset renames, .meta changes, and prefab modifications go through the Unity Editor, not the Linux sandbox.

## Pending Work
- Wire EconomyManager into actual placement spending
- Delete one-time editor scripts (SetupIdleBounceAnimation, PopulateCurrencyDatabase, etc.) — they log "DONE — You can safely delete" when no longer needed
- Create EconomyBalanceConfig .asset and assign to MapGeneratorV2
- Consider removing DrawButtonController once Statue building is confirmed working as replacement
- **Decompose MapGeneratorV2.cs** — 2695 lines, 10+ responsibilities. Needs splitting into focused subsystems (<600 lines each). See post-mortem audit for details.

## Credits
Built with Claude Code (Anthropic)
