# AI Developer Handoff — Clockwork Craft / RTChess / LittleCafe

**Last Updated:** 2026-02-21
**Purpose:** Onboarding document for any AI assistant (Claude Cowork, Claude Code, etc.) working on this project. Read this FIRST before making any changes.

---

## CRITICAL: Read Before Doing Anything

### Step 1: Read CLAUDE.md
The file `CLAUDE.md` in the project root is the **single source of truth** for the project's architecture, file structure, systems, and conventions. It is kept up to date as development progresses. **You must read it before writing any code.**

### Step 2: Read the GDD
The Game Design Document at `Assets/Docs/ClockworkCraft/ClockworkCraft-GDD.md` describes the game's design vision, the clockwork interval system, resource types, worker behavior, building system, and development phases. Read it to understand *what we're building* before you start.

### Step 3: Understand What Already Exists
This project has **extensive existing systems**. The number one mistake an AI can make here is creating new systems that duplicate what already exists. Before writing anything new, search the codebase for existing implementations.

---

## Golden Rules

1. **Never create something that already exists.** Search the codebase first. We have grid management, interval timers, placement systems, drag-and-drop, fog of war, connectivity, animation controllers, and more already built.

2. **Use the existing namespaces.** Shared/core code goes in `ClockworkGrid`. Cafe/world builder code goes in `LittleCafe`. Do not create new namespaces without discussing it first.

3. **Follow the singleton pattern.** All managers use `public static ManagerName Instance { get; private set; }` with Awake enforcement. See any existing manager for the pattern.

4. **Use the existing Animator system.** All PEPO prefabs use `ObjectAnimController.controller` in `Assets/Animations/`. It has these states and lowercase trigger parameters: `appear`, `interact`, `remove`, `interact_strong`, `interact_weak`. Do NOT create new animator controllers for placed objects.

5. **Respect the prefab hierarchy.** PEPO prefabs follow: `Root → AnimatorHolder → Recenter → [3D Model]`. The Animator lives on AnimatorHolder. Visual positioning adjustments go on the Recenter transform's localPosition. Never move the Root transform for visual effects — that's the grid position.

6. **Subscribe/unsubscribe properly.** Subscribe to events in `OnEnable()`, unsubscribe in `OnDisable()`. The IntervalTimer fires `OnIntervalTick(int intervalCount)` — this is the heartbeat of the game.

7. **Grid coordinates are everything.** All objects live on a discrete grid. Cell size is 1.5 units. Use `GridManager.Instance` for coordinate conversion, cell occupant lookup, and placement. Use `GridManager.GetCellOccupant(x, y)` to find what's at a cell.

8. **Check the Unity console log proactively.** After making changes, check `Logs/Unity_Console_Latest.log` for compile errors. Never ask the user to check the console — do it yourself.

9. **Data lives in ScriptableObject databases.** Do not hardcode stats. All object data (HP, attack power, draw weight, grid size, etc.) lives in database assets. See the Data section below.

10. **Use `Facing` enum for directions.** It's in `ClockworkGrid` namespace with extension methods for rotation and grid offsets. Don't reinvent direction handling.

---

## Project Architecture Summary

### Two Game Modes, One Core
- **RTChess (legacy):** Real-time chess combat. Units auto-rotate and fight on a timer. Code in `Scripts/Units/Unit.cs`. Still compiles but not actively developed.
- **LittleCafe / Clockwork Craft (active):** World builder evolving into a full RTS village builder. Players place workers, buildings, and environment objects on a grid. Active objects rotate and interact each interval tick.

### Key Systems (all exist, do not recreate)

