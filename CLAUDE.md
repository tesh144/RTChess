# RTChess - Real-Time Grid Strategy Game

## Project Overview
A Unity-based real-time chess-like strategy game with an interval-based clockwork system. Units automatically rotate and attack resources to gather tokens in a turn-based manner synchronized to a global interval timer.

## Current Status
**Latest Iteration:** Multi-Cell Placement & Asset Integration (Post Iteration 6)
**Last Updated:** 2026-02-16

**Key Achievements:**
- Card-game-style dock bar with drag-and-drop unit placement
- Multi-cell object support (1x1, 2x2, 3x3, etc.)
- GridObject component for defining object grid sizes
- Unity Animator-based placement animations (Unit_Appear)
- PEPO game assets integrated with proper scaling
- Fog of war system with reveal mechanics
- Wave-based enemy spawning
- Debug menu for testing

## Core Systems

### 1. Grid System (`Assets/Scripts/Core/GridManager.cs`)
- 11x11 grid (configurable via GameSetup or Inspector)
- Cell-based state management (Empty, PlayerUnit, EnemyUnit, Resource)
- World/grid coordinate conversion
- Singleton pattern
- **Tile prefabs assigned directly on GridManager Inspector** (gridTilePrefabA, gridTilePrefabB)
- Checkerboard pattern: alternates A/B prefabs using `(x + y) % 2 == 0`
- Falls back to default white/gray cubes if no prefabs assigned
- GameSetup finds existing GridManager in scene (does NOT create tile prefabs)

### 2. Interval Timer (`Assets/Scripts/Core/IntervalTimer.cs`)
- Global clock that drives all game actions
- Default: 2 second intervals
- Event-based system (`OnIntervalTick`)
- All gameplay mechanics sync to this timer

### 3. Resource System
**ResourceNode** (`Assets/Scripts/Core/ResourceNode.cs`)
- Harvestable nodes placed on grid
- HP: 10, Tokens per hit: 1, Bonus on destroy: 3
- Visual HP bar (green → red)
- Particle effects on destruction

**ResourceTokenManager** (`Assets/Scripts/Core/ResourceTokenManager.cs`)
- Singleton economy manager
- Tracks player tokens
- Floating "+X" text animations
- Event system for UI updates

### 4. Unit System (`Assets/Scripts/Units/Unit.cs`)
**Base Stats:**
- HP: 10
- Attack Damage: 3
- Attack Range: 1
- Attack Interval: 2 (attacks every 2 intervals)
- Resource Cost: 3 tokens

**Behavior:**
- Automatic rotation (clockwise for Player, counter-clockwise for Enemy)
- Attacks resources in facing direction
- Smooth rotation animations (0.25s)
- Attack VFX particles
- Only Player units earn tokens

**Facing System** (`Assets/Scripts/Units/Facing.cs`)
- North, South, East, West directions
- Rotation and grid offset calculations

### 5. UI System
- **IntervalUI**: Shows current interval and progress bar (top-left)
- **TokenUI**: Displays token count (top-right, gold color)
- **DockBarManager**: Bottom-center dock bar with draggable unit icons
- **DragDropHandler**: Manages drag state, ghost preview, and placement validation
- **DebugMenu**: Top-right debug panel for testing (toggle, token adjustment, placement controls)

### 6. Grid Object System (`Assets/Scripts/Components/GridObject.cs`)
- Component for marking objects with intended grid size (1x1, 2x2, etc.)
- Visual gizmos in editor: green = properly scaled, yellow = needs adjustment
- Multi-cell placement support via `GridManager.PlaceMultiCell()`
- Grid-centered positioning at (0,0,0) with cell size of 1.5 units
- Supports custom PEPO game assets and procedurally generated units

### 7. Animation System
**Placement Animations:**
- Uses Unity Animator with `PlaceableObject` controller
- Default animation: `Unit_Appear.anim` (hand-crafted with wobble effect)
- Plays automatically when objects are instantiated
- Alternative: `PlacementAnimation.cs` (code-based, currently unused but available for future)

**Combat Animations:**
- Unit_Attack.anim for attack actions
- Triggered via Animator parameter "attack"

### 8. Visual Systems
- Procedural 3D models via `UnitModelBuilder` and `ResourceNodeModelBuilder`
- PEPO game assets (3D models from asset store)
- Particle systems for attacks and destruction
- Grid visualization with cell highlights
- Billboard floating text
- Fog of war with fade-out effects

## Controls

### Normal Play
- **Draw Button**: Click to spend tokens and draw a random unit into dock (cost: 3, 4, 5, 6...)
- **Drag from Dock**: Drag unit icons from dock bar to grid (placement is FREE)
- **Hover Icons**: Icons magnify 1.3x on hover (macOS dock style)

### Debug Mode (Toggle via Debug Menu - Top Right)
- **Toggle Button**: Enable/disable debug placement controls
- **Token Adjustment**: +/-1, +/-10, +100 buttons to modify token count
- **Right-click**: Place Resource node (free, debug only)
- **Middle-click**: Place Enemy unit (free, debug only)

## Game Loop
1. Start with 10 tokens
2. Click "Draw" to spend tokens (3, 4, 5...) and draw units into dock
3. Drag units from dock to grid (placement is FREE)
4. Placed units have 2-interval cooldown (greyed out)
5. After cooldown, units auto-rotate and attack
6. Earn tokens from harvesting resources
7. Use debug menu to place resources and enemies for testing

