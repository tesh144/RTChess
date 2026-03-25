# Points of Interest — Fog-Edge POI Bubbles

**Date:** 2026-03-25
**Trello:** #130
**Status:** Approved for implementation

---

## Overview

A system that surfaces interesting objects hidden just beyond the player's explored border, giving exploration direction and rewarding discovery. World-space billboard bubbles float above fogged entities near the fog frontier. Corruption hearts always show a bubble. Up to five environment POI bubbles rotate through a rolling window as the player explores.

---

## Architecture

### New Files

| File | Purpose |
|---|---|
| `Assets/Scripts/Systems/POIManager.cs` | Singleton MonoBehaviour. Inspector-configured. Manages registries, rolling window, pool. |
| `Assets/Scripts/Systems/POIBubble.cs` | World-space billboard. DOTween pop-in, bob, fade-out. Returned to pool on dismiss. |
| `Assets/Scripts/Data/POITypeData.cs` | Plain serializable data class: typeName, label, color, approvalReward. |
| `Assets/Scripts/Data/POIDatabase.cs` | ScriptableObject. `List<POITypeData> entries`. `GetByTypeName(string)` lookup. |
| `Assets/Scripts/Data/POIDatabase.asset` | Runtime asset. Synced from Google Sheet via SheetSyncEditor. |
| `Assets/Prefabs/UI/POIBubble.prefab` | Root → Canvas (World Space) → Background Image + TMP Label. |

### Modified Files

| File | Change |
|---|---|
| `Assets/Scripts/LittleCafe/CorruptionHeart.cs` | Remove `SpawnFloatingIndicator()`, `floatingIndicatorPrefab`, `floatingIndicatorInstance`. Register/unregister with POIManager. |
| `Assets/ClockworkCraft/Scripts/Core/MapGeneratorV2.cs` | Register env POIs after each env spawn. Call `POIManager.Instance?.Initialize()` after all spawning. |

---

## POIManager

A plain MonoBehaviour added to the scene's Managers GameObject in the Inspector. All visual and tuning parameters are serialized.

### Inspector Fields

```csharp
[Header("Prefab & Database")]
[SerializeField] private POIBubble bubblePrefab;
[SerializeField] private POIDatabase poiDatabase;

[Header("Window Settings")]
[Tooltip("Maximum number of env-object POI bubbles shown at once.")]
[SerializeField] private int maxEnvBubbles = 5;

[Tooltip("A fog-side env object qualifies for the window if any revealed cell is within this many tiles.")]
[SerializeField] private float fogBorderRadius = 3f;

[Tooltip("Pre-instantiated pool size for heart bubbles (in addition to maxEnvBubbles).")]
[SerializeField] private int heartPoolSize = 6;

[Header("Animation")]
[SerializeField] private float bobHeight = 0.15f;
[SerializeField] private float bobDuration = 1.4f;
[SerializeField] private float popInDuration = 0.25f;
[SerializeField] private float fadeOutDuration = 0.4f;
[SerializeField] private float heightAboveGround = 2.5f;
```

### Registries

Two internal lists maintained at runtime:

- `heartRegistry` — all `CorruptionHeart` instances still in fog. Each always has a bubble.
- `envRegistry` — all env POI positions+types still in fog. Up to `maxEnvBubbles` shown at once via rolling window.

Both use `Vector2Int` grid positions as keys.

### Public API

```csharp
// Called by CorruptionHeart.Start()
public void RegisterHeart(CorruptionHeart heart);

// Called by CorruptionHeart.OnDestroy() or when heart is destroyed
public void UnregisterHeart(CorruptionHeart heart);

// Called by MapGeneratorV2 after each env object spawn
public void RegisterEnvPOI(Vector2Int gridPos, string assetName);

// Called by MapGeneratorV2 after all spawning is complete
public void Initialize();
```

All methods null-guard internally and are safe to call when POIManager is not in the scene.

### Rolling Window Algorithm

**Heart bubbles:** All hearts in `heartRegistry` always have an active bubble. No cap, no distance filter.

**Env bubble selection** (runs on `Initialize()` and each `FogManager.OnCellRevealed` event):

1. Filter `envRegistry` to candidates where the nearest revealed cell (Manhattan distance) is within `fogBorderRadius`.
2. Exclude entries already assigned an active bubble.
3. Sort ascending by distance to nearest revealed cell (most immediately discoverable first).
4. Fill open slots up to `maxEnvBubbles` from the sorted list.

### OnCellRevealed Handler

```
OnCellRevealed(int x, int y):
  coord = Vector2Int(x, y)
  if heartRegistry contains coord:
    → AwardApproval(heart type)
    → DismissBubble(coord)
    → Remove from heartRegistry
  if envRegistry contains coord:
    → AwardApproval(env type)
    → DismissBubble(coord)
    → Remove from envRegistry
  → Run env candidate selection (border has expanded)
```