| System | Singleton | File | What It Does |
|--------|-----------|------|-------------|
| Grid | `GridManager.Instance` | `Scripts/Core/GridManager.cs` | Grid state, cell occupants, coordinate conversion |
| Clock | `IntervalTimer.Instance` | `Scripts/Core/IntervalTimer.cs` | Global tick timer (default 2s). Everything runs on this. |
| Economy | `ResourceTokenManager.Instance` | `Scripts/Core/ResourceTokenManager.cs` | Token tracking |
| Drag & Drop | `DragDropHandler.Instance` | `Scripts/UI/DragDropHandler.cs` | Card-to-grid placement |
| Dock Bar | `DockBarManager.Instance` | `Scripts/UI/DockBarManager.cs` | Bottom card bar |
| Draw Pool | `RaritySystem.Instance` | `Scripts/Systems/RaritySystem.cs` | Weighted random card draw |
| Connectivity | `FurnitureConnectivityManager.Instance` | `Scripts/LittleCafe/FurnitureConnectivityManager.cs` | BFS grouping of adjacent furniture |
| Fog | `FogManager.Instance` | `Scripts/Systems/FogManager.cs` | Fog of war, tile reveal |
| Camera | `GridCamera.Instance` | `Scripts/LittleCafe/GridCamera.cs` | Orbiting camera with adaptive zoom |
| Removal | `FurnitureRemovalHandler` | `Scripts/LittleCafe/FurnitureRemovalHandler.cs` | Tap/hold to remove objects |
| Seating | `TableSeatingManager.Instance` | `Scripts/LittleCafe/TableSeatingManager.cs` | Chair-to-table attachment |
| Scene Setup | `CafeSceneSetupV2` | `Scripts/LittleCafe/CafeSceneSetupV2.cs` | Bootstraps the LittleCafe scene |

### Databases (ScriptableObjects)

All five databases share a common pattern: a `[Serializable]` data class with fields, a `ScriptableObject` database with a list and query methods, and a "Scan PEPO Folder" editor context menu.

| Database | Data Class | Key Fields | Defaults |
|----------|-----------|------------|----------|
| FurnitureDatabase | FurnitureData | type, isFunctional, isWalkable, drawWeight, gridSize, visualScale, isActive | isActive=false |
| BuildingDatabase | BuildingData | type, hp, attackPower, isActive, gridSize | hp=10, atk=0, isActive=false |
| WorkerDatabase | WorkerData | type, hp, attackPower, isActive, gridSize | hp=3, atk=1, isActive=true |
| UnitDatabase | UnitData | type (GameUnitType), isEnemy, hp, attackPower, isActive | hp=3, atk=1, isActive=true |
| EnvironmentDatabase | EnvironmentData | type, hp, attackPower, isActive, gridSize | hp=5, atk=0, isActive=false |

**Important:** `UnitData` uses `GameUnitType` (not `UnitType`) to avoid a naming collision with `ClockworkGrid.UnitType` in UnitStats.cs.

**Universal fields across all databases:**
- `hp` (int) — Health points. HP reaching 0 is a trigger event (not always destruction).
- `attackPower` (int) — Damage dealt per successful interaction.
- `isActive` (bool) — Whether this object performs an action each interval tick.

### Animation System

**Controller:** `Assets/Animations/ObjectAnimController.controller`
**States:** Idle (default), Appear, Interact, Remove, InteractStrong, InteractWeak
**Parameters (all triggers, all lowercase):** `appear`, `interact`, `remove`, `interact_strong`, `interact_weak`

**Animation Clips:**
- `Furniture_Appear.anim` — Drop/wobble spawn
- `Furniture_Interact_Weak.anim` — Light jiggle (furniture tap)
- `Furniture_Remove.anim` — Disappear
- `Character_Interact_Strong.anim` — Dramatic leap forward + impact + calm slide back (attack/valid interaction)
- `Character_Interact_Weak.anim` — Timid hop + hesitation + settle (invalid target)

**Direction convention:** All objects face along their local Z axis. Rotate to face target before playing interaction animations.

### Placement Flow (how objects get onto the grid)
1. `DockBarManager` shows cards → player drags a `UnitIcon`
2. `UnitIcon.OnBeginDrag()` → `DragDropHandler.StartDrag()`
3. Camera zooms in, shows cell highlights and arc line
4. On release: `DragDropHandler.EndDrag()` instantiates prefab
5. `FurnitureObject.OnPlaced()` registers with grid, reveals fog, registers with connectivity
6. Camera zooms to adaptive default and focuses on placed object

