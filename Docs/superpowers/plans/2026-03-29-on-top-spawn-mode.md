# On Top Spawn Mode — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an `OnTop` spawn mode that places environment objects (e.g. water lilies) on the Object layer above existing Surface tiles (e.g. Water), using a coverage percentage and min spacing.

**Architecture:** Extend the existing `SpawnMode` enum with `OnTop`. Add `requiredSurface` and `coveragePercent` fields to `EnvironmentSpawnEntry`. MapPlanner runs a second pass after normal placement, writing to a separate `onTopPlanGrid`. MapGeneratorV2 spawns from this grid after normal environment spawning.

**Tech Stack:** Unity C#, existing MapPlanner/MapGeneratorV2/GridManager systems.

**Design doc:** `docs/plans/2026-03-29-on-top-spawn-mode-design.md`

---

### Task 1: Extend SpawnMode Enum and EnvironmentSpawnEntry

**Files:**
- Modify: `Assets/ClockworkCraft/Scripts/Data/SpawnEntryData.cs:9-14` (SpawnMode enum)
- Modify: `Assets/ClockworkCraft/Scripts/Data/SpawnEntryData.cs:24-50` (EnvironmentSpawnEntry class)

- [ ] **Step 1: Add OnTop to SpawnMode enum**

In `Assets/ClockworkCraft/Scripts/Data/SpawnEntryData.cs`, add `OnTop` after `Edge`:

```csharp
public enum SpawnMode
{
    Scattered,  // Random per-tile placement with optional min spacing
    Clustered,  // BFS blobs from random seed points
    Edge,       // Spawns along the border of existing clusters of a specific type
    OnTop       // Spawns on the Object layer above existing Surface tiles
}
```

- [ ] **Step 2: Add requiredSurface and coveragePercent fields to EnvironmentSpawnEntry**

Add these fields at the end of the `EnvironmentSpawnEntry` class, after the Edge settings block:

```csharp
// ── OnTop settings (only used when spawnMode == OnTop) ──
[Tooltip("Surface type this object spawns on top of (OnTop mode only).")]
public ClockworkGrid.SurfaceType requiredSurface = ClockworkGrid.SurfaceType.Water;

[Tooltip("Fraction of qualifying surface tiles to cover (0-1). 0.3 = 30% of matching tiles.")]
[Range(0f, 1f)] public float coveragePercent = 0.3f;
```

- [ ] **Step 3: Commit**

```bash
git add Assets/ClockworkCraft/Scripts/Data/SpawnEntryData.cs
git commit -m "feat: add OnTop spawn mode enum and EnvironmentSpawnEntry fields"
```

---

### Task 2: Add OnTop Default in SpawnEntrySyncer

**Files:**
- Modify: `Assets/ClockworkCraft/Scripts/Core/SpawnEntrySyncer.cs:154-219` (CreateDefaultEntry method)

- [ ] **Step 1: Add lily/pad/ontop default case**

In `SpawnEntrySyncer.CreateDefaultEntry()`, add a case before the generic default (before the final `else`):

```csharp
else if (lower.Contains("lily") || lower.Contains("pad") || lower.Contains("lotus"))
{
    // Water plants: spawn on top of water tiles
    entry.spawnMode      = SpawnMode.OnTop;
    entry.spawnWeight    = 0f; // OnTop doesn't use weight-based budgets
    entry.requiredSurface = ClockworkGrid.SurfaceType.Water;
    entry.coveragePercent = 0.25f;
    entry.minSpacing     = 2;
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/ClockworkCraft/Scripts/Core/SpawnEntrySyncer.cs
git commit -m "feat: add OnTop default for lily/pad entries in SpawnEntrySyncer"
```

---

### Task 3: Exclude OnTop Entries from Budget-Based Planning

**Files:**
- Modify: `Assets/ClockworkCraft/Scripts/Core/MapPlanner.cs:51-89` (PlaceAllEntries validation)

- [ ] **Step 1: Filter out OnTop entries from the normal planning pass**

In `MapPlanner.PlaceAllEntries()`, update the environment validation loop (around line 58-67) to skip OnTop entries. They'll be handled in a separate pass. Add `entry.spawnMode == SpawnMode.OnTop` to the skip conditions:

