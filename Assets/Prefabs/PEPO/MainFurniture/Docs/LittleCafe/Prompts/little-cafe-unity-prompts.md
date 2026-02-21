# Little Cafe - Unity Implementation Prompts for Claude Code

---

## 🎯 IMPORTANT: Project Intent

**You are building a SECOND GAME in the same Unity project.**

This is **NOT** a modification or replacement of RTChess. This is an **ADAPTATION** of the existing RTChess/Clockwork Grid systems to create a completely different game called "Little Cafe."

### What This Means:
- **RTChess remains untouched** - All RTChess gameplay, scenes, and scripts stay intact and functional
- **Shared systems are REUSED** - We're leveraging the grid system, pathfinding, and camera that already work
- **New game, different gameplay** - Little Cafe is a restaurant management game, not a chess game
- **Coexist peacefully** - Both games live in the same project, sharing common systems

### Think of it like this:
RTChess proved that your grid + pathfinding + camera systems work perfectly. Now we're saying "let's use those same proven systems to build a restaurant game" instead of building from scratch.

**Architecture:**
```
Unity Project Root
├── RTChess/              ← KEEP EVERYTHING (chess game)
├── Shared/               ← Grid, pathfinding, camera (already exists, used by both)
└── LittleCafe/           ← NEW (restaurant game, uses Shared systems)
```

---

## Project Context

Building "Little Cafe" - a tap-to-manage restaurant game inspired by Plate Up and Overcooked. The project will extend the existing **RTChess/Clockwork Grid** Unity project, reusing its 15x15 grid system, A* pathfinding, and isometric camera.

**Critical:** This is an EXTENSION of RTChess, not a replacement. All existing RTChess/Clockwork Grid code remains intact and functional. We're building a second game that shares the same foundation systems.

---

## Phase 1: Kitchen Builder (PRIORITY)

### Goal
Create a drag-and-drop kitchen layout builder that lets players place equipment tiles on the grid, exactly like RTChess character placement but for cafe equipment instead.

### User Story
As a player, I want to drag kitchen equipment (cooking stations, counters, sinks, plate racks) from a palette onto the 15x15 grid to design my cafe layout before starting service.

### Technical Requirements

**Reuse from RTChess:**
- 15x15 grid system (already implemented)
- Grid tile selection/highlighting
- Drag-and-drop mechanics (adapt character placement to equipment placement)
- Isometric camera system
- Grid coordinate system

**New Components Needed:**
1. **Equipment Tile System**
   - Base `Equipment` class (similar to chess pieces but for cafe equipment)
   - Equipment types: CookingStation, ServingCounter, WashingStation, PlateRack
   - Each equipment occupies exactly 1x1 grid tile
   - Visual representation: Simple colored cubes for now (detailed models later)

2. **Equipment Palette UI**
   - Left sidebar showing available equipment types
   - Drag equipment from palette → grid
   - Unlimited equipment (no cost/limits in this phase)
   - Equipment categories:
     - 🍳 Cooking Station (red cube)
     - 🍽️ Serving Counter (green cube)
     - 🚰 Washing Station (blue cube)
     - 🍽️ Plate Rack (pink cube)
     - ⬛ Wall (black cube)
     - 🚪 Door (yellow cube)

3. **Grid Placement Rules**
   - Equipment can only be placed on empty tiles
   - Click tile to select, click again to remove equipment
   - Visual feedback: highlight valid/invalid placement tiles
   - Save/load layout to JSON

4. **Layout Zones**
   - Define two zones: Kitchen Zone (rows 0-4), Dining Zone (rows 6-11)
   - Visual differentiation: different floor colors (peach for kitchen, light green for dining)
   - Row 5 and row 12: Wall zones

### Acceptance Criteria
- [ ] Can drag cooking station from palette onto any empty grid tile
- [ ] Can drag serving counter, washing station, plate rack from palette
- [ ] Can place walls and doors
- [ ] Grid shows visual zones (kitchen = peach, dining = light green)
- [ ] Can click equipment on grid to remove it
- [ ] Equipment appears as colored cubes (correct colors per type)
- [ ] Can save layout to JSON file
- [ ] Can load layout from JSON file
- [ ] "Clear Layout" button removes all equipment

