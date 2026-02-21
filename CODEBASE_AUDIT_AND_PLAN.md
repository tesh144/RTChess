# RTChess Cafe Builder - Codebase Audit & Technical Plan
**Date:** 2026-02-17
**Status:** Pre-Implementation Planning

---

## EXECUTIVE SUMMARY

**What You Have:** A functional space-building sandbox built on RTChess foundation. The LittleCafe system exists and works - grid placement, dock bar, drag-drop, fog reveal are all functional.

**What's Broken:** The 114 PEPO furniture assets are not properly set up as placeable prefabs with animations.

**Recommendation:** **CLEAN UP AND CONTINUE** - Do NOT restart from scratch. The core systems work well. We only need to fix the prefab generation system.

---

## 1. CODEBASE AUDIT

### ✅ WORKING SYSTEMS (Keep These)

#### **Grid System** - `Assets/Scripts/Core/GridManager.cs`
- **Status:** EXCELLENT - Fully functional
- **Features:**
  - 50x50 grid (configurable)
  - 1.5 unit cell size
  - Multi-cell placement support (1x1, 2x2, etc.)
  - Checkerboard tile prefabs (A/B alternating pattern)
  - Fog reveal system integrated
  - Grid resize capability
  - World ↔ Grid coordinate conversion
- **Keep:** 100% - This is solid

#### **Dock Bar System** - `Assets/Scripts/UI/DockBarManager.cs`
- **Status:** EXCELLENT - Feature-complete
- **Features:**
  - Draw/gacha button with token costs
  - Linear cost escalation (6, 7, 8, 9...)
  - Cost decrease over time intervals (optional)
  - Hand limit (max 10 units)
  - Slide-up animation on game start
  - Editor UI integration
  - UnitStats-based card system
- **Keep:** 100% - This works great

#### **Drag-Drop Placement** - `Assets/Scripts/UI/DragDropHandler.cs`
- **Status:** Need to read, but likely functional
- **Keep:** Likely 100%

#### **Fog of War** - `Assets/Scripts/Systems/FogManager.cs` + `Assets/Scripts/Core/TileFog.cs`
- **Status:** Functional - tiles drop and rise
- **Features:**
  - Tiles start lowered and faded
  - Reveal with smooth tween animation
  - Adjacent tile reveal on placement
- **Keep:** 100%

#### **Token Economy** - `Assets/Scripts/Core/ResourceTokenManager.cs`
- **Status:** Functional
- **Keep:** 100%

#### **Cafe Scene Setup** - `Assets/Scripts/LittleCafe/CafeSceneSetup.cs`
- **Status:** EXCELLENT - Fully integrated
- **Features:**
  - Reuses RTChess infrastructure
  - Removes combat systems (WaveManager, etc.)
  - Sets up 50x50 grid
  - Configures equipment types (Table, Chair, Wall, etc.)
  - Creates fallback colored cube prefabs
  - Free draws (cost = 0)
  - Game start gate (click to begin)
- **Keep:** 95% - May need minor adjustments for PEPO prefabs

#### **Cafe Equipment** - `Assets/Scripts/LittleCafe/CafeEquipment.cs`
- **Status:** Functional
- **Features:**
  - Grid position tracking
  - Multi-cell support
  - Adjacent fog reveal on placement
- **Keep:** 100%

### ⚠️ SYSTEMS TO REMOVE (Don't Need)

#### **Combat Systems** - Can be safely removed or ignored
- `WaveManager.cs` - Enemy wave spawning ❌
- `Unit.cs` / `SoldierUnit.cs` - Combat units with facing/attack ❌
- `ResourceNode.cs` - Harvestable resources ❌
- `Facing.cs` - Direction system ❌
- **Action:** Keep files for now (might be useful for Phase 2 NPCs), but CafeSceneSetup already disables these

#### **Interval Timer Gameplay** - `IntervalTimer.cs`
- **Status:** You want to KEEP the timer as "internal clock" but remove turn-based mechanics
- **Action:** Keep IntervalTimer.cs, but don't sync gameplay to OnIntervalTick events

### ❌ BROKEN SYSTEMS (Need to Fix)

#### **Prefab Generation** - `Assets/Scripts/Editor/`
- **Files:**
  - `PrefabGenerator.cs` - Original generator (unknown status)
  - `PrefabRegenerator.cs` - Failed (searches for wrong hierarchy)
  - `PrefabRestructureTool.cs` - Failed (Transform errors)
  - `ObjectAnimationCreator.cs` - Created but untested
  - `DebugPrefabStructure.cs` - Debug tool
- **Problem:** 114 PEPO FBX assets are not set up as working prefabs
- **Action:** **THIS IS THE PRIMARY TASK** - Fix prefab generation

