# RTChess / LittleCafe — Project Documentation

## Project Overview
A Unity-based grid strategy game with two game modes sharing a common core. The project started as a real-time chess-like combat game (RTChess) and evolved into a cafe/world builder (LittleCafe) using the same grid, camera, and placement systems.

**Unity Version:** 2022.3.62f3
**Last Updated:** 2026-02-21

## Current Status

### Active Development: LittleCafe (Cafe/World Builder)
The primary focus is the cafe builder mode — a cozy grid-based placement game where players draw furniture cards, drag-and-drop them onto a grid, and build layouts. This has grown into a broader world-building concept with buildings, workers, and environment objects.

**Implemented Features:**
- Card-game-style dock bar with drag-and-drop placement
- Multi-cell object support (1x1, 2x2, 3x3, etc.)
- PEPO 3D game assets with proper scaling
- Fog of war with tile reveal on placement
- Adaptive camera zoom (grows with revealed tiles)
- Orbiting camera with scroll-wheel zoom and auto-rotate
- Furniture connectivity system (tables group, chairs attach to table groups)
- Debug perimeter outlines around connected groups
- Chair auto-tuck (slides toward adjacent table after placement animation)
- Tap-and-hold furniture removal with radial loading bar
- Tap interaction animation on placed objects
- Per-item draw weights controlling card draw probability
- Draw cost that decreases over time via interval timer
- Placement animations (appear, interact, remove) via Animator
- Layout save/load (JSON serialization)

### Legacy Mode: RTChess (Combat)
The original combat system is still intact but not actively developed. Units auto-rotate, attack resources, and fight enemies on a timer.

## Architecture

### Namespaces
- `ClockworkGrid` — Core shared systems (GridManager, IntervalTimer, DragDropHandler, etc.)
- `LittleCafe` — Cafe/world builder systems (FurnitureObject, GridCamera, connectivity, etc.)

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
- `WaveManager.Instance` — Enemy wave spawning (RTChess)
- `SFXManager.Instance` — Sound effects

### Camera System
`ICameraSystem` interface with `CameraSystemLocator` service locator. GridCamera (LittleCafe) and CameraController (RTChess) both implement it. Code references `CameraSystemLocator.Current` for scene-agnostic camera access.

**GridCamera features:**
- Orbiting around a pivot point
- Scroll-wheel zoom clamped to [minDistance, currentMaxDistance]
- Adaptive zoom: default and max distance grow with `FogManager.GetRevealedCount()`
- Auto-rotate (slow orbit)
- Camera shake
- Focus on position / cell
- `ZoomToDefault()` — recalculates adaptive zoom and zooms to it (called after placement)

### Event System
- `IntervalTimer.OnIntervalTick` — Subscribe for interval-based actions
- `ResourceTokenManager.OnTokensChanged` — UI updates on token change
- `FurnitureConnectivityManager.ConnectivityChanged` — Fires when groups are rebuilt

## Core Systems

### Grid System (`Assets/Scripts/Core/GridManager.cs`)
- Configurable grid size (default 11x11, cafe uses custom sizes)
- Cell states: Empty, PlayerUnit, EnemyUnit, Resource
- `cellOccupants[,]` — GameObject array tracking what's on each cell
- `GetOccupant(x, y)` — Public accessor for cell lookup
- `PlaceMultiCell()` / `RemoveMultiCell()` — Multi-cell footprint support
- Checkerboard tile prefabs (A/B pattern)
- `WorldToGridPosition()` / `GridToWorldPosition()` — Coordinate conversion
- Cell size: 1.5 units

### Placement Flow (Dock → Grid)
1. `DockBarManager` shows cards, player drags a `UnitIcon`
2. `UnitIcon.OnBeginDrag()` calls `DragDropHandler.StartDrag()`
3. Camera zooms in (50% of current distance), auto-rotate off
4. `DragDropHandler.UpdateDrag()` raycasts to ground plane, validates cells, shows arc line + cell highlights
5. On release: `DragDropHandler.EndDrag()` instantiates prefab, applies `furnitureTypeOverride` from UnitStats, calls `FurnitureObject.OnPlaced()`
6. `OnPlaced()` registers with grid, reveals fog tiles, registers with connectivity manager, triggers appear animation
7. Camera calls `ZoomToDefault()` (adaptive zoom out) and `FocusOnPosition()` on placed object