```csharp
foreach (var entry in envEntries)
{
    if (entry.spawnWeight <= 0f) continue;
    if (entry.spawnMode == SpawnMode.Edge && string.IsNullOrEmpty(entry.edgeBorderOf)) continue;
    if (entry.spawnMode == SpawnMode.OnTop) continue; // handled in separate pass
    var data = envDB.GetByName(entry.environmentName);
    if (data == null || data.prefab == null) continue;
    valid.Add((entry, data));
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/ClockworkCraft/Scripts/Core/MapPlanner.cs
git commit -m "feat: exclude OnTop entries from budget-based planning pass"
```

---

### Task 4: Implement PlaceOnTopEntries in MapPlanner

**Files:**
- Modify: `Assets/ClockworkCraft/Scripts/Core/MapPlanner.cs` (add new public method)

- [ ] **Step 1: Add the PlaceOnTopEntries method**

Add this method to `MapPlanner`, after the existing `PlaceAllEntries` method (after line 248):

```csharp
/// <summary>
/// Second-pass placement for OnTop entries. Scans planGrid for tiles
/// matching each entry's requiredSurface and places objects into a
/// separate onTopPlanGrid at the configured coverage percentage.
/// Must be called AFTER PlaceAllEntries so surface tiles are already planned.
/// </summary>
public void PlaceOnTopEntries(
    List<EnvironmentSpawnEntry> envEntries, EnvironmentDatabase envDB,
    string[,] onTopPlanGrid)
{
    var onTopEntries = new List<(EnvironmentSpawnEntry entry, EnvironmentData data)>();
    foreach (var entry in envEntries)
    {
        if (entry.spawnMode != SpawnMode.OnTop) continue;
        var data = envDB.GetByName(entry.environmentName);
        if (data == null || data.prefab == null) continue;
        onTopEntries.Add((entry, data));
    }

    if (onTopEntries.Count == 0) return;

    // Build a lookup: for each environment name, what SurfaceType does it map to?
    // Surface entries in planGrid are stored by their environment name (e.g. "Water").
    // We need to know which planGrid names correspond to which SurfaceType.
    var nameToSurface = new Dictionary<string, ClockworkGrid.SurfaceType>();
    foreach (var envData in envDB.AllEnvironment)
    {
        if (envData.layerType != LittleCafe.EnvironmentLayerType.Surface) continue;
        string lower = envData.assetName.ToLowerInvariant();
        if (lower.Contains("corrupt"))
            nameToSurface[envData.assetName] = ClockworkGrid.SurfaceType.Corruption;
        else if (lower.Contains("lava"))
            nameToSurface[envData.assetName] = ClockworkGrid.SurfaceType.Lava;
        else
            nameToSurface[envData.assetName] = ClockworkGrid.SurfaceType.Water;
    }

    foreach (var (entry, data) in onTopEntries)
    {
        // Find all planGrid tiles with a surface matching requiredSurface
        var qualifying = new List<Vector2Int>();
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            string cellName = planGrid[x, y];
            if (cellName == null) continue;
            if (!nameToSurface.TryGetValue(cellName, out var surfType)) continue;
            if (surfType != entry.requiredSurface) continue;
            // Skip if onTopPlanGrid already has something here
            if (onTopPlanGrid[x, y] != null) continue;
            qualifying.Add(new Vector2Int(x, y));
        }

        if (qualifying.Count == 0)
        {
            Debug.Log($"[MapPlanner] OnTop '{entry.environmentName}': 0 qualifying {entry.requiredSurface} tiles found");
            continue;
        }

        int targetCount = Mathf.RoundToInt(qualifying.Count * entry.coveragePercent);
        if (targetCount <= 0) continue;

        ShuffleList(qualifying);

        var placed = new List<Vector2Int>();
        foreach (var pos in qualifying)
        {
            if (placed.Count >= targetCount) break;

            if (entry.minSpacing > 0 && IsTooClose(pos.x, pos.y, placed, entry.minSpacing))
                continue;

            onTopPlanGrid[pos.x, pos.y] = entry.environmentName;
            placed.Add(pos);
        }

        Debug.Log($"[MapPlanner] OnTop '{entry.environmentName}' on {entry.requiredSurface}: {placed.Count} tiles (target={targetCount}, qualifying={qualifying.Count})");
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/ClockworkCraft/Scripts/Core/MapPlanner.cs
git commit -m "feat: implement PlaceOnTopEntries second-pass planning"
```

---

### Task 5: Integrate OnTop into MapGeneratorV2 Pipeline

