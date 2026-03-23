# Corruption System — Implementation Plan
**Date:** 2026-03-23
**Spec:** `docs/superpowers/specs/2026-03-23-corruption-system-design.md`

---

## Order of Implementation

Dependencies flow in this direction:
`BuildingProductionManager` → `CorruptionHeart` → `CorruptionOverlay` → `CorruptionManager` → `MapGeneratorV2` → `GridEntityActor`

Work through each step in order. Each step should compile cleanly before moving to the next.

---

## Step 1 — BuildingProductionManager: Add Pause/Resume API

**File:** `Assets/Scripts/LittleCafe/BuildingProductionManager.cs`

Find the internal data structure that tracks a building's production entry (likely a class or struct in the file). Add a `bool isPaused` field to it.

Add two public methods:

```csharp
public void PauseBuilding(GameObject building)
{
    // Find the entry for this building
    // If not found or already paused, return (idempotent)
    // Set entry.isPaused = true
}

public void ResumeBuilding(GameObject building)
{
    // Find the entry for this building
    // If not found or not paused, return (idempotent)
    // Set entry.isPaused = false
}
```

In the existing tick/update method that processes production, add a guard at the top of each entry's processing:
```csharp
if (entry.isPaused) continue;
```

The timer value is NOT reset — it preserves its current countdown when paused.

**Test:** Confirm the file compiles. No behaviour change expected yet.

---

## Step 2 — CorruptionHeart: New MonoBehaviour

**File:** `Assets/Scripts/LittleCafe/CorruptionHeart.cs`
**Namespace:** `LittleCafe`

```csharp
public class CorruptionHeart : MonoBehaviour
{
    [SerializeField] private int maxHP = 10;
    [SerializeField] private GameObject floatingIndicator; // Assign in Inspector or spawned in Start

    public bool IsActive { get; private set; } = false;
    public Vector2Int GridPosition { get; set; } // Set by map gen before Start runs

    private GridEntityHealth health;

    private void Awake()
    {
        health = gameObject.AddComponent<GridEntityHealth>();
        // Configure health maxHP — GridEntityHealth needs a way to set maxHP at runtime
        // Follow the same pattern used elsewhere in the codebase for runtime HP assignment
    }

    private void Start()
    {
        health.OnEntityDestroyed += OnHeartDestroyed;
        if (CorruptionManager.Instance != null)
            CorruptionManager.Instance.RegisterHeart(this);
    }

    public void Activate()
    {
        IsActive = true;
    }

    private void OnHeartDestroyed(GridEntityHealth _)
    {
        if (CorruptionManager.Instance != null)
            CorruptionManager.Instance.ClearHeartCluster(this);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        health.OnEntityDestroyed -= OnHeartDestroyed;
    }
}
```

Note on `floatingIndicator`: if no prefab is assigned, spawn a simple placeholder quad in `Start()` so the system works before art is ready. Position it `+2f` on the Y axis above the heart's world position. Ensure its renderer uses a layer/sorting order that renders above fog — check how `TileFog` handles its layer and place the indicator above it.

**Test:** File compiles. No wiring yet.

---

## Step 3 — CorruptionOverlay: New MonoBehaviour

**File:** `Assets/Scripts/LittleCafe/CorruptionOverlay.cs`
**Namespace:** `LittleCafe`

