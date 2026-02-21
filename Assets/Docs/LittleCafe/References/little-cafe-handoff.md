# Little Cafe — Conversation Handoff & Design State

## What This Is
This document captures the full state of a game design conversation for "Little Cafe" — a restaurant management game prototype being built in Unity. Paste this into a new session to continue exactly where we left off.

---

## Project Context

### Existing Codebase
Little Cafe is being built as a **new branch** in the same Unity repo as "Clockwork Grid" (project folder: RTChess). The Clockwork Grid game is complete. Key reusable systems from that project:
- Unity project structure & build pipeline
- Singleton manager pattern (GridManager.Instance, etc.)
- Event-driven architecture
- Grid system (needs expansion from 4x4 to larger, and conversion to isometric)
- Economy/token manager (becomes gold/currency)
- Debug menu approach

### What Does NOT Carry Over (needs fresh implementation)
- No interval/tick system (this game is real-time continuous)
- No unit rotation/facing (staff need pathfinding)
- No dock bar/draw system (replaced by tap interactions)
- No wave system (replaced by customer arrival patterns)
- No fog of war
- No combat

---

## Game Overview

| Field | Value |
|-------|-------|
| **Title** | Little Cafe |
| **Genre** | Restaurant management / Idle tycoon |
| **Platform** | Mobile (iPhone primary) |
| **Perspective** | 3D isometric |
| **Input** | Tap-based |
| **Engine** | Unity (existing RTChess project, new branch) |
| **Prototype Goal** | Full customer service pipeline with tap interactions |

### High Concept
A tap-to-manage restaurant game. Customers arrive, sit, order. Player taps to trigger each step: cooking, serving, payment, cleanup. Staff level up to automate steps over time. Limited plates cycle through the system (cook → serve → eat → dirty → wash → reuse) creating Overcooked-style pressure.

---

## Confirmed Design Decisions

### Grid & Movement
- **Grid-based pathfinding (A*)** — staff move tile-by-tile on the grid
- **Everything is 1x1 scale** — characters, equipment, tables, stations all occupy 1 grid tile (confirmed from proportion reference image)

### Plate System (Key Mechanic)
- **Plates are a limited physical resource** that cycles through the system
- **Plate lifecycle:** Clean plate on rack → Chef takes plate to cook → Food on plate to serving window → Waiter delivers to table → Customer eats → Dirty plate sits on table (BLOCKS table from reuse) → Waiter clears dirty plate to washing area → Chef washes → Clean plate returns to rack
- **Plate Racks:** 1x1 grid objects, plates visually stack on top, max 3 plates per rack
- **If all plates are dirty/in-use, kitchen cannot cook** — creates pipeline pressure
- **Visual:** Physical 3D stacked plates on racks, player can see at a glance how many are available

### Dirty Dishes & Table Blocking
- Dirty plates physically block the table from being occupied by new customers
- Plates must be washed before reuse (limited plate pool, like Overcooked)

### Cafe Zones (from diagrams)
- **Kitchen** (back): Cooking stations, serving window, dishwashing station, chef queue area, plate racks
- **Dining** (front/center): Tables of various sizes, chairs, waiter queue area, decor
- **Entrance/Outside**: Customer queue (visible line of waiting customers)
- **Beverage Area** (future unlock): Bar, drink station, bartender queue

### Service Pipeline (12 steps)
1. Customer arrives → joins outside queue
2. Table available → customer auto-walks to table, sits
3. Customer displays order bubble
4. **TAP** order bubble → order added to kitchen queue
5. **TAP** cooking station → chef walks to station, starts cooking (uses 1 clean plate)
6. Cooking complete → food on plate appears at serving window
7. **TAP** serving window → waiter collects food, walks to table, delivers
8. Customer eats (takes X seconds), pays automatically
9. **TAP** money/tips → collect payment from table
10. Customer leaves → dirty plate remains on table (table BLOCKED)
11. **TAP** dirty dishes → waiter collects, delivers to washing area
12. **TAP** washing station → chef washes plate → clean plate returns to rack

### Staff System
**Classes:**
- **Chef**: Cook food, deliver to serving window, wash dishes (1 action at a time)
- **Waiter**: Collect orders, deliver food, clear dirty dishes (1 action at a time)
- **Bartender**: (future) Make and serve drinks

**Behavior:**
- Each staff does 1 action at a time
- When idle, staff return to their queue area
- **Low-level staff**: Only work after player tap (manual)
- **High-level staff**: Auto-work on available tasks (automation unlock)

**Queue System:**
- Each class has a queue (starting size: 1)
- Queue size = max staff of that class working simultaneously
- Queue size upgradable with gold

**Staff Stats:**
- Productivity (revenue multiplier)
- Efficiency (speed multiplier)
- Level (determines automation unlock threshold)

**Staff Progression Loop:**
1. Staff generate idle currency passively
2. Assign staff to minigame
3. Win minigame → increase currency production
4. Use currency to upgrade staff (level up)
5. Higher level = faster, more revenue, eventually auto-works

