# Points of Interest System — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Surface interesting fogged objects near the player's explored border as floating world-space bubbles, rewarding discovery with Approval currency.

**Architecture:** POIManager singleton manages two registries (hearts + env objects) and a pool of POIBubble instances. Hearts always show a bubble. Env objects use a rolling window of up to 5 bubbles filtered by fog-border proximity. FogManager.OnCellRevealed drives all state transitions. POIDatabase (synced from Google Sheets) maps asset names to labels, colors, and rewards.

**Tech Stack:** Unity C#, World Space Canvas + TextMeshPro for bubbles, manual Update-based tweening (project does not use DOTween — use the same easing patterns found in TileFog.cs and PoofEffect.cs), SheetSyncEditor for data pipeline.

**Spec:** `docs/superpowers/specs/2026-03-25-poi-system-design.md`

---

## File Map

| Action | File | Responsibility |
|--------|------|----------------|
| Create | `Assets/Scripts/Systems/POIManager.cs` | Singleton. Registries, rolling window, pool, fog event handler, approval rewards. |
| Create | `Assets/Scripts/Systems/POIBubble.cs` | World-space billboard bubble. Pop-in, bob, fade-out animations. Pool-friendly Setup/Dismiss API. |
| Create | `Assets/Scripts/Data/POITypeData.cs` | Serializable data class: typeName, label, color, approvalReward. |
| Create | `Assets/Scripts/Data/POIDatabase.cs` | ScriptableObject holding `List<POITypeData>` with `GetByTypeName(string)` lookup. |
| Create | `Assets/Scripts/Data/POIDatabase.asset` | Runtime asset instance (created via Unity menu after POIDatabase.cs exists). |
| Create | `Assets/Prefabs/UI/POIBubble.prefab` | Human step — Root with POIBubble.cs → Canvas (World Space) → Background Image + TMP Label. |
| Modify | `Assets/Scripts/LittleCafe/CorruptionHeart.cs` | Remove floating indicator. Register/unregister with POIManager. |
| Modify | `Assets/ClockworkCraft/Scripts/Core/MapGeneratorV2.cs` | Register env POIs during spawn loop. Call POIManager.Initialize() after all spawning. |
| Modify | `Assets/Scripts/Editor/SheetSyncEditor.cs` | Add "Points of Interest" sync case. |

---

### Task 1: POITypeData — Data Class

**Files:**
- Create: `Assets/Scripts/Data/POITypeData.cs`

- [ ] **Step 1: Create the data class**

```csharp
using UnityEngine;

namespace ClockworkCraft
{
    [System.Serializable]
    public class POITypeData
    {
        public string typeName;       // keyword matched against assetName (e.g. "Tree", "Corruption")
        public string label;          // displayed on bubble (e.g. "Forest", "Gold")
        public Color bubbleColor;     // bubble background tint
        public int approvalReward;    // Approval currency awarded on discovery
    }
}
```

- [ ] **Step 2: Commit**

```
git add Assets/Scripts/Data/POITypeData.cs
git commit -m "feat(poi): add POITypeData serializable data class"
```

---

### Task 2: POIDatabase — ScriptableObject

**Files:**
- Create: `Assets/Scripts/Data/POIDatabase.cs`