### Furniture System

**FurnitureObject** (`Assets/Scripts/Components/FurnitureObject.cs`)
- Base component for all placeable objects
- Serialized fields: furnitureType, isFunctional, isWalkable, gridSize
- `SetType()` — Runtime type override (fixes variant prefab inheritance issues)
- `OnPlaced()` / `OnRemoved()` — Lifecycle hooks
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

### Connectivity System

**FurnitureConnectivityManager** — Two-pass BFS grouping:
- Pass 1: Groups same-type functional furniture (tables with tables, counters with counters, etc.). Skips Chair and Decoration types.
- Pass 2: Attaches chairs to adjacent table groups.
- Strict orthogonal adjacency only (no diagonal connections)
- `StrictOverlapX/Y` — Ensures actual face overlap for multi-cell items

**FurnitureGroup** — Contains members, occupied cells, debug color. Provides `GetAllPerimeterPositions()` for seating.

**FurnitureConnectionDebugVisualizer** — GL.QUADS perimeter outlines and fill for each group, diamond markers for seating positions.

### Removal System (`FurnitureRemovalHandler`)
- Grid-based detection: raycasts to ground plane → grid cell → `GetOccupant()` → FurnitureObject
- This ensures chairs tucked under tables are still selectable via their original grid square
- Tap (< 0.2s): triggers `interact` animation
- Hold past 0.2s: fires interact animation, shows radial loading bar
- Hold 3s: triggers `remove` animation, calls `OnRemoved()`, destroys GameObject
- Won't activate during drag (`DragDropHandler.IsDragging`)

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
- `Character_Interact_Strong.anim` — Push forward along local Z (0.85 units, slightly past cell edge), brief hold, return. Used for valid target interactions/attacks. Duration: 0.5s
- `Character_Interact_Weak.anim` — Hesitant nudge forward along local Z (0.25 units), then settle back. Used when target is invalid. Duration: 0.5s

**Direction convention:** All objects have a "face" along their local Z axis. Before playing an interaction animation, the object should be rotated to face its target.

**Note:** There is also `ObjectAnimatorController.controller` in Prefabs/ — this is the old RTChess controller (not used by PEPO furniture). The PEPO prefabs use `ObjectAnimController` in Animations/.

### Grid Entity System (Clockwork Interaction Loop)

The clockwork interaction loop brings the RTChess auto-rotate-and-attack behavior into the LittleCafe world builder. Any placed object with `isActive=true` in its database entry participates.

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

**GridEntityManager** (`Assets/Scripts/LittleCafe/GridEntityManager.cs`)
- Singleton registry and factory
- `AttachComponents(go, hp, attackPower, isActive)` — Adds GridEntityHealth and/or GridEntityActor based on stats
- Tracks all living health entities and active actors
- Listens to `GridEntityHealth.OnAnyEntityDestroyed` — cleans up grid, destroys object after remove animation
- Overloads for each database type: `AttachFromWorkerData()`, `AttachFromUnitData()`, etc.
- Created by `CafeSceneSetupV2.SetupFurnitureSystems()`

**Integration:** `DragDropHandler.EndDrag()` calls `GridEntityManager.AttachComponents()` after `FurnitureObject.OnPlaced()`, passing stats from `UnitStats` (which now includes `isActive`, `maxHP`, `attackDamage`).

### Draw & Economy System
- `RaritySystem` manages the draw pool of available items
- `UnitStats` holds per-item data including `drawWeight`, `furnitureTypeOverride`, `isActive`, `maxHP`, `attackDamage`
- `GetEffectiveDrawWeight()` — Uses explicit drawWeight if > 0, else falls back to rarity-based weight
- Draw cost starts at 0 in cafe mode and decreases over time via interval timer
- `UnitIcon` shows weight badge (green "xN") on dock cards

