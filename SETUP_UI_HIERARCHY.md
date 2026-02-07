# UI Hierarchy Setup Guide

This document provides complete instructions for manually creating all UI elements in the Unity Editor so they're visible and editable in the scene hierarchy.

---

## Why Manual UI Setup?

Previously, all UI was created at runtime via code (in GameSetup.cs), making it:
- **Invisible in the editor** - can't see or adjust layout
- **Hard to customize** - requires code changes for visual tweaks
- **Difficult to layer** - no control over rendering order

Now you create everything manually in the scene, giving you:
- **Full visual control** - position, size, colors, fonts
- **Easy customization** - tweak in Inspector, see changes instantly
- **Layer management** - control UI rendering order directly

---

## Prerequisites

1. Open `Assets/Scenes/SampleScene.unity` in Unity Editor
2. Ensure you have TextMeshPro package installed (Window → Package Manager)

---

## Step 1: Create Main Canvas

1. **Right-click** in Hierarchy → **UI** → **Canvas**
2. **Name it:** `UICanvas`
3. **Configure Canvas component:**
   - Render Mode: **Screen Space - Overlay**
   - Sorting Order: **100**
4. **Add components if missing:**
   - Canvas Scaler
   - Graphic Raycaster

---

## Step 2: Create Event System

If not already present:

1. **Right-click** in Hierarchy → **UI** → **Event System**
2. Ensure it has:
   - EventSystem component
   - StandaloneInputModule component

---

## Step 3: Interval Timer Bar (Left Edge)

### 3.1 Create Background Strip

1. **Right-click** `UICanvas` → **UI** → **Image**
2. **Name it:** `IntervalBarBackground`
3. **Configure Image:**
   - Color: RGB(25, 25, 25), Alpha 178 → #191919B2 (dark semi-transparent)
4. **Configure Rect Transform:**
   - **Anchors:** Left stretch (Min: 0, 0 | Max: 0, 1)
   - **Pivot:** X: 0, Y: 0.5
   - **Pos X:** 0
   - **Pos Y:** 0
   - **Width:** 25
   - **Height:** -40 (leaves 20px padding top/bottom)

### 3.2 Create Fill Bar

1. **Right-click** `IntervalBarBackground` → **UI** → **Image**
2. **Name it:** `IntervalBarFill`
3. **Configure Image:**
   - Color: RGB(0, 212, 255) → #00D4FF (bright cyan)
   - **Image Type:** Filled
   - **Fill Method:** Vertical
   - **Fill Origin:** Bottom
4. **Configure Rect Transform:**
   - **Anchors:** Stretch all (Min: 0, 0 | Max: 1, 1)
   - **Offsets:** All 0
5. **Add Component:** `IntervalUI`
   - This script will control the fill bar

### 3.3 Create Interval Number Text

1. **Right-click** `IntervalBarBackground` → **UI** → **Text - TextMeshPro**
2. **Name it:** `IntervalText`
3. **Configure:**
   - **Text:** "0" (placeholder)
   - **Font:** Bold, 16pt
   - **Color:** White
   - **Alignment:** Center / Middle
4. **Configure Rect Transform:**
   - **Anchors:** Top stretch (Min: 0, 1 | Max: 1, 1)
   - **Pivot:** X: 0.5, Y: 1
   - **Pos Y:** 5
   - **Height:** 30

### 3.4 Assign IntervalUI References

1. **Select** `IntervalBarBackground`
2. **In Inspector**, find `IntervalUI` component
3. **Assign:**
   - **Interval Text:** Drag `IntervalText` here
   - **Vertical Bar:** Drag `IntervalBarFill` here

---

## Step 4: Token Display (Top-Right Corner)

### 4.1 Create Container

1. **Right-click** `UICanvas` → **UI** → **Image**
2. **Name it:** `TokenContainer`
3. **Configure Image:**
   - Color: RGB(38, 38, 51), Alpha 230 → #262633E6 (dark purple-gray)
4. **Configure Rect Transform:**
   - **Anchors:** Top-right (Min: 1, 1 | Max: 1, 1)
   - **Pivot:** X: 1, Y: 1
   - **Pos:** X: -20, Y: -20
   - **Width:** 140
   - **Height:** 60