- [ ] **Step 1: Create the database ScriptableObject**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace ClockworkCraft
{
    [CreateAssetMenu(fileName = "POIDatabase", menuName = "RTChess/POI Database")]
    public class POIDatabase : ScriptableObject
    {
        [SerializeField] private List<POITypeData> entries = new List<POITypeData>();

        /// <summary>All entries (for editor sync).</summary>
        public List<POITypeData> Entries => entries;

        /// <summary>
        /// Find the first entry whose typeName appears (case-insensitive) inside the given assetName.
        /// E.g. assetName "PineTree_01" matches typeName "Tree".
        /// Returns null if no match.
        /// </summary>
        public POITypeData GetByTypeName(string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) return null;
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.typeName)) continue;
                if (assetName.IndexOf(entry.typeName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return entry;
            }
            return null;
        }
    }
}
```

- [ ] **Step 2: Commit**

```
git add Assets/Scripts/Data/POIDatabase.cs
git commit -m "feat(poi): add POIDatabase ScriptableObject with fuzzy name matching"
```

---

### Task 3: POIBubble — World-Space Billboard

**Files:**
- Create: `Assets/Scripts/Systems/POIBubble.cs`

This is the visual bubble component. It uses manual Update-based tweening matching project patterns (no DOTween). The prefab itself is a human step — this script goes on the root.

- [ ] **Step 1: Create POIBubble.cs**

```csharp
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace ClockworkCraft
{
    /// <summary>
    /// World-space billboard bubble shown above a POI.
    /// Pooled by POIManager — call Setup() to activate, Dismiss() to fade out and return to pool.
    ///
    /// Prefab structure:
    ///   POIBubble (root, this script)
    ///     └─ Canvas (World Space, sortingOrder=100)
    ///          ├─ Background (Image)
    ///          └─ Label (TextMeshProUGUI)
    /// </summary>
    public class POIBubble : MonoBehaviour
    {
        [Header("References (set on prefab)")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI label;

        // Animation params — injected by POIManager.Setup via SetAnimParams()
        private float popInDuration = 0.25f;
        private float bobHeight = 0.15f;
        private float bobDuration = 1.4f;
        private float fadeOutDuration = 0.4f;

        // State
        private enum State { Inactive, PoppingIn, Bobbing, Dismissing }
        private State state = State.Inactive;
        private float timer;
        private Vector3 basePosition;
        private float bobTimer;

        // ── Public API ──────────────────────────────────────────────────

        /// <summary>Inject animation params from POIManager inspector fields.</summary>
        public void SetAnimParams(float popIn, float bob, float bobDur, float fadeOut)
        {
            popInDuration = popIn;
            bobHeight = bob;
            bobDuration = bobDur;
            fadeOutDuration = fadeOut;
        }

        /// <summary>Activate this bubble at the given world position with label and color.</summary>
        public void Setup(string text, Color color, Vector3 worldPos)
        {
            basePosition = worldPos;
            transform.position = worldPos;

            if (label != null) label.text = text;
            if (background != null) background.color = color;
            if (canvasGroup != null) canvasGroup.alpha = 1f;

            transform.localScale = Vector3.zero;
            gameObject.SetActive(true);

            state = State.PoppingIn;
            timer = 0f;
            bobTimer = 0f;
        }

        /// <summary>Start fade-out, then deactivate and return to pool.</summary>
        public void Dismiss()
        {
            if (state == State.Inactive || state == State.Dismissing) return;
            state = State.Dismissing;
            timer = 0f;
        }

        public bool IsActive => state != State.Inactive;

        // ── Animation ───────────────────────────────────────────────────

        private void Update()
        {
            switch (state)
            {
                case State.PoppingIn:
                    UpdatePopIn();
                    break;
                case State.Bobbing:
                    UpdateBob();
                    break;
                case State.Dismissing:
                    UpdateDismiss();
                    break;
            }
        }

        private void UpdatePopIn()
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / popInDuration);

            // OutBack easing: overshoot then settle
            float eased = 1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f);
            transform.localScale = Vector3.one * eased;

            if (t >= 1f)
            {
                transform.localScale = Vector3.one;
                state = State.Bobbing;
                bobTimer = 0f;
            }
        }

        private void UpdateBob()
        {
            bobTimer += Time.deltaTime;
            // InOutSine yoyo
            float t = Mathf.PingPong(bobTimer / bobDuration, 1f);
            float eased = -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;
            transform.position = basePosition + Vector3.up * (eased * bobHeight);
        }

        private void UpdateDismiss()
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / fadeOutDuration);

            transform.localScale = Vector3.one * (1f - t);
            if (canvasGroup != null) canvasGroup.alpha = 1f - t;

            if (t >= 1f)
            {
                state = State.Inactive;
                gameObject.SetActive(false);
            }
        }

        // ── Billboarding ────────────────────────────────────────────────

        private void LateUpdate()
        {
            if (state == State.Inactive) return;
            if (Camera.main != null)
                transform.rotation = Camera.main.transform.rotation;
        }
    }
}
```

- [ ] **Step 2: Commit**

```
git add Assets/Scripts/Systems/POIBubble.cs
git commit -m "feat(poi): add POIBubble world-space billboard with pop-in, bob, dismiss animations"
```

---

### Task 4: POIManager — Core Singleton

**Files:**
- Create: `Assets/Scripts/Systems/POIManager.cs`

- [ ] **Step 1: Create POIManager.cs**

```csharp
using System.Collections.Generic;
using UnityEngine;
using ClockworkGrid;
using LittleCafe;

