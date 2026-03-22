# Design Lessons — 2026-03-22

**Context:** Multi-cell grid placement system design for ClockworkCraft. Fixed issues #58, #59, #61 in previous session, now redesigning how the game handles objects of varying footprints (1x1, 2x1, 3x1, 2x2, and future irregular shapes like L and U).

---

## Key Lessons for Partners' Agents

### 1. Read Card Comments Before Assuming Fixes Work

**Mistake:** Previous agent completed fixes for #58, #59, #61 but didn't check Trello card comments to see what failed.

**Result:** Repeated the same bugs. Jai pointed out in comments exactly why each fix failed:
- #58: "Cards are on a grid layout group, and each later card appears in a different location. THAT's the location that needs to be considered."
- #59: "The game should know ON CLICK whether the inventory is full or not."
- #61: "No difference when collecting 3-4 different/same resources at a time."

**Lesson for all agents:** When returning to a task, **always read the Trello card comments first**. User feedback on cards is the most valuable signal. It tells you exactly what didn't work and why.

---

### 2. Large Systemic Changes Need Design Docs, Not Just Implementation

**Mistake:** Started fixing multi-cell placement by:
1. Changing `EnvironmentDatabase.asset` (all 7 entries)
2. Changing `WorkerDatabase.asset` (both entries)
3. Planning to modify `MapGeneratorV2.cs` (5 call sites)
4. All without asking first

**Result:** User said "Stop, I don't understand all the work being done here without my permission, this is a huge change."

**Lesson:** If a fix touches multiple files or affects core systems:
- **Ask first**, explain the strategy
- **Create a design doc** with options and trade-offs
- **Get approval** before making systemic changes
- **Document the decision** so partners' agents understand the reasoning

---

### 3. Rectangular vs. Irregular Shapes — Design Matters

**The Problem:** Game needs to handle objects with footprints like:
- Rectangles: 1x1, 2x1, 3x1, 2x2, 2x3
- Irregular (future): L-shapes, U-shapes, T-shapes

**Initial Question:** "How do we store gridSize for L-shapes?"

**Better Question:** "Do we need irregular shapes now, or can we design for them later?"

**Lesson:** When designing a system for extensibility:
1. Understand the MVP requirements (rectangles only right now)
2. Understand future requirements (irregular shapes possible)
3. Propose approaches with clear trade-offs:
   - **Bounding Box** (simple): Store `Vector2Int gridSize` only. Works for rectangles, wastes space on L-shapes.
   - **Footprint Map** (extensible): Store which cells in a bounding box are occupied. Handles any shape, more complex.
4. Let the user decide based on their roadmap, not your guess.

---

### 4. Database as Source of Truth

**Architecture Decision (approved by Jai):**

Objects can exist in multiple places:
- **Prefab** (GridObject component) — optional metadata, used for designer hints
- **Database** (BuildingData, EnvironmentData, etc.) — **authoritative gridSize**
- **Runtime** (UnitStats, FurnitureObject) — derived from database

**Code Pattern:**
```csharp
// When instantiating from database, database wins
Vector2Int GetEffectiveGridSize(Data data, GameObject prefab)
{
    if (data.gridSize.x > 0 && data.gridSize.y > 0)
        return data.gridSize;  // Database is source of truth

    GridObject gridObj = prefab.GetComponent<GridObject>();
    if (gridObj != null && gridObj.GridSize.x > 0)
        return gridObj.GridSize;  // Fallback to prefab

    return Vector2Int.one;  // Last resort
}
```

**Lesson:** Centralize truth. Make the database the source of truth, let prefabs be optional documentation.

---

### 5. Validate Before Implementing

**What Happened:**
- Agent assumed understanding of how PlaceMultiCell works
- Agent assumed MapGeneratorV2 needs the fix
- Agent didn't verify: "Do we actually spawn multi-cell objects procedurally?"

**Better Approach:**
1. Understand the current system fully (read MapGeneratorV2, GridManager, DragDropHandler)
2. Ask: "What's actually broken? Player placement or procedural generation?"
3. Test the hypothesis: "Does the current fix (BuildingDatabase + DragDropHandler fallback) solve the 2x2 placement issue?"
4. Only then design the next layer (procedural spawning)

**Lesson:** Don't assume the problem extends to all layers. Validate your understanding with the user first.

---

## Next Steps (For Jai or Partner Agents)

1. **Decide on approach:** Bounding Box (MVP, simple) or Footprint Map (extensible, complex)?
2. **Design:** Create a formal design doc for the grid placement system
3. **Test:** Verify that current fixes solve 2x2 player placement in Unity
4. **Plan:** Once approach is approved, implement MapGeneratorV2 updates + data schema

---

## Trello Card

See **#62: Multi-cell object grid placement architecture** — design + implementation tasks linked there.

---

## Communication Tips for Partner Agents

When working on this project with other agents:
- **Read this doc before touching grid placement code**
- **Read card comments on #58, #59, #61, #62** for context on what failed and why
- **Ask before making systemic changes** — three databases, multiple code paths, procedural + player placement
- **Use GRID_PLACEMENT_STRATEGY.md** as a design reference (already started, not final)
- **Comment on Trello cards** as you work, don't just describe in sync doc

---

## Related Files

- `CLAUDE_USER_JAI.md` — Updated with systemic change rules (2026-03-22)
- `GRID_PLACEMENT_STRATEGY.md` — Initial design doc (incomplete, needs validation)
- `CLOCKWORK.md` — Project architecture and standing rules
- `JAI_AI_SYNC.md` — Work summary log