### 4.2 Create Token Icon

1. **Right-click** `TokenContainer` → **UI** → **Image**
2. **Name it:** `TokenIcon`
3. **Configure Image:**
   - Color: RGB(255, 230, 51) → #FFE633 (gold)
   - **Image Type:** Filled
   - **Fill Method:** Radial 360
   - **Fill Origin:** Top
4. **Configure Rect Transform:**
   - **Anchors:** Left center (Min: 0, 0.5 | Max: 0, 0.5)
   - **Pivot:** X: 0, Y: 0.5
   - **Pos:** X: 15, Y: 0
   - **Width:** 40
   - **Height:** 40

### 4.3 Create Token Text

1. **Right-click** `TokenContainer` → **UI** → **Text - TextMeshPro**
2. **Name it:** `TokenText`
3. **Configure:**
   - **Text:** "10" (placeholder)
   - **Font:** Bold, 36pt
   - **Color:** RGB(255, 242, 204) → #FFF2CC (light cream)
   - **Alignment:** Right / Middle
4. **Configure Rect Transform:**
   - **Anchors:** Right stretch (Min: 0.4, 0 | Max: 1, 1)
   - **Pivot:** X: 1, Y: 0.5
   - **Pos:** X: -10, Y: 0

### 4.4 Add TokenUI Component

1. **Select** `TokenContainer`
2. **Add Component:** `TokenUI`
3. **Assign:**
   - **Token Text:** Drag `TokenText` here
   - **Token Icon:** Drag `TokenIcon` here
   - **Container:** Drag `TokenContainer` itself here

---

## Step 5: Spawn Timeline (Top-Center)

**See:** [SETUP_SPAWN_TIMELINE_UI.md](SETUP_SPAWN_TIMELINE_UI.md) for complete timeline setup.

**Quick Summary:**
1. Create `SpawnTimelineContainer` under `UICanvas` (top-center, 700x120)
2. Add `WaveNumberText`, `DotContainer`, `CountdownText`
3. Add `SpawnTimelineUI` component and assign references

---

## Step 6: Dock Bar (Bottom-Center)

### 6.1 Create DockBarManager GameObject

1. **Right-click** `UICanvas` → **Create Empty**
2. **Name it:** `DockBarManager`
3. **Add Component:** `DockBarManager`
4. **Configure Rect Transform:**
   - **Anchors:** Bottom center (Min: 0.5, 0 | Max: 0.5, 0)
   - **Pivot:** X: 0.5, Y: 0
   - **Pos:** X: 0, Y: 20
   - **Width:** 800
   - **Height:** 120

**Note:** DockBarManager creates its own UI hierarchy at runtime (dock icons panel, deal button, etc.). You only need to create the parent GameObject with the DockBarManager component.

---

## Step 7: Standalone Manager GameObjects

These are NOT under UICanvas - they're standalone GameObjects in the scene root:

### 7.1 DragDropHandler

1. **Right-click** in Hierarchy → **Create Empty**
2. **Name it:** `DragDropHandler`
3. **Add Component:** `DragDropHandler`

### 7.2 HandManager

1. **Right-click** in Hierarchy → **Create Empty**
2. **Name it:** `HandManager`
3. **Add Component:** `HandManager`

---

## Step 8: Debug Components (Optional)

### 8.1 DebugPanel

**Note:** This is complex with many buttons. For now, you can skip this - DebugPanel will show a warning but the game will run.

To create (optional):
1. Create `DebugPanelRoot` under `UICanvas` (center, 400x350, dark background)
2. Add title, buttons (Pause, Speed 1x/2x/4x, +100 Tokens, Clear All)
3. Add `DebugPanel` component to `UICanvas` and assign all button references

### 8.2 HiddenDebugButton

1. **Right-click** `UICanvas` → **UI** → **Image**
2. **Name it:** `HiddenDebugButton`
3. **Configure Image:**
   - Color: Black, Alpha 3 → #000000 (nearly invisible)
4. **Configure Rect Transform:**
   - **Anchors:** Top-right (Min: 1, 1 | Max: 1, 1)
   - **Pivot:** X: 1, Y: 1
   - **Pos:** X: -10, Y: -10
   - **Width:** 100
   - **Height:** 100
