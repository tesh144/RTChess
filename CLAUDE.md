# RTChess - Real-Time Grid Strategy Game

## Project Overview
A Unity-based real-time chess-like strategy game with an interval-based clockwork system. Units automatically rotate and attack resources to gather tokens in a turn-based manner synchronized to a global interval timer.

## Current Status
**Latest Iteration:** Dock Bar & Draw System (Iteration 4)
**Last Updated:** 2026-02-07

**Key Achievements:**
- Card-game-style dock bar with drag-and-drop unit placement
- Linear cost escalation for drawing units (3, 4, 5, 6...)
- Placement is FREE (tokens spent on drawing)
- 2-interval cooldown for placed units
- Debug menu for testing with resource and enemy placement

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

### 6. Visual Systems
- Procedural 3D models via `UnitModelBuilder` and `ResourceNodeModelBuilder`
- Particle systems for attacks and destruction
- Grid visualization with cell highlights
- Billboard floating text

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
│   │   ├── GridManager.cs        # Grid system
│   │   ├── GridVisualizer.cs     # Grid rendering
│   │   ├── IntervalTimer.cs      # Global clock
│   │   ├── ResourceNode.cs       # Harvestable nodes
│   │   └── ResourceTokenManager.cs # Economy
│   ├── Units/
│   │   ├── Unit.cs               # Base unit class
│   │   ├── SoldierUnit.cs        # Soldier implementation
│   │   ├── Facing.cs             # Direction system
│   │   ├── UnitModelBuilder.cs   # 3D model generation
│   │   └── ResourceNodeModelBuilder.cs
│   ├── UI/
│   │   ├── IntervalUI.cs         # Interval display
│   │   ├── TokenUI.cs            # Token counter
│   │   ├── DockBarManager.cs     # Dock bar controller
│   │   ├── UnitIcon.cs           # Draggable unit icon
│   │   ├── DragDropHandler.cs    # Drag state and ghost preview
│   │   └── DebugMenu.cs          # Debug panel with placement controls
│   ├── Components/
│   │   └── PlacementCooldown.cs  # 2-interval cooldown component
│   └── Debug/
│       └── CellDebugPlacer.cs    # Click-to-place
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
- Avoid Unity scene dependencies (use prefabs/procedural generation)
- All gameplay synced to interval timer (no Update() frame-dependent logic)
- Visual feedback for all actions (particles, animations, UI)
- Event-driven design (loose coupling)

## Team Workflow
- **Main development**: This account (jai)
- **Feature branches**: Other team members
- **Merge strategy**: Pull requests → review → merge to master
- **Communication**: Coordinate before editing same files

## Credits
Built with Claude Code (Anthropic)
