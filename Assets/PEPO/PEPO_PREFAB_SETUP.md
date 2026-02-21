# PEPO Prefab Generation System

## Quick Start

1. **Open Unity**
2. **Menu:** `Tools → PEPO → Generate Prefabs`
3. **Click:** "Generate All Prefabs"
4. **Done!** 114 prefabs created in `Assets/Prefabs/PEPO/`

---

## What Gets Created

### Folder Structure
```
Assets/Prefabs/PEPO/
├── Indoor/    (39 prefabs: tables, chairs, beds, lamps, decor)
├── Outdoor/   (40 prefabs: structures, props, tools, fences)
└── Nature/    (35 prefabs: trees, flowers, plants, rocks)
```

### Prefab Components

Each PEPO prefab includes:

✅ **Outline** - For selection highlighting
- Component: `Outline.cs`
- Default: Invisible (enable on selection)
- Color: White
- Width: Adjustable

✅ **Animator** - Drop/placement animation
- Controller: `PlaceableObject.controller`
- Animation: `Unit_Appear.anim` (existing drop animation)
- Plays automatically when placed

✅ **Transform Structure**
```
Root GameObject
└── ModelHolder (rotated -90° X for FBX correction)
    └── FBX Model Instance
```

❌ **NO Audio Source** - Handled centrally by `SFXManager.PlayPlayerPlacement()`
❌ **NO World Rotation Lock** - Not needed for static objects

---

## Audio System

### Current Setup (Already Working!)

**Your existing DragDropHandler** already plays centralized placement audio:

```csharp
// Line 276-277 in DragDropHandler.cs
if (SFXManager.Instance != null)
    SFXManager.Instance.PlayPlayerPlacement();
```

**Benefits:**
- ✅ Centralized (one AudioSource, not 114)
- ✅ Efficient memory usage
- ✅ Consistent sound across all objects
- ✅ Easy to adjust volume/pitch globally

**Sound File:** `Assets/Resources/drop_sfx.wav`

### Alternative Option (PlacementAudioManager)

I also created `PlacementAudioManager.cs` as a backup/alternative system if you want to separate placement audio from general SFX:

**To use:**
1. Create empty GameObject in scene
2. Add `PlacementAudioManager` component
3. Assign `drop_sfx` audio clip
4. Call `PlacementAudioManager.Instance.PlayPlacementSound()` from placement code

**Features:**
- Pitch variation for variety
- 2D or 3D spatial audio options
- Independent from SFXManager

---

## Component Details

### 1. Outline Component

**Purpose:** Visual selection feedback (highlight when clicked/hovered)

**Usage:**
```csharp
// Enable outline on selection
outline.OutlineWidth = 5f;

// Disable outline
outline.OutlineWidth = 0f;
```

**Customization:**
- Change color: `outline.OutlineColor = Color.cyan;`
- Adjust width: `outline.OutlineWidth = 3f;`
- Change mode: `outline.OutlineMode = Outline.Mode.OutlineAll;`

---

### 2. Animator Component

**Purpose:** Plays drop animation when object is placed

**Animation Flow:**
1. Object instantiated
2. Animator auto-plays `Unit_Appear` animation
3. Object rotates/bounces into place (~1.3 seconds)
4. Animation completes, object ready

**Controller:** `Assets/Prefabs/PlaceableObject.controller`

**Customization:**
- Speed: Adjust animator speed parameter
- Custom animation: Replace in PlaceableObject controller
- Disable: Remove Animator component from prefab

---

## World Rotation Lock (NOT INCLUDED)

**What it does:** Keeps X/Z rotation fixed while allowing Y rotation (for units that face different directions)

**Why it's not on PEPO objects:**
- Furniture/props/trees are static
- Don't rotate to face enemies
- Don't need orientation locking

**If you need it:** Add manually to specific prefabs that should rotate on grid

---

## Regenerating Prefabs

### Clear & Regenerate All
1. Open generator window
2. Click "Clear Existing Prefabs" (⚠️ careful!)
3. Confirm deletion
4. Click "Generate All Prefabs"

### Selective Regeneration
- Delete individual prefabs manually
- Run generator (skips existing prefabs)
- Only missing prefabs are created

---

## Troubleshooting

### Issue: Prefabs have no Outline
**Fix:** Outline component from QuickOutline asset. Ensure `Assets/QuickOutline/` exists.

### Issue: Animation doesn't play
**Fix:** Check `PlaceableObject.controller` references `Unit_Appear.anim` correctly.

### Issue: Wrong orientation
**Fix:** Adjust ModelHolder rotation in prefab (currently -90° X axis).

### Issue: Textures missing
**Fix:** Textures in `Assets/PEPO/.../Textures/` folders. Unity should auto-find them.

---

## Files Created

### Scripts
- `/Assets/Editor/PEPOPrefabGenerator.cs` - Generator tool
- `/Assets/Scripts/Core/PlacementAudioManager.cs` - Alternative audio system (optional)

### Assets
- `/Assets/Prefabs/PlaceableObject.controller` - Animator controller
- `/Assets/Prefabs/PEPO/` - All generated prefabs (114 total)

### Documentation
- `/Assets/PEPO/PEPO_PREFAB_SETUP.md` - This file

---

## Next Steps

1. ✅ Generate prefabs using the tool
2. Test drag-and-drop from dock to grid
3. Verify drop animation plays
4. Check outline highlights on selection
5. Confirm audio plays (via SFXManager)
6. Adjust Outline colors/widths as needed
7. Tweak animation speed if desired

---

## Notes

- **Pivot Alignment:** All prefabs aligned at (0,0,0) like soldier prefabs
- **Grid Compatibility:** Ready to use with existing GridManager
- **Performance:** No per-prefab AudioSources (efficient!)
- **Consistency:** All objects use same drop animation (polished feel)
- **Extensibility:** Easy to add more components via generator script

---

**Created:** 2026-02-16
**Tool:** PEPOPrefabGenerator
**Source Assets:** 114 FBX models in Assets/PEPO/
