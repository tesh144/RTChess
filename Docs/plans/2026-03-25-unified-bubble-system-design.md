# Unified Bubble System — Design Document

**Date:** 2026-03-25
**Card:** #130 (Points of Interest — fog-edge POI bubbles for nearby objects)
**Status:** Design approved, implementation pending

---

## Overview

A single prefab-based bubble system that handles all world-space popup types in ClockworkCraft: POI fog indicators, building input/output prompts, and alerts. Rolled out incrementally — POI bubbles first, building popup migration later.

## Prefab Architecture

One prefab containing all bubble visual variants as children. A `BubbleType` enum selects which child is active at spawn time. Only the relevant child is enabled; everything else stays dormant.

```
WorldCanvas_POI (scene root, World Space Canvas)
└── Holder
    └── AllBubbles (pool container)
        └── BubblePopup (pooled prefab instance) ← one per active bubble
            ├── POI_Gold    ← Valuable POI (goldmine, treasure)
            ├── POI_Grey    ← Neutral POI (rocks, bones, flowers)
            ├── POI_Red     ← Danger POI (corruption hearts)
            ├── Bubble_Insert   ← HoldToFill resource input (future)
            ├── Bubble_Collect  ← Output ready to collect (future)
            └── Bubble_Alert    ← Attention/problem state (future)
```

Each child shares the same internal structure: background sprite, TMP label, optional icon. They differ in color tint, layout, and which elements are visible.

The existing POIBubble.cs animation code (OutBack pop-in, InOutSine bob, scale+alpha fade-out) operates on the root transform, so it works regardless of which visual child is active.

## POI Value Tiers

POI bubbles use three value-based tiers that map to visual children:

| Tier | Child | Meaning | Examples |
|------|-------|---------|----------|
| Gold | POI_Gold | Valuable resource | Goldmine, treasure |
| Grey | POI_Grey | Neutral environment | Rocks, bones, flowers |
| Red | POI_Red | Danger/threat | Corruption hearts |

Each POI type entry in the database maps to one of these tiers. The tier determines which prefab child activates.

## Manager Architecture

The existing POIManager stays as the foundation. It already provides:

- Object pooling (pre-allocated pool + overflow)
- Rolling window of ~5 env POI bubbles visible at once
- Manhattan distance culling (shows POIs within ~3 tiles of revealed border)
- FogManager integration (dismisses bubbles on cell reveal, awards approval)
- Corruption heart registration (always shown, not subject to rolling window)
- Billboard rotation (world-space canvas faces camera)

No rewrite needed. When building popup migration happens later, the manager gains a generic `SpawnBubble()` API alongside the existing POI-specific methods.

## Data Pipeline

New **"Points of Interest"** tab in the Google Sheet (`1UvfldgEvr3dM_OqHfNyDHi_8qGoiO72CwTDrCRbUNy0`), flowing through the standard pipeline:

```
Google Sheet (POI tab) → SheetCache.json → SheetSyncEditor.cs → POIDatabase.asset
```

The existing POIDatabase.cs ScriptableObject and POITypeData.cs data class are already written. Fields:

- `typeName` — keyword matched against environment assetNames
- `label` — display text on the bubble (e.g. "Forest", "Gold")
- `bubbleColor` — tint color for the background
- `approvalReward` — approval currency awarded on discovery

Additional field needed: `tier` (Gold/Grey/Red) to select which visual child activates.

## Rollout Plan

**First deliverable:** POIDatabase .asset populated from Google Sheet, wired into POIManager. POI_Gold, POI_Grey, POI_Red children active. Fog-edge bubbles appear in-game.

**Future — Building popup migration:** Replace BuildingProductionManager's bespoke canvas/popup code with Bubble_Insert, Bubble_Collect, Bubble_Alert variants from the same prefab. POIManager evolves to handle building bubbles via a generic API.

## Building Bubble Types (Future Reference)

| Type | Child | Trigger | Interaction |
|------|-------|---------|-------------|
| Insert | Bubble_Insert | Building has `HoldToFill` input type | Player pours resources from currency bar |
| Collect | Bubble_Collect | Production timer completes | Player taps to collect output (e.g. "Worker") |
| Alert | Bubble_Alert | TBD | TBD — problem/attention states |

These exist in the prefab but remain dormant until the building migration phase.

## Affected Systems

- `POIManager.cs` — wire up new prefab, configure pool from WorldCanvas_POI hierarchy
- `POIBubble.cs` — add BubbleType enum, child toggle logic
- `POIDatabase.asset` — create instance, populate from sheet
- `POITypeData.cs` — add tier field
- `SheetSyncEditor.cs` — add SyncPOI() for new sheet tab
- `SheetCache.json` — cache POI sheet data
- `FogManager.cs` — already integrated, no changes needed
- `CorruptionHeart.cs` — already registers with POIManager, no changes needed

## Existing Code State

POIBubble.cs and POIManager.cs are ~95% complete. Animation system is fully implemented with easing functions. POIDatabase.cs and POITypeData.cs exist as empty shells. The main gap is: no prefab asset, no populated database, no sheet tab.