## File Structure
```
Assets/
├── Scripts/
│   ├── Core/
│   │   ├── GameSetup.cs          # Scene bootstrap
│   │   ├── GridManager.cs        # Grid system (multi-cell support)
│   │   ├── GridVisualizer.cs     # Grid rendering
│   │   ├── IntervalTimer.cs      # Global clock
│   │   ├── ResourceNode.cs       # Harvestable nodes
│   │   ├── ResourceTokenManager.cs # Economy
│   │   ├── WaveManager.cs        # Enemy wave spawning
│   │   └── SFXManager.cs         # Sound effects
│   ├── Units/
│   │   ├── Unit.cs               # Base unit class with combat
│   │   ├── SoldierUnit.cs        # Soldier implementation
│   │   ├── Facing.cs             # Direction system
│   │   ├── UnitModelBuilder.cs   # 3D model generation
│   │   └── ResourceNodeModelBuilder.cs
│   ├── UI/
│   │   ├── IntervalUI.cs         # Interval display
│   │   ├── TokenUI.cs            # Token counter
│   │   ├── DockBarManager.cs     # Dock bar controller
│   │   ├── UnitIcon.cs           # Draggable unit icon
│   │   ├── DragDropHandler.cs    # Drag state and placement validation
│   │   └── DebugMenu.cs          # Debug panel with placement controls
│   ├── Components/
│   │   ├── GridObject.cs         # Grid size component (1x1, 2x2, etc.)
│   │   └── PlacementAnimation.cs # Code-based animation (unused, for future)
│   ├── Systems/
│   │   ├── FogManager.cs         # Fog of war system
│   │   └── RaritySystem.cs       # Unit rarity tiers
│   ├── Data/
│   │   ├── UnitStats.cs          # ScriptableObject for unit data
│   │   └── ResourceNodeStats.cs  # ScriptableObject for resource data
│   ├── LittleCafe/               # Cafe scene (separate game mode)
│   │   ├── CafeEquipment.cs
│   │   └── [other cafe scripts]
│   └── Debug/
│       └── CellDebugPlacer.cs    # Click-to-place debugging
├── Prefabs/
│   ├── PlaceableObject.controller # Animator for placement
│   ├── Unit_Appear.anim           # Spawn animation
│   ├── Unit_Attack.anim           # Attack animation
│   └── [PEPO asset prefabs]
```

## Known Patterns

### Singleton Pattern
Used for managers (prefix with `Instance`):
- `GridManager.Instance`
- `IntervalTimer.Instance`
- `ResourceTokenManager.Instance`
- `DragDropHandler.Instance`
- `DockBarManager.Instance`

### Event System
- `IntervalTimer.OnIntervalTick` - Subscribe for interval-based actions
- `ResourceTokenManager.OnTokensChanged` - Subscribe for UI updates

### Reflection Usage
`GameSetup.cs` uses reflection to set private serialized fields at runtime via `SetPrivateField()` helper.

## Development Notes

### Testing
- Scene bootstraps from empty GameObject with `GameSetup` component
- Everything creates programmatically (no scene dependencies)
- Easy to test different configurations via GameSetup inspector fields

### Next Features (Ideas)
- [x] Token spending for unit placement (Iteration 4: Draw system)
- [x] Unit vs Unit combat (Iteration 3)
- [x] Dock bar with drag-and-drop (Iteration 4)
- [x] Placement cooldown system (Iteration 4)
- [x] Debug menu for testing (Iteration 4)
- [ ] Enemy unit spawning/AI
- [ ] Movement system
- [ ] Different unit types with rarity system (Iteration 6)
- [ ] Victory/defeat conditions
- [ ] Wave-based enemy spawning
- [ ] Grid size variations

### Architecture Notes
- **Avoid Unity scene dependencies**: Use prefabs and procedural generation
- **Interval-based gameplay**: All gameplay synced to interval timer (no Update() frame-dependent logic)
- **Visual feedback**: Particles, animations, and UI for all actions
- **Event-driven design**: Loose coupling via event systems
- **Singleton pattern**: Used for managers (GridManager, IntervalTimer, etc.)
- **Component-based**: GridObject, Unit, CafeEquipment components define behavior
- **ScriptableObjects**: UnitStats and ResourceNodeStats for data-driven design
- **Multi-cell support**: Objects can occupy 1x1, 2x2, or larger grid spaces
- **Animation system**: Unity Animator for placement/combat (extensible via PlacementAnimation.cs for custom code-based animations)

### Design Principles for Future Development
1. **Grid-centric design**: All objects positioned relative to 1.5-unit grid cells
2. **Animator-first animations**: Use Unity Animator for visual effects (PlacementAnimation.cs available for special cases)
3. **Component composition**: Add GridObject to any prefab to make it placeable
4. **Data-driven balance**: Stats in ScriptableObjects for easy tweaking
5. **Event subscription**: Subscribe to IntervalTimer.OnIntervalTick for time-based actions
6. **Multi-scene support**: LittleCafe scene demonstrates separate game modes using same core systems

## Team Workflow
- **Main development**: This account (jai)
- **Feature branches**: Other team members
- **Merge strategy**: Pull requests → review → merge to master
- **Communication**: Coordinate before editing same files

## Credits
Built with Claude Code (Anthropic)
