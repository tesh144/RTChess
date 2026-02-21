# Grid Object Scaling Guide

## Overview

Your RTChess game uses a **1.5 Unity unit** grid cell size (defined in `GridManager.cs`). This guide explains how to scale game objects to fit grid cells properly using the new Grid Scaling Toolkit.

---

## 🎯 Quick Start

### For a Single Object:

1. **Open Grid Object Scaler:**
   - Menu: `Tools → RTChess → Grid Object Scaler`

2. **Select your object** in the Scene or Hierarchy

3. **The tool will auto-suggest a grid size** based on current bounds

4. **Click "Apply Scale"** to fit it to the grid

5. **Done!** Green grid overlay confirms proper alignment

### For Many Objects (Batch):

1. **Open Grid Batch Scaler:**
   - Menu: `Tools → RTChess → Grid Batch Scaler`

2. **Set folder path** (e.g., `Assets/Prefabs`)

3. **Click "Process All Prefabs"**

4. **Review results** - all prefabs now scaled to grid cells

---

## 📦 Tools Included

### 1. Grid Object Scaler (Window)
**Location:** `Tools → RTChess → Grid Object Scaler`

**Use for:** Manually scaling individual objects with visual feedback

**Features:**
- ✅ Visual grid overlay in Scene view
- ✅ Auto-suggested slot size based on bounds
- ✅ Manual override (e.g., force to 2×2)
- ✅ Quick presets (1×1, 2×2, 3×3, 1×2)
- ✅ Real-time scale preview
- ✅ Green target box shows final size

**Workflow:**
```
1. Select object → Tool auto-calculates size
2. Adjust size if needed (or use preset)
3. Click "Apply Scale" or "Fit to Bounds"
4. Green box in Scene = perfectly aligned!
```

---

### 2. GridObject Component
**Location:** Add via `Add Component → Grid Object`

**Use for:** Marking objects with their intended grid size

**Features:**
- ✅ Stores grid size (e.g., 2×2 cells)
- ✅ Shows green/yellow gizmo (green = properly scaled)
- ✅ Quick-resize buttons in Inspector
- ✅ Auto-detect grid size from bounds

**Inspector Buttons:**
- **Fit to Grid Size:** Scales object to match grid size setting
- **Auto-Detect Size:** Calculates grid size from current bounds
- **Size Presets:** Quick buttons (1×1, 2×2, 3×3, 1×2, 2×1)

**Visual Feedback:**
- **Green wireframe:** Object is properly scaled ✓
- **Yellow wireframe:** Object needs rescaling ⚠️
- **Cyan corner markers:** Show grid cell boundaries

---

### 3. Grid Batch Scaler (Window)
**Location:** `Tools → RTChess → Grid Batch Scaler`

**Use for:** Processing many prefabs at once

**Features:**
- ✅ Batch process entire folders
- ✅ Auto-add GridObject component if missing
- ✅ Progress bar for large batches
- ✅ Detailed results log
- ✅ Undo support (prefabs are saved)

**Settings:**
- **Prefab Folder:** Path to process (e.g., `Assets/Prefabs`)
- **Include Subfolders:** Search recursively
- **Only Process with GridObject:** Skip prefabs without component
- **Auto-Add GridObject:** Add component and auto-detect size

**Results:**
- ✓ Success: Prefab scaled successfully
- ✗ Failed: Error message (e.g., no renderers)
- Grid size shown (e.g., 2×2)

---

## 🔧 Recommended Workflow

### Initial Setup (All Prefabs):

1. **Run Batch Scaler once:**
   ```
   Tools → RTChess → Grid Batch Scaler
   - Folder: Assets/Prefabs
   - Auto-Add GridObject: ✓
   - Click "Process All Prefabs"
   ```

2. **Review results:**
   - Green ✓ = Good
   - Red ✗ = Needs manual fix

3. **Fix problem prefabs manually:**
   - Open prefab
   - Use Grid Object Scaler window
   - Adjust size if auto-detect was wrong

### Creating New Objects:

1. **Add GridObject component** to new prefabs

2. **Open Grid Object Scaler** window

3. **Verify auto-suggested size** is correct

4. **Click "Fit to Grid Size"** if needed

5. **Check green gizmo** in Scene view

---

## 📐 Understanding Grid Sizes

### Grid Cell Size
- **1.5 Unity units** (defined in `GridManager.cellSize`)
- This is the size of one grid square

### Object Slot Size
Objects can occupy multiple cells:

| Slot Size | World Size | Example Objects |
|-----------|------------|-----------------|
| 1×1       | 1.5 × 1.5  | Small units, basic resources |
| 2×2       | 3.0 × 3.0  | Large units, buildings |
| 3×3       | 4.5 × 4.5  | Huge structures |
| 1×2       | 1.5 × 3.0  | Rectangular objects |

**Formula:**
```
World Size = Grid Slots × 1.5 units
```

**Example:**
- 2×2 slots = 3.0 × 3.0 units (X × Z)

---

## 🎨 Visual Feedback

### Scene View Gizmos

When **GridObject component** is attached:

