# Clockwork Craft — Game Design Document

**Version:** 1.1
**Working Title:** Clockwork Craft
**Last Updated:** February 2026
**Platform:** PC (desktop first), Mobile planned
**Engine:** Unity — existing RTChess project

---

## Table of Contents

1. [Overview](#1-overview)
2. [Core Mechanic — The Clockwork Interval System](#2-core-mechanic--the-clockwork-interval-system)
3. [Resource System](#3-resource-system)
4. [Worker System](#4-worker-system)
5. [Building System](#5-building-system)
6. [Map & World Generation](#6-map--world-generation)
7. [Game Loop](#7-game-loop)
8. [Future Scope](#8-future-scope)
9. [Technical Architecture](#9-technical-architecture)
10. [Design Principles](#10-design-principles)
11. [Development Phases](#11-development-phases)

> **Changelog:** v1.1 — Added Tile Takeover mechanic (Section 2), full environmental generation spec (Section 6), tile type interactability table (Section 3), and MapGenerationSettings ScriptableObject spec (Section 9).

---

## 1. Overview

Clockwork Craft is a real-time strategy game built on top of the Clockwork Grid (RTChess) engine. It reuses the proven interval-based unit mechanic from the original game jam project and expands it into a full village-builder and resource-management game in the spirit of Warcraft — designed from the ground up around the clockwork rotation system.

Each unit placed on the grid automatically rotates clockwise at every global interval tick and interacts with whatever object it faces — harvesting resources, constructing buildings, or attacking enemies. Players gather four resources (Gold, Wood, Food, Stone), construct buildings that unlock upgrades, and progressively grow their village across a procedurally generated map.

| Field | Value |
|-------|-------|
| Genre | Real-Time Strategy / Village Builder |
| Platform | PC (desktop first), Mobile planned |
| Engine | Unity — existing RTChess project |
| Perspective | Top-down / Isometric |
| Session Type | Procedurally generated per run (roguelike-adjacent) |
| Controls | Mouse + Keyboard (PC); Touch (Mobile) |
| Current Focus | Resource gathering and building system — combat is future scope |
| Status | Pre-production — design phase |

---

## 2. Core Mechanic — The Clockwork Interval System

> **DESIGN PRINCIPLE:** Every unit is a clockwork gear. It doesn't think for itself — it turns, faces something, and acts on it. The player's job is to arrange those gears so the right things face each other at the right time.

This mechanic is inherited directly from the RTChess game jam project and is the soul of Clockwork Craft. It must never be removed or fundamentally changed.

### How It Works

- A global Interval Timer fires at a configurable rate (default: 2 seconds per tick).
- At each tick, every unit on the grid rotates one step in its assigned direction (workers rotate clockwise by default).
- After rotating, each unit checks the cell directly in front of it (its facing direction).
- If that cell contains an interactable object (resource node, building under construction, enemy), the unit performs its action.
- If the cell is empty or contains a non-interactable object, the unit does nothing and waits for the next tick.

### Interaction Rules

| Unit Facing | Result |
|-------------|--------|
| Resource Node | Harvests 1 yield unit of that resource (scaled by node tier) |
| Construction Site | Contributes 1 construction progress tick |
| Another Worker | No interaction (idle tick) |
| Building | No interaction unless the building has a worker slot |
| Enemy Unit | Deals attack damage — **future scope, see Section 8** |

### Tile Takeover — The Chain-Clear Mechanic

> **THIS IS CORE FEEL.** The tile takeover is what makes Clockwork Craft satisfying to play. It must be implemented correctly and must never be changed without a full design review.

When a worker reduces a resource node to 0 HP, instead of simply destroying the node and staying in place, **the worker physically advances onto the cleared tile** — exactly like a chess piece capturing.

**Step by step:**
1. Worker faces a tree (for example) and deals damage each tick.
2. Tree HP reaches 0. The tree is removed. A loot drop is awarded.
3. The worker moves forward one cell, now occupying the tile the tree was on.
4. The worker's facing direction does not change — it still faces the same direction.
5. On the very next tick, the worker faces the cell ahead of its new position.
6. If another tree (or any interactable node) is there, it immediately starts attacking it.
7. This continues tick after tick — the worker **chains through an entire row of trees** with no player input.

**Why this matters for map design:** Trees must be generated in strings and clusters, not isolated singles. A lone tree gives one loot drop. A string of five trees in a line, with a worker placed at one end, gives five loot drops in sequence and feels spectacular. This is the satisfying visual the maps must be built to enable.

**Non-interactable tiles do NOT trigger takeover.** If a worker faces water or a locked rock node, it does nothing and rotates on the next tick. It does not advance.

### Player Actions (Between Ticks)

The player interacts with the game between interval ticks. All of the following actions are available at any time and take effect on the next tick:

- Place a unit on any empty grid cell
- Pick up (remove) a unit from the grid
- Rotate a unit manually (override automatic rotation direction)
- Place a building / construction site on an empty cell
- Remove a building (if not occupied)

> **CLOCKWORK RULE:** Units are never moved by the player — except via the tile takeover mechanic when a node is cleared. All other movement is rotation. The player arranges units; the clock does the work.

---

## 3. Resource System

There are four resources in Clockwork Craft. All four are equal currencies — there is no population cap or hard limit. Each resource has distinct uses to ensure all four remain relevant throughout a run.

### Tile Interactability

Not all map tiles can be harvested from the start. Tiles fall into three categories:

| Tile Type | Interactable at Start | Resource | Behaviour | Chain-Clear? |
|-----------|----------------------|----------|-----------|--------------|
| Grass / Empty | — | — | Passable, buildable | — |
| Tree | ✅ Yes | Wood | Worker damages each facing tick. On death: worker advances (chain-clear). | ✅ Yes |
| Gold Mine | ✅ Yes | Gold | Worker harvests each facing tick. On death: worker advances. | ✅ Yes |
| Wild Farm (Berry / Field) | ✅ Yes | Food | Worker harvests each facing tick. On death: worker advances. | ✅ Yes |
| Rock / Quarry | ❌ Locked | Stone | Worker skips (no interaction). Unlockable later via research. | — |
| Water | ❌ Locked | — | Impassable barrier. Worker skips. Unlockable later via research. | — |
| Town Hall | Special | — | Pre-placed. Cannot be harvested or removed. | — |

> **RULE:** If a tile is not interactable, a worker facing it does nothing for that tick and rotates normally on the next. It does not advance. The tile takeover only triggers on a confirmed kill of an interactable node.

### Resource Table

| Resource | Node Type | Primary Uses | Tier Yield |
|----------|-----------|--------------|------------|
| Gold | Gold Mine | Train workers, buy upgrades, trade for other resources | Tier 1: 1/tick · Tier 2: 2/tick · Tier 3: 3/tick |
| Wood | Tree / Forest | Build all basic and intermediate structures | Tier 1: 1/tick · Tier 2: 2/tick · Tier 3: 3/tick |
| Food | Farm / Field | Sustain workers (passive upkeep), unlock advanced buildings | Tier 1: 1/tick · Tier 2: 2/tick · Tier 3: 3/tick |
| Stone | Rock / Quarry | Build advanced and defensive structures, unlock high-tier upgrades | Tier 1: 1/tick · Tier 2: 2/tick · Tier 3: 3/tick |

### Resource Tiers

Resource nodes come in three tiers of richness, assigned procedurally at map generation:

- **Tier 1 (Common):** Yields 1 unit per tick. Abundant on the map.
- **Tier 2 (Uncommon):** Yields 2 units per tick. Moderately distributed, often slightly further from spawn.
- **Tier 3 (Rare):** Yields 3 units per tick. Scarce, high-value nodes — typically near map edges or behind obstacles.

### Resource Node HP

All resource nodes have an HP pool. Workers chip away at this HP as they harvest. When a node is depleted:

- It is removed from the grid.
- A bonus loot drop of that resource type is awarded (like RTChess resource nodes).
- The cell becomes empty and can be built upon.

> **DESIGN NOTE:** Node depletion is intentional scarcity pressure. Players must plan their village around finite local resources and eventually expand outward to new nodes — a natural pacing driver.

---

## 4. Worker System

Workers are the only unit type in the current scope. They are the player's hands — everything that gets done on the grid is done by a worker rotating into position and acting at the tick.

### Worker Base Stats

| Stat | Value |
|------|-------|
| HP | 10 |
| Attack Damage | N/A (current scope — workers do not fight) |
| Rotation | Clockwise, 1 step per tick |
| Harvest Yield | Depends on node tier (see Resource System) |
| Cost to Train | 3 Gold + 2 Food |
| Placement | Drag from dock to any empty grid cell |
| Cooldown | 2 ticks after placement before first action (spin-up delay) |

### Placement Strategy

The key skill of Clockwork Craft is placement geometry. A worker placed adjacent to a resource node will rotate through four facings per full cycle (N → E → S → W → N). It only harvests on the ticks where it faces that node. Players must consider:

- How many ticks per cycle does this worker face the target node?
- Can a worker be positioned so it faces two different nodes in the same cycle?
- Are there obstacles or buildings that waste facing ticks?

### Worker Efficiency

A worker placed directly beside a node (orthogonally adjacent) will face it once every four ticks. Two workers placed on opposite sides of a node will each face it once every four ticks, doubling throughput. Players can stack workers around high-tier nodes to maximise yield.

### Worker Dock

Workers are drawn from a dock bar at the bottom of the screen (inherited from RTChess). The player spends Gold + Food to draw a new worker into the dock, then drags it onto the grid for free. Workers removed from the grid return to a pool and can be redeployed.

---

## 5. Building System

Buildings occupy a single 1x1 grid cell. They serve as upgrades, production enablers, and visual indicators of village progress. All buildings must be constructed by placing a worker adjacent to the construction site — the worker's rotation will tick construction progress instead of harvesting.

> **DESIGN RULE:** No building has an automated effect. Every benefit a building provides is unlocked through worker interaction or the player's placement decisions. Buildings are tools, not passive bonuses.

### Town Hall (Headquarters)

The Town Hall is the player's starting building, pre-placed at the center of the map on run start. It cannot be destroyed or moved. It is the anchor of the village.

- Defines the player's "village radius" — buildings can only be placed within N cells of it.
- Upgrading the Town Hall expands the village radius and unlocks higher-tier buildings.
- Losing the Town Hall (in future combat scope) ends the run.

### Building Catalogue (Phase 1 Scope)

| Building | Cost | Effect / Unlock |
|----------|------|-----------------|
| Town Hall | Pre-placed (free) | Anchor of the village. Defines placement radius. Upgradeable. |
| Lumber Mill | 50 Wood + 20 Gold | Increases Wood yield from all adjacent Tree nodes by +1/tick. |
| Mine Shaft | 30 Stone + 30 Gold | Increases Gold yield from all adjacent Gold Mine nodes by +1/tick. |
| Granary | 40 Wood + 20 Gold | Doubles Food yield from all adjacent Farm nodes. Required for large worker counts. |
| Stonemason | 40 Stone + 40 Gold | Increases Stone yield from adjacent Quarry nodes by +1/tick. Required to build Tier 2 buildings. |
| Barracks | 60 Wood + 40 Gold + 20 Stone | Future scope: Unlocks military units. No effect in current phase. |
| Workshop | 80 Wood + 60 Gold + 40 Stone | Unlocks Tier 2 upgrades for existing buildings. Required for Town Hall upgrade. |
| Wall Segment | 20 Stone | Impassable obstacle. Blocks unit rotation interaction. Future scope: defensive. |
| Gate | 30 Stone + 10 Wood | Passable Wall variant. Allows units to path through. Future scope: directional access. |

### Construction Mechanic

1. Player places a Construction Site token on an empty grid cell (cost deducted immediately from resources).
2. A worker must be placed adjacent to the site. Each tick the worker faces the site, it adds 1 construction progress.
3. When progress reaches the required threshold (default: 5 ticks of work), the site transforms into the completed building.
4. Multiple workers facing the same site contribute cumulatively, speeding up construction.

### Building Upgrades

Each building has up to three upgrade levels. Upgrading requires:

- Sufficient resources (escalating cost per tier)
- A Workshop present on the map (for Tier 2+ upgrades)
- A worker facing the building for a construction-style upgrade tick sequence

---

## 6. Map & World Generation

Each run generates a new procedural map. The map is a grid of configurable size (default: 40×40). The Town Hall spawns at the center. All map generation parameters are exposed on a `MapGenerationSettings` ScriptableObject so designers can tune probabilities directly in the Unity Inspector without touching code.

### Fog of War

The entire map starts fogged. Only the area within `startingRevealRadius` cells of the Town Hall is visible at the beginning of each run. As the player places workers and buildings further from the base, the fog recedes around them. This creates exploration pressure and prevents players from immediately optimising the whole map.

> **DESIGN NOTE:** Fog of war is important not just for atmosphere but for pacing. Players discover high-tier nodes gradually, which keeps the early game focused on the starting area and prevents analysis paralysis on a fully-visible 40×40 map.

### Map Size Options

| Size | Grid | Notes |
|------|------|-------|
| Small | 20×20 | Fast games (~15 min). Tight, intense. Best for testing and tuning. |
| Medium | 40×40 | **Default.** Standard run (~30–45 min). Balanced expansion. |
| Large | 60×60 | Long sessions (60+ min). Intended for late-game combat content. |
| Custom | Configurable | Via `MapGenerationSettings` ScriptableObject or debug menu. |

### Generation Order

The map is generated in a fixed pass order. Each pass respects the cleared zones set by previous passes.

**Pass 1 — Clear Zone**
A circular area of radius `clearRadius` (default: 3) around the Town Hall center is marked as reserved. No resource nodes can be placed here. This guarantees the player has immediate build space.

**Pass 2 — Guaranteed Starting Resources**
These are always placed regardless of random seed:
- 1× Gold Mine at a random empty cell between `goldMineMinDist` and `goldMineMaxDist` cells from center (default: 4–7 cells). Always Tier 1.
- 2–3× Tree clusters directly bordering the clear zone (distance 4–6 cells). This ensures the chain-clear mechanic is immediately available.
- 1× Wild Farm within 6 cells of center (for Food income).

**Pass 3 — River Generation (Water)**
Rivers are generated using a random-walk algorithm starting from a random map edge:
1. Pick a random cell on any edge of the map.
2. Walk toward the opposite side, with a weighted random direction bias that keeps the path moving generally "across" the map.
3. Each step has a `riverWidenChance` probability of also marking the adjacent orthogonal cell as Water, creating a river 1–2 tiles wide.
4. Repeat for each river (default: 1–2 rivers per map).

Rivers create natural territory divisions that encourage interesting worker placement decisions around their banks.

**Pass 4 — Forest Generation (Trees)**
Trees are placed using Unity's `Mathf.PerlinNoise` with a per-run random offset (derived from the run seed):
1. For each cell (outside the clear zone), sample Perlin noise at `(x / treeNoiseScale, y / treeNoiseScale)`.
2. If the sample value exceeds `treeDensityThreshold`, place a Tree node.
3. Apply a post-process string pass: for each tree, check if there is another tree in a straight line within 2 cells. If so, fill any gap between them to ensure strings of 3–5+ trees in a row. This maximises chain-clear opportunities.

Trees should be the most common environmental tile type. Dense forests in the mid and outer zones are intentional — they are the primary source of Wood and the primary arena for chain-clearing.

**Pass 5 — Gold Mine Scatter**
Scatter additional Gold Mines (beyond the guaranteed starting one) across the map:
- Use `goldMineDensity` as the probability of any given cell becoming a Gold Mine.
- Enforce `goldMineMinSpacing` — no two Gold Mines closer than this distance (prevents clustering).
- Gold Mines become more common further from the center (outer zone bias).
- Assign tier: cells within radius 10 = Tier 1; radius 10–20 = mix of Tier 1/2; beyond radius 20 = mix of Tier 2/3.

**Pass 6 — Wild Farm Scatter**
Scatter additional Wild Farm nodes (Food resource) with the same pattern as Gold Mines but slightly higher density. Farms can exist at any radius. They are Tier 1 always in the starting zone; outer farms can be Tier 2.

**Pass 7 — Rock / Stone Scatter (Locked)**
Rocks are placed sparsely across the map:
- Use `rockDensity` as the probability per cell.
- Enforce `rockMinSpacing` between rocks.
- Rocks are **non-interactable** at game start. Workers do nothing when facing them. They exist as obstacles and future unlock targets.
- Rocks can be clustered in 2–3 adjacent groups to form "stone outcroppings."

**Pass 8 — Final Validation**
After all passes, run a validation sweep:
- Verify the guaranteed Gold Mine was placed (retry if blocked).
- Verify no node overlaps with the Town Hall cell.
- Verify no node was placed on a Water tile.
- Verify the starting area is accessible (no water fully surrounding the Town Hall).

### Inspector-Configurable Settings (MapGenerationSettings ScriptableObject)

All of the following fields are tunable in the Unity Inspector:

```
[Header("Map Settings")]
mapWidth            int         = 40
mapHeight           int         = 40
seed                int         = 0     // 0 = random per run

[Header("Clear Zone")]
clearRadius         int         = 3     // empty cell radius around Town Hall

[Header("Guaranteed Starting Resources")]
goldMineMinDist     int         = 4     // min distance from center
goldMineMaxDist     int         = 7     // max distance from center

[Header("Trees")]
treeDensityThreshold  float (0–1)  = 0.42    // Perlin threshold — higher = fewer trees
treeNoiseScale        float        = 6.0     // larger = bigger forest blobs
enableStringPass      bool         = true    // post-process to fill gaps in tree rows

[Header("Rivers / Water")]
riverCount          int (0–4)    = 2
riverMinLength      int          = 12
riverMaxLength      int          = 30
riverWidenChance    float (0–1)  = 0.15    // chance each step also marks adjacent cell

[Header("Gold Mines")]
goldMineDensity     float (0–0.1) = 0.015   // per-cell probability after clear zone
goldMineMinSpacing  int           = 6

[Header("Wild Farms")]
farmDensity         float (0–0.1) = 0.020
farmMinSpacing      int           = 5

[Header("Rocks (Locked)")]
rockDensity         float (0–0.1) = 0.012
rockMinSpacing      int           = 4

[Header("Fog of War")]
startingRevealRadius  int         = 4     // cells revealed around Town Hall at run start
```

### Map Zones (Radial)

Even though generation is noise-based rather than hard-zoned, the effective radial distribution produces these zones naturally:

- **Starting Zone (radius 0–6):** Clear area, guaranteed resources, sparse trees. The player's safe base.
- **Mid Zone (radius 7–18):** Dense forests, mix of all resource types, Tier 1–2 nodes. Primary expansion area.
- **Outer Zone (radius 19+):** High-density resources, Tier 2–3 nodes, more rivers and rock outcroppings. Future scope: enemies spawn here.

---

## 7. Game Loop

Clockwork Craft is currently a **sandbox experience**. There is no defined win/lose condition in this phase. The game loop is:

1. Run starts. Procedural map generates. Town Hall placed at center. Player starts with 20 Gold, 10 Wood, 5 Food, 0 Stone.
2. Player surveys the map, identifies nearby resource nodes.
3. Player spends resources to draw workers from the dock.
4. Player places workers adjacent to resource nodes to begin gathering.
5. Resources accumulate over time as the clockwork ticks.
6. Player spends resources to place construction sites for buildings.
7. Player places workers adjacent to construction sites to build.
8. Buildings complete, unlocking new capabilities and yield bonuses.
9. Player expands outward as nearby nodes deplete, unlocking higher-tier resources.
10. Village grows in complexity and output. Player experiments with placement geometry to optimise efficiency.

> **SANDBOX PHASE:** Win and lose conditions, enemy waves, and session timers are intentionally left out of this phase. The goal is to make the building and resource loop feel satisfying and deep before adding pressure systems. Victory/defeat will be designed once the core loop is proven fun.

---

## 8. Future Scope (Post-Sandbox)

The following systems are explicitly out of scope for the current development phase. They are documented here so the architecture can be designed to accommodate them from the start.

### 8.1 Combat System

Combat in Clockwork Craft uses the same interval mechanic. Military units rotate and attack whatever they face — exactly as workers harvest. No pathfinding or real-time movement; the clockwork handles it.

- Military unit facing Enemy unit → deals attack damage
- Enemy unit facing Player unit → deals attack damage
- Combat is resolved at the tick, not in real-time
- Enemy AI: enemy units are placed on the grid by the wave spawner and rotate according to their own rotation direction, creating emergent combat formations

### 8.2 Military Units

A Soldier unit will be introduced as a separate unit class from the Worker. Key differences:

- Soldiers have high HP and deal attack damage; Workers have low HP and deal harvest damage
- Soldiers are trained at the Barracks (already in Building Catalogue, no-op in current phase)
- Soldiers cost Gold + Food to train (no Wood/Stone required)
- Soldiers cannot harvest resources (if facing a resource node, no interaction occurs)

### 8.3 Enemy Waves

Enemy units spawn from the map edges and move toward the Town Hall using a simplified wave system (inherited from RTChess WaveManager). They are placed on grid cells and interact via the clockwork system.

### 8.4 Win / Lose Conditions

To be designed once the sandbox phase is complete. Candidate conditions:

- Survive N enemy waves (wave-based win)
- Town Hall destroyed = lose
- Reach a total resource/building milestone = win
- Timed survival: last as long as possible (score-based run)

### 8.5 Tech Tree & Research

A light tech tree branching from the Workshop, allowing players to unlock passive bonuses, new unit types, and building variants using a Research mechanic (worker faces Research building to progress research).

---

## 9. Technical Architecture

Clockwork Craft is a second game built in the same Unity project as RTChess. **The RTChess codebase must remain fully intact and functional.** All new code lives under `Assets/ClockworkCraft/`.

### Reused Systems from RTChess

| System | How It's Used |
|--------|---------------|
| `GridManager` | Core N×N grid. Expand to configurable size (default 40x40 vs RTChess 11x11). |
| `IntervalTimer` | Global clock — unchanged. All gameplay syncs to `OnIntervalTick`. |
| `Unit` / Worker base class | Adapted: Worker extends Unit. Harvest replaces attack-on-resource logic. |
| `ResourceNode` | Adapted: add resource type enum (Gold/Wood/Food/Stone), tier system, yield-per-tier. |
| `ResourceTokenManager` | Adapted into 4-currency manager (one counter per resource type). |
| `DockBarManager` | Reused: worker draw/drag system mirrors unit placement. |
| `DragDropHandler` | Reused: placement validation on grid. |
| `GridVisualizer` | Reused: add zone coloring for map biomes. |
| `WaveManager` | Reserved for future enemy spawning — do not modify or delete. |
| `SFXManager` | Reused: add new audio events for harvest, construction, completion. |

### New Systems Required

| System | Purpose |
|--------|---------|
| `BuildingManager` | Singleton. Tracks all placed buildings. Handles construction progress per tick. |
| `MapGenerator` | Generates procedural grid layout using `MapGenerationSettings`. Runs all 8 passes in order. Exposes `RegenerateMap(int seed)` for testing. |
| `MapGenerationSettings` | ScriptableObject. All map tuning parameters (density, noise scale, distances, fog radius). Lives in `Assets/ClockworkCraft/Data/`. |
| `ResourceManager` | Replaces single-currency ResourceTokenManager. Tracks Gold, Wood, Food, Stone independently. |
| `WorkerManager` | Tracks all workers, their states (idle, harvesting, constructing, advancing via takeover). |
| `BuildingCatalogue` | ScriptableObject list of all building definitions (cost, HP, construction ticks, effects). |
| `NodeManager` | Tracks all resource nodes on the map. Handles per-tick damage, depletion, loot drop, tile takeover trigger. |
| `FogManager` | Tracks revealed/fogged cells. Reveals on unit placement, building placement, or worker advance. |
| `UpgradeSystem` | Manages building upgrade state per instance. Reads from BuildingCatalogue. |
| `MapCamera` | Extends RTChess IsometricCamera with zoom-to-fit and drag-to-pan for large maps. |

### Singleton Pattern (Consistent with RTChess)

- `BuildingManager.Instance`
- `ResourceManager.Instance`
- `WorkerManager.Instance`
- `NodeManager.Instance`
- `MapGenerator.Instance`

### Event System (Consistent with RTChess)

- `IntervalTimer.OnIntervalTick` — all units and buildings subscribe for per-tick actions
- `ResourceManager.OnResourceChanged(ResourceType, int newValue)` — drives UI updates
- `BuildingManager.OnBuildingPlaced(Building)` — triggers neighbour yield bonuses
- `BuildingManager.OnBuildingCompleted(Building)` — unlocks downstream effects
- `NodeManager.OnNodeDepleted(ResourceNode)` — triggers loot drop, grid cleanup

### File Structure

```
Assets/
├── ClockworkCraft/                  ← All new game code
│   ├── Scripts/
│   │   ├── Core/                   ← MapGenerator, ResourceManager, BuildingManager, etc.
│   │   ├── Units/                  ← Worker (extends RTChess Unit), future Soldier
│   │   ├── Buildings/              ← Building base class, all building types
│   │   ├── UI/                     ← Resource HUD, Building palette, Upgrade panel
│   │   └── Data/                   ← ScriptableObjects: BuildingData, NodeData
│   ├── Prefabs/                    ← Worker, resource nodes, buildings, UI panels
│   └── Scenes/
│       └── ClockworkCraft.unity
└── RTChess/                        ← DO NOT MODIFY (original game jam code stays intact)
```

---

## 10. Design Principles

These principles must guide every design and implementation decision. When in doubt, refer back to these.

**1. Clockwork is Law.**
The interval mechanic is sacred. Every interaction — harvesting, building, eventually fighting — happens via unit rotation at the tick. Do not add real-time mechanics that bypass the tick.

**2. Placement is the game.**
The player does not control units directly. They arrange them. The fun comes from spatial thinking: what faces what, for how many ticks per cycle, and in what order.

**3. All four resources matter.**
No resource should become irrelevant. If a player can ignore Wood, Food, or Stone entirely, the design has failed. Every building and upgrade must use a meaningful mix of resources.

**4. Readable at a glance.**
The player must be able to look at the grid and immediately understand what is happening. Unit facing directions, resource yields, and building states should be visually self-explanatory without needing to open a menu.

**5. Sandbox first.**
Do not add pressure systems (waves, timers, lose conditions) until the core building and gathering loop is proven fun. The sandbox must be satisfying before it becomes a challenge.

**6. Reuse, don't rebuild.**
The RTChess codebase is a proven foundation. Extend it, wrap it, adapt it. Never rewrite a system that already works.

**7. Interval-first performance.**
No `Update()` frame logic for gameplay. Everything happens at `OnIntervalTick`. This keeps the game deterministic and mobile-performant.

---

## 11. Development Phases

### Phase 1 — Procedural Map & Environmental Generation ← CURRENT PHASE
- `MapGenerationSettings` ScriptableObject with all inspector-tunable parameters
- `MapGenerator` singleton implementing all 8 generation passes
- Tile type system: Grass, Tree, Gold Mine, Wild Farm, Rock (locked), Water (locked)
- Perlin noise forest generation with string-fill post-process pass
- River generation via random walk with widening
- Guaranteed starting conditions (Gold Mine, trees, farm within reach of Town Hall)
- Radial zone bias (density increases with distance from center)
- Fog of war from the start, with starting reveal radius around Town Hall
- Resource node HP, depletion, and loot drop
- Tile takeover: worker advances onto cleared tile and chains into next node
- Worker dock (draw/drag from RTChess — reused)
- Resource HUD: Gold, Wood, Food, Stone counters

### Phase 2 — Building System
- Construction site placement and worker-driven build progress
- Town Hall + 4 core buildings (Lumber Mill, Mine Shaft, Granary, Stonemason)
- Building catalogue (ScriptableObjects)
- Yield bonus system (buildings affect adjacent nodes)
- Basic upgrade flow (Tier 1 → Tier 2 with Workshop requirement)

### Phase 3 — Map Expansion & Village Scale
- Camera zoom/pan for large maps
- Fog of war / exploration reveal
- Full building catalogue (all buildings from Section 5)
- Village radius gating (Town Hall upgrade unlocks more build space)
- UI polish: building palette, upgrade panel, resource tooltips

### Phase 4 — Combat & Enemies (Future)
- Enemy wave spawner (adapted from RTChess WaveManager)
- Soldier unit type (Barracks-trained)
- Win/lose conditions
- Defensive buildings (Walls, Gates, Towers)

### Phase 5 — Polish & Feel
- Harvest animations, particle effects, construction VFX
- Sound design: per-resource harvest sounds, building complete fanfare
- Mobile touch controls and UI scaling
- Difficulty settings (map size, resource density, interval speed)

---

*End of Document — Clockwork Craft GDD v1.1*