namespace ClockworkCraft
{
    /// <summary>
    /// Manages POI bubbles floating above fogged objects near the explored border.
    /// Hearts always show a bubble. Env objects use a rolling window of up to maxEnvBubbles.
    /// Added to the Managers GameObject in the Inspector.
    /// </summary>
    public class POIManager : MonoBehaviour
    {
        public static POIManager Instance { get; private set; }

        [Header("Prefab & Database")]
        [SerializeField] private POIBubble bubblePrefab;
        [SerializeField] private POIDatabase poiDatabase;

        [Header("Window Settings")]
        [Tooltip("Maximum number of env-object POI bubbles shown at once.")]
        [SerializeField] private int maxEnvBubbles = 5;

        [Tooltip("A fog-side env object qualifies if any revealed cell is within this many tiles (Manhattan distance).")]
        [SerializeField] private float fogBorderRadius = 3f;

        [Tooltip("Pre-instantiated pool size for heart bubbles (in addition to maxEnvBubbles).")]
        [SerializeField] private int heartPoolSize = 6;

        [Header("Animation")]
        [SerializeField] private float bobHeight = 0.15f;
        [SerializeField] private float bobDuration = 1.4f;
        [SerializeField] private float popInDuration = 0.25f;
        [SerializeField] private float fadeOutDuration = 0.4f;
        [SerializeField] private float heightAboveGround = 2.5f;

        // ── Registries ──────────────────────────────────────────────────

        private struct EnvPOIEntry
        {
            public Vector2Int gridPos;
            public string assetName;
        }

        private readonly Dictionary<Vector2Int, CorruptionHeart> heartRegistry
            = new Dictionary<Vector2Int, CorruptionHeart>();

        private readonly Dictionary<Vector2Int, EnvPOIEntry> envRegistry
            = new Dictionary<Vector2Int, EnvPOIEntry>();

        // Active bubbles keyed by grid position
        private readonly Dictionary<Vector2Int, POIBubble> activeBubbles
            = new Dictionary<Vector2Int, POIBubble>();

        // Pool
        private readonly List<POIBubble> pool = new List<POIBubble>();

        // ── Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            CreatePool();
        }

        private void OnDestroy()
        {
            if (FogManager.Instance != null)
                FogManager.Instance.OnCellRevealed -= OnCellRevealed;
        }

        // ── Public API ──────────────────────────────────────────────────

        /// <summary>Called by CorruptionHeart after it registers with CorruptionManager.</summary>
        public void RegisterHeart(CorruptionHeart heart)
        {
            if (heart == null) return;
            var pos = heart.GridPosition;
            if (heartRegistry.ContainsKey(pos)) return;
            heartRegistry[pos] = heart;

            // Hearts always get a bubble immediately
            ShowBubble(pos, "Corruption");
        }

        /// <summary>Called by CorruptionHeart.OnDestroy().</summary>
        public void UnregisterHeart(CorruptionHeart heart)
        {
            if (heart == null) return;
            var pos = heart.GridPosition;
            DismissBubble(pos);
            heartRegistry.Remove(pos);
        }

        /// <summary>Called by MapGeneratorV2 after each env object spawn.</summary>
        public void RegisterEnvPOI(Vector2Int gridPos, string assetName)
        {
            if (poiDatabase == null) return;
            // Only register if this asset type has a POI entry
            if (poiDatabase.GetByTypeName(assetName) == null) return;
            if (envRegistry.ContainsKey(gridPos)) return;

            envRegistry[gridPos] = new EnvPOIEntry
            {
                gridPos = gridPos,
                assetName = assetName
            };
        }

        /// <summary>Called by MapGeneratorV2 after all spawning is complete.</summary>
        public void Initialize()
        {
            if (FogManager.Instance != null)
            {
                FogManager.Instance.OnCellRevealed -= OnCellRevealed; // avoid double-subscribe
                FogManager.Instance.OnCellRevealed += OnCellRevealed;
            }

            RefreshEnvWindow();
            Debug.Log($"[POIManager] Initialized. Hearts: {heartRegistry.Count}, Env POIs: {envRegistry.Count}");
        }

        // ── Fog Event ───────────────────────────────────────────────────

