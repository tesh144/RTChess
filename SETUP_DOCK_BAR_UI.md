# Dock Bar UI Setup Guide

This guide shows you how to connect your existing dock bar UI (the red card holders and white draw button) to the DockBarManager system.

---

## Your Current UI Setup

Based on your screenshot:
- **Red cards at bottom** = Container for unit icons (cards you've drawn)
- **White square button on right** = Draw/Gacha button (click to spend tokens and draw a new card)

---

## Setup Steps

### 1. Identify Your UI Elements in the Hierarchy

You should have something like:

```
UICanvas
├── DockBarBackground (or similar parent container)
│   ├── DockIconsPanel ← Parent container for the red cards
│   └── DrawButton ← The white button on the right
```

If you don't have a clear structure yet, organize it like this:

1. **Create container for icons** (if not exists):
   - Right-click UICanvas → Create Empty
   - Name: `DockIconsPanel`
   - Position it where your red cards are
   - Add Component: `HorizontalLayoutGroup` (optional, DockBarManager will add if missing)

2. **Create Draw Button** (if not exists):
   - Right-click UICanvas → UI → Button
   - Name: `DrawButton`
   - Position it on the right side (where white button is)
   - Add Component: `Button` (should already have)

### 2. Add DockBarManager Component

1. **Create DockBarManager GameObject**:
   - Right-click in Hierarchy → Create Empty
   - Name: `DockBarManager`
   - Position: Anywhere (doesn't need to be under UICanvas)

2. **Add Component**:
   - Select DockBarManager
   - Add Component → Search "DockBarManager"

### 3. Assign References in Inspector

Select `DockBarManager` in Hierarchy, then in Inspector:

**Editor UI References:**
- **Dock Icons Container**: Drag your icon container from Hierarchy (the parent of red cards)
- **Draw Button**: Drag your white button from Hierarchy
- **Draw Button Text** (optional): Leave empty if button already has TextMeshProUGUI child, or drag text component

**Runtime Creation Settings:**
- **Create UI At Runtime**: Leave UNCHECKED (false) since you're using editor UI

### 4. Structure Your Dock Icons Container

The container for red cards should have:
- **RectTransform** component
- **HorizontalLayoutGroup** component (optional, will be auto-added)

**Recommended settings for HorizontalLayoutGroup:**
```
Spacing: 10
Padding: Left 10, Right 10, Top 10, Bottom 10
Child Alignment: Middle Center
Child Control Width: OFF
Child Control Height: OFF
Child Force Expand: OFF (both)
```

### 5. Setup Draw Button

Your draw button should have:
- **Button** component
- **Image** component (for background)
- **TextMeshProUGUI** child (for "Draw: 3T" text)

**Button Colors (optional):**
```
Normal: Green-ish (0.2, 0.6, 0.2)
Highlighted: Brighter green (0.3, 0.8, 0.3)
Pressed: Dark green (0.1, 0.4, 0.1)
Disabled: Gray (0.3, 0.3, 0.3)
```

**Text Settings:**
```
Font Size: 20
Alignment: Center
Color: White
Font Style: Bold
```

---

## How It Works

### When Game Starts:
1. **GameSetup.SetupDockBar()** finds DockBarManager, HandManager, DragDropHandler
2. **DockBarManager.Initialize()** is called
3. DockBarManager detects you've assigned editor UI references
4. Connects draw button click → `OnDealButtonClicked()`
5. **HandManager.GiveStartingHand()** adds 3 Soldier cards
6. **DockBarManager.OnHandChanged()** spawns 3 UnitIcon instances in your container

### When You Click Draw Button:
1. Button calls `DockBarManager.OnDealButtonClicked()`
2. Checks if you have enough tokens (starts at 2T, then 3T, 4T, etc.)
3. Calls `HandManager.DrawUnit()` which:
   - Spends tokens via ResourceTokenManager
   - Draws random unit from RaritySystem (weighted: 60% Soldier, 35% Ninja, 5% Ogre)
   - Adds to hand (max 10 cards)
4. `OnHandChanged` event fires → DockBarManager refreshes UI
5. New UnitIcon appears in your dock container

### When You Drag a Card:
1. **UnitIcon** detects drag start → calls `DragDropHandler.StartDrag()`
2. **DragDropHandler** creates ghost preview in 3D world
3. Validates placement as you move mouse (green = valid, red = invalid)
4. On drop:
   - Valid: Spawns unit on grid (FREE, no tokens), removes card from dock
   - Invalid: Card snaps back to dock

---

## Visual Hierarchy Example

```
UICanvas
├── IntervalBarBackground (existing)
├── TokenContainer (existing)
├── SpawnTimelineContainer (existing)
└── DockBarBackground (NEW - your existing UI)
    ├── DockIconsPanel (holds red cards)
    │   ├── UnitIcon (spawned at runtime)
    │   ├── UnitIcon (spawned at runtime)
    │   └── ... (up to 10 cards)
    └── DrawButton (white button)
        └── ButtonText (TextMeshProUGUI)

DockBarManager (standalone GameObject with DockBarManager component)
HandManager (standalone GameObject with HandManager component)
DragDropHandler (standalone GameObject with DragDropHandler component)
```

---

## Testing

### Test 1: Game Starts with 3 Soldiers
1. Enter Play Mode
2. Check Console: "Starting hand: 3 Soldiers"
3. Verify 3 blue card icons appear in dock
4. Each card should show:
   - Blue background (Soldier color)
   - "Soldier" label above
   - "3" cost badge below

### Test 2: Draw Button
1. Check starting tokens (should be 10)
2. Draw button shows "Draw:\n2T"
3. Click button
4. 2 tokens spent → 8 remaining
5. New card appears in dock (random: Soldier/Ninja/Ogre)
6. Button updates to "Draw:\n3T"

### Test 3: Drag Card to Grid
1. Click and drag a card from dock
2. Ghost preview appears in 3D world
3. Ghost is green when over empty cell, red when invalid
4. Drop on valid cell:
   - Unit spawns on grid
   - Card removed from dock
   - Unit is grayed out for 2 intervals (cooldown)

### Test 4: Hand Full
1. Draw 10 cards total (hand limit)
2. Button displays "Hand\nFull!"
3. Button is disabled (grayed out)
4. Place a unit to free space
5. Button re-enables

---

## Expected Console Output

```
SetupDockBar: Found and initialized dock bar components
HandManager initialized with RaritySystem
Starting hand: 3 Soldiers
DockBarManager: Using editor-created UI elements
```

---

## Troubleshooting

### Problem: "No UI references assigned" error
**Solution:** Ensure you've dragged DockIconsContainer and DrawButton into Inspector fields

### Problem: Cards don't appear when game starts
**Solution:**
- Check HandManager exists in scene
- Check Console for "Starting hand: 3 Soldiers"
- Verify DockIconsContainer reference is assigned

### Problem: Draw button doesn't work
**Solution:**
- Ensure Button component is on the button GameObject
- Check button has `Interactable` enabled
- Verify you have enough tokens

### Problem: Cards appear but can't drag them
**Solution:**
- Ensure DragDropHandler exists in scene as standalone GameObject
- Check DragDropHandler has the component attached
- Verify UnitIcon script is on spawned card objects

### Problem: Drag works but placement fails
**Solution:**
- Ensure GridManager is initialized
- Check target cell is empty (not occupied)
- Check cell is revealed (Fog of War system)

---

## Card Visual Details

Each card that spawns will automatically have:

**Unit Type Colors:**
- Soldier (Common): Blue `(0.3, 0.5, 1.0)`
- Ninja (Rare): Green `(0.3, 1.0, 0.5)`
- Ogre (Epic): Red `(1.0, 0.3, 0.3)`

**Card Layout:**
```
   Soldier     ← Type label (top)
  ┌────────┐
  │        │
  │  BLUE  │   ← Main card (70x70px)
  │        │
  └────────┘
      3        ← Cost badge (bottom)
```

**Hover Effect:**
- Card scales to 1.2× size on mouse hover (macOS dock magnification style)

**Draw Cost Escalation:**
- 1st draw: 2 tokens
- 2nd draw: 3 tokens
- 3rd draw: 4 tokens
- Continues: +1 per draw

---

## Next Steps

Once your dock bar is working:
1. Customize card visuals (add unit portraits, fancier backgrounds)
2. Add audio feedback for draw and placement
3. Add card draw animation (slide in from right)
4. Add particle effects for rare draws
5. Customize button appearance to match your art style

The core system is now connected and functional!