```csharp
public class CorruptionOverlay : MonoBehaviour
{
    [SerializeField] private int maxHP = 3;

    // Set by CorruptionManager immediately after AddComponent
    public CorruptionHeart OwnerHeart { get; set; }
    public Vector2Int GridPosition { get; set; }

    public GridEntityHealth Health { get; private set; }

    private GameObject visualChild;
    private GameObject pausedOccupant;
    private System.Action<GridEntityHealth> occupantDeathHandler;

    private void Awake()
    {
        Health = gameObject.AddComponent<GridEntityHealth>();
        // Set Health.maxHP = maxHP (follow existing runtime HP assignment pattern)
    }

    private void Start()
    {
        Health.OnEntityDestroyed += OnOverlayDestroyed;

        // Spawn placeholder visual as child (replace with real prefab later)
        visualChild = GameObject.CreatePrimitive(PrimitiveType.Quad);
        visualChild.transform.SetParent(transform);
        visualChild.transform.localPosition = Vector3.up * 0.6f;
        visualChild.transform.localScale = Vector3.one * 0.8f;
        // Tint it purple/dark as placeholder
        var r = visualChild.GetComponent<MeshRenderer>();
        if (r != null) r.material.color = new Color(0.5f, 0f, 0.8f, 0.7f);
    }

    /// <summary>
    /// Called by CorruptionManager after setting GridPosition/OwnerHeart.
    /// Pauses a building occupant if one is present.
    /// </summary>
    public void InitWithOccupant(GameObject occupant)
    {
        if (occupant == null) return;

        // Check if it's a building with a production entry
        if (BuildingProductionManager.Instance != null)
        {
            BuildingProductionManager.Instance.PauseBuilding(occupant);
            pausedOccupant = occupant;

            // Subscribe to occupant death to clean up the cached reference
            var occupantHealth = occupant.GetComponent<GridEntityHealth>();
            if (occupantHealth != null)
            {
                occupantDeathHandler = (_) => { pausedOccupant = null; };
                occupantHealth.OnEntityDestroyed += occupantDeathHandler;
            }
        }
    }

    private void OnOverlayDestroyed(GridEntityHealth _)
    {
        if (CorruptionManager.Instance != null)
            CorruptionManager.Instance.ClearTile(GridPosition.x, GridPosition.y, OwnerHeart);
    }

    /// <summary>Called by CorruptionManager.ClearTile before destroying this component.</summary>
    public void Cleanup()
    {
        if (pausedOccupant != null && BuildingProductionManager.Instance != null)
            BuildingProductionManager.Instance.ResumeBuilding(pausedOccupant);

        // Unsubscribe occupant death handler
        if (pausedOccupant != null)
        {
            var occupantHealth = pausedOccupant.GetComponent<GridEntityHealth>();
            if (occupantHealth != null && occupantDeathHandler != null)
                occupantHealth.OnEntityDestroyed -= occupantDeathHandler;
        }

        if (visualChild != null) Destroy(visualChild);
    }

    private void OnDestroy()
    {
        Health.OnEntityDestroyed -= OnOverlayDestroyed;
    }
}
```

**Test:** File compiles. No wiring yet.

---

## Step 4 — CorruptionManager: New Singleton

**File:** `Assets/Scripts/Systems/CorruptionManager.cs`
**Namespace:** `LittleCafe` (or `ClockworkGrid` — match whichever namespace `EconomyManager` uses)