**Files:**
- Modify: `Assets/ClockworkCraft/Scripts/Core/MapGeneratorV2.cs`

- [ ] **Step 1: Add onTopPlanGrid field**

Add a new field next to `planGrid` (around line 94):

```csharp
private string[,] onTopPlanGrid;
```

- [ ] **Step 2: Initialize onTopPlanGrid in InitPlanGrid**

In `InitPlanGrid()` (around line 340), add after `planGrid[center.x, center.y] = "__center__";`:

```csharp
onTopPlanGrid = new string[width, height];
```

- [ ] **Step 3: Call PlaceOnTopEntries after PlaceAllEntries**

In `GenerateMapStaggered()`, add `PlaceOnTopEntries()` on the line immediately after `PlaceAllEntries()` (line 294) and before `PlaceCorruptionEntities()` (line 295):

```csharp
PlaceAllEntries();
PlaceOnTopEntries();              // ← new
PlaceCorruptionEntities();
```

- [ ] **Step 4: Add PlaceOnTopEntries method**

Add a new method in the Plan Phase section (after `PlaceAllEntries` method, around line 348):

```csharp
void PlaceOnTopEntries()
{
    var planner = new MapPlanner(planGrid, rng, width, height, center, clearCenterCardinal);
    planner.PlaceOnTopEntries(spawnEntries, environmentDatabase, onTopPlanGrid);
}
```

- [ ] **Step 5: Add SpawnAllOnTopStaggered coroutine**

Add after `SpawnAllStaggered()` (around line 509). This coroutine is almost identical to `SpawnAllStaggered()` but reads from `onTopPlanGrid`:

```csharp
/// <summary>
/// Spawns "On Top" objects from the onTopPlanGrid — objects placed on the
/// Object layer above existing Surface tiles (e.g. water lilies on water).
/// Runs after SpawnAllStaggered so the surface GameObjects already exist.
/// </summary>
System.Collections.IEnumerator SpawnAllOnTopStaggered()
{
    if (onTopPlanGrid == null) yield break;

    const int BATCH_SIZE = 25;
    int count = 0;

    for (int x = 0; x < width; x++)
    for (int y = 0; y < height; y++)
    {
        string envName = onTopPlanGrid[x, y];
        if (envName == null) continue;

        EnvironmentData envData = environmentDatabase.GetByName(envName);
        if (envData == null || envData.prefab == null) continue;

        Vector3 worldPos = GridManager.Instance.GridToWorldPosition(x, y);
        worldPos.y += 0.01f;
        Quaternion randomRot = Quaternion.Euler(0f, 90f * rng.Next(4), 0f);
        GameObject obj = Instantiate(envData.prefab, worldPos, randomRot);
        obj.name = envData.assetName;

        if (obj.TryGetComponent<ResourceNode>(out var node))
        {
            node.hp              = envData.hp;
            node.lootHpCost      = envData.lootHpCost;
            node.lootYield       = envData.lootYield;
            node.lootBonusAmount = envData.lootYield;
            node.isInteractable  = InteractionRegistry.Instance != null
                                   ? InteractionRegistry.Instance.IsUnlocked(envData.assetName) : true;
            node.resourceType    = envData.lootResourceType != ResourceType.None
                                   ? envData.lootResourceType
                                   : GuessResourceType(envName);
            node.Initialize(x, y);

            float dist = Vector2Int.Distance(new Vector2Int(x, y), center);
            node.tier = dist < 10f ? 1
                      : dist < 20f ? (rng.NextDouble() < 0.5 ? 1 : 2)
                      :              (rng.NextDouble() < 0.4 ? 2 : 3);

            NodeManager.Instance?.RegisterNode(node);
        }

        if (enableFog)
        {
            var fogHideable = obj.AddComponent<FogHideable>();
            fogHideable.Initialize(x, y);
        }

        if (GridEntityManager.Instance != null)
        {
            GridEntityManager.Instance.AttachFromEnvironmentData(obj, envData);
            ApplyEnvironmentDesaturationDefaults(obj, addIfMissing: true);
        }

        if (obj.activeSelf)
            TriggerAppearAnimation(obj);

        // OnTop objects go on the Object layer — the surface is already placed
        GridManager.Instance?.PlaceUnit(x, y, obj, CellState.Resource);
        POIManager.Instance?.RegisterEnvPOI(new Vector2Int(x, y), envData.assetName);

        count++;
        if (count % BATCH_SIZE == 0)
            yield return null;
    }

    if (count > 0)
        Debug.Log($"[MapGenV2] Spawned {count} OnTop environment objects");
}
```

