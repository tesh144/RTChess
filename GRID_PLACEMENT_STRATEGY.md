# Multi-Cell Grid Placement Strategy

## Problem
Objects in the game have varying footprints:
- 1x1 (most objects)
- 2x2 (was Training Facility, now Barracks as 1x1)
- 2x1, 1x2 (future tables, etc)
- **Irregular shapes** (L-shapes, T-shapes, etc)

Currently, MapGeneratorV2 and DragDropHandler don't properly handle multi-cell placement, and gridSize is zeroed out in databases.

---

## Decision 1: Where Should gridSize Live?

### Current State
- **Prefabs**: Some have `GridObject` component with gridSize
- **Database entries**: Have `gridSize` field but all were zeroed (now fixed to 1x1 or correct sizes)
- **Runtime**: DragDropHandler checks prefab's GridObject first, then falls back to UnitStats.gridSize

### Recommended Approach: **"Database is source of truth, prefab is optional metadata"**

1. **Database (BuildingData, EnvironmentData, UnitData) is the authoritative source** for gridSize
   - When an object is loaded from database → its gridSize is definitive
   - Easy to tune and adjust without touching prefabs

2. **Prefab's GridObject is optional documentation/override**
   - If a prefab has GridObject with non-zero gridSize → it indicates intended size
   - Used by designers/artists as a reminder during prefab creation
   - But if missing or wrong, the database value wins

3. **Code flow (already partially implemented)**
   ```csharp
   // When instantiating from database
   Vector2Int GetEffectiveGridSize(Data data, GameObject prefab)
   {
       // Database is source of truth
       if (data.gridSize.x > 0 && data.gridSize.y > 0)
           return data.gridSize;

       // Fallback to prefab if database is empty
       GridObject gridObj = prefab.GetComponent<GridObject>();
       if (gridObj != null && gridObj.GridSize.x > 0 && gridObj.GridSize.y > 0)
           return gridObj.GridSize;

       // Last resort: assume 1x1
       return Vector2Int.one;
   }
   ```

---

## Decision 2: How to Handle Irregular Shapes?

### Option A: Bounding Box (Simple, Current Approach)
**Concept**: Use the smallest rectangle that fits the shape.

**Example: L-shape**
```
  ##     Bounding box: 2x2
  #     Reserved cells: (0,0), (0,1), (1,0)
        Unused cell: (1,1)
```

**Pros**:
- Simple to implement — just a Vector2Int
- Existing `PlaceMultiCell` code works as-is
- Works fine if you don't mind wasting 1 cell

**Cons**:
- Wastes grid space for irregular shapes
- If you spawn many L-shaped objects, map gets sparse unnecessarily

**Code**:
```csharp
public Vector2Int gridSize = new Vector2Int(2, 2);  // Bounding box
```

---

### Option B: Footprint Map (Flexible, Future-Proof)
**Concept**: Store which cells in the bounding box are actually occupied.

**Example: L-shape**
```
  ##     Footprint:
  #      [true,  true]
         [true,  false]
```

**Pros**:
- No wasted grid cells
- Can represent any shape (L, T, cross, etc)
- Scales well for complex buildings

**Cons**:
- More complex to implement
- Requires new data structures
- GridManager's placement logic needs rewrite to check footprint, not just bounding box

**Code** (pseudocode):
```csharp
[System.Serializable]
public class GridFootprint
{
    public Vector2Int size;  // Bounding box
    public bool[] occupiedCells;  // Which cells in the bounding box are used

    public bool IsOccupied(int localX, int localY)
    {
        if (localX < 0 || localX >= size.x) return false;
        if (localY < 0 || localY >= size.y) return false;
        return occupiedCells[localY * size.x + localX];
    }
}
```

---

## Recommendation for MVP

**Start with Option A (Bounding Box)**:
1. It's already partially implemented (`PlaceMultiCell`)
2. Covers the immediate need (2x2 Training Facility, 2x1 tables)
3. Can upgrade to footprint map later when you have complex shapes

**Implementation steps**:
1. ✅ Fix all database gridSize values (done for BuildingDatabase, EnvironmentDatabase, WorkerDatabase)
2. ✅ Ensure DragDropHandler uses database gridSize (done)
3. 🔄 Update MapGeneratorV2 to call `PlaceMultiCell` instead of `PlaceUnit` when gridSize > 1x1
4. 🔄 Add validation in MapGeneratorV2 planning phase: don't place objects whose footprints don't fit
5. 🔄 Test in Unity with 2x2 Training Facility

---

## When to Switch to Footprint Map

**Indicators**:
- You have L-shaped, T-shaped, or cross-shaped buildings
- Grid efficiency becomes a problem (sparse map)
- Multiple irregular buildings compete for limited space

---

## Next Steps

1. **Decide**: Bounding Box (Option A) or Footprint Map (Option B)?
2. If **Bounding Box**: Proceed with MapGeneratorV2 updates
3. If **Footprint Map**: Design new data structures first (separate task)