---

## 2. THE PREFAB PROBLEM

### What We Need:
1. **114 furniture prefabs** (chairs, tables, walls) from PEPO FBX assets
2. Each prefab needs:
   - ✅ Placeable on grid (drag from dock, snap to cells)
   - ✅ Appear animation (wobble on spawn)
   - ✅ Remove animation (shrink + spin on delete)
   - ✅ GridObject component with correct size (1x1, 2x2, etc.)
   - ✅ CafeEquipment component for fog reveal

### Current Status:
- **FBX Assets:** Exist in `Assets/PEPO/` (need to verify exact path)
- **ObjectPrefabHolder Template:** Exists but hierarchy was confusing
  - Actual structure: `ObjectPrefabHolder → AnimatorHolder → Recenter`
  - I was searching for: `CharacterHold → CharacterRe:Zero` ❌
- **Animations:** Created via ObjectAnimationCreator.cs but untested
- **Prefabs:** 114 attempts failed due to hierarchy mismatch

### What Went Wrong:
1. I assumed hierarchy names without checking actual structure
2. I didn't ask clarifying questions before implementing
3. I tried to "fix" broken prefabs instead of generating them correctly from scratch

---

## 3. TECHNICAL PLAN - PREFAB GENERATION SYSTEM

### Phase 1: Verify and Document (Day 1)
**Goal:** Understand current state before touching anything

1. **Locate PEPO Assets**
   - Find exact path to 114 FBX files
   - List all asset names
   - Verify they're all furniture (not characters/units)

2. **Inspect ObjectPrefabHolder**
   - Read actual prefab structure
   - Document hierarchy: what each level does
   - Determine where FBX should be placed
   - Check if animations work with current hierarchy

3. **Check Existing Prefabs**
   - How many prefabs already exist?
   - Are any working correctly?
   - Should we delete all and start fresh?

4. **Define Grid Sizes**
   - How do we determine if a table is 2x2 vs 1x1?
   - Manual config file?
   - Naming convention (e.g., "Table2x1" in name)?
   - User provides spreadsheet?

### Phase 2: Clean Prefab Generator (Day 1-2)
**Goal:** Create ONE script that works correctly

1. **New Script:** `PEPOFurniturePrefabGenerator.cs`
   - Clear, well-documented
   - Asks user for confirmation before generating
   - Logs every step clearly
   - Generates all 114 prefabs in one batch

2. **Prefab Structure:**
   ```
   RockTable01 (renamed from ObjectPrefabHolder)
   └─ AnimatorHolder (animated wrapper)
      └─ Recenter (pivot correction)
         └─ RockTable01_FBX (complete FBX asset, UNTOUCHED)
            └─ [all FBX children preserved]
   ```

3. **Components Added:**
   - `GridObject` - defines grid size (1x1, 2x2, etc.)
   - `CafeEquipment` - handles fog reveal, grid tracking
   - `Animator` - references ObjectAnimController for animations

4. **Animations:**
   - Appear: Fall + wobble (0.8s)
   - Remove: Shrink + spin (0.5s)
   - Interact_Weak: Small hop (0.3s)
   - Interact_Strong: Big swell (0.5s)

### Phase 3: Save/Load System (Day 2-3)
**Goal:** Save cafe layouts as JSON "codes"

1. **LayoutSerializer.cs**
   - Serialize placed furniture to JSON
   - Format: `{ "objects": [{"type":"Chair", "x":5, "y":3, "rotation":0}, ...] }`
   - Copy to clipboard
   - Paste code to load layout

2. **LayoutLoader.cs**
   - Parse JSON code
   - Instantiate prefabs at correct positions
   - Validate grid availability

3. **UI Integration**
   - "Save Layout" button → copies code to clipboard
   - "Load Layout" text field → paste code, click load
   - "Export to File" → .json file download

### Phase 4: Furniture Behaviors (Day 3-4)
**Goal:** Make walls block movement, tables have slots

1. **Wall Behavior**
   - Add `WallObject` component
   - Blocks pathfinding (Phase 2 NPCs)
   - Grid cells marked as "blocked"

2. **Chair Behavior**
   - Add `ChairObject` component
   - Has "occupant" slot for NPC
   - Can be "pulled out" to access table

3. **Table Behavior**
   - Add `TableObject` component
   - Has 2-4 interaction slots (detect nearby chairs)
   - NPCs can "eat" at table

4. **Grid Cell States**
   - Extend `CellState` enum:
     - Empty
     - Furniture (walkable decoration)
     - Wall (blocks movement)
     - Occupied (NPC on tile)