### Fog of War
- `FogManager` tracks revealed cells in a bool grid
- `TileFog` component on each tile handles visual state (lowered + transparent when fogged)
- Placing furniture reveals surrounding tiles based on `revealRadius`
- `GetRevealedCount()` — Used by GridCamera for adaptive zoom calculation

## Data Layer — ScriptableObject Databases

All databases follow the same pattern: a `[Serializable]` data class + a `ScriptableObject` database with list, query methods, and an editor "Scan PEPO Folder" context menu.

**Universal Combat Stats** — Worker, Unit, Building, and Environment databases all include:
- `hp` (int) — Health points. When HP reaches 0 it triggers an event: usually removal/destruction for resources, but can mean completion for buildings or other custom behavior. Fixed from database, not modified at runtime.
- `attackPower` (int) — Damage dealt to a target's HP per successful interaction. A worker with attackPower=1 hitting a tree with hp=10 reduces the tree to 9 HP.
- Defaults: Workers/Units default to hp=3, attackPower=1. Buildings default to hp=10, attackPower=0. Environment defaults to hp=5, attackPower=0.

**Universal Activity Flag** — All five databases (Furniture, Building, Worker, Unit, Environment) include:
- `isActive` (bool) — Whether this object performs an action each interval tick. Active objects (workers, units) do something on the clock; passive objects (buildings, furniture, environment) just sit there. Workers and Units default to `true`; Furniture, Buildings, and Environment default to `false`. Can be overridden per-entry.

### FurnitureDatabase (`LittleCafe/Furniture Database`)
- **Data class:** `FurnitureData` — assetName, FurnitureType, isFunctional, isWalkable, drawWeight, gridSize, visualScale, prefab, icon
- **Types:** Decoration, Table, Chair, Wall, Countertop, Sink, Cooker
- **Asset:** `Assets/Scripts/Data/FurnitureDatabase.asset`
- **Used by:** CafeSceneSetupV2 to populate RaritySystem draw pool

### BuildingDatabase (`LittleCafe/Building Database`)
- **Data class:** `BuildingData` — same fields as FurnitureData but with BuildingType
- **Types:** Generic, House, Shop, Workshop, Storage, Civic, Military, Religious
- **Status:** Schema created, ready for prefab assignment

### WorkerDatabase (`LittleCafe/Worker Database`)
- **Data class:** `WorkerData` — same fields, defaults to isWalkable=true
- **Types:** Generic, Villager, Farmer, Miner, Builder, Merchant, Guard, Crafter
- **Status:** Schema created, ready for prefab assignment

### UnitDatabase (`LittleCafe/Unit Database`)
- **Data class:** `UnitData` — same fields as WorkerData plus `isEnemy` bool to distinguish allied vs enemy units
- **Enum:** `GameUnitType` (named to avoid clash with `ClockworkGrid.UnitType`)
- **Types:** Generic, Villager, Farmer, Miner, Builder, Merchant, Guard, Crafter, Soldier, Archer, Beast, Boss
- **Query helpers:** `GetEnemyUnits()`, `GetAlliedUnits()`, `GetByType()`, `GetFunctionalUnits()`
- **Status:** Schema created, ready for prefab assignment

### EnvironmentDatabase (`LittleCafe/Environment Database`)
- **Data class:** `EnvironmentData` — same fields with EnvironmentType
- **Types:** Generic, Tree, Rock, Water, Path, Fence, Terrain, Flora
- **Status:** Schema created, ready for prefab assignment