        private void OnCellRevealed(int x, int y)
        {
            var coord = new Vector2Int(x, y);

            // Heart discovered
            if (heartRegistry.TryGetValue(coord, out var heart) && heart != null)
            {
                AwardApproval("Corruption");
                DismissBubble(coord);
                heartRegistry.Remove(coord);
            }

            // Env POI discovered
            if (envRegistry.TryGetValue(coord, out var entry))
            {
                AwardApproval(entry.assetName);
                DismissBubble(coord);
                envRegistry.Remove(coord);
            }

            // Border expanded — refresh which env POIs qualify
            RefreshEnvWindow();
        }

        // ── Rolling Window ──────────────────────────────────────────────

        private void RefreshEnvWindow()
        {
            if (FogManager.Instance == null) return;

            // Count current active env bubbles (exclude hearts)
            int activeEnvCount = 0;
            foreach (var kvp in activeBubbles)
            {
                if (!heartRegistry.ContainsKey(kvp.Key) && kvp.Value.IsActive)
                    activeEnvCount++;
            }

            int openSlots = maxEnvBubbles - activeEnvCount;
            if (openSlots <= 0) return;

            // Build candidates: in fog, near border, not already showing a bubble
            var candidates = new List<(Vector2Int pos, EnvPOIEntry entry, int dist)>();

            foreach (var kvp in envRegistry)
            {
                var pos = kvp.Key;
                if (activeBubbles.ContainsKey(pos)) continue;

                // Must still be in fog
                if (FogManager.Instance.IsCellRevealed(pos.x, pos.y)) continue;

                int minDist = MinManhattanDistToRevealed(pos);
                if (minDist <= (int)fogBorderRadius)
                    candidates.Add((pos, kvp.Value, minDist));
            }

            // Sort by distance ascending (most discoverable first)
            candidates.Sort((a, b) => a.dist.CompareTo(b.dist));

            // Fill open slots
            int filled = 0;
            for (int i = 0; i < candidates.Count && filled < openSlots; i++)
            {
                ShowBubble(candidates[i].pos, candidates[i].entry.assetName);
                filled++;
            }
        }

        private int MinManhattanDistToRevealed(Vector2Int pos)
        {
            // Scan a square around pos up to fogBorderRadius
            int radius = (int)fogBorderRadius + 1;
            int minDist = int.MaxValue;

            for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
            {
                int nx = pos.x + dx;
                int ny = pos.y + dy;
                if (FogManager.Instance.IsCellRevealed(nx, ny))
                {
                    int dist = Mathf.Abs(dx) + Mathf.Abs(dy);
                    if (dist < minDist) minDist = dist;
                }
            }

            return minDist;
        }

        // ── Bubble Management ───────────────────────────────────────────

        private void ShowBubble(Vector2Int gridPos, string assetName)
        {
            if (activeBubbles.ContainsKey(gridPos)) return;

            var bubble = GetFromPool();
            if (bubble == null) return;

            var data = poiDatabase != null ? poiDatabase.GetByTypeName(assetName) : null;
            string text = data != null ? data.label : assetName;
            Color color = data != null ? data.bubbleColor : Color.white;

            Vector3 worldPos = GridManager.Instance != null
                ? GridManager.Instance.GridToWorldPosition(gridPos.x, gridPos.y)
                : new Vector3(gridPos.x, 0f, gridPos.y);
            worldPos.y += heightAboveGround;

            bubble.Setup(text, color, worldPos);
            activeBubbles[gridPos] = bubble;
        }

        private void DismissBubble(Vector2Int gridPos)
        {
            if (!activeBubbles.TryGetValue(gridPos, out var bubble)) return;
            bubble.Dismiss();
            activeBubbles.Remove(gridPos);
        }

        private void AwardApproval(string assetName)
        {
            if (poiDatabase == null) return;
            var data = poiDatabase.GetByTypeName(assetName);
            if (data == null || data.approvalReward <= 0) return;

            if (ResourceManager.Instance != null)
                ResourceManager.Instance.AddResource(ResourceType.Approval, data.approvalReward);
        }

        // ── Pool ────────────────────────────────────────────────────────

        private void CreatePool()
        {
            if (bubblePrefab == null) return;
            int total = maxEnvBubbles + heartPoolSize;

            for (int i = 0; i < total; i++)
            {
                var bubble = Instantiate(bubblePrefab, transform);
                bubble.SetAnimParams(popInDuration, bobHeight, bobDuration, fadeOutDuration);
                bubble.gameObject.SetActive(false);
                pool.Add(bubble);
            }
        }

