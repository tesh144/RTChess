# Phase 1: Kitchen Builder - Implementation Guide

## 🎯 Goal
Build an in-game drag-and-drop kitchen builder where players design custom cafe layouts that they can save and use during gameplay rounds.

**This is NOT a dev tool - this is a core gameplay feature.**

---

## 📋 What You're Building

**Build Mode System:**
- Drag equipment from a palette onto a 15x15 grid
- Place walls, doors, cooking stations, counters, etc.
- Visual zones (kitchen = peach, dining = light green)
- Save layouts to JSON files
- Load saved layouts
- Switch to Play Mode with current layout

**Think:** The Sims build mode, but for a restaurant kitchen

---

## 🔧 Technical Foundation

### Reuse from RTChess

**DO use these existing systems:**
- ✅ `GridManager` - The 15x15 grid system (already works)
- ✅ Grid coordinate system and world position conversion
- ✅ Isometric camera (already set up)
- ✅ Tile highlighting/selection system

**DO NOT modify:**
- ❌ Don't change RTChess code
- ❌ Don't modify existing chess pieces or game logic
- ❌ RTChess must remain fully functional

**Create new:**
- ✅ New `LittleCafe/` folder in Assets
- ✅ New equipment classes (similar to chess pieces, but simpler)
- ✅ New UI for equipment palette
- ✅ New save/load system
- ✅ New scene: `LittleCafe.unity`

---

## 🗂️ Folder Structure to Create

```
Assets/
├── RTChess/                     ← KEEP (don't touch)
│   ├── Scripts/
│   │   ├── Core/
│   │   │   └── GridManager.cs  ← REUSE THIS
│   │   └── Camera/
│   │       └── IsometricCamera.cs ← REUSE THIS
│   └── Scenes/
│       └── RTChess.unity        ← KEEP WORKING
│
├── LittleCafe/                  ← CREATE NEW
│   ├── Scripts/
│   │   ├── Equipment/           ← Equipment types
│   │   │   ├── Equipment.cs
│   │   │   ├── CookingStation.cs
│   │   │   ├── ServingCounter.cs
│   │   │   ├── WashingStation.cs
│   │   │   ├── PlateRack.cs
│   │   │   ├── Wall.cs
│   │   │   └── Door.cs
│   │   ├── Builder/             ← Build mode systems
│   │   │   ├── EquipmentPalette.cs
│   │   │   ├── EquipmentPlacer.cs
│   │   │   └── GridZoneRenderer.cs
│   │   ├── Managers/            ← Core systems
│   │   │   ├── GameModeManager.cs
│   │   │   └── LayoutManager.cs
│   │   └── Data/                ← Serializable data
│   │       ├── LayoutData.cs
│   │       └── EquipmentData.cs
│   ├── Prefabs/
│   │   └── Equipment/
│   │       ├── CookingStation.prefab
│   │       ├── ServingCounter.prefab
│   │       └── (etc.)
│   ├── Materials/               ← Colors for zones
│   │   ├── KitchenZone.mat
│   │   └── DiningZone.mat
│   ├── UI/
│   │   └── BuildModeCanvas.prefab
│   └── Scenes/
│       └── LittleCafe.unity     ← NEW SCENE
│
└── StreamingAssets/             ← CREATE IF NOT EXISTS
    └── Layouts/
        └── reference_kitchen.json  ← Default layout
```

---

## 🎨 Equipment Types & Visual Representation

For Phase 1, use **simple colored cubes** (detailed models come later):