### Implementation Notes
```csharp
// Adapt RTChess piece placement to equipment placement
// Instead of ChessPiece class, create Equipment class

public abstract class Equipment : MonoBehaviour {
    public EquipmentType type;
    public GridPosition gridPosition;

    public virtual void OnPlaced() { }
    public virtual void OnRemoved() { }
}

public enum EquipmentType {
    CookingStation,
    ServingCounter,
    WashingStation,
    PlateRack,
    Wall,
    Door
}

// Reuse GridManager from RTChess
// Adapt drag-drop from piece placement to equipment placement
```

### Reference Layout
Use this exact layout for testing/validation:
```
ROW 0-4: Kitchen Zone (peach background)
- Chef Queue: Column 0, rows 0-4 (orange markers, not equipment)
- Cooking Stations: Column 5, rows 2-4 (red cubes)
- Serving Counter: Row 2, columns 7-10 (green cubes)
- Washing Station: Row 2, column 11 (blue cube)
- Plate Rack: Row 1, column 11 (pink cube)

ROW 5: Wall Zone
- Walls: All columns except 7-8 (black cubes)
- Doors: Columns 7-8 (yellow cubes)

ROW 6-11: Dining Zone (light green background)
- Leave empty for now (tables/chairs in Phase 2)

ROW 12: Wall Zone
- Walls: All columns except 7-8 (black cubes)
- Doors: Columns 7-8 (yellow cubes)
```

### JSON Save Format
```json
{
  "layoutName": "My Kitchen",
  "gridSize": 15,
  "equipment": [
    {
      "type": "CookingStation",
      "position": { "row": 2, "col": 5 }
    },
    {
      "type": "ServingCounter",
      "position": { "row": 2, "col": 7 }
    }
  ]
}
```

---

## Phase 2: Character AI System (Chefs, Waiters, Customers)

### Goal
Implement working AI for all three character types with pathfinding, task systems, and basic behaviors.

### Requirements

**Foundation:**
- Reuse RTChess A* pathfinding system
- Base `Character` class with movement and task queue
- Characters spawn from designated queue zones
- Characters navigate around obstacles (equipment, walls, other characters)

**Chef AI:**
- Spawns from chef queue (column 0, rows 0-4)
- Can be assigned cooking tasks via tap
- Pathfinding to: plate rack → cooking station → serving counter
- Idle state: returns to chef queue
- Visual: Simple capsule or cube (color: orange)

**Waiter AI:**
- Spawns from waiter queue (column 14, rows 6-11)
- Can be assigned serving tasks via tap
- Pathfinding to: customer → counter → table → washing station
- Idle state: returns to waiter queue
- Visual: Simple capsule or cube (color: teal)

**Customer AI:**
- Spawns from customer queue (row 13, columns 1-5)
- States: Waiting → Walking → Seated → Eating → Leaving
- Pathfinding to: entrance door → table → exit door
- Display order bubble above head when seated
- Timer-based eating behavior (10 seconds)
- Auto-leaves when done eating
- Visual: Simple capsule or cube (color: purple)

**Task Assignment System:**
- Tap on customer/order/plate → nearest available staff auto-assigned
- Visual feedback: character highlights when selected
- Task queue: characters can have multiple tasks queued
- Task states: Idle → Walking → Working → Complete

**Queue Zones:**
- Visual markers on floor showing spawn zones
- Chef Queue: Column 0, rows 0-4 (orange floor tint)
- Waiter Queue: Column 14, rows 6-11 (teal floor tint)
- Customer Queue: Row 13, columns 1-5 (purple floor tint)
- Characters auto-return to queue when idle