## File Structure
```
Assets/
├── Scripts/
│   ├── Core/                          # Shared systems (both game modes)
│   │   ├── GameSetup.cs               # Scene bootstrap
│   │   ├── GridManager.cs             # Grid state, cell occupants, coordinates
│   │   ├── GridVisualizer.cs          # Grid rendering
│   │   ├── IntervalTimer.cs           # Global clock
│   │   ├── ResourceNode.cs            # Harvestable nodes (RTChess)
│   │   ├── ResourceTokenManager.cs    # Token economy
│   │   ├── WaveManager.cs             # Enemy wave spawning (RTChess)
│   │   ├── SFXManager.cs              # Sound effects
│   │   ├── TileFog.cs                 # Per-tile fog component
│   │   ├── CameraController.cs        # RTChess camera
│   │   ├── CameraSystemLocator.cs     # Camera service locator
│   │   ├── ICameraSystem.cs           # Camera interface
│   │   └── PlacementAudioManager.cs   # Placement sounds
│   │
│   ├── Components/                    # Shared components
│   │   ├── FurnitureObject.cs         # Base furniture (+ FurnitureType enum)
│   │   ├── ChairObject.cs             # Chair specialization
│   │   ├── TableObject.cs             # Table specialization
│   │   ├── WallObject.cs              # Wall specialization
│   │   ├── ChairPositionController.cs # Chair tuck via Recenter transform
│   │   ├── GridObject.cs              # Grid size component (1x1, 2x2, etc.)
│   │   └── PlacementAnimation.cs      # Code-based animation (unused)
│   │
│   ├── Data/                          # ScriptableObject databases
│   │   ├── FurnitureData.cs           # Furniture config + FurnitureDatabase.cs
│   │   ├── BuildingData.cs            # Building config + BuildingDatabase.cs
│   │   ├── WorkerData.cs              # Worker config + WorkerDatabase.cs
│   │   ├── UnitData.cs                # Unit config (allied + enemy) + UnitDatabase.cs
│   │   ├── EnvironmentData.cs         # Environment config + EnvironmentDatabase.cs
│   │   ├── UnitStats.cs               # Per-unit stats (draw weight, type override, etc.)
│   │   └── ResourceNodeStats.cs       # Resource node stats
│   │
│   ├── LittleCafe/                    # Cafe/world builder systems
│   │   ├── CafeSceneSetupV2.cs        # Scene bootstrap for cafe mode
│   │   ├── GridCamera.cs              # Orbiting camera with adaptive zoom
│   │   ├── FurnitureConnectivityManager.cs  # BFS grouping
│   │   ├── FurnitureConnectionDebugVisualizer.cs  # GL outline rendering
│   │   ├── FurnitureGroup.cs          # Group data structure
│   │   ├── FurnitureRemovalHandler.cs # Tap/hold removal with loading bar
│   │   ├── TableSeatingManager.cs     # Chair-to-table seat tracking
│   │   ├── GridEntityHealth.cs        # HP, IDamageable, damage flash, death events
│   │   ├── GridEntityActor.cs         # Clockwork brain: rotate, scan, interact
│   │   ├── GridEntityManager.cs       # Entity registry + component factory
│   │   ├── LayoutSerializer.cs        # JSON save/load
│   │   ├── LayoutManager.cs           # Layout management
│   │   ├── CafeEquipment.cs           # Legacy equipment component
│   │   └── [other legacy cafe scripts]
│   │
│   ├── Systems/                       # Game-wide systems
│   │   ├── FogManager.cs              # Fog of war + GetRevealedCount()
│   │   ├── RaritySystem.cs            # Weighted random draw
│   │   └── GridExpansionManager.cs    # Dynamic grid resizing
│   │
│   ├── UI/                            # UI components
│   │   ├── DockBarManager.cs          # Bottom dock bar
│   │   ├── DragDropHandler.cs         # Drag state, arc line, placement
│   │   ├── UnitIcon.cs                # Draggable card with weight badge
│   │   ├── TokenUI.cs                 # Token counter display
│   │   ├── DebugMenu.cs              # Debug panel
│   │   └── [other UI scripts]
│   │
│   ├── Units/                         # RTChess combat units
│   │   ├── Unit.cs                    # Base unit with combat
│   │   ├── Facing.cs                  # Direction system
│   │   └── [model builders, etc.]
│   │
│   └── Editor/                        # Editor-only scripts
│       ├── PEPOPrefabGenerator.cs     # Generates prefab variants from FBX + database
│       └── [other editor tools]
│
├── Animations/
│   ├── ObjectAnimController.controller  # THE controller used by PEPO prefabs
│   ├── ObjectAnimations/
│   │   ├── Object_Appear.anim
│   │   ├── Object_Interact.anim
│   │   ├── Object_Destroy.anim
│   │   └── Object_Idle.anim
│   ├── Furniture_Appear.anim
│   ├── Furniture_Interact_Weak.anim
│   ├── Furniture_Interact_Strong.anim
│   ├── Furniture_Remove.anim
│   ├── Character_Interact_Strong.anim
│   └── Character_Interact_Weak.anim
│
├── Prefabs/
│   ├── PEPO/
│   │   ├── MainFurniture/             # Variant prefabs used in game
│   │   │   ├── DiningTable Variant.prefab
│   │   │   ├── Chair_1 Variant.prefab
│   │   │   ├── Chair_2 Variant.prefab
│   │   │   ├── Counter Variant.prefab
│   │   │   ├── Wall Variant.prefab
│   │   │   ├── WoodFence_cross Variant.prefab
│   │   │   ├── WoodFence_straight Variant.prefab
│   │   │   ├── PineTree Variant.prefab
│   │   │   ├── Sink_1 Variant.prefab
│   │   │   ├── Sink_2 Variant.prefab
│   │   │   ├── Furnace Variant.prefab
│   │   │   └── Firepit Variant.prefab
│   │   └── [base PEPO FBX prefabs]
│   └── ObjectAnimatorController.controller  # OLD RTChess controller (not used by furniture)
│
├── Docs/LittleCafe/                   # Design documentation
│   ├── GDD/Little-Cafe-GDD.docx
│   ├── Diagrams/
│   ├── Prompts/
│   └── References/
│
└── Scripts/Data/
    └── FurnitureDatabase.asset        # The active furniture database asset
```

