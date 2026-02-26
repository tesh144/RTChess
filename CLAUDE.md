# ClockworkCraft — Project Documentation

## Project Overview
A Unity-based grid strategy game that evolved from a real-time chess prototype (RTChess) through a cafe builder (LittleCafe) into a world-building simulation (ClockworkCraft). All three share a common core of grid, camera, and placement systems.

**Unity Version:** 2022.3.62f3
**Last Updated:** 2026-02-23

## Current Status

### Active Development: ClockworkCraft (World Builder)
The primary focus is ClockworkCraft — a procedurally generated grid world where players draw worker cards from a deck, drag-and-drop them onto the map, and workers autonomously interact with environment objects on a clockwork tick. The map is generated with clustered forests, edge-spawned water, scattered rocks and gold mines, all hidden by fog of war until the player reveals them.

**Implemented Features:**
- Procedural map generation (MapGeneratorV2) with three spawn modes: Scattered, Clustered, Edge
- Card-game-style dock bar with drag-and-drop placement
- Multi-cell object support (1x1, 2x2, 3x3, etc.)
- PEPO 3D game assets with proper scaling
- Fog of war with tile reveal on placement
- Adaptive camera zoom (grows with revealed tiles)
- Orbiting camera with scroll-wheel zoom, auto-rotate, and WASD pan
- Clockwork interaction loop: workers rotate, scan, and interact with targets each tick
- Grid entity system with HP, damage, and death events
- Furniture connectivity system (tables group, chairs attach to table groups)
- Chair auto-tuck (slides toward adjacent table after placement animation)
- Tap-and-hold removal with radial loading bar
- Tap interaction animation on placed objects
- Per-item draw weights controlling card draw probability
- Placement animations (appear, interact, remove) via Animator
- Layout save/load (JSON serialization)
- Worker deck populated from WorkerDatabase
- Environment objects from EnvironmentDatabase
- Five ScriptableObject databases: Furniture, Building, Worker, Unit, Environment
- Fog toggle for debugging full map visibility
- Seeded deterministic map generation

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

## Core Systems

### Grid System (`Assets/Scripts/Core/GridManager.cs`)
- Configurable grid size (default 11x11, ClockworkCraft uses 40x40)
- Cell states: Empty, PlayerUnit, EnemyUnit, Resource
- `cellOccupants[,]` — GameObject array tracking what's on each cell
- `GetOccupant(x, y)` — Public accessor for cell lookup
- `PlaceMultiCell()` / `RemoveMultiCell()` — Multi-cell footprint support
- Checkerboard tile prefabs (A/B pattern)
- `WorldToGridPosition()` / `GridToWorldPosition()` — Coordinate conversion
- `InitializeGrid(width, height)` — Called by MapGeneratorV2 or CafeSceneSetupV2
- Cell size: 1.5 units

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

**GridEntityManager** (`Assets/Scripts/LittleCafe/GridEntityManager.cs`)
- Singleton registry and factory
- `AttachComponents(go, hp, attackPower, isActive)` — Adds GridEntityHealth and/or GridEntityActor based on stats
- Overloads: `AttachFromWorkerData()`, `AttachFromUnitData()`, `AttachFromEnvironmentData()`, etc.
- Listens to `GridEntityHealth.OnAnyEntityDestroyed` — cleans up grid, destroys object after remove animation

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
- `mapWidth` / `mapHeight` (default 40×40)
- `seed` (0 = random each run)
- `enableFog` (toggle for debugging)
- `startingRevealRadius` (cells revealed around center at start)
- `environmentDatabase` / `workerDatabase` / `buildingDatabase`
- `centerEnvironmentName` (dropdown from EnvironmentDatabase)
- `clearingRadius` (Chebyshev distance — 1 = 3×3 clearing)
- `spawnEntries` (list, one per EnvironmentDatabase item, use "Sync from Database" button)