**Selected Object:**
- **Green wireframe box:** Target grid footprint
- **Red wireframe box:** Current actual bounds
- **Cyan corner spheres:** Grid cell corners
- **Label:** Shows grid size and scale status

**Unselected Object:**
- **Green wireframe:** Properly scaled ✓
- **Yellow wireframe:** Needs scaling ⚠️

### Grid Object Scaler Window

When window is open:
- **Blue grid lines:** Show grid cells in Scene
- **Green target box:** Shows intended slot size
- **Yellow corner markers:** Cell boundaries
- **Yellow label:** Displays grid size (e.g., "2×2 cells")

---

## ⚙️ Advanced Usage

### Manual Scaling

If you prefer manual control:

1. **Calculate target size:**
   ```
   Target Width (X) = Desired Slots X × 1.5
   Target Depth (Z) = Desired Slots Z × 1.5
   ```

2. **Measure current bounds:**
   - Select object
   - Check Grid Object Scaler window → "Current Bounds"

3. **Calculate scale factor:**
   ```
   Scale Factor = Target Size / Current Size
   ```

4. **Apply to transform:**
   - Multiply `transform.localScale` by scale factor

### Centering Objects

**GridObject.centerInSlot** setting:
- ✓ **True:** Object centered in grid footprint (default)
- ✗ **False:** Object positioned at grid anchor (top-left cell)

Use **False** for multi-cell objects that place from anchor point (like your resources).

---

## 🐛 Troubleshooting

### "Object has no renderers"
**Problem:** Object has no MeshRenderer/SkinnedMeshRenderer
**Solution:** Add visual geometry or parent under object with renderer

### "Scale factor 10x warning"
**Problem:** Object is way too small/large for grid
**Solution:**
1. Check if grid size is correct
2. Try different slot size
3. Object might need different scale entirely

### "Yellow gizmo after scaling"
**Problem:** Object still not matching grid size
**Solution:**
1. Click "Fit to Grid Size" again
2. Check if object has complex hierarchy
3. Try "Fit to Bounds" instead of "Apply Scale"

### "Batch scaler skipped my prefab"
**Problem:** Prefab has no GridObject and auto-add is off
**Solution:** Enable "Auto-Add GridObject if Missing"

---

## 📝 Best Practices

### For Artists/Designers:

1. ✅ **Always add GridObject component** to new prefabs
2. ✅ **Use Grid Object Scaler window** for visual confirmation
3. ✅ **Check green gizmos** before saving prefab
4. ✅ **Use presets** for common sizes (1×1, 2×2)

### For Programmers:

1. ✅ **Reference `GridObject.GridSize`** for placement logic
2. ✅ **Use `GridObject.GetWorldSize()`** for accurate dimensions
3. ✅ **Check `GridObject.IsProperlyScaled()`** for validation
4. ✅ **Run batch scaler** after importing new assets

### For Team Workflow:

1. ✅ **Batch scale all prefabs** before committing
2. ✅ **Include GridObject** in prefab templates
3. ✅ **Document custom sizes** (e.g., boss = 3×3)
4. ✅ **Run batch scaler** on Asset Import (optional)

---

## 🎮 Integration with Game Code

### Placement System

```csharp
// Example: Place object on grid
GridObject gridObj = prefab.GetComponent<GridObject>();
if (gridObj != null)
{
    Vector2Int size = gridObj.GridSize;

    if (GridManager.Instance.AreAllCellsAvailable(x, y, size))
    {
        Vector3 worldPos = GridManager.Instance.GetFootprintCenter(x, y, size);
        GameObject instance = Instantiate(prefab, worldPos, Quaternion.identity);
        GridManager.Instance.PlaceMultiCell(x, y, size, instance, CellState.Resource);
    }
}
```

### Validation

```csharp
// Check if all prefabs are properly scaled
GameObject[] prefabs = Resources.LoadAll<GameObject>("Prefabs");
foreach (var prefab in prefabs)
{
    GridObject gridObj = prefab.GetComponent<GridObject>();
    if (gridObj != null && !gridObj.IsProperlyScaled())
    {
        Debug.LogWarning($"{prefab.name} is not properly scaled!");
    }
}
```

---

## 📂 File Structure

```
Assets/
├── Scripts/
│   ├── Components/
│   │   └── GridObject.cs                 # Component for prefabs
│   └── Editor/
│       ├── GridObjectScaler.cs           # Window tool
│       ├── GridObjectEditor.cs           # Custom Inspector
│       └── GridBatchScaler.cs            # Batch processor
└── Prefabs/
    └── [Your prefabs with GridObject]
```

---

## 🚀 Next Steps

1. **Run batch scaler** on all existing prefabs
2. **Add GridObject** to your prefab templates
3. **Test placement** with new sizes
4. **Adjust fog of war** respawn logic for multi-cell objects

---

## 💡 Tips

- **Use 1×1 for most small objects** (units, small resources)
- **Use 2×2 for buildings/large resources** (like your tables)
- **Rectangular sizes (1×2, 2×1)** work great for walls, paths
- **3×3 or larger** for special structures, boss units

**Grid cell = 1.5 units = size of one square tile in your game**

---

**Questions?** Check the Grid Object Scaler window tooltips or Inspector help boxes for context-specific guidance!
