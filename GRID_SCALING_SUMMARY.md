# Grid Scaling Toolkit - Quick Reference

## 🎯 Your Grid Configuration
- **Cell Size:** 1.5 Unity units
- **Defined in:** `GridManager.cs` (line 18)
- **Formula:** World Size = Grid Slots × 1.5 units

---

## 🛠️ Tools Created

### 1. Grid Object Scaler Window
**Menu:** `Tools → RTChess → Grid Object Scaler`

**What it does:**
- Visual grid overlay in Scene view
- Auto-suggests slot size from bounds
- Manual override options
- One-click scaling

**When to use:** Scaling individual objects with visual feedback

---

### 2. GridObject Component
**Add via:** `Add Component → Grid Object`

**What it does:**
- Stores grid size (e.g., 2×2 cells)
- Green/yellow gizmo (green = properly scaled)
- Quick-resize buttons in Inspector
- Auto-detect grid size

**When to use:** Mark all game prefabs with their intended grid size

---

### 3. Grid Batch Scaler Window
**Menu:** `Tools → RTChess → Grid Batch Scaler`

**What it does:**
- Process entire folders of prefabs
- Auto-add GridObject component
- Detailed results log

**When to use:** Initial setup or bulk rescaling

---

### 4. Grid Overlay Toggle
**Menu:** `Tools → RTChess → Toggle Grid Overlay`

**What it does:**
- Always-on grid visualization
- Shows full game grid in Scene view
- Thicker lines every 5 cells

**When to use:** General grid alignment work

---

## ⚡ Quick Workflows

### First Time Setup (Recommended)

```
1. Tools → RTChess → Grid Batch Scaler
2. Set folder: Assets/Prefabs
3. Enable "Auto-Add GridObject if Missing"
4. Click "Process All Prefabs"
5. Review results
6. Fix any errors manually with Grid Object Scaler
```

### Scaling a Single Object

```
1. Select object in Scene/Hierarchy
2. Tools → RTChess → Grid Object Scaler
3. Verify suggested size (or adjust manually)
4. Click "Apply Scale"
5. Check green gizmo = success!
```

### Creating New Prefabs

```
1. Create your object
2. Add Component → Grid Object
3. Open Grid Object Scaler window
4. Adjust size if needed
5. Click "Fit to Grid Size"
6. Save as prefab
```

---

## 📊 Common Grid Sizes

| Grid Size | World Size | Use For |
|-----------|------------|---------|
| 1×1       | 1.5 × 1.5  | Units, small resources |
| 2×2       | 3.0 × 3.0  | Buildings, large resources, tables |
| 3×3       | 4.5 × 4.5  | Huge structures |
| 1×2       | 1.5 × 3.0  | Walls, paths |

---

## 🎨 Visual Indicators

### GridObject Gizmos
- 🟢 **Green wireframe:** Properly scaled
- 🟡 **Yellow wireframe:** Needs scaling
- 🔵 **Cyan spheres:** Corner markers

### Grid Scaler Window
- 🔵 **Blue grid lines:** Cell boundaries
- 🟢 **Green target box:** Intended size
- 🟡 **Yellow corners:** Cell edges

---

## 🔍 Inspector Quick Actions

When **GridObject** is selected:

**Buttons:**
- **Fit to Grid Size:** Scale to match grid size
- **Auto-Detect Size:** Calculate size from bounds
- **1×1, 2×2, 3×3, 1×2, 2×1:** Quick presets

**Status:**
- ✓ Properly scaled
- ⚠️ Needs rescaling

---

## 🐛 Common Issues

| Problem | Solution |
|---------|----------|
| "No renderers" error | Add MeshRenderer or SkinnedMeshRenderer |
| Yellow gizmo after scaling | Click "Fit to Grid Size" again |
| Batch scaler skipped prefab | Enable "Auto-Add GridObject" |
| Object way too big/small | Wrong grid size - try different preset |

---

## 📁 Files Created

```
Assets/
├── Scripts/
│   ├── Components/
│   │   └── GridObject.cs                   # Component
│   └── Editor/
│       ├── GridObjectScaler.cs             # Window tool
│       ├── GridObjectEditor.cs             # Inspector
│       ├── GridBatchScaler.cs              # Batch processor
│       └── GridVisualizerOverlay.cs        # Grid overlay
```

---

## 🎮 Integration Example

```csharp
// Placement system example
GridObject gridObj = prefab.GetComponent<GridObject>();
Vector2Int size = gridObj.GridSize; // e.g., (2, 2)

if (GridManager.Instance.AreAllCellsAvailable(x, y, size))
{
    Vector3 worldPos = GridManager.Instance.GetFootprintCenter(x, y, size);
    GameObject obj = Instantiate(prefab, worldPos, Quaternion.identity);
    GridManager.Instance.PlaceMultiCell(x, y, size, obj, CellState.Resource);
}
```

---

## ✅ Next Steps

1. ✅ Run Grid Batch Scaler on all prefabs
2. ✅ Verify results (fix any errors)
3. ✅ Add GridObject to new prefab templates
4. ✅ Test placement system with new sizes
5. ✅ Update fog of war respawn logic for multi-cell objects

---

**For detailed explanations, see:** [GRID_SCALING_GUIDE.md](GRID_SCALING_GUIDE.md)
