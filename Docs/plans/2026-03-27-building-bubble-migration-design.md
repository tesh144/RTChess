# Building Bubble Migration — Insert/Collect to WorldCanvas Prefab

**Date:** 2026-03-27
**Card:** #155 — Migrate building bubbles (Insert/Collect) to WorldCanvas prefab system

## Summary

Replace legacy procedural popup canvases in BuildingProductionManager with the POIBubble + WorldCanvas_Popups prefab system already used by POI bubbles. Add fill bar support for Insert bubbles and icon resolution for both Insert and Collect.

## Insert Bubble

- Already partially wired via `SpawnInsertBubble()` → `POIBubble.Setup(BubbleType.Bubble_Insert)`
- **Fill bar:** Find Image named "Fill" on the Bubble_Insert child. Update `fillImage.fillAmount = holdFillProgress / (float)EffectiveFillCost` on each `IncrementHoldFill()` call
- **Icon:** Resolve via shared `ResolveIcon()` — resource input uses CurrencyDatabase icon, card input uses UnitStats.icon from CardPool
- **Dismissal:** When fill reaches 100%, dismiss bubble and start production timer

## Collect Bubble

- Replace legacy `CreateDefaultPopupCanvas()` path with `POIBubble.Setup(BubbleType.Bubble_Collect)`
- **Icon:** Same resolution logic but for output type
- **Dismissal:** On tap → `CollectReward()`

## Shared: ResolveIcon()

One method that takes a ProductionEntry + context (input vs output) and returns the appropriate Sprite. Checks CurrencyDatabase for resource types, CardPool/UnitStats for card types.

## Cleanup

Remove `CreateDefaultPopupCanvas()` and its procedural Image/Text generation. Fields `popupCanvasObj`, `popupIconImage`, `popupCollider` on ProductionEntry become unused.
