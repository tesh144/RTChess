# UI Setup Verification Checklist

This checklist helps you verify that the code changes are synced up and ready for Part 2 (creating UI in Unity Editor).

---

## ✅ Part 1: Code Changes (COMPLETED)

### GameSetup.cs Modifications
- [x] **SetupUI()** - No longer creates UI at runtime, just ensures EventSystem exists
- [x] **SetupDockBar()** - Finds existing DockBarManager, HandManager, DragDropHandler instead of creating
- [x] **SetupDebugPanel()** - Simplified to just find existing DebugPanel and HiddenDebugButton
- [x] **SetupDebugMenu()** - Finds existing DebugMenu and initializes
- [x] **SetupDebugPlacer()** - Finds existing CellDebugPlacer and assigns prefabs
- [x] **SetupGameOverManager()** - Finds existing GameOverManager and initializes
- [x] All old UI creation code removed (~350+ lines deleted)

### Documentation Created
- [x] **SETUP_UI_HIERARCHY.md** - Complete guide for all UI components
- [x] **SETUP_SPAWN_TIMELINE_UI.md** - Detailed timeline setup instructions
- [x] **VERIFICATION_CHECKLIST.md** - This file

---

## 📋 Part 2: Unity Editor Setup (NEXT STEPS)

Follow these guides in order:

### Step 1: Essential UI Components (Required for game to run)
**Guide:** [SETUP_UI_HIERARCHY.md](SETUP_UI_HIERARCHY.md)

**Create these first:**
1. [ ] UICanvas with EventSystem
2. [ ] IntervalBarBackground (with IntervalBarFill, IntervalText, IntervalUI component)
3. [ ] TokenContainer (with TokenIcon, TokenText, TokenUI component)
4. [ ] DragDropHandler GameObject (standalone, not under Canvas)
5. [ ] HandManager GameObject (standalone, not under Canvas)
6. [ ] DockBarManager GameObject (under UICanvas, has DockBarManager component)

**Expected Console Output (when these are created):**
```
SetupUI: UI should be manually created in scene hierarchy
SetupDockBar: Found and initialized dock bar components
```

**If missing, you'll see warnings like:**
```
DragDropHandler not found in scene. Please create it manually.
HandManager not found in scene. Please create it manually.
```

### Step 2: Spawn Timeline (Required for wave system)
**Guide:** [SETUP_SPAWN_TIMELINE_UI.md](SETUP_SPAWN_TIMELINE_UI.md)

**Create:**
1. [ ] SpawnTimelineContainer (under UICanvas)
2. [ ] WaveNumberText
3. [ ] DotContainer
4. [ ] CountdownText
5. [ ] SpawnTimelineUI component with all references assigned

**When created, WaveManager will automatically call:**
```csharp
SpawnTimelineUI.Instance.InitializeWave(waveNumber, spawnCode);
```

### Step 3: Optional Debug Components (Can skip for now)
**Guide:** [SETUP_UI_HIERARCHY.md](SETUP_UI_HIERARCHY.md) - Section 8

**Create if needed:**
1. [ ] DebugPanel (complex, many buttons - can skip)
2. [ ] HiddenDebugButton (top-right corner tap button)
3. [ ] DebugMenu
4. [ ] DebugPlacer

**If skipped, you'll see warnings but game will run:**
```
DebugPanel not found in scene. Debug panel will not be available.
```

### Step 4: Game Over Manager (Optional)
1. [ ] GameOverManager GameObject (standalone)
2. [ ] GameOverManager component

---

## 🔍 Verification Tests

### Test 1: Scene Opens Without Errors
1. Open `Assets/Scenes/SampleScene.unity`
2. Check Console for errors (NOT warnings, warnings are expected)
3. **Expected:** No red errors, only yellow warnings about missing UI

### Test 2: Minimal Setup Test
Create only the essential components:
- UICanvas with EventSystem
- HandManager GameObject
- DragDropHandler GameObject
- DockBarManager GameObject under UICanvas

