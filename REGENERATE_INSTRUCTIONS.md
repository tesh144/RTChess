# Quick Regeneration Instructions

## All prefabs have been deleted and are ready to regenerate with:
✓ Unlit materials (all textures)
✓ Animated blob shadows (fade to 50%)
✓ No z-fighting (Y=0.01 offset)

## To Generate:

1. **Open Unity Editor** with the RTChess project

2. **Go to the menu**: `Tools > PEPO > Generate Prefabs (Batch Mode)`

3. **Wait** ~30-60 seconds for 114 prefabs to generate

4. **Check Console** for confirmation: "✓ Complete! Created: 114 prefabs"

## Result:
All PEPO prefabs will be in `Assets/Prefabs/PEPO/` organized by:
- Indoor/
- Outdoor/
- Nature/

Each prefab will have the new shadow and unlit material setup automatically applied.

## Test:
- Draw a unit
- Drag to grid
- Watch for smooth drop with shadow fade-in
- Verify no shadow flickering
