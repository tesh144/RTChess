# 🚀 START HERE - Little Cafe Phase 1

**Welcome VS Code Claude!** This guide tells you exactly what to do to start implementing Phase 1.

---

## 📚 Step 0: Read These Documents (In Order)

1. **README.md** ← Understand the project structure
2. **Prompts/kitchen-builder-architecture.md** ← Understand the system design
3. **Prompts/phase-1-implementation-guide.md** ← Your detailed implementation guide
4. **phase-1-checklist.md** ← Track your progress

**Optional (for context):**
- References/little-cafe-handoff.md (original requirements)
- Prompts/little-cafe-unity-prompts.md (all phases overview)
- Diagrams/little-cafe-design-diagram-correct.html (visual reference)

---

## 🎯 Your Mission: Build the Kitchen Builder

**What you're building:**
An in-game drag-and-drop system where players design custom cafe layouts, save them, and use them during gameplay.

**Think:** The Sims build mode, but for restaurant kitchens.

**NOT a dev tool** - this is a core gameplay feature!

---

## ✅ Quick Start Checklist

**Before you start coding:**

- [ ] Open the Unity project at `/Users/jai/RTChess`
- [ ] Verify RTChess scene still works (don't break it!)
- [ ] Locate `GridManager.cs` in RTChess/Scripts/Core/
- [ ] Understand how RTChess grid system works
- [ ] Find `IsometricCamera` setup

**First steps:**

1. [ ] Create folder structure (see phase-1-implementation-guide.md Step 1)
2. [ ] Create data structures (LayoutData, EquipmentData, etc.)
3. [ ] Create Equipment base class
4. [ ] Create concrete equipment classes (CookingStation, etc.)
5. [ ] Create equipment prefabs (simple colored cubes)
6. [ ] Create new scene: LittleCafe.unity
7. [ ] Setup scene hierarchy (GridManager, Camera, UI)
8. [ ] Create LayoutManager
9. [ ] Create GameModeManager
10. [ ] Create EquipmentPalette UI
11. [ ] Create EquipmentPlacer (drag-drop logic)
12. [ ] Test placement
13. [ ] Implement save/load
14. [ ] Test mode switching
15. [ ] Create reference kitchen layout

---

## 🎨 Visual Quick Reference

**Equipment Colors:**
- 🔴 Cooking Station: #FF6B6B
- 🟢 Serving Counter: #4FB05D
- 🔵 Washing Station: #6BCBFF
- 🌸 Plate Rack: #FF69B4
- ⬛ Wall: #2D2D2D
- 🟡 Door: #FFD93D

**Zone Colors:**
- Kitchen (rows 0-4): #FFDDB3 (peach)
- Dining (rows 6-11): #CAFFBF (light green)

**Grid Size:** 15x15

---

## 🎬 Scene Structure Template

```
LittleCafe Scene
├── GridManager (reference from RTChess)
├── IsometricCamera (reference from RTChess)
├── GameSetup
│   ├── GameModeManager (component)
│   └── LayoutManager (component)
├── BuildModeCanvas (UI)
│   ├── EquipmentPalette
│   │   ├── CookingStationButton
│   │   ├── CounterButton
│   │   ├── SinkButton
│   │   ├── PlateRackButton
│   │   ├── WallButton
│   │   └── DoorButton
│   ├── SaveButton
│   ├── LoadButton
│   ├── ClearButton
│   └── StartServiceButton
└── PlayModeCanvas (UI, inactive)
    └── (empty for Phase 1)
```

---

## 💡 Key Implementation Notes

**1. RTChess Integration:**
- **DO:** Reuse GridManager, IsometricCamera
- **DON'T:** Modify RTChess code
- **ADAPT:** Your placement logic to RTChess's grid API

**2. For Phase 1, Keep It Simple:**
- Colored cubes only (no fancy models)
- Basic UI (Unity defaults)
- No animations
- No sounds
- Focus on functionality

**3. Most Important Systems:**
- LayoutManager (save/load)
- GameModeManager (Build/Play switching)
- EquipmentPlacer (drag-drop)

**4. Test Frequently:**
- After each major component, test it
- Build incrementally
- Don't wait until the end to test

---

## 🐛 Common First-Time Issues

**Problem:** "GridManager not found"
- **Fix:** Check RTChess namespace. You may need to add `using RTChess.Core;` or similar

**Problem:** "Equipment doesn't appear when placed"
- **Fix:** Check prefab assignments in LayoutManager Inspector

**Problem:** "Can't click equipment"
- **Fix:** Equipment needs colliders for raycasting

**Problem:** "Ghost preview doesn't follow cursor"
- **Fix:** Verify Main Camera is tagged "MainCamera"

**Problem:** "Save/Load crashes"
- **Fix:** Ensure Layouts folder exists, check JSON serialization

---

## 🎯 Your First Goal

**By end of first session, you should have:**

1. ✅ Folder structure created
2. ✅ LittleCafe.unity scene set up
3. ✅ At least one equipment type (CookingStation) placeable
4. ✅ Can see equipment appear on grid when clicked

**That proves the system works!** Then expand to other equipment types.

---

## 📞 Need Help?

If you get stuck:

1. **Check the implementation guide** - it has detailed code examples
2. **Check RTChess code** - see how they do grid placement
3. **Check the checklist** - make sure you haven't skipped a step
4. **Test in isolation** - create a simple test case

**Remember:** The goal is a working prototype, not perfect code. Make it work first, polish later.

---

## 🏁 Definition of Done (Phase 1)

Phase 1 is complete when:

- [ ] Can design a custom kitchen in Build Mode
- [ ] Can save that kitchen to JSON
- [ ] Can load that kitchen from JSON
- [ ] Can switch to Play Mode (layout persists)
- [ ] Can switch back to Build Mode
- [ ] Can recreate the reference kitchen layout exactly
- [ ] No critical bugs

**Then you're ready for Phase 2: Character AI!**

---

## 🚀 Ready? Let's Build!

Start by reading the implementation guide, then dive into Step 1 (folder structure). Good luck! 🎉

**Remember:** This is a player-facing feature, not just a dev tool. Make it feel good to use!