### Recommended Spawn Settings
- **Goldmine:** Scattered, weight 0.5, spacing 8
- **Tree:** Clustered, 6 clusters, size 4–15, spread 0.65
- **Water:** Edge, border of Tree, edge density 0.4
- **Rock:** Scattered, weight 4, spacing 3
- **Mountain:** Scattered, weight 1, spacing 6

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
```
Assets/
├── Scripts/
│   ├── Core/                          # Shared systems
│   │   ├── GridManager.cs             # Grid state, cell occupants, coordinates
│   │   ├── GridVisualizer.cs          # Grid rendering
│   │   ├── IntervalTimer.cs           # Global clock
│   │   ├── ResourceTokenManager.cs    # Token economy
│   │   ├── SFXManager.cs              # Sound effects
│   │   ├── TileFog.cs                 # Per-tile fog component
│   │   ├── CameraPan.cs              # WASD/arrow + middle-mouse pan
│   │   ├── CameraSystemLocator.cs     # Camera service locator
│   │   ├── ICameraSystem.cs           # Camera interface
│   │   ├── PlacementAudioManager.cs   # Placement sounds
│   │   └── GradientBackground.cs      # Background gradient
│   │
│   ├── Components/                    # Shared components
│   │   ├── FurnitureObject.cs         # Base furniture (+ FurnitureType enum)
│   │   ├── ChairObject.cs             # Chair specialization
│   │   ├── TableObject.cs             # Table specialization
│   │   ├── WallObject.cs              # Wall specialization
│   │   ├── ChairPositionController.cs # Chair tuck via Recenter transform
│   │   ├── GridObject.cs              # Grid size component (1x1, 2x2, etc.)
│   │   └── AnimatorLifecycleManager.cs # Animator lifecycle hooks
│   │
│   ├── Data/                          # ScriptableObject databases
│   │   ├── FurnitureData.cs + FurnitureDatabase.cs
│   │   ├── BuildingData.cs + BuildingDatabase.cs
│   │   ├── WorkerData.cs + WorkerDatabase.cs
│   │   ├── UnitData.cs + UnitDatabase.cs
│   │   ├── EnvironmentData.cs + EnvironmentDatabase.cs
│   │   └── UnitStats.cs               # Per-unit stats for RaritySystem
│   │
│   ├── LittleCafe/                    # Cafe/world builder systems
│   │   ├── CafeSceneSetupV2.cs        # Cafe scene bootstrap (FurnitureDatabase)
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
│   │   ├── LayoutLoader.cs            # Load layouts from JSON
│   │   ├── GameModeManager.cs         # Build/Play mode switching
│   │   └── GameStartGate.cs           # Wait for first click before starting
│   │
│   ├── Systems/                       # Game-wide systems
│   │   ├── FogManager.cs              # Fog of war + GetRevealedCount()
│   │   └── RaritySystem.cs            # Weighted random draw
│   │
│   ├── UI/                            # UI components
│   │   ├── DockBarManager.cs          # Bottom dock bar
│   │   ├── DragDropHandler.cs         # Drag state, arc line, placement
│   │   ├── UnitIcon.cs                # Draggable card with weight badge
│   │   ├── TokenUI.cs                 # Token counter display
│   │   ├── DebugMenu.cs              # Debug panel toggle
│   │   ├── DebugPanel.cs             # Debug tools (pause, speed, clear)
│   │   ├── HiddenDebugButton.cs       # Hidden button to toggle debug
│   │   └── CoinFlyEffect.cs           # Floating coin visual
│   │
│   ├── Units/                         # Shared unit utilities
│   │   ├── Facing.cs                  # Direction system (used by GridEntityActor)
│   │   └── IDamageable.cs             # Damage interface
│   │
│   ├── Visuals/                       # Visual effects
│   │   ├── FogGridVisualizer.cs       # Fog grid debug visualization
│   │   └── FogVisual.cs              # Fog visual effects
│   │
│   └── Editor/                        # Editor-only tools
│       ├── PEPOPrefabGenerator.cs     # Generates prefab variants from FBX + database
│       ├── PEPOPrefabFixer.cs         # Fixes PEPO prefab issues
│       ├── SetupObjectAnimController.cs # Sets up animator on PEPO prefabs
│       ├── CafeBuilderAutoSetup.cs    # Auto-configures cafe scenes
│       ├── AutoConsoleErrorDetector.cs # Monitors console for errors
│       ├── CompilationMonitor.cs      # Tracks compilation state
│       ├── ConsoleLogMonitor.cs       # Logs console to file
│       ├── ConsoleLogger.cs           # Console logging utility
│       ├── ConsoleLogReader.cs        # Reads console logs
│       ├── AutoErrorFixer.cs          # Auto-detects and fixes errors
│       ├── PlayModeMonitor.cs         # Monitors play mode state
│       └── AnimationDebugger.cs       # Debug animator state
│
├── ClockworkCraft/
│   └── Scripts/
│       ├── Core/
│       │   ├── ClockworkCraftSceneSetup.cs  # Camera-only bootstrap [-100]
│       │   ├── MapGeneratorV2.cs            # Procedural map generation [-10]
│       │   ├── NodeManager.cs               # Resource node registry
│       │   └── ResourceManager.cs           # Resource economy
│       ├── World/
│       │   ├── ResourceNode.cs              # ClockworkCraft resource node
│       │   ├── FogHideable.cs               # Hides objects until fog reveals
│       │   └── TileType.cs                  # TileType + ResourceType enums
│       └── Editor/
│           └── MapGeneratorV2Editor.cs      # Custom inspector for MapGeneratorV2
│
├── Animations/
│   ├── ObjectAnimController.controller      # THE controller for PEPO prefabs
│   ├── Furniture_Appear.anim
│   ├── Furniture_Interact_Weak.anim
│   ├── Furniture_Remove.anim
│   ├── Character_Interact_Strong.anim
│   └── Character_Interact_Weak.anim
│
├── Prefabs/
│   └── PEPO/
│       └── MainFurniture/                   # Variant prefabs used in game
│
└── Scenes/
    ├── LittleCafe-Builder.unity             # Cafe builder scene
    └── SampleScene.unity                    # Original scene
```

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

## Current Bug (2026-02-23)
Game doesn't start properly in ClockworkCraft scene. Symptoms: tiles don't appear, red errors on play.
Recent changes made: CafeSceneSetupV2 deferToMapGen pattern, MapGeneratorV2 BuildingDatabase field added, InitializeGridManager moved into RunGenerate.
**ACTION NEEDED:** MapGeneratorV2's `buildingDatabase` field must be assigned in Unity Inspector.

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

## Development Workflow
- **Main development:** jai
- **Auto-compile:** Unity stays open, scripts auto-compile on save
- **Console logs:** Check `Logs/Unity_Console_Latest.log` proactively — never ask user to check
- **Prefab generation:** Use PEPOPrefabGenerator editor script for new assets
- **Database editing:** Edit database .asset files in Inspector, use "Scan PEPO Folder" to sync

## Credits
Built with Claude Code (Anthropic)
