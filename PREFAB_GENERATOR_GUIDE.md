# Prefab Generator Tool - Setup Guide

## Overview
The PrefabGenerator creates properly structured animated object prefabs with a universal animation system that works across all objects.

## Installation ✅
- **Location:** `Assets/Scripts/Editor/PrefabGenerator.cs`
- **Status:** Installed and ready to use
- **Access:** `Tools → Prefab Generator` (after Unity compiles the script)

---

## Quick Start

### 1. Prepare Your Assets

Before using the tool, ensure you have:

- [ ] **Source FBX/Objects**: Original assets in a folder (e.g., `Assets/Models/PEPO/`)
- [ ] **Unlit Material**: Create or locate an unlit material (e.g., `Assets/Materials/UnlitMaterial.mat`)
- [ ] **Animator Controller**: Your shared animator controller (e.g., `Assets/Prefabs/PlaceableObject.controller`)

### 2. Open the Tool

1. In Unity, go to **Tools → Prefab Generator**
2. A window will appear with configuration options

### 3. Configure Paths

Fill in these paths in the tool window:

| Field | Example Path | Purpose |
|-------|--------------|---------|
| **Source Folder** | `Assets/Models/PEPO/` | Where your FBX/object assets are |
| **Unlit Material** | `Assets/Materials/UnlitMaterial.mat` | Material to apply to all objects |
| **Animator Controller** | `Assets/Prefabs/PlaceableObject.controller` | Your animation controller |
| **Output Folder** | `Assets/Prefabs/Generated/` | Where new prefabs will be saved |

### 4. Generate Prefabs

1. Click **"Load Assets"** to scan the source folder
2. Review the list of objects that will be processed
3. Check **"Clean Output Folder"** if you want to delete old prefabs first
4. Click **"Generate Prefabs"** to create all prefabs

---

## Prefab Structure

Each generated prefab follows this hierarchy:

```
[ObjectName]_Prefab (root)
├─ Animator (component with PlaceableObject controller)
├─ Outline (component - needs to be added manually if required)
└─ CharacterHold (GameObject - gets animated)
   └─ CharacterRe:Zero (GameObject - handles pivot corrections)
      └─ [Complete Asset] (e.g., BeachBall01 with full hierarchy)
```

### What This Means

- **Root Level**: Contains Animator with shared controller
- **CharacterHold**: Target for Position/Rotation/Scale animations
- **CharacterRe:Zero**: Corrects assets with incorrect pivot points
- **Asset**: Complete original asset with internal structure preserved

---

## Features

### ✅ Automatic Processing
- Creates standardized hierarchy for every object
- Applies unlit material to all renderers
- Removes shadow objects automatically
- Saves prefabs ready to use

### ✅ Batch Processing
- Process all objects at once
- Progress bar shows status
- Handles hundreds of objects efficiently

### ✅ Clean Output
- Option to delete old prefabs before generating
- Creates output folder if it doesn't exist
- Organized naming: `[ObjectName]_Prefab.prefab`

### ✅ Scene Cleanup
- **"Remove All Shadows from Scene"** button
- Finds and deletes all shadow objects in current scene
- Useful for cleaning up before regenerating

---

## Integration with RTChess

### Current Animation System
RTChess uses **Unity Animator** for placement animations:
- **Controller**: `PlaceableObject.controller`
- **Animation**: `Unit_Appear.anim` (with wobble effect)
- Plays automatically when objects are instantiated

### Using Generated Prefabs

1. **After Generation**: Prefabs are ready to place via DragDropHandler
2. **Add GridObject Component**: Mark grid size (1x1, 2x2, etc.)
3. **Create UnitStats**: Add ScriptableObject for stats if needed
4. **Add to Dock**: Include in DockBarManager's unit pool

### Example Workflow

```
1. Generate prefab with PrefabGenerator
2. Open prefab in Unity
3. Add GridObject component → Set GridSize (1x1, 2x2, etc.)
4. Create UnitStats ScriptableObject for the unit
5. Add to DockBarManager's available units
6. Drag from dock in-game → Placement animation plays automatically
```

---

## Troubleshooting

### "Could not find unlit material"
- Check the path is correct (relative to Assets/)
- Ensure material exists at that location
- Create a simple unlit material if needed

### "Could not find animator controller"
- Verify PlaceableObject.controller exists
- Check path spelling and capitalization
- Use forward slashes (/) not backslashes (\)

### Prefabs look wrong
- Check if CharacterRe:Zero has correct offset values
- Verify unlit material is applied correctly
- Try regenerating with "Clean Output Folder" enabled

### Animations don't play
- Ensure Animator component has PlaceableObject controller assigned
- Check Unit_Appear.anim is in the controller
- Verify "Appear" is set as default state

---

## Advanced Usage

### Custom Pivot Corrections

If an object has incorrect pivot point, you can manually adjust **CharacterRe:Zero**:

1. Open generated prefab
2. Select CharacterRe:Zero
3. Adjust Position/Rotation to re-center the asset
4. Save prefab

This correction will be preserved across regenerations.

### Adding Custom Components

After generation, you can add components to any level:

- **Root**: GridObject, custom scripts
- **CharacterHold**: Effects that should animate with object
- **CharacterRe:Zero**: Offset-specific components
- **Asset**: Direct modifications to model

### Batch Variant Creation

For creating multiple variants of the same base:

1. Generate base prefab
2. Create Prefab Variant (right-click → Create → Prefab Variant)
3. Modify variant (different materials, components, etc.)
4. Animations inherited from base automatically

---

## Next Steps

### Immediate Actions
- [ ] Verify paths in PrefabGenerator match your project
- [ ] Create unlit material if you don't have one
- [ ] Run tool on a small test folder first (2-3 objects)
- [ ] Check generated prefabs look correct
- [ ] Run full batch generation

### After Generation
- [ ] Add GridObject components to prefabs
- [ ] Create UnitStats for gameplay integration
- [ ] Test placement animations work
- [ ] Add prefabs to DockBarManager
- [ ] Test in-game via drag-drop system

---

## Files Included

- `Assets/Scripts/Editor/PrefabGenerator.cs` - The tool script
- `Unity_Project_Handoff.docx` - Full handoff documentation
- `PREFAB_GENERATOR_GUIDE.md` - This guide

---

## Important Notes

⚠️ **Before First Use:**
- Back up your project (or commit to git)
- Test on a small folder first (2-3 objects)
- Verify all paths are correct

⚠️ **Remember:**
- NO shadow objects in generated prefabs
- Complete assets placed under CharacterRe:Zero
- Unlit materials required for all objects
- Work from original FBX files, not existing prefabs

---

**Questions?** The tool is ready to use. Open Unity, go to `Tools → Prefab Generator`, and start creating properly structured prefabs!