        private POIBubble GetFromPool()
        {
            foreach (var bubble in pool)
            {
                if (!bubble.IsActive && !bubble.gameObject.activeSelf)
                    return bubble;
            }
            // Pool exhausted — create overflow instance
            if (bubblePrefab == null) return null;
            var overflow = Instantiate(bubblePrefab, transform);
            overflow.SetAnimParams(popInDuration, bobHeight, bobDuration, fadeOutDuration);
            overflow.gameObject.SetActive(false);
            pool.Add(overflow);
            return overflow;
        }
    }
}
```

- [ ] **Step 2: Commit**

```
git add Assets/Scripts/Systems/POIManager.cs
git commit -m "feat(poi): add POIManager singleton with registries, rolling window, pool"
```

---

### Task 5: Modify CorruptionHeart — Remove Indicator, Register with POIManager

**Files:**
- Modify: `Assets/Scripts/LittleCafe/CorruptionHeart.cs`

- [ ] **Step 1: Remove the floating indicator field, instance, and method**

Remove these items:
- The `[Header("Visuals")]` block and `floatingIndicatorPrefab` field (line 45-47)
- The `floatingIndicatorInstance` field (line 69)
- The `SpawnFloatingIndicator()` call inside `EnsureInitialized()` (line 112)
- The `if (floatingIndicatorInstance != null) Destroy(floatingIndicatorInstance);` cleanup in `OnDestroy()` (lines 136-137)
- The entire `SpawnFloatingIndicator()` method (lines 289-315)

- [ ] **Step 2: Add POIManager registration**

In `EnsureInitialized()`, at the end (after the CorruptionManager block), add:

```csharp
// Register with POIManager for fog-edge bubble display.
// This lives inside EnsureInitialized (not Start) because FogHideable can
// deactivate hearts before Start() fires — EnsureInitialized is called
// explicitly by MapGeneratorV2 before that happens.
ClockworkCraft.POIManager.Instance?.RegisterHeart(this);
```

In `OnDestroy()`, add:

```csharp
ClockworkCraft.POIManager.Instance?.UnregisterHeart(this);
```

- [ ] **Step 3: Commit**

```
git add Assets/Scripts/LittleCafe/CorruptionHeart.cs
git commit -m "refactor(poi): replace CorruptionHeart floating indicator with POIManager registration"
```

---

### Task 6: Modify MapGeneratorV2 — Register Env POIs

**Files:**
- Modify: `Assets/ClockworkCraft/Scripts/Core/MapGeneratorV2.cs`

- [ ] **Step 1: Add env POI registration in SpawnAllStaggered**

Inside the `SpawnAllStaggered()` coroutine, after the `GridManager.Instance?.PlaceUnit(x, y, obj, CellState.Resource);` line, add:

```csharp
// Register as POI candidate if this env type is in the POI database
POIManager.Instance?.RegisterEnvPOI(new Vector2Int(x, y), envData.assetName);
```

- [ ] **Step 2: Add Initialize call after all spawning**

At the end of the map generation coroutine (after `SpawnAllCorruptionEntitiesStaggered`), add:

```csharp
// Initialize POI system now that all objects are registered
POIManager.Instance?.Initialize();
```

- [ ] **Step 3: Commit**

```
git add Assets/ClockworkCraft/Scripts/Core/MapGeneratorV2.cs
git commit -m "feat(poi): register env POIs during map gen, initialize POIManager after spawn"
```

---

### Task 7: SheetSyncEditor — Add POI Database Sync

**Files:**
- Modify: `Assets/Scripts/Editor/SheetSyncEditor.cs`

- [ ] **Step 1: Add POIDatabase field, discovery, and using statement**

Add `using ClockworkCraft;` at the top of the file if not already present.

Add field near other database fields:

```csharp
private POIDatabase poiDB;
```

Add to `FindDatabases()`:

```csharp
if (poiDB == null)
    poiDB = FindAsset<POIDatabase>();