### Approval Reward

```csharp
var data = poiDatabase.GetByTypeName(typeName);
if (data != null && data.approvalReward > 0)
    ResourceManager.Instance?.AddResource(ResourceType.Approval, data.approvalReward);
```

### Object Pool

Pre-instantiated on `Awake()`. Pool size = `maxEnvBubbles + heartPoolSize`. `Setup()` activates a pooled instance; dismiss deactivates and returns it. All instances are children of POIManager's transform to keep the hierarchy clean.

---

## POIBubble

### Prefab Structure

```
POIBubble (root)              ← POIBubble.cs
  └─ Canvas (World Space)
       ├─ Background          ← Image (sprite assigned in POIManager Inspector)
       ├─ Label               ← TextMeshPro
       └─ Arrow               ← Image, downward-pointing (optional)
```

Canvas size: ~1 × 0.5 world units. Sort layer: above world objects.

### API

```csharp
public void Setup(string label, Color color, Vector3 worldPos);
public void Dismiss();  // triggers fade-out then returns to pool
```

### Animations (DOTween)

| Animation | Implementation |
|---|---|
| Pop-in | `transform.DOScale(1f, popInDuration).From(0f).SetEase(Ease.OutBack)` |
| Bob | `transform.DOLocalMoveY(+bobHeight, bobDuration).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine)` |
| Dismiss | Parallel: scale → 0 + canvasGroup alpha → 0 over `fadeOutDuration`, then `gameObject.SetActive(false)` |

Bob tween uses `SetRelative()` from the `heightAboveGround` base position.

### Billboarding

```csharp
private void LateUpdate()
{
    if (Camera.main != null)
        transform.rotation = Camera.main.transform.rotation;
}
```

---

## Data Pipeline

### Google Sheet — new "Points of Interest" tab

| Type | Label | Color (hex) | Approval Reward |
|---|---|---|---|
| Tree | Forest | #4CAF50 | 5 |
| Goldmine | Gold | #FFC107 | 15 |
| Corruption | Corruption | #9C27B0 | 25 |
| Flower | Flowers | #E91E63 | 3 |
| Water | Water | #2196F3 | 5 |

### POITypeData.cs

```csharp
[System.Serializable]
public class POITypeData
{
    public string typeName;       // keyword matched against assetName
    public string label;          // displayed on bubble
    public Color bubbleColor;
    public int approvalReward;
}
```

### POIDatabase.cs

```csharp
[CreateAssetMenu(fileName = "POIDatabase", menuName = "RTChess/POI Database")]
public class POIDatabase : ScriptableObject
{
    [SerializeField] private List<POITypeData> entries;

    public POITypeData GetByTypeName(string assetName)
    {
        // Case-insensitive contains check: assetName.Contains(entry.typeName)
        // Returns first match, or null if none
    }
}
```

**Env object matching:** `assetName.Contains(entry.typeName)`, case-insensitive. E.g., asset "PineTree_01" matches type "Tree". Same loose-matching pattern used elsewhere in the codebase.

**Corruption hearts:** matched by passing the string `"Corruption"` directly from `RegisterHeart()`.

---

## CorruptionHeart Changes

Remove entirely:
- `[SerializeField] private GameObject floatingIndicatorPrefab` (and its `[Header("Visuals")]` block)
- `private GameObject floatingIndicatorInstance`
- `private void SpawnFloatingIndicator()`
- The call to `SpawnFloatingIndicator()` in `EnsureInitialized()`
- The `Destroy(floatingIndicatorInstance)` cleanup in `EnsureInitialized()`

Add:

```csharp
// In Start(), after RegisterHeart() / EnsureInitialized():
POIManager.Instance?.RegisterHeart(this);

// In OnDestroy():
POIManager.Instance?.UnregisterHeart(this);
```

---

## MapGeneratorV2 Changes

After each environment object is spawned in the environment spawn loop:

```csharp
POIManager.Instance?.RegisterEnvPOI(new Vector2Int(x, y), entry.assetName);
```

After all spawn phases are complete (end of map generation):

```csharp
POIManager.Instance?.Initialize();
```

---

## Human Steps (after implementation)

- [ ] Add POIManager component to scene's Managers GameObject
- [ ] Assign `POIBubble` prefab to the `bubblePrefab` field
- [ ] Assign `POIDatabase.asset` to the `poiDatabase` field
- [ ] Create "Points of Interest" tab in the Google Sheet with the columns above
- [ ] Run SheetSyncEditor to populate `POIDatabase.asset`

---

## Out of Scope (future passes)

- Cards-in-hand tooltips (tracked separately in #116)
- POI icons/sprites per type (placeholder colour-coded bubbles for now)
- POI sounds on discovery
- Minimap integration
