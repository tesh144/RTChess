# RTChess - Real-Time Grid Strategy Game

## Project Overview
A Unity-based real-time chess-like strategy game with an interval-based clockwork system. Units automatically rotate and attack resources to gather tokens in a turn-based manner synchronized to a global interval timer.

## Current Status
**Latest Iteration:** Token Economy + Debug Tools (Iteration 3)
**Last Updated:** 2026-02-07

## Core Systems

### 1. Grid System (`Assets/Scripts/Core/GridManager.cs`)
- 4x4 grid (configurable)
- Cell-based state management (Empty, PlayerUnit, EnemyUnit, Resource)
- World/grid coordinate conversion
- Singleton pattern

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
- Instructions bar (bottom-center)

### 6. Visual Systems
- Procedural 3D models via `UnitModelBuilder` and `ResourceNodeModelBuilder`
- Particle systems for attacks and destruction
- Grid visualization with cell highlights
- Billboard floating text

## Controls
- **Left-click**: Place Soldier unit (debug mode)
- **Right-click**: Place Resource node (debug mode)

## Game Loop
1. Place resource nodes on grid
2. Place soldier units nearby
3. Soldiers auto-rotate every N intervals
4. Soldiers attack resources in facing direction
5. Earn tokens from harvesting
6. *(Future)* Spend tokens to place units

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
│   │   └── TokenUI.cs            # Token counter
│   └── Debug/
│       └── CellDebugPlacer.cs    # Click-to-place
```

## Known Patterns

### Singleton Pattern
Used for managers (prefix with `Instance`):
- `GridManager.Instance`
- `IntervalTimer.Instance`
- `ResourceTokenManager.Instance`

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
- [ ] Token spending for unit placement
- [ ] Enemy unit spawning/AI
- [ ] Unit vs Unit combat
- [ ] Movement system
- [ ] Different unit types (archer, tank, etc.)
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