### The Clockwork Loop (how active objects behave)
Each interval tick (2s default), every active object:
1. **Rotates** one step clockwise (N→E→S→W→N)
2. **Scans** the cell in its facing direction
3. **Interacts** — if valid target: `interact_strong` + deal damage. If invalid: `interact_weak`. If empty: idle.

This loop is the core gameplay mechanic. It was originally in `Unit.cs` (RTChess) and is being rebuilt as clean components in the `LittleCafe` namespace:
- `GridEntityHealth.cs` — HP tracking, IDamageable, damage flash, death events
- `GridEntityActor.cs` — The clockwork brain (rotate, scan, interact) *(in progress)*
- `GridEntityManager.cs` — Registry and factory *(in progress)*

---

## Currently In Progress

### Attack Sequence / Interaction Loop (Feb 2026)
Building the clockwork interaction system as new, clean components in `LittleCafe` namespace. This brings the RTChess auto-rotate-and-attack behavior into the world builder context.

**New files being created:**
- `Assets/Scripts/LittleCafe/GridEntityHealth.cs` ✅ Created
- `Assets/Scripts/LittleCafe/GridEntityActor.cs` — Pending
- `Assets/Scripts/LittleCafe/GridEntityManager.cs` — Pending

**Integration points to modify:**
- `CafeSceneSetupV2.cs` — Add GridEntityManager to SetupFurnitureSystems()
- `DragDropHandler.cs` — After placement, call GridEntityManager.AttachComponents()

**Do NOT modify:** `Unit.cs`, `FurnitureObject.cs`, `IntervalTimer.cs`, `Facing.cs` — these stay as-is.

---

## File Structure Quick Reference

```
Assets/
├── Scripts/
│   ├── Core/           ← Shared (GridManager, IntervalTimer, etc.)
│   ├── Components/     ← Shared components (FurnitureObject, ChairObject, etc.)
│   ├── Data/           ← ScriptableObject databases (all five DBs + UnitStats)
│   ├── LittleCafe/     ← Cafe/world builder (GridCamera, connectivity, GridEntity*, etc.)
│   ├── Systems/        ← Game-wide (FogManager, RaritySystem)
│   ├── UI/             ← UI (DockBarManager, DragDropHandler, UnitIcon)
│   ├── Units/          ← RTChess legacy (Unit.cs, Facing.cs, IDamageable.cs)
│   └── Editor/         ← Editor-only (PEPOPrefabGenerator)
├── Animations/         ← ObjectAnimController + all .anim clips
├── Prefabs/PEPO/       ← Generated prefab variants
└── Docs/ClockworkCraft/ ← GDD and this handoff document
```

---

## Common Gotchas

1. **Two animator controllers exist.** Use `Assets/Animations/ObjectAnimController.controller` (PEPO furniture). NOT `Assets/Prefabs/ObjectAnimatorController.controller` (old RTChess, different trigger names).

2. **Variant prefab type inheritance.** PEPO variant prefabs may inherit wrong `furnitureType` from base. The runtime fix: `UnitStats.furnitureTypeOverride` is set from database, then applied via `FurnitureObject.SetType()`.

3. **Chair tuck vs selection.** Chairs visually slide under tables via Recenter transform, but grid occupancy stays at original position. Removal uses grid-based detection, so chairs are always selectable at their original square.

4. **`GameUnitType` not `UnitType`.** The unit database enum is `GameUnitType` to avoid collision with `ClockworkGrid.UnitType`.

5. **IDamageable is in ClockworkGrid namespace.** Reuse it — don't create a new damage interface.

---

## How to Contribute Safely

1. **Read CLAUDE.md** — It has the full architecture details
2. **Search before creating** — `grep -r "ClassName" Assets/Scripts/` before making a new class
3. **Follow existing patterns** — Look at how similar components are structured
4. **Test via console log** — Check `Logs/Unity_Console_Latest.log` after changes
5. **Update CLAUDE.md** — After adding or changing systems, update the documentation
6. **Commit with context** — Include what system was changed and why

---

## Contact / Coordination

This project is developed by **Jai** with AI assistance (Claude Cowork / Claude Code). Multiple AI instances may be working on different parts simultaneously via Git. Always pull latest before starting work, and always check CLAUDE.md for the current state of the project.