```csharp
public class CorruptionManager : MonoBehaviour
{
    public static CorruptionManager Instance { get; private set; }

    [SerializeField] private float spreadInterval = 30f;
    [SerializeField] private int heartActivationRadius = 5;

    private readonly List<CorruptionHeart> allHearts = new();
    private readonly Dictionary<CorruptionHeart, HashSet<Vector2Int>> heartTiles = new();
    private readonly HashSet<Vector2Int> allCorruptedTiles = new();

    private float spreadTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        spreadTimer = spreadInterval;
        if (FogManager.Instance != null)
            FogManager.Instance.OnCellRevealed += OnCellRevealed;
    }

    private void Update()
    {
        spreadTimer -= Time.deltaTime;
        if (spreadTimer <= 0f)
        {
            spreadTimer = spreadInterval;
            SpreadAll();
        }
    }

    // ── Registration ──────────────────────────────────────────────────────

    public void RegisterHeart(CorruptionHeart heart)
    {
        if (!allHearts.Contains(heart))
        {
            allHearts.Add(heart);
            heartTiles[heart] = new HashSet<Vector2Int>();
        }
    }

    // ── Corruption ────────────────────────────────────────────────────────

    public void CorruptTile(int x, int y, CorruptionHeart owner)
    {
        var coord = new Vector2Int(x, y);
        if (allCorruptedTiles.Contains(coord)) return;

        var tile = GridManager.Instance?.GetGridTile(x, y);
        if (tile == null) return;

        var overlay = tile.AddComponent<CorruptionOverlay>();
        overlay.OwnerHeart = owner;
        overlay.GridPosition = coord;

        // Pass building occupant for pausing
        var occupant = GridManager.Instance.GetCellOccupant(x, y);
        overlay.InitWithOccupant(occupant);

        heartTiles[owner].Add(coord);
        allCorruptedTiles.Add(coord);
    }

    public void ClearTile(int x, int y, CorruptionHeart owner)
    {
        var coord = new Vector2Int(x, y);
        var tile = GridManager.Instance?.GetGridTile(x, y);
        if (tile != null)
        {
            var overlay = tile.GetComponent<CorruptionOverlay>();
            if (overlay != null)
            {
                overlay.Cleanup();
                Destroy(overlay);
            }
        }
        heartTiles[owner].Remove(coord);
        allCorruptedTiles.Remove(coord);
    }

    public void ClearHeartCluster(CorruptionHeart heart)
    {
        if (!heartTiles.ContainsKey(heart)) return;

        // Copy to avoid modifying the collection during iteration
        var tiles = new List<Vector2Int>(heartTiles[heart]);
        foreach (var coord in tiles)
            ClearTile(coord.x, coord.y, heart);

        heartTiles.Remove(heart);
        allHearts.Remove(heart);
    }

    public bool IsCorrupted(int x, int y) => allCorruptedTiles.Contains(new Vector2Int(x, y));

    // ── Spread ────────────────────────────────────────────────────────────

    private void SpreadAll()
    {
        foreach (var heart in allHearts)
        {
            if (!heart.IsActive) continue;

            // Copy to avoid modifying during iteration
            var snapshot = new List<Vector2Int>(heartTiles[heart]);
            foreach (var coord in snapshot)
            {
                TrySpreadTo(coord.x + 1, coord.y, heart);
                TrySpreadTo(coord.x - 1, coord.y, heart);
                TrySpreadTo(coord.x, coord.y + 1, heart);
                TrySpreadTo(coord.x, coord.y - 1, heart);
            }
        }
    }

    private void TrySpreadTo(int x, int y, CorruptionHeart owner)
    {
        if (GridManager.Instance == null || !GridManager.Instance.IsValidCell(x, y)) return;
        if (IsCorrupted(x, y)) return;
        CorruptTile(x, y, owner);
    }

    // ── Fog / Activation ─────────────────────────────────────────────────

    private void OnCellRevealed(int x, int y)
    {
        // 1. Activate dormant hearts within radius
        foreach (var heart in allHearts)
        {
            if (heart.IsActive) continue;
            int dx = Mathf.Abs(x - heart.GridPosition.x);
            int dy = Mathf.Abs(y - heart.GridPosition.y);
            // Use Chebyshev distance (matches a square radius feel)
            if (dx <= heartActivationRadius && dy <= heartActivationRadius)
            {
                heart.Activate();
                CorruptTile(heart.GridPosition.x, heart.GridPosition.y, heart);
            }
        }

        // 2. Connected reveal — if this tile is corrupted, reveal the whole cluster
        var coord = new Vector2Int(x, y);
        if (!allCorruptedTiles.Contains(coord)) return;

        var tile = GridManager.Instance?.GetGridTile(x, y);
        if (tile == null) return;
        var overlay = tile.GetComponent<CorruptionOverlay>();
        if (overlay == null) return;

        var owner = overlay.OwnerHeart;
        if (owner == null || !heartTiles.ContainsKey(owner)) return;

        foreach (var c in heartTiles[owner])
            FogManager.Instance?.RevealCell(c.x, c.y);

        FogManager.Instance?.RevealCell(owner.GridPosition.x, owner.GridPosition.y);
    }

    private void OnDestroy()
    {
        if (FogManager.Instance != null)
            FogManager.Instance.OnCellRevealed -= OnCellRevealed;
    }
}
```

