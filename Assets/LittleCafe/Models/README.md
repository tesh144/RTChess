# Little Cafe 3D Models

## 📦 Models in This Folder

These are the 3D equipment models for Little Cafe. They are ready to import and use in Unity.

**Files:**
- `counter_3d.obj` - Kitchen counter (wood brown #D4A574)
- `stove_3d.obj` - Cooking stove (red #FF6B6B)
- `sink_3d.obj` - Washing sink (cyan #6BCBFF)
- `pot_3d.obj` - Cooking pot (dark grey #6C757D)
- `plate_rack_3d.obj` - Plate rack (pink #FF69B4)
- `bin_3d.obj` - Trash bin (dark grey #495057)

---

## 🎮 Quick Start in Unity

### 1. Import Models
The .obj files in this folder will automatically be imported by Unity. You should see them in your Project panel.

### 2. Create Materials
For each model, create an Unlit material with solid color:

1. Right-click in Project → **Create → Material**
2. Name it (e.g., "CounterMaterial")
3. In Inspector, set **Shader → Unlit → Color**
4. Set the color (see colors above)

### 3. Create Prefabs
1. Drag a model .obj from Project panel into Scene
2. In Inspector, drag the matching material onto it
3. Drag the model from Hierarchy back into Project panel to create a prefab
4. Delete from scene (prefab is saved)

### 4. Use in Code
Reference these prefabs in your Equipment scripts:

```csharp
[SerializeField] private GameObject counterPrefab;
[SerializeField] private GameObject stovePrefab;
// etc.
```

---

## 📐 Model Specifications

### Kitchen Counter
- **Dimensions:** 1.0 × 0.95 × 1.0 units
- **Color:** Wood brown #D4A574
- **Features:** Raised countertop surface
- **Grid Size:** 1×1

### Cooking Stove
- **Dimensions:** 1.0 × 0.95 × 1.0 units
- **Color:** Red #FF6B6B
- **Features:** Two burner circles on top
- **Grid Size:** 1×1

### Washing Sink
- **Dimensions:** 1.0 × 0.9 × 1.0 units (1.2 with faucet)
- **Color:** Cyan blue #6BCBFF
- **Features:** Inset basin, small faucet
- **Grid Size:** 1×1

### Cooking Pot
- **Dimensions:** 0.3 × 0.25 × 0.3 units
- **Color:** Dark grey #6C757D
- **Features:** Handles on sides
- **Note:** Sits ON TOP of stove, separate object

### Plate Rack
- **Dimensions:** 1.0 × 0.15 × 1.0 units
- **Color:** Pink #FF69B4
- **Features:** Circular raised rim for plates
- **Grid Size:** 1×1 (low profile)

### Trash Bin
- **Dimensions:** 0.4 × 0.56 × 0.4 units
- **Color:** Dark grey #495057
- **Features:** Tapered sides, lid on top
- **Grid Size:** 0.4×0.4 (smaller than grid)

---

## ✅ Quality Check

All models:
- ✓ Simple geometric shapes (low-poly)
- ✓ 15-35 polygons each
- ✓ Designed for unlit shader
- ✓ Sized for 1×1 grid units
- ✓ Modular (counters connect seamlessly)
- ✓ Ready for Unity import

---

## 🔧 Troubleshooting

**Models look too dark?**
- Make sure you're using **Unlit/Color** shader, not Standard
- Unlit shaders don't need lights and show pure color

**Models don't snap to grid?**
- Check GridManager coordinate conversion
- Models are designed at 1.0 unit = 1 grid cell
- Verify your grid cell size matches

**Wrong size?**
- Scale in Unity: Select model → Inspector → Transform → Scale
- For pot: should be much smaller (sits on stove)
- For bin: smaller than grid cell

**Need more detail?**
- These are Phase 1 simple models
- Can add more detail later
- Focus on gameplay first!

---

## 📝 Next Steps

After importing these models:
1. Create prefabs for each equipment type
2. Update your Equipment classes to use prefabs
3. Replace procedural cubes with these models
4. Test in Build Mode

**Phase 1 Goal:** Get the kitchen builder working with these simple models!