## Known Issues & Gotchas

### Variant Prefab Type Inheritance
PEPO variant prefabs may inherit incorrect `furnitureType` from their base prefab. The runtime fix: `UnitStats.furnitureTypeOverride` is set from `FurnitureData.type` in CafeSceneSetupV2, then applied via `FurnitureObject.SetType()` before `OnPlaced()` in DragDropHandler. If adding new prefab variants, either regenerate via PEPOPrefabGenerator or ensure the runtime override is set.

### Two Animator Controllers
- `Assets/Animations/ObjectAnimController.controller` — Used by PEPO furniture. Has Idle, Appear, Interact, Remove states with lowercase trigger names (`appear`, `interact`, `remove`).
- `Assets/Prefabs/ObjectAnimatorController.controller` — Old RTChess controller. Has different trigger names (`Interact`, `Destroy` — uppercase). Not used by furniture.

### Chair Tuck & Selection
Chairs visually slide under tables via the Recenter transform, but their grid cell occupancy stays at the original position. The removal system uses grid-based detection (raycast → ground plane → grid cell → occupant) so chairs are always selectable by tapping their original square.

## Design Principles
1. **Grid-centric** — All objects positioned relative to 1.5-unit grid cells
2. **Data-driven** — Stats in ScriptableObject databases for easy tweaking
3. **Animator-first** — Unity Animator for all visual effects
4. **Component composition** — FurnitureObject + GridObject on any prefab makes it placeable
5. **Event-driven** — Loose coupling via events (IntervalTimer, TokenManager, Connectivity)
6. **Scene-agnostic camera** — ICameraSystem interface + CameraSystemLocator
7. **Runtime type safety** — furnitureTypeOverride corrects variant prefab inheritance
8. **Recenter transform** — Visual positioning layer free from Animator control

## Development Workflow
- **Main development:** jai
- **Auto-compile:** Unity stays open, scripts auto-compile on save
- **Console logs:** Check `Logs/Unity_Console_Latest.log` proactively
- **Prefab generation:** Use PEPOPrefabGenerator editor script for new assets
- **Database editing:** Edit FurnitureDatabase.asset (or Building/Worker/Environment) in Inspector

## Credits
Built with Claude Code (Anthropic)