**Enter Play Mode:**
- Game should start
- No red errors (only warnings about missing interval bar, token display, etc.)
- Can't place units yet (dock bar needs references assigned)

### Test 3: Full UI Test
After creating all UI from guides:

**Enter Play Mode:**
- Interval timer bar appears on left edge, fills vertically
- Token count displays in top-right corner (starts at 10)
- Dock bar appears at bottom (initially empty until you draw units)
- Spawn timeline appears at top-center when wave starts
- No warnings in console about missing components

**Gameplay:**
- Interval bar fills every 2 seconds
- Can draw units from dock bar (costs tokens)
- Can drag units from dock to grid
- Timeline shows wave progress with colored dots

---

## 🐛 Common Issues & Solutions

### Issue: "NullReferenceException" in IntervalUI or TokenUI
**Cause:** Component exists but references not assigned
**Fix:**
1. Select IntervalBarBackground in Hierarchy
2. In Inspector, find IntervalUI component
3. Drag IntervalText and IntervalBarFill into the fields

### Issue: "DockBarManager.Initialize() NullReferenceException"
**Cause:** DockBarManager exists but Canvas reference is missing
**Fix:** Ensure DockBarManager is a child of UICanvas, not standalone

### Issue: Timeline never appears
**Cause:** SpawnTimelineUI component missing or references not assigned
**Fix:**
1. Create SpawnTimelineContainer under UICanvas
2. Add SpawnTimelineUI component
3. Assign all three references (WaveNumberText, DotContainer, CountdownText)

### Issue: "HandManager not found" warning
**Cause:** HandManager GameObject doesn't exist in scene
**Fix:** Create empty GameObject named "HandManager", add HandManager component

### Issue: Can't place units from dock bar
**Cause:** DockBarManager not initialized with HandManager reference
**Fix:** Ensure DockBarManager.Initialize(canvas, handManager) is called
- This happens automatically if both components exist in scene

---

## 📊 Expected Console Output (Clean Setup)

When all required UI is created, you should see:
```
SetupUI: UI should be manually created in scene hierarchy
SetupDockBar: Found and initialized dock bar components
SetupDebugPanel: Checked for debug panel components [with warnings if debug components skipped]
SetupDebugMenu: Debug menu will not be available [if skipped]
SetupDebugPlacer: Found and configured debug placer [if created]
SetupGameOverManager: Found and initialized game over manager [if created]
```

**Warnings are OK for optional components (DebugPanel, DebugMenu, etc.)**
**Red errors mean something is wrong - check the error message**

---

## 🎯 Minimum Required Setup (Game Can Run)

If you want to test quickly, create only these:

1. **UICanvas** (with EventSystem)
2. **HandManager** GameObject with HandManager component
3. **DragDropHandler** GameObject with DragDropHandler component
4. **DockBarManager** GameObject under UICanvas with DockBarManager component

This lets the game start. You won't see interval bar, tokens, or timeline, but the core systems will initialize.

Then add other UI components incrementally as you test.

---

## 📝 Current Status

**Code Changes:** ✅ Complete
- All GameSetup methods converted to find existing components
- All old UI creation code removed
- Documentation guides created

**Next Step:** Create UI manually in Unity Editor following guides
**Start Here:** [SETUP_UI_HIERARCHY.md](SETUP_UI_HIERARCHY.md) - Step 1

---

## 🚀 Quick Start for Part 2

1. Open Unity and load `SampleScene.unity`
2. Open [SETUP_UI_HIERARCHY.md](SETUP_UI_HIERARCHY.md)
3. Follow Step 1 (Canvas)
4. Follow Step 2 (EventSystem)
5. Follow Step 3 (Interval Timer Bar)
6. Follow Step 4 (Token Display)
7. Continue through remaining steps...
8. Test in Play Mode after each major component

Take your time and verify each component is correctly configured before moving to the next!
