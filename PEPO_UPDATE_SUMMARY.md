# PEPO Prefab Update - Unlit Materials & Animated Shadows

## Changes Made

### 1. Unlit Shader Conversion
- All PEPO prefab materials now use **Unlit/Texture** shader instead of Standard
- Preserves original textures and colors
- Provides consistent, flat-shaded aesthetic for card game style
- No more lighting dependency

### 2. Animated Blob Shadows
- Added **Shadow** GameObject as child of root (separate from ModelHolder)
- Quad mesh positioned at Y=0.01 (prevents z-fighting)
- Unlit/Transparent material with black color
- **Fades in during drop animation**: 0% → 50% opacity over 0.5 seconds
- Shadow rotated 90° on X to face downward
- Scale: 0.8 × 0.8 (slightly smaller than object footprint)

### 3. Updated Animation
**PEPO_Drop.anim** now includes:
- Position animation (Y: 2 → 0.1 → 0) with bounce
- Scale animation (0.5 → 1.05 → 1.0) with overshoot
- **NEW**: Shadow material alpha animation (0 → 0.5)
- Duration: 0.5 seconds, non-looping

## File Structure of Generated Prefabs

```
[PrefabName]  (Root)
├── Shadow  (Quad mesh with fade-in animation)
└── ModelHolder
    └── [FBX Model Instance]  (Unlit materials)

Components on Root:
- Outline (selection highlighting)
- Animator (with PEPO_Drop animation)
```

## Files Modified

### Editor Scripts
- `Assets/Editor/PEPOPrefabGeneratorBatch.cs`
  - Added `CreateBlobShadow()` method
  - Added `ConvertMaterialsToUnlit()` method
  - Updated `CreatePrefabFromFBX()` to include shadow and material conversion

- `Assets/Editor/PEPOPrefabGenerator.cs`
  - Same changes as batch version
  - Editor window UI unchanged

### Animation
- `Assets/Prefabs/PEPO_Drop.anim`
  - Added material alpha curve for Shadow/MeshRenderer
  - Fades from 0 to 0.5 over 0.5 seconds

## How to Regenerate Prefabs

### Option 1: Unity Editor Menu (Recommended)
1. Open Unity Editor
2. Go to **Tools > PEPO > Generate Prefabs (Batch Mode)**
3. Wait for generation to complete (114 prefabs)
4. Check Console for confirmation message

### Option 2: Bash Script
```bash
cd /sessions/gracious-gallant-mayer
./regenerate_pepo_prefabs.sh
```

### Option 3: Manual Unity Batch Mode
```bash
/Applications/Unity/Hub/Editor/[VERSION]/Unity.app/Contents/MacOS/Unity \
    -quit \
    -batchmode \
    -projectPath /sessions/gracious-gallant-mayer/mnt/RTChess \
    -executeMethod PEPOPrefabGeneratorBatch.GenerateAll \
    -logFile -
```

## Technical Details

### Shadow Fade Animation Curve
```
Time 0.0: Alpha = 0.0 (fully transparent)
Time 0.5: Alpha = 0.5 (50% opacity)
```

### Material Conversion
- **Before**: Standard shader with lighting calculations
- **After**: Unlit/Texture shader with direct texture display
- Textures and colors preserved during conversion
- All child MeshRenderers processed recursively

### Shadow Rendering Settings
- Shadow casting: Off
- Receive shadows: Off
- Render queue: Transparent
- Z-test: Normal

## Benefits

1. **Performance**: Unlit materials are cheaper to render (no lighting calculations)
2. **Visual Consistency**: Objects look the same regardless of lighting
3. **Shadow Control**: Precise control over shadow appearance and animation
4. **No Z-Fighting**: Shadow offset at Y=0.01 prevents flickering
5. **Polish**: Smooth shadow fade-in adds professional touch

## Notes

- Existing prefabs have been deleted; regeneration required
- Animation works immediately on placement (no manual triggering)
- Shadow quad has no collider (doesn't interfere with gameplay)
- Works with existing DragDropHandler placement system
- Compatible with existing Outline selection system

## Testing

After regeneration, test by:
1. Drawing units into dock bar
2. Dragging to grid
3. Watch for smooth drop animation with shadow fade-in
4. Verify no shadow flickering underneath objects
5. Check that textures display correctly (unlit)