5. **Add Component:** `HiddenDebugButton`

**Usage:** Tap this invisible button in top-right to toggle DebugPanel

### 8.3 DebugMenu

1. **Right-click** `UICanvas` → **Create Empty**
2. **Name it:** `DebugMenu`
3. **Add Component:** `DebugMenu`

### 8.4 CellDebugPlacer

1. **Right-click** in Hierarchy (scene root) → **Create Empty**
2. **Name it:** `DebugPlacer`
3. **Add Component:** `CellDebugPlacer`
4. **Assign in Inspector:**
   - Soldier Prefab (GameSetup will auto-assign if left empty)
   - Enemy Soldier Prefab
   - Resource Node Prefab

---

## Step 9: Game Over Manager

1. **Right-click** in Hierarchy (scene root) → **Create Empty**
2. **Name it:** `GameOverManager`
3. **Add Component:** `GameOverManager`

**Note:** GameOverManager creates its own UI hierarchy at runtime (game over panel, buttons, etc.). You only need the manager GameObject.

---

## Final Hierarchy Overview

```
Scene Root
├── UICanvas
│   ├── IntervalBarBackground
│   │   ├── IntervalBarFill [IntervalUI component here]
│   │   └── IntervalText
│   ├── TokenContainer [TokenUI component here]
│   │   ├── TokenIcon
│   │   └── TokenText
│   ├── SpawnTimelineContainer [SpawnTimelineUI component here]
│   │   ├── WaveNumberText
│   │   ├── DotContainer
│   │   └── CountdownText
│   ├── DockBarManager [DockBarManager component here]
│   ├── HiddenDebugButton [HiddenDebugButton component here]
│   ├── DebugPanelRoot (optional)
│   │   └── [buttons, labels, etc.]
│   └── DebugMenu [DebugMenu component here]
├── EventSystem
├── DragDropHandler [DragDropHandler component here]
├── HandManager [HandManager component here]
├── GameOverManager [GameOverManager component here]
└── DebugPlacer [CellDebugPlacer component here]
```

---

## Testing

1. **Save the scene** (Ctrl/Cmd + S)
2. **Enter Play Mode**
3. Check console for warnings about missing components
4. Verify:
   - Interval bar appears on left edge, fills as time passes
   - Token display appears in top-right
   - Spawn timeline appears when WaveManager starts
   - Dock bar appears at bottom-center
   - Can tap top-right corner to show debug panel (if created)

---

## Troubleshooting

**"[Component] not found in scene"**
- You forgot to create that component manually
- Check spelling of GameObject names
- Ensure components are added to correct GameObjects

**UI not visible:**
- Check Canvas render mode is Screen Space - Overlay
- Check anchors and positions are set correctly
- Check that UI elements are children of UICanvas

**Interval bar not updating:**
- Make sure IntervalUI component is on IntervalBarBackground
- Check that intervalText and verticalBar references are assigned

**Token display not updating:**
- Make sure TokenUI component is on TokenContainer
- Check that tokenText and tokenIcon references are assigned

**Timeline doesn't appear:**
- See SETUP_SPAWN_TIMELINE_UI.md for detailed timeline setup
- Ensure SpawnTimelineUI component has all references assigned

---

## Customization

### Colors
- All colors can be tweaked in Inspector on Image components
- Token gold: #FFE633
- Interval bar cyan: #00D4FF
- Enemy red: #D84040
- Resource green: #40D850

### Positions
- Adjust Rect Transform anchors and positions
- Use Unity's Anchor Presets (click anchor icon in Inspector)

### Sizes
- Change Width/Height in Rect Transform
- Adjust font sizes on TextMeshPro components

### Fonts
- Assign custom fonts to TextMeshPro components
- Change Bold/Italic in Font Style dropdown

---

## Next Steps

Once UI is set up:
1. Test in Play Mode to verify all systems work
2. Customize colors, fonts, positions to match your game style
3. Add visual effects (glow, pulse, etc.) using Unity animations
4. Layer UI elements by adjusting order in Hierarchy

All UI is now fully under your control!