### Acceptance Criteria
- [ ] Chefs spawn from chef queue and can navigate to plate rack, cooking station, counter
- [ ] Waiters spawn from waiter queue and can navigate to customers, counter, tables, sink
- [ ] Customers spawn from customer queue and walk through doors
- [ ] All characters use A* pathfinding and avoid obstacles
- [ ] Can tap to assign tasks to nearest available staff
- [ ] Characters show current state/task in debug UI
- [ ] Characters return to queue zones when idle
- [ ] Customer eating timer works (10 seconds, then auto-leave)
- [ ] Basic collision avoidance (characters don't overlap)

### Implementation Notes
```csharp
// Base character class
public abstract class Character : MonoBehaviour {
    public CharacterType type;
    public CharacterState state;
    public Queue<Task> taskQueue;

    public void AssignTask(Task task) { }
    public void MoveToPosition(GridPosition target) { } // Uses A* from RTChess
    public virtual void UpdateState() { }
}

public enum CharacterType {
    Chef,
    Waiter,
    Customer
}

public enum CharacterState {
    Idle,
    Walking,
    Working,
    Waiting
}

// Task system
public class Task {
    public TaskType type;
    public GridPosition targetPosition;
    public object targetObject; // Equipment, customer, plate, etc.
}

public enum TaskType {
    // Chef tasks
    GetPlate,
    CookDish,
    DeliverToCounter,

    // Waiter tasks
    SeatCustomer,
    TakeOrder,
    PickupDish,
    ServeDish,
    CollectDirtyPlate,
    WashPlate,

    // Customer tasks
    WalkToTable,
    WaitForFood,
    Eat,
    Leave
}
```

---

## Phase 3: Tables & Chairs Placement

### Goal
Add table and chair placement with directional facing.

### Requirements
- Table tile (purple cube)
- Chair tiles with facing direction (up/down/left/right arrows on green cubes)
- Tables can only be placed in Dining Zone (rows 6-11)
- Chairs auto-snap to adjacent table tiles (or manual placement)
- Click chair to rotate facing direction

### Reference Layout
```
ROW 6:
- Table at (6,0), Chair facing left at (6,1)

ROWS 7-9: Three table clusters per row
- Cluster 1: Chair-right (col 2), Table (col 3), Chair-left (col 4)
- Cluster 2: Chair-right (col 6), Table (col 7), Chair-left (col 8)
- Cluster 3: Chair-right (col 10), Table (col 11), Chair-left (col 12)

ROW 11:
- Table at (11,0), Chair facing left at (11,1)
```

### Acceptance Criteria
- [ ] Can place tables in dining zone only
- [ ] Can place chairs adjacent to tables
- [ ] Chairs show directional arrow indicating facing
- [ ] Can rotate chair facing by clicking
- [ ] Tables/chairs saved/loaded with layout

---

## Phase 4: Staff Character System

### Goal
Add chef and waiter characters that can be spawned and move on the grid using A* pathfinding.

### Requirements
- Reuse RTChess A* pathfinding system
- Create Chef and Waiter character classes
- Characters spawn from queue zones
- Characters can walk to any grid tile using pathfinding
- Collision: characters can't walk through equipment or walls
- Click character to show debug path

### Acceptance Criteria
- [ ] Chef spawns from chef queue (column 0)
- [ ] Waiter spawns from waiter queue (column 14)
- [ ] Characters use A* to navigate around obstacles
- [ ] Characters represented as simple 3D models/capsules
- [ ] Can click character to select and show current task

---

## Phase 5: Plate System & State Management

### Goal
Implement the 6-plate resource pool and plate states (clean, dirty, cooking).

### Requirements
- PlateManager: Tracks all 6 plates and their states
- Plate states: AtRack (clean), InUse (cooking/serving), Dirty (on table/sink)
- Plates physically move between locations (rack → chef → counter → table → sink → rack)
- Visual: Simple disc objects with color indicating state
  - Clean plates: White
  - Cooking plates: Yellow
  - Served plates: Green
  - Dirty plates: Brown
- Pressure mechanic: All 6 plates in use = can't start new orders

### Acceptance Criteria
- [ ] Exactly 6 plates exist in game
- [ ] Plates spawn at plate rack on game start
- [ ] Can see all plate states in debug UI
- [ ] Plates change color based on state
- [ ] PlateManager prevents >6 plates from being used

---

## Phase 6: Customer System

### Goal
Add customers that enter, sit at tables, order food, eat, and leave.

### Requirements
- Customer spawns outside restaurant (row 13)
- Batch entry: Multiple customers enter at once when player taps
- Customer walks to open table when seated by waiter
- Customer displays order icon above head (simple food icon)
- Customer eats when food is served (timer)
- Customer auto-pays and leaves when done

### Acceptance Criteria
- [ ] Customers spawn in customer queue
- [ ] Tap to batch-enter customers
- [ ] Customers wait to be seated
- [ ] Customers display order after being seated
- [ ] Customers have eating timer (10 seconds)
- [ ] Customers leave table when done, dirty plate remains

---

## Phase 7: Core Gameplay Loop (13-Step Service Pipeline)

### Goal
Implement the complete tap-based service pipeline from customer arrival to plate washing.

### Requirements
Implement all 13 steps from the service pipeline:
1. **Batch Entry** - Tap outside queue → all customers enter
2. **Auto-Walk In** - First customer walks through door
3. **Seat Customer** - Tap customer → waiter seats at table
4. **Take Order** - Tap seated customer → waiter takes order
5. **Get Plate** - Tap order → chef gets clean plate from rack
6. **Cook** - Chef auto-walks to cooking station, cooks dish
7. **Deliver to Counter** - Chef carries dish to serving counter
8. **Pick Up Dish** - Tap dish on counter → waiter picks up
9. **Serve** - Waiter auto-walks to customer, serves dish
10. **Eat & Pay** - Customer eats (timer), auto-pays, leaves
11. **Collect Dirty Plate** - Tap dirty plate → waiter collects (can stack multiple)
12. **Walk to Sink** - Waiter auto-walks to washing station
13. **Wash** - Tap washing station → wash ONE plate (repeat for stack)

### Tap Interaction Model
- Nearest available staff auto-assigned to task
- Visual feedback for valid/invalid taps
- Task queue system for staff

### Acceptance Criteria
- [ ] All 13 steps functional and in correct order
- [ ] Tap interactions work as specified
- [ ] Staff auto-pathfind to correct locations
- [ ] Plates move through full lifecycle
- [ ] Can stack multiple dirty plates before washing
- [ ] One-by-one washing (tap per plate)

---

## Phase 8: Game Loop & Scoring

### Goal
Add "Lunch Rush" 3-minute timed rounds with scoring.

### Requirements
- 3-minute countdown timer
- Wave system: Customers arrive in batches throughout round
- Score based on: customers served, speed, tips
- End-of-round summary: Total customers served, total revenue, star rating
- Restart button for new round with same layout

### Acceptance Criteria
- [ ] Round timer counts down from 3:00
- [ ] Customers spawn in waves (every 30 seconds)
- [ ] Score tracks customers served and money earned
- [ ] Round ends at 0:00 with summary screen
- [ ] Can restart round with same cafe layout

---

## Phase 9: Staff Progression & Automation

### Goal
Implement staff leveling system with automation unlocks.

### Requirements
- Staff start at Level 1 (manual control required)
- Staff gain XP from completing tasks
- Level 2+: Staff auto-perform assigned tasks without player taps
- Player can still manually assign tasks at any level
- Visual: Level indicator above staff character

### Staff Automation by Level
- **Level 1:** All tasks require player tap
- **Level 2:** Auto-seat customers (waiter)
- **Level 3:** Auto-take orders (waiter)
- **Level 4:** Auto-start cooking when order placed (chef)
- **Level 5:** Auto-pick up completed dishes (waiter)
- **Level 6:** Auto-collect dirty plates (waiter)

### Acceptance Criteria
- [ ] Staff start at Level 1
- [ ] Staff gain XP from tasks
- [ ] Staff level up at XP thresholds
- [ ] Automation activates at correct levels
- [ ] Can still manually control high-level staff

---

## Phase 10: Economy & Upgrades

### Goal
Add money system and purchasable upgrades.

### Requirements
- Earn money from serving customers
- Spend money on:
  - Additional staff (hire more chefs/waiters)
  - Equipment upgrades (faster cooking, more counter space)
  - Capacity upgrades (more tables)
- Persistent progression between rounds

### Acceptance Criteria
- [ ] Money earned from completed orders
- [ ] Shop menu to purchase upgrades
- [ ] Can hire additional staff (max 3 chefs, 3 waiters)
- [ ] Equipment upgrades apply stat bonuses
- [ ] Money persists between rounds

---

## Phase 11: Polish & Juice

### Goal
Add visual polish, animations, and game feel improvements.

### Requirements
- Staff walk animations
- Plate carry animations
- Customer emotes (happy/angry)
- Particle effects (cooking steam, sparkles on clean plates)
- Sound effects for each action
- Camera shake on round start/end
- UI polish and transitions

---

## Phase 12: Advanced Features (Future)

Ideas for post-MVP features:
- Multiple restaurants (unlock new layouts)
- Recipe system (different dishes)
- Special customer types (VIPs, critics)
- Kitchen disasters (spills, burnt food)
- Multiplayer co-op
- Daily challenges
- Leaderboards

---

## Development Guidelines for Claude Code

### Project Structure
```
Assets/
├── RTChess/                    (KEEP - existing code)
│   ├── Scripts/
│   │   ├── Grid/
│   │   ├── Pathfinding/
│   │   └── Camera/
│   └── Prefabs/
├── LittleCafe/                 (NEW - cafe-specific code)
│   ├── Scripts/
│   │   ├── Equipment/
│   │   ├── Staff/
│   │   ├── Customer/
│   │   ├── Plate/
│   │   ├── Builder/           (Phase 1 focus)
│   │   └── Managers/
│   ├── Prefabs/
│   │   ├── Equipment/
│   │   ├── Characters/
│   │   └── UI/
│   └── Scenes/
│       └── KitchenBuilder.unity
```

### Code Style
- Follow RTChess naming conventions
- Use Unity best practices (MonoBehaviour, ScriptableObjects, Prefabs)
- Modular design: separate managers for Equipment, Staff, Customers, Plates
- Event-driven architecture for game state changes

### Testing Strategy
- Each phase should be independently testable
- Create test scene for each major system
- Use the reference layout for validation
- Debug UI showing internal state (plate count, staff tasks, etc.)

### Performance Considerations
- Grid system already optimized in RTChess
- Limit particle effects for mobile
- Object pooling for customers/plates
- Optimize pathfinding (reuse RTChess optimizations)

---

## Next Steps for Implementation

1. **Start with Phase 1** - get the kitchen builder working first
2. Validate Phase 1 against reference layout before moving on
3. Each subsequent phase builds on the previous
4. Test thoroughly after each phase
5. Phase 7 (core gameplay loop) is the "vertical slice" - playable end-to-end

---

## Questions to Resolve During Development

- Should equipment have orientation (e.g., counter facing direction)?
- Do we need undo/redo for layout builder?
- Should layout builder have pre-made templates?
- Maximum number of each equipment type?
- Should walls block pathfinding completely or just visual?

---

## Resources & References

- **Game References:** Plate Up, Overcooked, Gold & Goblins, Idle Bank
- **Visual Style:** Simple 3D with flat colors (expand to detailed models later)
- **Target Platform:** Mobile (iOS/Android) - design for touch controls
- **Existing Codebase:** RTChess/Clockwork Grid (15x15 grid, A*, isometric camera)

---

## Success Metrics

**Phase 1 Complete When:**
- Can build the reference kitchen layout exactly
- Can save and load layouts
- All equipment types placeable with correct colors
- Grid zones visible and functional

**MVP Complete When:**
- Can play a full 3-minute round
- All 13 service pipeline steps work
- Customers are served and leave satisfied
- Score is calculated correctly
- Can restart with same layout

**Full Game Complete When:**
- All 12 phases implemented
- Game feels responsive and "juicy"
- Economy system allows meaningful progression
- Multiple rounds playable with increasing difficulty

---

## Contact & Clarifications

If any design details are ambiguous during implementation:
- Refer to the handoff document (little-cafe-handoff.md)
- Refer to the visual diagram (little-cafe-design-diagram-correct.html)
- Default to Plate Up mechanics when in doubt
- Prioritize player control and feedback over automation
