# Little Cafe 2D Sprites

This folder contains 2D icon sprites used for the UI card system in Little Cafe. These are the visual representations players see in the dock bar before dragging equipment into the scene.

## File Naming Convention

Save your Hyper 3D generated images here with these names:
- `counter_icon.png` - Kitchen Counter (1×1)
- `stove_icon.png` - Cooking Stove (1×1)
- `sink_icon.png` - Washing Sink (1×1)
- `plate_rack_icon.png` - Plate Rack (1×1)
- `bin_icon.png` - Trash Bin (small)
- `pot_icon.png` - Cooking Pot (small)
- `chair_icon.png` - Dining Chair (small)
- `table_icon.png` - Dining Table (2×1)

## Processing TODO

Once images are uploaded here, they need to be processed to:
1. Remove organic circular shadows/bases
2. Add clean geometric grid footprint indicators:
   - 1×1 items: Square base outline
   - 2×1 table: Rectangular base
   - Small items: Smaller square bases
3. Export as transparent PNGs

## Unity Import Settings

After processing, configure in Unity:
- **Texture Type**: Sprite (2D and UI)
- **Sprite Mode**: Single
- **Pixels Per Unit**: 100 (or adjust based on your UI scale)
- **Filter Mode**: Bilinear
- **Compression**: None (for icon quality)
- **Max Size**: 512 or 1024

## Usage

These sprites will be used in:
- DockBarManager UI system
- UnitIcon components for drag-and-drop
- Card-based equipment selection interface
