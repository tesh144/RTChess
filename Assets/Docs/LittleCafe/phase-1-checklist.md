# Phase 1: Kitchen Builder - Implementation Checklist

**Start Date:** _______
**Target Completion:** _______

---

## 📁 Folder Structure

- [ ] Created `Assets/LittleCafe/` root folder
- [ ] Created `Scripts/Equipment/` subfolder
- [ ] Created `Scripts/Builder/` subfolder
- [ ] Created `Scripts/Managers/` subfolder
- [ ] Created `Scripts/Data/` subfolder
- [ ] Created `Prefabs/Equipment/` subfolder
- [ ] Created `Materials/` subfolder
- [ ] Created `UI/` subfolder
- [ ] Created `Scenes/` subfolder
- [ ] Created `StreamingAssets/Layouts/` folder

---

## 🎨 Data Structures

- [ ] `LayoutData.cs` - Main layout container
- [ ] `EquipmentData.cs` - Equipment serialization
- [ ] `GridPosition.cs` - Row/col position struct
- [ ] `ZoneData.cs` - Kitchen/dining zones
- [ ] `QueueData.cs` - Queue zone definitions

---

## 🔧 Equipment Classes

- [ ] `Equipment.cs` - Base equipment class
- [ ] `CookingStation.cs` - Red cube
- [ ] `ServingCounter.cs` - Green cube
- [ ] `WashingStation.cs` - Blue cube
- [ ] `PlateRack.cs` - Pink cube
- [ ] `Wall.cs` - Black cube
- [ ] `Door.cs` - Yellow cube

---

## 🎮 Core Systems

- [ ] `GameModeManager.cs` - Mode switching (Build/Play)
- [ ] `LayoutManager.cs` - Save/load system
- [ ] `EquipmentPlacer.cs` - Drag-drop placement logic
- [ ] `EquipmentPalette.cs` - UI palette controller
- [ ] `GridZoneRenderer.cs` - Visual zone coloring (optional)

---

## 🎬 Scene Setup

- [ ] Created `LittleCafe.unity` scene
- [ ] Added GridManager reference (from RTChess)
- [ ] Added IsometricCamera reference (from RTChess)
- [ ] Added GameSetup GameObject
- [ ] Added GameModeManager component
- [ ] Added LayoutManager component
- [ ] Created BuildModeCanvas
- [ ] Created equipment palette UI (buttons)
- [ ] Created Save button
- [ ] Created Load button
- [ ] Created Clear button
- [ ] Created Start Service button
- [ ] Created PlayModeCanvas (empty placeholder)

---

## 🎨 Prefabs

- [ ] CookingStation prefab (red cube)
- [ ] ServingCounter prefab (green cube)
- [ ] WashingStation prefab (blue cube)
- [ ] PlateRack prefab (pink cube)
- [ ] Wall prefab (black cube)
- [ ] Door prefab (yellow cube)
- [ ] All prefabs assigned in LayoutManager Inspector

---

## 💾 Save/Load System

- [ ] SaveCurrentLayout() saves to JSON
- [ ] LoadLayout() loads from JSON
- [ ] BuildLayoutDataFromScene() serializes current state
- [ ] InstantiateLayout() deserializes and spawns equipment
- [ ] ClearCurrentLayout() removes all equipment
- [ ] Layouts save to `persistentDataPath/Layouts/`
- [ ] Default layouts load from `StreamingAssets/Layouts/`
- [ ] Created `reference_kitchen.json` in StreamingAssets

---

## 🖱️ Placement System

- [ ] Click equipment button to select
- [ ] Ghost preview follows cursor
- [ ] Left-click places equipment
- [ ] Equipment snaps to grid
- [ ] Right-click removes equipment
- [ ] Can't place on occupied cells
- [ ] Visual feedback for valid/invalid placement

---

## 🎨 Visual Zones

- [ ] Kitchen zone floor (rows 0-4, peach tint)
- [ ] Dining zone floor (rows 6-11, light green tint)
- [ ] Chef queue visual (col 0, rows 0-4, orange tint)
- [ ] Waiter queue visual (col 14, rows 6-11, teal tint)
- [ ] Customer queue visual (row 13, cols 1-5, purple tint)

---

## 🔄 Mode Switching

- [ ] Game starts in Build Mode
- [ ] "Start Service" switches to Play Mode
- [ ] Build UI hides in Play Mode
- [ ] Play UI shows in Play Mode
- [ ] Layout auto-saves when switching to Play
- [ ] "Edit Kitchen" switches back to Build Mode
- [ ] Layout persists between mode switches

---

## ✅ Functional Tests

- [ ] Can place cooking station at (2,5)
- [ ] Can place serving counter at (2,7) through (2,10)
- [ ] Can place washing station at (2,11)
- [ ] Can place plate rack at (1,11)
- [ ] Can place walls at row 5 (except doors)
- [ ] Can place doors at (5,7) and (5,8)
- [ ] Can remove any placed equipment
- [ ] Can save layout to `player_kitchen.json`
- [ ] Can load saved layout
- [ ] Can clear all equipment
- [ ] Can switch to Play Mode
- [ ] Can switch back to Build Mode
- [ ] Layout matches after load

---

## 🎯 Reference Layout Test

**Create exact reference kitchen:**

- [ ] Plate Rack at (1, 11)
- [ ] Cooking Station at (2, 5)
- [ ] Serving Counter at (2, 7)
- [ ] Serving Counter at (2, 8)
- [ ] Serving Counter at (2, 9)
- [ ] Serving Counter at (2, 10)
- [ ] Washing Station at (2, 11)
- [ ] Cooking Station at (3, 5)
- [ ] Cooking Station at (4, 5)
- [ ] Walls at row 5 (cols 0-6, 9-14)
- [ ] Doors at (5, 7) and (5, 8)
- [ ] Walls at row 12 (cols 0-6, 9-14)
- [ ] Doors at (12, 7) and (12, 8)
- [ ] Save as `reference_kitchen.json`
- [ ] Copy to StreamingAssets/Layouts/
- [ ] Verify loads correctly

---

## 🐛 Bug Fixes & Polish

- [ ] No console errors on scene load
- [ ] No console errors when placing equipment
- [ ] No console errors when saving/loading
- [ ] Equipment positions exactly match grid cells
- [ ] Ghost preview updates smoothly
- [ ] UI buttons all work correctly
- [ ] Mode switching is instant (no lag)
- [ ] Equipment colors match specification

---

## 📝 Documentation

- [ ] Code comments on key methods
- [ ] Inspector tooltips on public fields
- [ ] Debug logs for save/load operations
- [ ] README with setup instructions

---

## 🎉 Phase 1 Complete When:

- [ ] All items above are checked
- [ ] Can design custom kitchen
- [ ] Can save and load layouts
- [ ] Can switch between Build/Play modes
- [ ] Reference kitchen can be recreated exactly
- [ ] No critical bugs
- [ ] Ready to start Phase 2 (Character AI)

---

**Notes:**

_Use this space for issues, questions, or reminders_

```
[Your notes here]
```