```

Add ObjectField in the "Database References" OnGUI section (follow the pattern of the existing database ObjectFields):

```csharp
poiDB = (POIDatabase)EditorGUILayout.ObjectField("POI Database", poiDB, typeof(POIDatabase), false);
```

- [ ] **Step 2: Add UI section in OnGUI**

Add a "Points of Interest" section following the same pattern as existing database sections (boxed vertical layout, entry count, sync button).

- [ ] **Step 3: Add SyncPOI method**

```csharp
private void SyncPOI()
{
    if (poiDB == null || cachedData?.sheets == null) return;
    if (!cachedData.sheets.ContainsKey("Points of Interest")) return;

    var sheet = cachedData.sheets["Points of Interest"];
    var entries = poiDB.Entries;
    entries.Clear();

    foreach (var row in sheet.rows)
    {
        string typeName = GetValue(row, "Type");
        if (string.IsNullOrEmpty(typeName)) continue;

        string labelText = GetValue(row, "Label");
        string colorHex = GetValue(row, "Color (hex)");
        string rewardStr = GetValue(row, "Approval Reward");

        Color color = Color.white;
        if (!string.IsNullOrEmpty(colorHex))
            ColorUtility.TryParseHtmlString(colorHex, out color);

        int reward = 0;
        int.TryParse(rewardStr, out reward);

        entries.Add(new POITypeData
        {
            typeName = typeName,
            label = string.IsNullOrEmpty(labelText) ? typeName : labelText,
            bubbleColor = color,
            approvalReward = reward
        });
    }

    UnityEditor.EditorUtility.SetDirty(poiDB);
    UnityEditor.AssetDatabase.SaveAssets();
    SetStatus($"POI synced: {entries.Count} entries", MessageType.Info);
    Debug.Log($"[SheetSync] POI synced: {entries.Count} entries.");
}
```

- [ ] **Step 4: Add to Sync All button**

Add `if (poiDB != null) SyncPOI();` to the Sync All handler.

- [ ] **Step 5: Commit**

```
git add Assets/Scripts/Editor/SheetSyncEditor.cs
git commit -m "feat(poi): add Points of Interest sync case to SheetSyncEditor"
```

---

### Task 8: Create POIDatabase Asset

- [ ] **Step 1: Create the asset**

In Unity: Right-click in `Assets/Scripts/Data/` → Create → RTChess → POI Database. Name it `POIDatabase.asset`.

- [ ] **Step 2: Populate with default entries**

If the Google Sheet tab doesn't exist yet, manually add these entries in the Inspector:

| typeName | label | bubbleColor | approvalReward |
|----------|-------|-------------|----------------|
| Tree | Forest | #4CAF50 | 5 |
| Goldmine | Gold | #FFC107 | 15 |
| Corruption | Corruption | #9C27B0 | 25 |
| Flower | Flowers | #E91E63 | 3 |
| Water | Water | #2196F3 | 5 |

- [ ] **Step 3: Commit**

```
git add Assets/Scripts/Data/POIDatabase.asset Assets/Scripts/Data/POIDatabase.asset.meta
git commit -m "feat(poi): add POIDatabase asset with default entries"
```

---

### Task 9: Verification — Enter Play Mode

- [ ] **Step 1: Verify no compile errors**

Open Unity console. Confirm 0 errors.

- [ ] **Step 2: Enter play mode and explore toward a heart**

Confirm:
- POI bubbles appear floating above fogged objects near the explored border
- Heart bubbles show "Corruption" label in purple
- Env bubbles show appropriate labels (e.g. "Forest" for trees)
- Bubbles pop in with overshoot animation
- Bubbles bob gently up and down
- When the player reveals a POI tile, the bubble dismisses with a fade-out
- Approval currency is awarded on discovery (check console log for `[ResourceManager] +N Approval`)
- No more than 5 env bubbles shown at once
- Old magenta placeholder quad no longer appears on corruption hearts

---

## Human Steps (post-implementation)

- [ ] Create POIBubble prefab: Root (POIBubble.cs + CanvasGroup) → Canvas (World Space, sortingOrder=100, ~1x0.5 world units) → Background (Image) + Label (TextMeshProUGUI) + Arrow (Image, downward-pointing, optional)
- [ ] Add POIManager component to the Managers GameObject in the scene
- [ ] Assign POIBubble prefab and POIDatabase.asset to the POIManager Inspector fields
- [ ] Create "Points of Interest" tab in the Google Sheet with columns: Type, Label, Color (hex), Approval Reward
- [ ] Run SheetSyncEditor to populate POIDatabase.asset from the sheet