**Test:** File compiles. No wiring to the scene yet.

---

## Step 5 — MapGeneratorV2: Register CorruptionManager in EnsureManagers

**File:** `Assets/ClockworkCraft/Scripts/Core/MapGeneratorV2.cs`

Inside `EnsureManagers()`, add after the existing manager creation calls:

```csharp
if (LittleCafe.CorruptionManager.Instance == null)
    new GameObject("CorruptionManager").AddComponent<LittleCafe.CorruptionManager>();
```

Follow the exact same pattern used for `BuildingProductionManager` just above it.

**Test:** Enter play mode. `CorruptionManager` should appear in the hierarchy. No errors.

---

## Step 6 — GridEntityActor: Add Corruption Targeting Priority

**File:** `Assets/Scripts/LittleCafe/GridEntityActor.cs`

There are two or three callsites where the actor resolves `GridEntityHealth targetHealth = occupant.GetComponent<GridEntityHealth>()`. At each one, add a corruption check **before** the occupant resolution:

```csharp
// Corruption priority — attack overlay before the occupant underneath
var gridTile = gm.GetGridTile(targetX, targetY); // use whatever variable names are in context
LittleCafe.CorruptionOverlay corruptionOverlay = gridTile != null
    ? gridTile.GetComponent<LittleCafe.CorruptionOverlay>()
    : null;

if (corruptionOverlay != null && corruptionOverlay.Health != null && !corruptionOverlay.Health.IsDestroyed)
{
    // Attack the overlay instead — use the same TakeDamage call that follows
    targetHealth = corruptionOverlay.Health;
}
else
{
    // Normal resolution
    targetHealth = occupant.GetComponent<GridEntityHealth>();
}
```

Find the variable names used in context (the grid position variable may be `newX`/`newY`, `targetX`/`targetY`, or similar — match what's already there).

**Test:** Play mode, place a worker near a manually-spawned `CorruptionHeart` prefab. Worker should attack corruption overlay HP first, then underlying object.

---

## Step 7 — Manual Test Checklist

- [ ] `CorruptionManager` appears in hierarchy at play mode start
- [ ] Manually place a `CorruptionHeart` GameObject in the scene with `GridPosition` set; confirm it registers with manager
- [ ] Revealing a tile within 5 tiles of the heart activates it and seeds first corrupted tile
- [ ] After 30 seconds, corruption spreads one tile orthogonally in all directions
- [ ] Revealing a corrupted tile reveals the entire cluster through fog
- [ ] Worker attacks corruption overlay HP, not the underlying occupant HP
- [ ] Destroying the overlay (HP to zero) clears that tile only
- [ ] Destroying the heart (HP to zero) clears all its owned tiles
- [ ] A building on a corrupted tile shows paused production timer; clears on tile clear
- [ ] Two hearts with touching clusters clear independently on separate heart deaths

---

## Notes for Implementing Agent

- `GridEntityHealth` maxHP must be set at runtime. Check how other components do this — look for `health.maxHP = X` or a `SetMaxHP(int)` method in the existing codebase.
- The floating indicator rendering layer: find what layer `TileFog` uses and place the indicator one layer above.
- The `CorruptionHeart.GridPosition` must be set by whatever code spawns the heart **before** `Start()` runs. Use `Awake()` initialization order or set it on the same frame before enabling the object.
- Chebyshev distance is used for the activation radius check (square area, not circle). This can be changed to Manhattan or Euclidean if preferred — it's an isolated check in `OnCellRevealed`.
- `CorruptionOverlay.Cleanup()` must be called by `CorruptionManager.ClearTile()` **before** `Destroy(overlay)`. The Destroy call schedules removal at end of frame; Cleanup runs immediately.
