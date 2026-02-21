# Fix PEPO Prefab Structure - Complete Guide

## The Problem

Your 114 PEPO prefabs currently have **incorrect structure** that prevents animations from working:

### Current (WRONG) Structure:
```
RockSculp01
├─ Animator ✅
├─ Outline ✅
├─ Shadow ❌ (causes performance issues)
└─ [PEPO model directly here] ❌ (animations can't find CharacterHold)
```

### Required (CORRECT) Structure:
```
RockSculp01
├─ Animator ✅
├─ Outline ✅
└─ CharacterHold ← Animations target this!
   └─ CharacterRe:Zero ← Handles pivot corrections
      └─ [PEPO model nested here]
```

**Why this matters:** The Unit_Appear animation targets `CharacterHold` for position/rotation/scale. Without it, animations won't play!

---

## The Solution

I've created **PrefabRestructureTool** that automatically fixes all prefabs:

### What It Does:
- ✅ Scans all PEPO prefabs to find ones needing fixes
- ✅ Adds CharacterHold and CharacterRe:Zero wrappers
- ✅ Moves existing content under CharacterRe:Zero
- ✅ Removes Shadow objects (performance killers)
- ✅ Keeps Animator and Outline at root level
- ✅ Processes all 114 prefabs in one click

---

## How to Use

### Step 1: BACKUP YOUR PROJECT! ⚠️

```bash
# Option A: Git commit (if using version control)
git add -A
git commit -m "Before prefab restructure"

# Option B: Manual backup
# Copy entire RTChess folder to RTChess_Backup
```

### Step 2: Open Unity

1. Open your RTChess project in Unity
2. Wait for scripts to compile
3. You should see no errors

### Step 3: Run the Tool

1. In Unity menu, go to **Tools → Prefab Restructure Tool**
2. Window will open showing the tool interface

### Step 4: Scan Prefabs

1. Verify path shows: `Assets/Prefabs/PEPO`
2. Click **"Scan for Prefabs to Fix"**
3. Tool will scan all prefabs and show which ones need fixing
4. You should see ~114 prefabs listed

### Step 5: Fix All Prefabs

1. Click **"Fix All Prefabs"** (big button at bottom)
2. Progress bar will show as it processes each prefab
3. Wait for completion dialog
4. Tool will show: "Fixed X prefabs, Failed: 0"

### Step 6: Verify the Fix

1. In Unity Project window, navigate to `Assets/Prefabs/PEPO`
2. Double-click any prefab (e.g., RockSculp01)
3. Check the hierarchy - you should now see:
   ```
   RockSculp01
   └─ CharacterHold
      └─ CharacterRe:Zero
         └─ [Original content here]
   ```
4. Verify Animator is still on root (shows in Inspector)

---

## Testing Animations

After fixing prefabs:

### Test 1: Placement Animation
1. Enter Play mode
2. Drag a PEPO object from dock to grid
3. **Should see:** Unit_Appear animation plays (wobble effect)
4. **If not working:** Check Console for errors

### Test 2: Multiple Objects
1. Place several different PEPO objects
2. All should animate the same way
3. No "Missing Animation" warnings

### Test 3: Combat Animations
1. Place units near resources
2. Units should attack and animate correctly
3. Check Unit_Attack animation plays

---

## What Gets Changed

For each prefab, the tool:

### Adds:
- CharacterHold GameObject (animation target)
- CharacterRe:Zero GameObject (pivot correction)

### Removes:
- Shadow objects (any GameObject with "Shadow" in name)

### Preserves:
- Animator component (stays at root)
- Outline component (stays at root)
- GridObject component (if present, stays at root)
- All materials and meshes
- Original structure of PEPO model (nested under CharacterRe:Zero)

### Doesn't Change:
- File names
- File locations
- Animator controller assignments
- Materials or textures
- Grid size settings

---

## Troubleshooting

### "Folder not found: Assets/Prefabs/PEPO"
**Fix:** Check if PEPO prefabs are in a different folder. Update the path in the tool.

### "Fixed 0 prefabs"
**Possible causes:**
1. Prefabs already have correct structure (scan again to verify)
2. Wrong folder path (check folder location)
3. Prefabs aren't actually prefabs (should have .prefab extension)

### Animations still don't play after fix
**Check:**
1. Animator has PlaceableObject controller assigned
2. Unit_Appear animation exists in controller
3. "Appear" is default state in animator
4. Console for any error messages

### Some prefabs look different after fix
**This is expected if:**
- Shadow objects were removed (magenta blobs gone = good!)
- Pivot point changed slightly (adjust CharacterRe:Zero position/rotation)

---

## Advanced: Manual Fix (if tool fails)

If the tool fails on specific prefabs, fix manually:

1. Open prefab in Unity
2. Create new GameObject "CharacterHold" at root
3. Create new GameObject "CharacterRe:Zero" under CharacterHold
4. Move all children (except Animator/Outline) under CharacterRe:Zero
5. Delete any Shadow objects
6. Save prefab
7. Apply changes

---

## After Fixing

### Immediate Next Steps:
- [ ] Test placement animations work
- [ ] Verify all PEPO objects animate correctly
- [ ] Check no Shadow objects remain
- [ ] Commit changes to version control (if using git)

### Future Workflow:
When adding NEW PEPO objects, use **PrefabGenerator** tool instead:
- It creates prefabs with correct structure from the start
- Located at: Tools → Prefab Generator
- See PREFAB_GENERATOR_GUIDE.md for instructions

---

## Files Created

- **PrefabRestructureTool.cs** - The fixing tool (Assets/Scripts/Editor/)
- **PREFAB_FIX_GUIDE.md** - This guide
- **PREFAB_GENERATOR_GUIDE.md** - For creating NEW prefabs

---

## Summary

**Before:** 114 prefabs with broken structure, Shadow objects, animations don't work
**After:** All prefabs have CharacterHold/CharacterRe:Zero structure, animations work universally!

**Time to fix:** ~2-3 minutes total
**Manual work saved:** Would take hours to fix 114 prefabs by hand!

---

**Ready?** Open Unity and run **Tools → Prefab Restructure Tool** to fix all your prefabs!