| Equipment | Color | Size |
|-----------|-------|------|
| Cooking Station | Red (#FF6B6B) | 1x1 cube |
| Serving Counter | Green (#4FB05D) | 1x1 cube |
| Washing Station | Blue (#6BCBFF) | 1x1 cube |
| Plate Rack | Pink (#FF69B4) | 1x1 cube |
| Wall | Black (#2D2D2D) | 1x1 cube |
| Door | Yellow (#FFD93D) | 1x1 cube |
| Table | Purple (#8B5CF6) | 1x1 cube (Phase 3) |
| Chair | Light Green (#90EE90) | 1x1 cube (Phase 3) |

**Kitchen Zone Floor:** Peach tint (#FFDDB3)
**Dining Zone Floor:** Light green tint (#CAFFBF)

---

## 📐 Grid Layout Reference

**Use this exact layout for testing/validation:**

```
ROW 0-4: KITCHEN ZONE (peach floor)
- Row 1, Col 11: Plate Rack (pink)
- Row 2, Col 5: Cooking Station (red)
- Row 2, Col 7-10: Serving Counter (green, 4 tiles)
- Row 2, Col 11: Washing Station (blue)
- Row 3, Col 5: Cooking Station (red)
- Row 4, Col 5: Cooking Station (red)

ROW 5: WALL (black with yellow doors at col 7-8)
- Col 0-6: Wall (black)
- Col 7-8: Door (yellow)
- Col 9-14: Wall (black)

ROW 6-11: DINING ZONE (light green floor)
- Leave empty for Phase 1 (tables come in Phase 3)

ROW 12: WALL (black with yellow doors at col 7-8)
- Same as Row 5

ROW 13-14: Empty (light green)
```

**Queue Zones (visual markers only, Phase 1):**
- Chef Queue: Col 0, Rows 0-4 (orange tint)
- Waiter Queue: Col 14, Rows 6-11 (teal tint)
- Customer Queue: Row 13, Cols 1-5 (purple tint)

---

## 🔨 Implementation Steps

### Step 1: Scene Setup

**Create `LittleCafe.unity` scene:**

1. Create new scene: `Assets/LittleCafe/Scenes/LittleCafe.unity`
2. Add empty GameObject: `GameSetup`
3. Find and reference existing `GridManager` from RTChess (or instantiate it)
4. Find and reference existing `IsometricCamera` from RTChess
5. Add `GameModeManager` component
6. Add `LayoutManager` component
7. Add Canvas for UI: `BuildModeCanvas`

**Scene Hierarchy:**
```
LittleCafe Scene
├── GridManager (from RTChess)
├── IsometricCamera (from RTChess)
├── GameSetup
│   ├── GameModeManager
│   └── LayoutManager
├── BuildModeCanvas
│   ├── EquipmentPalette (left sidebar)
│   ├── SaveButton
│   ├── LoadButton
│   ├── ClearButton
│   └── StartServiceButton
└── PlayModeCanvas (inactive by default)
    └── (empty for Phase 1, populated in Phase 2)
```

---

### Step 2: Core Data Structures

**Create `LayoutData.cs`:**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCafe.Data
{
    [Serializable]
    public class LayoutData
    {
        public string layoutName = "Player Kitchen";
        public string version = "1.0";
        public int gridSize = 15;
        public string createdDate;
        public List<EquipmentData> equipment = new List<EquipmentData>();
        public ZoneData zones = new ZoneData();
        public QueueData queues = new QueueData();
    }

    [Serializable]
    public class EquipmentData
    {
        public string type;  // "CookingStation", "ServingCounter", etc.
        public GridPosition position;
        public float rotation;
    }

    [Serializable]
    public class GridPosition
    {
        public int row;
        public int col;

        public GridPosition(int row, int col)
        {
            this.row = row;
            this.col = col;
        }
    }

    [Serializable]
    public class ZoneData
    {
        public int[] kitchenRows = new int[] { 0, 4 };
        public int[] diningRows = new int[] { 6, 11 };
        public int[] wallRows = new int[] { 5, 12 };
    }

    [Serializable]
    public class QueueData
    {
        public QueueZone chef = new QueueZone { col = 0, rowStart = 0, rowEnd = 4 };
        public QueueZone waiter = new QueueZone { col = 14, rowStart = 6, rowEnd = 11 };
        public QueueZone customer = new QueueZone { row = 13, colStart = 1, colEnd = 5 };
    }

    [Serializable]
    public class QueueZone
    {
        public int col = -1;
        public int row = -1;
        public int rowStart = -1;
        public int rowEnd = -1;
        public int colStart = -1;
        public int colEnd = -1;
    }
}
```

---

### Step 3: Equipment Base Class

**Create `Equipment.cs`:**

```csharp
using UnityEngine;
using LittleCafe.Data;

namespace LittleCafe.Equipment
{
    public enum EquipmentType
    {
        CookingStation,
        ServingCounter,
        WashingStation,
        PlateRack,
        Wall,
        Door,
        Table,
        Chair
    }

    public class Equipment : MonoBehaviour
    {
        [SerializeField] private EquipmentType equipmentType;
        [SerializeField] private Color equipmentColor = Color.white;

        public EquipmentType Type => equipmentType;
        public GridPosition GridPosition { get; set; }

        private MeshRenderer meshRenderer;

        protected virtual void Awake()
        {
            // Create simple cube visual
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(transform);
            cube.transform.localPosition = Vector3.zero;
            cube.transform.localScale = Vector3.one;

            meshRenderer = cube.GetComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = equipmentColor;
            meshRenderer.material = mat;

            // Remove collider (we'll use grid for placement logic)
            Destroy(cube.GetComponent<Collider>());
        }

        public virtual void OnPlaced(GridPosition position)
        {
            GridPosition = position;
            // Override in subclasses for custom behavior
        }

        public virtual void OnRemoved()
        {
            // Override in subclasses for custom behavior
        }

        public void SetColor(Color color)
        {
            equipmentColor = color;
            if (meshRenderer != null)
            {
                meshRenderer.material.color = color;
            }
        }
    }
}
```

**Create concrete equipment classes:**

```csharp
// CookingStation.cs
using UnityEngine;

namespace LittleCafe.Equipment
{
    public class CookingStation : Equipment
    {
        protected override void Awake()
        {
            base.Awake();
            SetColor(new Color(1f, 0.42f, 0.42f)); // Red #FF6B6B
        }
    }
}

// ServingCounter.cs
using UnityEngine;

namespace LittleCafe.Equipment
{
    public class ServingCounter : Equipment
    {
        protected override void Awake()
        {
            base.Awake();
            SetColor(new Color(0.31f, 0.69f, 0.37f)); // Green #4FB05D
        }
    }
}

// (Create similar classes for WashingStation, PlateRack, Wall, Door)
```

---

### Step 4: Layout Manager

**Create `LayoutManager.cs`:**

```csharp
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using LittleCafe.Data;
using LittleCafe.Equipment;

namespace LittleCafe.Managers
{
    public class LayoutManager : MonoBehaviour
    {
        public static LayoutManager Instance { get; private set; }

        [Header("Equipment Prefabs")]
        [SerializeField] private CookingStation cookingStationPrefab;
        [SerializeField] private ServingCounter servingCounterPrefab;
        [SerializeField] private WashingStation washingStationPrefab;
        [SerializeField] private PlateRack plateRackPrefab;
        [SerializeField] private Wall wallPrefab;
        [SerializeField] private Door doorPrefab;

        private List<Equipment.Equipment> currentEquipment = new List<Equipment.Equipment>();
        private string layoutsDirectory;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            layoutsDirectory = Path.Combine(Application.persistentDataPath, "Layouts");
            Directory.CreateDirectory(layoutsDirectory);
        }

        public void SaveCurrentLayout(string fileName)
        {
            LayoutData data = BuildLayoutDataFromScene();
            data.createdDate = System.DateTime.Now.ToString("yyyy-MM-dd");

            string json = JsonUtility.ToJson(data, true);
            string path = Path.Combine(layoutsDirectory, fileName);
            File.WriteAllText(path, json);

            Debug.Log($"Layout saved to: {path}");
        }

        private LayoutData BuildLayoutDataFromScene()
        {
            LayoutData data = new LayoutData();

            foreach (Equipment.Equipment eq in currentEquipment)
            {
                EquipmentData eqData = new EquipmentData
                {
                    type = eq.Type.ToString(),
                    position = eq.GridPosition,
                    rotation = eq.transform.rotation.eulerAngles.y
                };
                data.equipment.Add(eqData);
            }

            return data;
        }

        public LayoutData LoadLayout(string fileName)
        {
            string path = Path.Combine(layoutsDirectory, fileName);

            // Try persistent data first
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                return JsonUtility.FromJson<LayoutData>(json);
            }

            // Fall back to StreamingAssets for default layouts
            path = Path.Combine(Application.streamingAssetsPath, "Layouts", fileName);
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                return JsonUtility.FromJson<LayoutData>(json);
            }

            Debug.LogError($"Layout not found: {fileName}");
            return null;
        }

        public void InstantiateLayout(LayoutData data)
        {
            ClearCurrentLayout();

            foreach (EquipmentData eqData in data.equipment)
            {
                Equipment.Equipment instance = InstantiateEquipment(eqData.type);
                if (instance != null)
                {
                    instance.transform.rotation = Quaternion.Euler(0, eqData.rotation, 0);
                    PlaceEquipmentOnGrid(instance, eqData.position);
                }
            }
        }

        private Equipment.Equipment InstantiateEquipment(string typeName)
        {
            EquipmentType type = (EquipmentType)System.Enum.Parse(typeof(EquipmentType), typeName);

            Equipment.Equipment prefab = type switch
            {
                EquipmentType.CookingStation => cookingStationPrefab,
                EquipmentType.ServingCounter => servingCounterPrefab,
                EquipmentType.WashingStation => washingStationPrefab,
                EquipmentType.PlateRack => plateRackPrefab,
                EquipmentType.Wall => wallPrefab,
                EquipmentType.Door => doorPrefab,
                _ => null
            };

            if (prefab == null)
            {
                Debug.LogError($"No prefab found for equipment type: {type}");
                return null;
            }

            return Instantiate(prefab);
        }

        public void PlaceEquipmentOnGrid(Equipment.Equipment equipment, GridPosition gridPos)
        {
            // Use RTChess GridManager to convert grid position to world position
            // Vector3 worldPos = GridManager.Instance.GridToWorldPosition(gridPos.row, gridPos.col);
            // equipment.transform.position = worldPos;

            // TODO: Adapt to your specific GridManager implementation
            // For now, simple positioning:
            equipment.transform.position = new Vector3(gridPos.col, 0, gridPos.row);

            equipment.OnPlaced(gridPos);
            currentEquipment.Add(equipment);
        }

        public void ClearCurrentLayout()
        {
            foreach (Equipment.Equipment eq in currentEquipment)
            {
                if (eq != null)
                {
                    Destroy(eq.gameObject);
                }
            }
            currentEquipment.Clear();
        }

        public void AddEquipment(Equipment.Equipment equipment)
        {
            if (!currentEquipment.Contains(equipment))
            {
                currentEquipment.Add(equipment);
            }
        }

        public void RemoveEquipment(Equipment.Equipment equipment)
        {
            currentEquipment.Remove(equipment);
            Destroy(equipment.gameObject);
        }
    }
}
```

---

### Step 5: Game Mode Manager

**Create `GameModeManager.cs`:**

```csharp
using UnityEngine;

namespace LittleCafe.Managers
{
    public enum GameMode
    {
        Build,
        Play
    }

    public class GameModeManager : MonoBehaviour
    {
        public static GameModeManager Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private GameObject buildModeUI;
        [SerializeField] private GameObject playModeUI;

        [Header("System References")]
        [SerializeField] private GameObject buildModeSystems;
        [SerializeField] private GameObject playModeSystems;

        public GameMode CurrentMode { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Start in Build Mode
            SwitchToMode(GameMode.Build);
        }

        public void SwitchToMode(GameMode mode)
        {
            CurrentMode = mode;

            if (mode == GameMode.Build)
            {
                EnableBuildMode();
            }
            else if (mode == GameMode.Play)
            {
                EnablePlayMode();
            }
        }

        private void EnableBuildMode()
        {
            // Enable build systems
            if (buildModeUI != null) buildModeUI.SetActive(true);
            if (buildModeSystems != null) buildModeSystems.SetActive(true);

            // Disable play systems
            if (playModeUI != null) playModeUI.SetActive(false);
            if (playModeSystems != null) playModeSystems.SetActive(false);

            Debug.Log("Switched to Build Mode");
        }

        private void EnablePlayMode()
        {
            // Save layout before playing
            LayoutManager.Instance.SaveCurrentLayout("player_kitchen.json");

            // Disable build systems
            if (buildModeUI != null) buildModeUI.SetActive(false);
            if (buildModeSystems != null) buildModeSystems.SetActive(false);

            // Enable play systems
            if (playModeUI != null) playModeUI.SetActive(true);
            if (playModeSystems != null) playModeSystems.SetActive(true);

            Debug.Log("Switched to Play Mode");
        }

        // UI Callbacks
        public void OnStartServiceClicked()
        {
            SwitchToMode(GameMode.Play);
        }

        public void OnEditKitchenClicked()
        {
            SwitchToMode(GameMode.Build);
        }
    }
}
```

---

### Step 6: Equipment Palette UI

**Create `EquipmentPalette.cs`:**

```csharp
using UnityEngine;
using UnityEngine.UI;
using LittleCafe.Equipment;

namespace LittleCafe.Builder
{
    public class EquipmentPalette : MonoBehaviour
    {
        [Header("Equipment Prefabs")]
        [SerializeField] private CookingStation cookingStationPrefab;
        [SerializeField] private ServingCounter servingCounterPrefab;
        [SerializeField] private WashingStation washingStationPrefab;
        [SerializeField] private PlateRack plateRackPrefab;
        [SerializeField] private Wall wallPrefab;
        [SerializeField] private Door doorPrefab;

        [Header("UI")]
        [SerializeField] private Button cookingStationButton;
        [SerializeField] private Button servingCounterButton;
        [SerializeField] private Button washingStationButton;
        [SerializeField] private Button plateRackButton;
        [SerializeField] private Button wallButton;
        [SerializeField] private Button doorButton;

        private EquipmentPlacer placer;

        private void Start()
        {
            placer = FindObjectOfType<EquipmentPlacer>();

            // Setup button callbacks
            cookingStationButton.onClick.AddListener(() => SelectEquipment(cookingStationPrefab));
            servingCounterButton.onClick.AddListener(() => SelectEquipment(servingCounterPrefab));
            washingStationButton.onClick.AddListener(() => SelectEquipment(washingStationPrefab));
            plateRackButton.onClick.AddListener(() => SelectEquipment(plateRackPrefab));
            wallButton.onClick.AddListener(() => SelectEquipment(wallPrefab));
            doorButton.onClick.AddListener(() => SelectEquipment(doorPrefab));
        }

        private void SelectEquipment(Equipment.Equipment prefab)
        {
            if (placer != null)
            {
                placer.SetSelectedEquipment(prefab);
            }
        }
    }
}
```

---

### Step 7: Equipment Placer (Drag-Drop Logic)

**Create `EquipmentPlacer.cs`:**

```csharp
using UnityEngine;
using LittleCafe.Equipment;
using LittleCafe.Data;
using LittleCafe.Managers;

namespace LittleCafe.Builder
{
    public class EquipmentPlacer : MonoBehaviour
    {
        private Equipment.Equipment selectedPrefab;
        private Equipment.Equipment ghostEquipment;
        private Camera mainCamera;

        private void Start()
        {
            mainCamera = Camera.main;
        }

        private void Update()
        {
            if (GameModeManager.Instance.CurrentMode != GameMode.Build)
                return;

            HandleGhostPreview();
            HandlePlacement();
            HandleRemoval();
        }

        public void SetSelectedEquipment(Equipment.Equipment prefab)
        {
            selectedPrefab = prefab;

            // Create ghost preview
            if (ghostEquipment != null)
            {
                Destroy(ghostEquipment.gameObject);
            }

            ghostEquipment = Instantiate(prefab);
            ghostEquipment.GetComponentInChildren<MeshRenderer>().material.color =
                new Color(1, 1, 1, 0.5f); // Semi-transparent
        }

        private void HandleGhostPreview()
        {
            if (ghostEquipment == null) return;

            // Raycast to grid
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // Convert hit point to grid position
                // TODO: Use GridManager to get proper grid cell
                int row = Mathf.RoundToInt(hit.point.z);
                int col = Mathf.RoundToInt(hit.point.x);

                Vector3 worldPos = new Vector3(col, 0, row);
                ghostEquipment.transform.position = worldPos;
            }
        }

        private void HandlePlacement()
        {
            if (Input.GetMouseButtonDown(0) && selectedPrefab != null)
            {
                // Raycast to get grid cell
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    int row = Mathf.RoundToInt(hit.point.z);
                    int col = Mathf.RoundToInt(hit.point.x);

                    // Check if cell is empty
                    // TODO: Use GridManager to check cell state

                    // Place equipment
                    Equipment.Equipment instance = Instantiate(selectedPrefab);
                    GridPosition gridPos = new GridPosition(row, col);
                    LayoutManager.Instance.PlaceEquipmentOnGrid(instance, gridPos);

                    Debug.Log($"Placed {selectedPrefab.Type} at ({row}, {col})");
                }
            }
        }

        private void HandleRemoval()
        {
            if (Input.GetMouseButtonDown(1)) // Right click to remove
            {
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    Equipment.Equipment equipment = hit.collider.GetComponentInParent<Equipment.Equipment>();
                    if (equipment != null)
                    {
                        LayoutManager.Instance.RemoveEquipment(equipment);
                        Debug.Log($"Removed {equipment.Type}");
                    }
                }
            }
        }
    }
}
```

---

## ✅ Phase 1 Acceptance Criteria

Test these before considering Phase 1 complete:

- [ ] Can open `LittleCafe.unity` scene
- [ ] Scene starts in Build Mode
- [ ] Can see equipment palette UI (left sidebar)
- [ ] Can click equipment button to select it
- [ ] Ghost preview follows mouse cursor
- [ ] Can left-click to place equipment on grid
- [ ] Equipment appears at correct grid position
- [ ] Can right-click equipment to remove it
- [ ] Can place all equipment types (cooking, counter, washing, rack, wall, door)
- [ ] Equipment shows correct colors (red, green, blue, pink, black, yellow)
- [ ] Grid shows visual zones (kitchen = peach, dining = light green)
- [ ] Can click "Save Layout" button
- [ ] Layout saves to JSON file
- [ ] Can click "Load Layout" button
- [ ] Layout loads from JSON file
- [ ] Can click "Clear" button to remove all equipment
- [ ] Can click "Start Service" button
- [ ] Mode switches to Play Mode (UI changes)
- [ ] Layout persists when switching modes
- [ ] Can click "Edit Kitchen" to return to Build Mode
- [ ] Can recreate the reference kitchen layout exactly

---

## 🐛 Common Issues & Solutions

**Issue:** Grid positions don't match visual tiles
- **Solution:** Check RTChess GridManager coordinate system. Ensure you're using the same row/col mapping.

**Issue:** Equipment doesn't appear when placed
- **Solution:** Check that prefabs are assigned in LayoutManager Inspector. Verify camera can see equipment layer.

**Issue:** Can't click equipment to remove
- **Solution:** Equipment needs colliders for raycasting. Add BoxCollider to equipment prefabs.

**Issue:** Ghost preview doesn't follow cursor
- **Solution:** Ensure Main Camera is tagged "MainCamera". Check raycast layer masks.

**Issue:** Save/Load doesn't work
- **Solution:** Check `Application.persistentDataPath` permissions. Verify JSON format matches LayoutData structure.

---

## 🧪 Testing Workflow

**Manual Test Plan:**

1. **Scene Setup Test**
   - Open LittleCafe.unity
   - Verify grid appears
   - Verify zones are colored correctly

2. **Placement Test**
   - Select Cooking Station
   - Place at (2, 5)
   - Verify red cube appears
   - Verify position is correct

3. **Save Test**
   - Place equipment matching reference layout
   - Click Save button
   - Check `persistentDataPath/Layouts/player_kitchen.json`
   - Verify JSON contains equipment data

4. **Load Test**
   - Click Clear button
   - Grid should be empty
   - Click Load button
   - Equipment should reappear in saved positions

5. **Mode Switch Test**
   - Place equipment
   - Click "Start Service"
   - Verify UI changes (palette hides)
   - Click "Edit Kitchen"
   - Verify equipment still there

6. **Reference Layout Test**
   - Recreate exact reference kitchen layout
   - Save as `reference_kitchen.json`
   - Copy to `StreamingAssets/Layouts/`
   - Verify it loads correctly

---

## 📝 Notes for Implementation

**Integration with RTChess:**
- The biggest challenge is adapting RTChess's GridManager API
- Look for methods like `GetCellAtPosition()`, `WorldToGrid()`, `GridToWorld()`
- If GridManager uses different coordinate systems (x,y vs row,col), adapt accordingly

**UI Design:**
- Keep UI minimal for Phase 1
- Focus on functionality over polish
- Use Unity's default UI (no custom sprites needed yet)

**Performance:**
- Equipment instantiation should be instant for Phase 1
- Object pooling not needed until Phase 2 (many customers)
- JSON save/load is synchronous (fine for Phase 1)

**Future-Proofing:**
- Write code anticipating tables/chairs in Phase 3
- Keep equipment types extensible (easy to add new types)
- Layout data structure should support future features (metadata, ratings, etc.)

---

## 🎯 Success Criteria

**Phase 1 is complete when:**
1. You can design a custom kitchen in Build Mode
2. Save that design to a JSON file
3. Load that design and it appears correctly
4. Switch to Play Mode with that layout
5. Return to Build Mode and edit the layout
6. The reference kitchen layout can be recreated exactly

**Then you're ready for Phase 2: Character AI**

---

## 📚 Reference Documents

Make sure you've read:
- `little-cafe-unity-prompts.md` (overall project structure)
- `kitchen-builder-architecture.md` (system design)
- `little-cafe-handoff.md` (original requirements)
- `little-cafe-design-diagram-correct.html` (visual reference)

---

## ❓ Questions? Check These First:

**Q: Do I need to modify RTChess code?**
A: No! Keep RTChess completely separate. Only reference/reuse its systems.

**Q: What if RTChess's GridManager API is different?**
A: Adapt the LayoutManager placement logic to match RTChess's API. The rest of the system stays the same.

**Q: Should I make detailed 3D models?**
A: Not for Phase 1. Simple colored cubes are fine. Models come later.

**Q: What about animations?**
A: Not for Phase 1. Static placement only.

**Q: Do I need networking/multiplayer?**
A: No. Local single-player only.

---

**Ready to build! Start with scene setup, then work through the implementation steps in order. Good luck! 🚀**