### Customer System
- Customers arrive at configurable rate, join visible outside queue
- Auto-walk to available table when one is free (and clean)
- Patience timer — if food takes too long, customer leaves angry (no payment)
- Customer pays automatically after eating
- Tips based on satisfaction/wait time

### Economy
- **Gold**: Primary currency from customer payments + tips
- Spent on: staff upgrades, queue size upgrades, new equipment, tables, plates, decor, cafe expansion
- Revenue formula: Base recipe value × staff productivity × satisfaction bonus + tips

### References Cited in Design Docs
- **Overcooked** — plate pressure, kitchen chaos
- **Plate Up** — plate stacking on racks
- **Gold & Goblins** — staff/upgrade progression
- **Idle Bank** — idle currency generation, staff mode, assignment system
- **Fat Goose Gym** — economy loop reference

---

## OPEN QUESTIONS (Still Need Answers)

These were asked but not yet answered. Need to resolve before writing prototype prompts:

### Camera
- [ ] Fixed isometric angle, or can player rotate? (4-angle snap?)
- [ ] Pinch to zoom + drag to pan? Or fixed view?

### Core Tap Interaction
- [ ] Tap the OBJECT (order bubble, cooking station, serving window, dirty dishes)?
- [ ] Or tap the STAFF then tap destination?
- [ ] Or tap object and nearest available staff auto-assigns?

### Session Structure
- [ ] Timed rounds ("survive the lunch rush")?
- [ ] Endless/idle (continuous)?
- [ ] Day cycle (morning → lunch → dinner → close)?

### Cafe Layout
- [ ] Fixed zones, player places objects within them?
- [ ] Full freedom to place everything?
- [ ] Pre-designed layout, player just upgrades?

### Grid Size
- [ ] Starting cafe size in tiles? (~10x10? ~15x15? ~20x20?)

### Serving Window
- [ ] Is it a 1x1 object on the wall between kitchen and dining?

### Recipe Complexity (Prototype)
- [ ] One recipe? A few (3-5)? Full ingredient system?

### Plate Racks
- [ ] Fixed location, or player-placed on grid?
- [ ] One rack for clean, one near washing output?

### Carrying
- [ ] Staff carry 1 plate at a time, or can stack 2-3?

### Dirty Plate Storage
- [ ] Dirty plates go to a rack near sink? A wash basin? Just pile on table?

### Queue Visibility
- [ ] Queues visible as 3D characters in world? Or UI counter? Or hybrid?

### Prototype Scope
- [ ] Minimum first build: 1 chef, 1 waiter, 1 station, 2 tables, full pipeline? More? Less?

---

## Reference Images Available

The following images were shared during the conversation. Upload them again in the new session:

1. **FigJam pages (2 screenshots):**
   - Staff Functions & Automation page — chef/waiter actions, queue system, staff leveling, automation rules, upgrade UI mockups
   - Staff Progression Loop page — idle currency generation, minigame assignment, upgrade flow, class system (Chef/Bartender/Waiter)

2. **Cafe Layout & Core Mechanics page:**
   - Left side: Isometric cafe with numbered interaction points and action list
   - Right side: "Core Mechanic Change" — current vs proposed service pipeline, 8-step flow with screenshots, bottleneck analysis

3. **Annotated reference game screenshot (Image 1):**
   - Shows Outside Queue, Chef Queue, Waiter Queue labeled
   - "Tap to Serve, Tap to Cook, Tap to Deliver"
   - Timer visible (00:00:29)

4. **Scale/proportion reference (Image 2):**
   - Low-poly 3D isometric restaurant
   - Shows 1x1 grid scale: characters = 1 block, tables = 1 block, equipment = 1 block
   - Kitchen enclosed in walled area, dining area with scattered tables, outdoor space
   - NOT the art style target — this is the PROPORTION and SCALE reference

5. **Latest gameplay diagram (Image 3):**
   - Most current representation of planned gameplay
   - 3D isometric view with kitchen (top), dining (bottom), serving window pass-through
   - Curved arrows showing service flow paths
   - Visible: chef queue, waiter queue, customer queue (outside, bottom)
   - Tables (various sizes), cooking stations, serving counters, dishwashing area
   - Money piles on tables, food/order bubbles, checkmark indicators
   - "Cheat" debug button visible

---

## Clockwork Grid Assets Available
The completed Clockwork Grid game in the same repo includes:
- Two music tracks (120 BPM clockwork theme + lobby ambient) — may want new music for Little Cafe
- Full Unity project structure with singleton managers, event system, grid system
- The README.md documents the full architecture

---

## Next Steps When Resuming
1. Answer the open questions above
2. Finalize the GDD
3. Break prototype into phased Claude Code prompts (like we did for Clockwork Grid)
4. Phase 1 suggestion: Grid setup + isometric camera + basic cafe layout with kitchen/dining zones + A* pathfinding for one test character
