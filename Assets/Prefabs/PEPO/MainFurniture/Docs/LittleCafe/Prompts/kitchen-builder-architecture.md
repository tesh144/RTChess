# Kitchen Builder - System Architecture

## Overview
The kitchen builder system is a **core gameplay feature** where players design custom cafe layouts, save them, and play rounds using those layouts. This is NOT just a dev tool - it's part of the game loop.

## Core Requirement
**Player Experience:** Players can design their own kitchen layout using in-game drag-and-drop tools, save it, then play service rounds with that exact kitchen.

**Similar to:** Sims (build house → live in it), RollerCoaster Tycoon (build park → run it), Restaurant Story (design layout → serve customers)

**Solution:** Mode-based system where players toggle between Build Mode (design kitchen) and Play Mode (run service).

---

## System Design

### 1. Two Game Modes (Player-Facing)

**Build Mode**
- Player designs their kitchen layout
- Drag-and-drop equipment from palette
- Save/Load layouts
- Preview mode (test layout before playing)
- **Accessible anytime** (main menu option or in-game button)

**Play Mode**
- Player runs 3-minute service rounds
- Uses the currently loaded kitchen layout
- Layout is locked during service (can't edit while serving)
- After round ends, can return to Build Mode to redesign

**Mode Toggle Flow:**
```
Main Menu
  ↓
[Build Kitchen] or [Play Round]
  ↓                    ↓
Build Mode          Play Mode
  ↓                    ↓
Save Layout        Complete Round
  ↓                    ↓
[Play Round] ←→ [Edit Kitchen]
```

### 2. Scene Structure (REVISED)

**Option A: Single Scene with Mode Toggle (RECOMMENDED for player experience)**
```
Scenes/
└── LittleCafe.unity        ← Seamless toggle between Build/Play
```

**How it works:**
- Same scene, same grid, same equipment
- Toggle switches which systems are active:
  - **Build Mode:** Equipment palette UI, placement controls, save/load UI
  - **Play Mode:** Character spawning, customer systems, timer, scoring
- Transition is instant (no scene loading)
- Player's layout stays loaded the entire time

**Pros:**
- Smooth player experience (no loading screens)
- Layout persists between modes naturally
- Easy to iterate (redesign → test → redesign)
- Single scene = simpler state management

**Cons:**
- Need to enable/disable systems based on mode
- More complex initial setup

**Option B: Two Scenes (Alternative for development)**
```
Scenes/
├── KitchenBuilder.unity    ← Build and save
└── GamePlay.unity          ← Load and play
```

**Use case:** If you want to develop them separately first, then merge later

**Recommendation:** **Option A (single scene)** because this is a player feature, not a dev tool. Players expect seamless transitions.

---

## Game Loop Integration

### 3. How Players Use This System

**First Time Player:**
1. Start game → Tutorial
2. **Build Mode:** Guided tutorial shows how to place equipment
3. Build a simple kitchen (tutorial provides guidance)
4. Click "Start Service" → switches to Play Mode
5. **Play Mode:** Run first 3-minute service round
6. Round ends → Stats screen → Option to "Edit Kitchen" or "Play Again"

**Returning Player:**
1. Main Menu → "My Kitchen" or "Play Round"
2. **Option 1:** Edit Kitchen (Build Mode) → redesign → save → play
3. **Option 2:** Play Round (Play Mode) → loads last saved kitchen → starts service

**Core Loop:**
```
Build/Edit Kitchen (customize layout)
         ↓
    Save Layout
         ↓
    Start Service
         ↓
   Play Round (3min)
         ↓
    Round Ends
         ↓
  View Stats/Earnings
         ↓
  Unlock New Equipment?
         ↓
[Edit Kitchen] or [Play Again] ← loops back
```

**Progression Tie-In (Future):**
- Start with limited equipment (3 cooking stations, basic counters)
- Earn money from successful rounds
- Unlock new equipment types (advanced stations, decorations)
- Build bigger, more efficient kitchens
- Player's layout = expression of their strategy

---

## Layout Persistence System

### 3. JSON Layout Format

**File Location:** `Assets/LittleCafe/Layouts/`

**File Structure:**
```json
{
  "layoutName": "Reference Kitchen",
  "version": "1.0",
  "gridSize": 15,
  "createdDate": "2026-02-11",
  "equipment": [
    {
      "type": "CookingStation",
      "position": { "row": 2, "col": 5 },
      "rotation": 0
    },
    {
      "type": "ServingCounter",
      "position": { "row": 2, "col": 7 },
      "rotation": 0
    },
    {
      "type": "WashingStation",
      "position": { "row": 2, "col": 11 },
      "rotation": 0
    },
    {
      "type": "PlateRack",
      "position": { "row": 1, "col": 11 },
      "rotation": 0
    },
    {
      "type": "Wall",
      "position": { "row": 5, "col": 0 },
      "rotation": 0
    },
    {
      "type": "Door",
      "position": { "row": 5, "col": 7 },
      "rotation": 0
    }
  ],
  "zones": {
    "kitchenRows": [0, 4],
    "diningRows": [6, 11],
    "wallRows": [5, 12]
  },
  "queues": {
    "chef": { "col": 0, "rows": [0, 4] },
    "waiter": { "col": 14, "rows": [6, 11] },
    "customer": { "row": 13, "cols": [1, 5] }
  }
}
```

### 4. Default Layouts

**Included Layouts:**
- `reference_kitchen.json` - The exact layout from the design diagram (for development)
- `empty_layout.json` - Blank 15x15 grid (starting point for custom designs)
- `tutorial_layout.json` - Simple layout for tutorial/onboarding

**Usage:**
```csharp
// In GamePlay scene
public class GameSetup : MonoBehaviour {
    [SerializeField] private string defaultLayoutPath = "Layouts/reference_kitchen";

    void Start() {
        LayoutData layout = LayoutManager.LoadLayout(defaultLayoutPath);
        LayoutManager.InstantiateLayout(layout);
        // Now spawn characters, start gameplay, etc.
    }
}
```

---

## Development Workflow

### Phase 1: Build the Builder System

1. **Implement Build Mode** (drag-and-drop, palette, save/load)
2. **Create reference kitchen** using the builder
3. **Save as `reference_kitchen.json`** in StreamingAssets/Layouts/
4. **Test mode switching** (Build → Play → Build)
5. **Commit reference layout to Git**

### Phase 2+: Develop Gameplay Features

1. **Start game in Build Mode**
2. **Click "Load Default"** → loads reference_kitchen.json
3. **Click "Start Service"** → switches to Play Mode
4. **Test new features** (AI, customers, plates) on the reference kitchen
5. **After round ends** → can switch back to Build Mode to adjust
6. **No need to manually rebuild** - layout persists and can be loaded

### For Development Testing

**Option 1: Quick Test Setup**
```csharp
// In GameModeManager, add a dev shortcut
void Update() {
    if (Input.GetKeyDown(KeyCode.F1)) {
        LayoutManager.Instance.LoadLayout("reference_kitchen.json");
        LayoutManager.Instance.InstantiateLayout(data);
        SwitchToMode(GameMode.Play);
    }
}
```

**Option 2: Default Layout on Start**
```csharp
void Start() {
    // For development: auto-load reference kitchen
    LayoutData data = LayoutManager.Instance.LoadLayout("reference_kitchen.json");
    LayoutManager.Instance.InstantiateLayout(data);
    SwitchToMode(GameMode.Build); // or GameMode.Play for testing
}
```

---

## Implementation Classes

### GameModeManager (Singleton)

```csharp
public enum GameMode {
    Build,
    Play
}

public class GameModeManager : MonoBehaviour {
    public static GameModeManager Instance;

    public GameMode currentMode { get; private set; }

    [SerializeField] private GameObject buildModeUI;      // Equipment palette, save/load buttons
    [SerializeField] private GameObject playModeUI;       // Timer, score, customer UI
    [SerializeField] private GameObject buildModeSystems; // Placement controls
    [SerializeField] private GameObject playModeSystems;  // Character spawners, game logic

    void Start() {
        // Start in Build Mode by default
        SwitchToMode(GameMode.Build);
    }

    public void SwitchToMode(GameMode mode) {
        currentMode = mode;

        if (mode == GameMode.Build) {
            // Enable build systems
            buildModeUI.SetActive(true);
            buildModeSystems.SetActive(true);

            // Disable play systems
            playModeUI.SetActive(false);
            playModeSystems.SetActive(false);

            // Cleanup gameplay (despawn characters, customers, etc.)
            GameplayManager.Instance.CleanupRound();
        }
        else if (mode == GameMode.Play) {
            // Disable build systems
            buildModeUI.SetActive(false);
            buildModeSystems.SetActive(false);

            // Enable play systems
            playModeUI.SetActive(true);
            playModeSystems.SetActive(true);

            // Start gameplay (spawn characters, start timer, etc.)
            GameplayManager.Instance.StartRound();
        }
    }

    // Called by "Start Service" button in Build Mode
    public void OnStartServiceClicked() {
        // Auto-save current layout before playing
        LayoutManager.Instance.SaveCurrentLayout("player_kitchen.json");
        SwitchToMode(GameMode.Play);
    }

    // Called by "Edit Kitchen" button after round ends
    public void OnEditKitchenClicked() {
        SwitchToMode(GameMode.Build);
    }
}
```

### LayoutManager (Singleton)

```csharp
public class LayoutManager : MonoBehaviour {
    public static LayoutManager Instance;

    private List<Equipment> currentEquipment = new List<Equipment>();

    // Save current layout to JSON
    public void SaveCurrentLayout(string fileName) {
        LayoutData data = BuildLayoutDataFromScene();
        string json = JsonUtility.ToJson(data, true);
        string path = Path.Combine(Application.persistentDataPath, "Layouts", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, json);
        Debug.Log($"Layout saved: {path}");
    }

    // Build LayoutData from currently placed equipment
    private LayoutData BuildLayoutDataFromScene() {
        LayoutData data = new LayoutData();
        data.layoutName = "Player Kitchen";
        data.gridSize = 15;
        data.equipment = new List<EquipmentData>();

        foreach (Equipment eq in currentEquipment) {
            data.equipment.Add(new EquipmentData {
                type = eq.type,
                position = eq.gridPosition,
                rotation = eq.transform.rotation.eulerAngles.y
            });
        }

        return data;
    }

    // Load layout from JSON
    public LayoutData LoadLayout(string fileName) {
        string path = Path.Combine(Application.persistentDataPath, "Layouts", fileName);
        if (!File.Exists(path)) {
            Debug.LogWarning($"Layout not found: {path}, using default");
            return LoadDefaultLayout();
        }
        string json = File.ReadAllText(path);
        return JsonUtility.FromJson<LayoutData>(json);
    }

    // Load default reference layout from StreamingAssets
    private LayoutData LoadDefaultLayout() {
        string path = Path.Combine(Application.streamingAssetsPath, "Layouts/reference_kitchen.json");
        string json = File.ReadAllText(path);
        return JsonUtility.FromJson<LayoutData>(json);
    }

    // Instantiate equipment from layout data
    public void InstantiateLayout(LayoutData data) {
        ClearCurrentLayout();

        foreach (var equipmentData in data.equipment) {
            Equipment prefab = GetEquipmentPrefab(equipmentData.type);
            Equipment instance = Instantiate(prefab);
            instance.transform.rotation = Quaternion.Euler(0, equipmentData.rotation, 0);
            GridManager.Instance.PlaceEquipment(instance, equipmentData.position);
            currentEquipment.Add(instance);
        }
    }

    public void ClearCurrentLayout() {
        foreach (Equipment eq in currentEquipment) {
            Destroy(eq.gameObject);
        }
        currentEquipment.Clear();
    }
}
```

### LayoutData (Serializable)

```csharp
[System.Serializable]
public class LayoutData {
    public string layoutName;
    public string version;
    public int gridSize;
    public List<EquipmentData> equipment;
    public ZoneData zones;
    public QueueData queues;
}

[System.Serializable]
public class EquipmentData {
    public EquipmentType type;
    public GridPosition position;
    public int rotation;
}
```

### KitchenBuilderUI

```csharp
public class KitchenBuilderUI : MonoBehaviour {
    [SerializeField] private InputField layoutNameInput;

    // Save button callback
    public void OnSaveClicked() {
        LayoutData data = BuildCurrentLayout();
        string fileName = layoutNameInput.text + ".json";
        LayoutManager.Instance.SaveLayout(fileName, data);
        Debug.Log($"Layout saved: {fileName}");
    }

    // Load button callback
    public void OnLoadClicked() {
        // Show file picker, load selected layout
        LayoutData data = LayoutManager.Instance.LoadLayout("reference_kitchen.json");
        LayoutManager.Instance.ClearCurrentLayout();
        LayoutManager.Instance.InstantiateLayout(data);
    }
}
```

---

## Benefits of This Approach

✅ **No Redundant Work**
- Build the reference kitchen once in Phase 1
- Load it automatically in all future phases
- Never rebuild manually

✅ **Version Control Friendly**
- JSON files in Git
- Team members share the same layouts
- Easy to diff changes

✅ **Flexible Testing**
- Swap layouts easily for testing different scenarios
- Stress test with extreme layouts (100 tables, 1 cooking station)
- Tutorial with simple layout

✅ **Designer-Friendly**
- Non-programmers can create layouts
- Visual editor, not code
- Save and share layout files

✅ **Future Features Enable Easily**
- Player progression: Unlock new layouts
- Layout shop: Buy pre-made designs
- Community layouts: Share JSON files

---

## Phase 1 Deliverables

**Builder Mode Must Have:**
- [ ] Drag-and-drop equipment placement
- [ ] Save layout to JSON (with file name input)
- [ ] Load layout from JSON (with file picker)
- [ ] Clear layout button
- [ ] Visual preview of zones (kitchen/dining)

**Test Files to Create:**
- [ ] `reference_kitchen.json` (from design diagram)
- [ ] `empty_layout.json` (blank grid)
- [ ] Include these in the Git repo

**GamePlay Scene Integration:**
- [ ] GameSetup script that loads `reference_kitchen.json` on start
- [ ] Verify layout instantiates correctly
- [ ] Ready for Phase 2 character spawning

---

## Future Enhancements (Post-MVP)

- **Layout Validation:** Check if layout is playable (has required equipment, paths exist, etc.)
- **Layout Templates:** Pre-made starter layouts
- **Undo/Redo:** Stack for builder edits
- **Copy/Paste Equipment:** Duplicate equipment easily
- **Symmetry Mode:** Mirror placements
- **Layout Thumbnails:** Visual previews in load menu
- **Layout Metadata:** Author, difficulty rating, required upgrades

---

## Questions to Resolve During Implementation

- Should layouts include camera settings (zoom, rotation)?
- Do we need layout versioning (migrate old formats)?
- Should there be a "locked" flag to prevent editing saved layouts?
- Include spawn counts in layout (how many chefs/waiters)?

---

## Summary for VS Code Claude

When implementing Phase 1:

1. **Create LayoutManager** with Save/Load methods
2. **Create LayoutData** serializable classes
3. **KitchenBuilder scene** has UI for save/load
4. **Save as JSON** to `Assets/LittleCafe/Layouts/`
5. **Create `reference_kitchen.json`** matching design diagram
6. **GamePlay scene** loads layout on startup
7. **Test:** Build layout → Save → Load in GamePlay scene → Verify equipment appears

This ensures layouts are portable, reusable, and don't need rebuilding for every test.
