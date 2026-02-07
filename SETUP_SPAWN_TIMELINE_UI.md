# Spawn Timeline UI Setup Instructions

This document explains how to manually create the Spawn Timeline UI in the Unity Editor so it's visible and editable in the scene hierarchy.

---

## Why Manual Setup?

Previously, the timeline UI was created at runtime via code, making it invisible in the editor and hard to customize. Now you'll create it manually in the scene hierarchy, giving you full control over:
- Positioning and sizing
- Colors and fonts
- Layer ordering
- Visual tweaks

---

## Setup Steps

### 1. Open the Scene

Open `Assets/Scenes/SampleScene.unity` in Unity Editor.

### 2. Locate the UICanvas

In the Hierarchy, find the `UICanvas` GameObject (it should already exist from GameSetup.cs).

### 3. Create the Timeline Container

1. **Right-click** on `UICanvas` → **Create Empty**
2. **Name it:** `SpawnTimelineContainer`
3. **Add Component:** `Rect Transform`
4. **Configure Rect Transform:**
   - **Anchors:** Center-Top (X: 0.5, Y: 1.0)
   - **Pivot:** X: 0.5, Y: 1.0
   - **Pos Y:** -60 (60 pixels below top edge)
   - **Width:** 700
   - **Height:** 120

### 4. Create Wave Title Text

1. **Right-click** `SpawnTimelineContainer` → **UI** → **Text - TextMeshPro**
2. **Name it:** `WaveNumberText`
3. **Configure:**
   - **Text:** "WAVE 1" (placeholder)
   - **Font:** Bold, 36pt
   - **Color:** Red (#D84040) - RGB(216, 64, 64)
   - **Alignment:** Center
   - **Anchors:** Stretch top 30%
     - Anchor Min: (0, 0.7)
     - Anchor Max: (1, 1)

### 5. Create Dot Container

1. **Right-click** `SpawnTimelineContainer` → **Create Empty**
2. **Name it:** `DotContainer`
3. **Configure Rect Transform:**
   - **Anchors:** Center (0.5, 0.4)
   - **Pivot:** Center (0.5, 0.5)
   - **Pos:** (0, 0)
   - **Width:** 600
   - **Height:** 30

### 6. Add SpawnTimelineUI Component

1. **Select** `SpawnTimelineContainer` in Hierarchy
2. **Add Component:** `SpawnTimelineUI`
3. **Assign References in Inspector:**
   - **Wave Number Text:** Drag `WaveNumberText` here
   - **Dot Container:** Drag `DotContainer` here
4. **Visual Settings (Optional):**
   - Dot Spacing: 60 (default)
   - Line Width: 3 (default)
   - Enemy Color: #D84040 (red)
   - Resource Color: #40D850 (green)
   - Empty Color: #808080 (gray)

---

## Visual Hierarchy

Your final hierarchy should look like this:

```
UICanvas
├── IntervalBarBackground (existing)
├── TokenContainer (existing)
├── DockBarManager (existing)
└── SpawnTimelineContainer ✨ NEW
    ├── WaveNumberText (TextMeshPro)
    └── DotContainer (dots and lines spawn here at runtime)
```

---

## Testing

1. **Save the scene**
2. **Enter Play Mode**
3. The WaveManager should automatically call `SpawnTimelineUI.Instance.InitializeWave()` when a wave starts
4. Dots and connecting lines will appear dynamically in the `DotContainer`
5. The active dot will pulse, completed dots fade to 50% opacity
6. Peace period shows grayed message: "Peace Period - X ticks until Wave Y"

---

## Customization Tips

### Change Timeline Position
- Adjust `SpawnTimelineContainer` **Pos Y** to move up/down
- Change **Anchors** to position left/right

### Adjust Dot Spacing
- Select `SpawnTimelineContainer`
- In Inspector, change **Dot Spacing** (default: 60px)

### Modify Colors
- Enemy Color: Red spawns
- Resource Color: Green spawns
- Empty Color: Gray "nothing" ticks

### Font Style
- Change font on `WaveNumberText` (TextMeshPro font asset)
- Adjust sizes, bold/italic, etc.

---

## Integration with WaveManager

The WaveManager will automatically find and control the timeline:

```csharp
// WaveManager calls these methods automatically:
SpawnTimelineUI.Instance.InitializeWave(waveNumber, "10102");
SpawnTimelineUI.Instance.AdvanceDot(); // Each spawn event
SpawnTimelineUI.Instance.ShowPeacePeriod(ticks, nextWave, nextCode);
```

You don't need to write any code - just create the UI hierarchy and assign the references!

---

## Troubleshooting

**Timeline doesn't appear:**
- Check that `SpawnTimelineContainer` has the `SpawnTimelineUI` component attached
- Verify all references are assigned in the Inspector
- Check Console for errors

**Dots appear in wrong position:**
- Verify `DotContainer` anchors are set to center (0.5, 0.4)
- Check that `DotContainer` width is at least 600px

**Colors look wrong:**
- Check the Visual Settings in `SpawnTimelineUI` Inspector
- Verify RGB values match the spec (D84040, 40D850, 808080)

---

## Next Steps

Once the UI is set up, you can:
1. Test with different spawn codes in WaveManager
2. Adjust visual settings to match your game's style
3. Add animations or effects to the container
4. Layer the timeline with other UI elements

The timeline is now fully under your control in the Unity Editor!