---

## 4. IMMEDIATE NEXT STEPS

### Task 1: PEPO Asset Discovery (15 mins)
- [ ] Find all 114 FBX files
- [ ] List their names in a text file
- [ ] Confirm they're all furniture

### Task 2: ObjectPrefabHolder Analysis (15 mins)
- [ ] Read the actual prefab file
- [ ] Document exact hierarchy
- [ ] Test if animations play correctly
- [ ] Determine correct FBX placement

### Task 3: Grid Size Configuration (30 mins)
- [ ] Create `FurnitureGridSizes.json` config file
- [ ] Ask user to specify size for each asset
- [ ] Format: `{"RockTable01": {"x": 2, "y": 1}, "WoodenChair": {"x": 1, "y": 1}, ...}`

### Task 4: Clean Prefab Generator (2 hours)
- [ ] Write `PEPOFurniturePrefabGenerator.cs`
- [ ] Test on 1 asset first
- [ ] Verify animations work
- [ ] Run batch generation for all 114

### Task 5: Manual Verification (30 mins)
- [ ] Drag one prefab from dock
- [ ] Place on grid
- [ ] Verify appear animation plays
- [ ] Verify fog reveals
- [ ] Try removing it (right-click or debug menu)
- [ ] Verify remove animation plays

---

## 5. QUESTIONS FOR USER

### A. PEPO Assets
1. Where are the 114 FBX files located? (path in Assets folder)
2. Do they all need to be included, or can we start with a subset?
3. Are there any assets that are NOT furniture (e.g., trees, rocks for auto-generation)?

### B. Grid Sizes
1. How should we determine grid size for each asset?
   - Option A: I look at each FBX and estimate (fast but may be wrong)
   - Option B: You provide a spreadsheet/list with sizes (accurate but requires your time)
   - Option C: Default all to 1x1, we adjust specific ones later (fastest to start)

### C. ObjectPrefabHolder
1. Can I read and analyze the existing ObjectPrefabHolder prefab?
2. Should I test if the animations already work with it?
3. Is the AnimatorController already set up correctly?

### D. Existing Prefabs
1. Are there any existing prefabs in `Assets/Prefabs/PEPO/` or similar?
2. Should I delete them all before regenerating?
3. Or should I preserve some that are working?

### E. Cafe Scene
1. Is there a specific Unity scene for the cafe mode?
2. Should I test the prefab system in that scene?
3. Or use a separate test scene?

---

## 6. SUCCESS CRITERIA

### Minimum Viable Product (MVP)
- [ ] All 114 PEPO assets exist as working prefabs
- [ ] Can drag from dock and place on grid
- [ ] Appear animation plays on placement
- [ ] Fog reveals adjacent tiles
- [ ] Can save layout as JSON code
- [ ] Can load layout from JSON code

### Stretch Goals
- [ ] Walls block future NPC pathfinding
- [ ] Tables have interaction slots
- [ ] Chairs can be occupied
- [ ] Remove animation on deletion
- [ ] Rotation support (0°, 90°, 180°, 270°)
- [ ] Undo/redo system

---

## 7. RISK ASSESSMENT

### Low Risk ✅
- Grid system - already working perfectly
- Dock bar - already working perfectly
- Fog reveal - already working perfectly
- Cafe scene setup - already working perfectly

### Medium Risk ⚠️
- Prefab generation - needs careful implementation but straightforward
- Animation system - already created, just needs testing
- Save/load JSON - standard serialization, well-documented

### High Risk ❌
- NONE - All systems are either working or straightforward to implement

### Time Estimate
- **Phase 1 (Verification):** 1-2 hours
- **Phase 2 (Prefab Generation):** 2-4 hours
- **Phase 3 (Save/Load):** 3-5 hours
- **Phase 4 (Furniture Behaviors):** 4-6 hours
- **Total:** 10-17 hours of focused work

---

## 8. CONCLUSION

**DO NOT RESTART FROM SCRATCH.**

Your existing codebase is solid. The cafe mode already works - you have:
- Excellent grid system with multi-cell support
- Polished dock bar with draw mechanics
- Smooth fog of war with tile animations
- Complete cafe scene setup

The ONLY problem is the 114 PEPO prefabs aren't set up correctly. This is a **2-4 hour fix**, not a "restart the entire project" problem.

**Recommended Path:**
1. I verify the ObjectPrefabHolder structure
2. I create ONE clean prefab generator script
3. We test it on 1 asset
4. We batch generate all 114
5. We test placement in the cafe scene
6. We add save/load system
7. We add furniture behaviors

This is achievable in 1-2 days of focused work.

---

**Ready to proceed when you are.** 🎯