- [ ] **Step 6: Wire SpawnAllOnTopStaggered into the pipeline**

In `GenerateMapStaggered()`, after `yield return StartCoroutine(SpawnAllStaggered());` (around line 319), add:

```csharp
// ── Spawn on-top environment (staggered) ─────────────
yield return StartCoroutine(SpawnAllOnTopStaggered());
```

- [ ] **Step 7: Commit**

```bash
git add Assets/ClockworkCraft/Scripts/Core/MapGeneratorV2.cs
git commit -m "feat: integrate OnTop planning and spawning into map generation pipeline"
```

---

### Task 6: Update Custom Editor for OnTop Mode

**Files:**
- Modify: `Assets/ClockworkCraft/Scripts/Editor/MapGeneratorV2Editor.cs:317-373` (DrawSpawnModeFields method)

- [ ] **Step 1: Add OnTop case to DrawSpawnModeFields**

In `DrawSpawnModeFields()`, add a new case after the `Edge` case (before the closing `}` of the switch, around line 372):

```csharp
case SpawnMode.OnTop:
    SerializedProperty surfaceProp = entryProp.FindPropertyRelative("requiredSurface");
    SerializedProperty coverageProp = entryProp.FindPropertyRelative("coveragePercent");
    EditorGUILayout.PropertyField(surfaceProp, new GUIContent("Required Surface",
        "Surface type this object spawns on top of."));
    EditorGUILayout.PropertyField(coverageProp, new GUIContent("Coverage %",
        "Fraction of qualifying surface tiles to cover (0-1)."));
    EditorGUILayout.PropertyField(spacingProp, new GUIContent("Min Spacing",
        "Minimum cell distance between instances."));
    break;
```

- [ ] **Step 2: Exclude OnTop entries from weight/budget display**

In the environment entry cards section (around line 168-197), the header shows weight percentage and budget which don't apply to OnTop entries. Update the header display for OnTop entries. Replace the `EditorGUILayout.LabelField` line (around line 189) with:

```csharp
if (mode == SpawnMode.OnTop)
{
    SerializedProperty covProp = entryProp.FindPropertyRelative("coveragePercent");
    EditorGUILayout.LabelField(
        $"\u2618  {entryName}  \u2014  OnTop ({covProp.floatValue * 100f:F0}% coverage)",
        EditorStyles.boldLabel);
}
else
{
    EditorGUILayout.LabelField($"\u2618  {entryName}  \u2014  {relPct:F0}% (~{entryBudget} tiles)", EditorStyles.boldLabel);
}
```

- [ ] **Step 3: Exclude OnTop from combined weight total**

In the combined weight calculation (around line 158-159), skip OnTop entries so they don't dilute the weight pool:

```csharp
foreach (var e in gen.spawnEntries)
    if (e.spawnWeight > 0f && e.spawnMode != SpawnMode.OnTop) totalCombinedWeight += e.spawnWeight;
```

- [ ] **Step 4: Commit**

```bash
git add Assets/ClockworkCraft/Scripts/Editor/MapGeneratorV2Editor.cs
git commit -m "feat: update MapGeneratorV2 custom editor for OnTop spawn mode"
```

---

### Task 7: Verify Build and Test in Unity

- [ ] **Step 1: Check for compile errors**

Open Unity and check the Console for any compilation errors. All scripts should compile cleanly.

- [ ] **Step 2: Test the inspector**

1. Select the MapGeneratorV2 object in the scene
2. Click "Sync from Database"
3. Verify any entry can be set to `OnTop` mode
4. Verify the OnTop-specific fields (Required Surface, Coverage %, Min Spacing) appear when OnTop is selected
5. Verify the header shows "OnTop (X% coverage)" instead of weight percentage

- [ ] **Step 3: Test map generation**

1. Create a test entry with `spawnMode = OnTop`, `requiredSurface = Water`, `coveragePercent = 0.3`, `minSpacing = 2`
2. Generate a map
3. Verify objects spawn on water tiles
4. Verify spacing is respected
5. Verify the console log shows `[MapPlanner] OnTop '...' on Water: N tiles`

- [ ] **Step 4: Final commit**

```bash
git add -A
git commit -m "feat: complete OnTop spawn mode — places objects on surface tiles"
```
