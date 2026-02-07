# GDD Amendment: Wave System Redesign
**Date:** 2026-02-07
**Iteration:** 10 (Wave System Overhaul)
**Status:** Draft for Implementation

---

## Executive Summary

This amendment replaces the existing automatic wave spawning system with a **timeline-based sequence system**. Waves are now defined by numerical spawn codes that execute at fixed intervals, creating predictable spawn patterns that players can learn and strategize around.

**Key Changes:**
- Wave completion: Ends when spawn sequence finishes (not enemy death)
- Peace periods: 4×5 ticks between waves (configurable)
- Resource spawning: Timeline-based only (remove automatic spawner)
- Spawn codes: 0=nothing, 1=enemy, 2=resource
- Timeline UI: Visual dot representation of upcoming spawns
- AI placement: 7-level priority system for spawn positioning

---

## Core Mechanics

### 1. Spawn Code System

**Format:** String of digits with no spaces
**Example:** `"10102"` = Enemy, Nothing, Enemy, Nothing, Resource

**Code Definitions:**
- `0` = Empty tick (no spawn)
- `1` = Enemy unit
- `2` = Resource node

**Tick Spacing:** 4 ticks between each spawn event
**Example Execution for "10102":**
- Tick 0: Spawn enemy
- Tick 4: Do nothing
- Tick 8: Spawn enemy
- Tick 12: Do nothing
- Tick 16: Spawn resource → **Wave Complete**

### 2. Wave Lifecycle

**Phase 1: Peace Period (Between Waves)**
- Duration: `peacePeriodMultiplier × gridSides` ticks
  - Default: `5 × 4 = 20 ticks`
  - `gridSides` = 4 (constant, represents 4 sides of grid)
  - `peacePeriodMultiplier` = Inspector configurable (default: 5)
- Player actions: Place units, harvest resources, rotate units
- Spawning: None
- UI: Timeline shows grayed-out preview of next wave

**Phase 2: Active Wave**
- Duration: `(spawnCode.Length - 1) × 4 + 1` ticks
  - Example: "10102" = (5-1)×4+1 = 17 ticks
- Spawning: Execute spawn code sequence at 4-tick intervals
- Wave ends: When final spawn event executes
- Enemy persistence: Enemies remain alive after wave ends

**Phase 3: Wave Complete → Peace Period**
- Immediately transition to next peace period
- Surviving enemies continue moving/attacking
- No forced enemy cleanup

### 3. Wave Progression

**Wave Configuration (Inspector):**
```csharp
[System.Serializable]
public class WaveConfig
{
    public int waveNumber;
    public string spawnCode;  // e.g., "10102"
    public int enemyLevel;    // 1, 2, or 3
    public int resourceLevel; // 1, 2, or 3
}
```

**Default Wave Sequence:**
- Wave 1: `"1"` (1 enemy, Level 1)
- Wave 2: `"101"` (2 enemies, Level 1)
- Wave 3: `"10102"` (2 enemies + 1 resource, Level 1)
- Wave 4: `"1010102"` (3 enemies + 1 resource, Level 1/2 mix)
- Wave 5: `"10101020102"` (5 enemies + 2 resources, Level 2)
- ...escalates in complexity

**Difficulty Scaling:**
- Early waves: Short codes, Level 1 units
- Mid waves: Longer codes, mixed levels
- Late waves: Dense codes ("11111"), Level 3 bosses

---

## AI Placement Priority System

When spawning enemies (code `1`) or resources (code `2`), use this 7-level priority hierarchy:

### Priority Levels (Highest → Lowest)

**Priority 1: Adjacent to Player Units**
- Definition: Cells orthogonally adjacent (N/S/E/W) to player units
- Reason: Creates immediate threat/interaction
- Validation: Must be empty, must be valid grid position

**Priority 2: Fogged Cells (Fog of War)**
- Definition: Cells still covered by fog (not revealed)
- Reason: Surprise spawns from unexplored areas
- Validation: Must be empty

**Priority 3: Revealed but Distant Cells**
- Definition: Revealed cells >4 cells away from any player unit
- Reason: Spawn away from player's immediate control
- Validation: Must be empty

**Priority 4: Grid Edges (Outer Ring)**
- Definition: Cells on outermost row/column of grid
- Reason: Spawn from "off-screen" directions
- Validation: Must be empty

**Priority 5: Adjacent to Resources**
- Definition: Cells orthogonally adjacent to existing resources
- Reason: Create contestable resource clusters
- Validation: Must be empty

**Priority 6: Random Empty Cell**
- Definition: Any valid empty cell on grid
- Reason: Fallback when higher priorities unavailable
- Validation: Must be empty

**Priority 7: Fail to Spawn**
- Definition: No valid cells available
- Action: Log warning, skip spawn event
- Reason: Grid may be completely full

### Multi-Cell Resource Spawning

Resources occupy multiple cells based on level:
- **Level 1:** 1×1 (single cell)
- **Level 2:** 2×1 (horizontal orientation)
- **Level 3:** 2×2 (square formation)

**Placement Algorithm:**
1. Check priority levels in order (1→7)
2. For multi-cell resources, validate ALL cells are empty
3. If anchor cell passes but extension cells blocked, try next candidate
4. Example: Level 2 resource needs cell (x,y) AND (x+1,y) both empty

---

## Timeline UI Specification

### Visual Design

**Location:** Top-center of screen, slightly below top edge
**Layout:** Horizontal line with dots along it (continuous line connecting dots)
**Style:** Clean, simple graphics with solid colors
**Color Scheme:** Red for enemies, green for resources, gray for empty ticks

**Visual Structure:**
```
           WAVE 2
    ●━━━●━━━●━━━●━━━😈
    ↑
  Current
```

**Dot Types:**
- **Red Circle (●):** Enemy spawn (code `1`)
  - Size: ~20px diameter
  - Color: `#D84040` (red)
  - Connected by horizontal red line

- **Green Circle (●):** Resource spawn (code `2`)
  - Size: ~20px diameter
  - Color: `#40D850` (green)
  - Connected by horizontal line

- **Gray Circle (○):** Empty tick (code `0`)
  - Size: ~16px diameter (slightly smaller)
  - Color: `#808080` (gray)
  - Outline only, not filled

- **Boss Icon (😈 or 👿):** Large enemy/boss (future)
  - Size: ~40px (2× normal dot size)
  - Red demon emoji or custom angry face icon
  - Placed at end of timeline

**Visual States:**
- **Upcoming:** Full opacity, normal size
- **Active (Current):** Pulsing animation (scale 1.0 → 1.3 → 1.0), brighter glow
- **Completed:** Fade to 50% opacity, no glow
- **Line Progress:** Portion of line behind current dot becomes darker/grayed out

**Connecting Line:**
- Width: 3-4px
- Style: Solid (can be rough/hand-drawn style)
- Color: Matches dot color (red for enemies, green for resources)
- Active portion: Full color
- Completed portion: Faded to 40% opacity

### UI Layout

**Position:** Top-center screen, anchored (0.5, 1.0), offset Y: -60px
**Size:** Dynamic width based on spawn code length (min: 400px, max: 800px)

```
┌──────────────────────────────────────────────────┐
│                    WAVE 2                        │
│         ●━━━●━━━○━━━●━━━○━━━😈                  │
│         ↑                                        │
│    Next spawn: 3 ticks                           │
└──────────────────────────────────────────────────┘
```

**Components:**
1. **Wave Title Text** ("WAVE X")
   - Font: Bold, clean sans-serif
   - Size: 36pt
   - Color: Red `#D84040`
   - Position: Centered above timeline

2. **Timeline Container**
   - Horizontal layout
   - Auto-sized based on spawn code length
   - Padding: 20px left/right
   - Spacing: ~60px between dots (accommodates 4 ticks per spawn)

3. **Spawn Dots**
   - Instantiated from spawn code string
   - Connected by LineRenderer or UI Image (stretched)
   - Each dot is UI Image with circle sprite

4. **Current Position Indicator**
   - Small arrow (↑) or glow effect under active dot
   - Animates when transitioning to next spawn

5. **Countdown Text** (optional)
   - "Next spawn: X ticks"
   - Font: 18pt, white with black outline
   - Position: Below timeline, centered

### Peace Period UI

During peace periods, show preview of next wave:
```
┌─────────────────────────────────────────┐
│  Peace Period: 12 ticks remaining       │
│  Next Wave: 4                            │
│  Preview: [1010102] (grayed out)        │
└─────────────────────────────────────────┘
```

---

## Technical Implementation

### WaveManager Refactor

**New Class Structure:**
```csharp
public enum WaveState
{
    PeacePeriod,
    ActiveWave
}

public class WaveManager : MonoBehaviour
{
    [Header("Wave Configuration")]
    [SerializeField] private List<WaveConfig> waveConfigs;
    [SerializeField] private int peacePeriodMultiplier = 5;
    private const int GRID_SIDES = 4;

    [Header("Spawn Timing")]
    private const int SPAWN_INTERVAL_TICKS = 4;

    // State
    private WaveState currentState;
    private int currentWaveIndex;
    private string currentSpawnCode;
    private int spawnCodeIndex;
    private int ticksSinceLastSpawn;
    private int ticksInPeacePeriod;

    // Events
    public event Action<int> OnWaveStart;
    public event Action<int> OnWaveComplete;
    public event Action<int, int> OnSpawnEvent; // (spawnType, index)

    private void OnIntervalTick(int intervalCount)
    {
        if (currentState == WaveState.PeacePeriod)
        {
            ticksInPeacePeriod++;
            int peaceDuration = GRID_SIDES * peacePeriodMultiplier;

            if (ticksInPeacePeriod >= peaceDuration)
            {
                StartNextWave();
            }
        }
        else if (currentState == WaveState.ActiveWave)
        {
            ticksSinceLastSpawn++;

            if (ticksSinceLastSpawn >= SPAWN_INTERVAL_TICKS)
            {
                ExecuteSpawnEvent();
                ticksSinceLastSpawn = 0;
                spawnCodeIndex++;

                if (spawnCodeIndex >= currentSpawnCode.Length)
                {
                    EndWave();
                }
            }
        }
    }

    private void ExecuteSpawnEvent()
    {
        char code = currentSpawnCode[spawnCodeIndex];
        int spawnType = int.Parse(code.ToString());

        OnSpawnEvent?.Invoke(spawnType, spawnCodeIndex);

        if (spawnType == 1)
        {
            SpawnEnemy();
        }
        else if (spawnType == 2)
        {
            SpawnResource();
        }
        // spawnType == 0: Do nothing
    }

    private void SpawnEnemy()
    {
        Vector2Int spawnPos = enemySpawner.FindSpawnPosition(useAIPriority: true);
        if (spawnPos != Vector2Int.one * -1)
        {
            int level = waveConfigs[currentWaveIndex].enemyLevel;
            enemySpawner.SpawnEnemyAtPosition(spawnPos, level);
        }
    }

    private void SpawnResource()
    {
        Vector2Int spawnPos = resourceSpawner.FindSpawnPosition(useAIPriority: true);
        if (spawnPos != Vector2Int.one * -1)
        {
            int level = waveConfigs[currentWaveIndex].resourceLevel;
            resourceSpawner.SpawnResourceAtPosition(spawnPos, level);
        }
    }

    private void EndWave()
    {
        currentState = WaveState.PeacePeriod;
        ticksInPeacePeriod = 0;
        OnWaveComplete?.Invoke(currentWaveIndex);
        Debug.Log($"Wave {currentWaveIndex} complete!");
    }
}
```

### TimelineUI Component

**New Script:** `Assets/Scripts/UI/TimelineUI.cs`

```csharp
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace ClockworkGrid
{
    public class TimelineUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Text waveNumberText;
        [SerializeField] private Text countdownText;
        [SerializeField] private Transform dotContainer;

        [Header("Dot Prefabs")]
        [SerializeField] private GameObject dotPrefab;
        [SerializeField] private GameObject bossDotPrefab; // Emoji/icon for bosses

        [Header("Visual Settings")]
        [SerializeField] private float dotSpacing = 60f;
        [SerializeField] private float lineWidth = 3f;
        [SerializeField] private Color enemyColor = new Color(0.85f, 0.25f, 0.25f); // Red
        [SerializeField] private Color resourceColor = new Color(0.25f, 0.85f, 0.31f); // Green
        [SerializeField] private Color emptyColor = new Color(0.5f, 0.5f, 0.5f); // Gray

        // State
        private List<GameObject> spawnDots = new List<GameObject>();
        private List<Image> connectionLines = new List<Image>();
        private int currentDotIndex = 0;
        private string currentSpawnCode;

        public void InitializeWave(int waveNumber, string spawnCode)
        {
            ClearTimeline();

            currentSpawnCode = spawnCode;
            waveNumberText.text = $"WAVE {waveNumber}";
            waveNumberText.fontSize = 36;
            waveNumberText.fontStyle = FontStyle.Bold;
            waveNumberText.color = enemyColor;

            // Create dots and connecting lines
            for (int i = 0; i < spawnCode.Length; i++)
            {
                char code = spawnCode[i];

                // Create dot
                GameObject dot = Instantiate(dotPrefab, dotContainer);
                dot.name = $"Dot_{i}_{code}";

                // Position dot
                RectTransform dotRect = dot.GetComponent<RectTransform>();
                dotRect.anchoredPosition = new Vector2(i * dotSpacing, 0);

                // Set dot appearance
                Image dotImage = dot.GetComponent<Image>();
                ConfigureDot(dotImage, code);

                spawnDots.Add(dot);

                // Create connecting line (except after last dot)
                if (i < spawnCode.Length - 1)
                {
                    GameObject lineObj = new GameObject($"Line_{i}");
                    lineObj.transform.SetParent(dotContainer, false);

                    Image lineImage = lineObj.AddComponent<Image>();
                    lineImage.color = GetColorForCode(code);

                    RectTransform lineRect = lineObj.GetComponent<RectTransform>();
                    lineRect.sizeDelta = new Vector2(dotSpacing, lineWidth);
                    lineRect.anchoredPosition = new Vector2(i * dotSpacing + dotSpacing / 2, 0);

                    connectionLines.Add(lineImage);
                }
            }

            currentDotIndex = 0;
            UpdateActiveState();
        }

        private void ConfigureDot(Image dotImage, char code)
        {
            RectTransform rect = dotImage.GetComponent<RectTransform>();

            if (code == '0')
            {
                // Empty tick: smaller outline circle
                dotImage.color = emptyColor;
                rect.sizeDelta = Vector2.one * 16f;
                // TODO: Set sprite to outline circle (not filled)
            }
            else if (code == '1')
            {
                // Enemy: red filled circle
                dotImage.color = enemyColor;
                rect.sizeDelta = Vector2.one * 20f;
            }
            else if (code == '2')
            {
                // Resource: green filled circle
                dotImage.color = resourceColor;
                rect.sizeDelta = Vector2.one * 20f;
            }
            else if (code == '3')
            {
                // Boss (future): large red dot or emoji
                dotImage.color = enemyColor;
                rect.sizeDelta = Vector2.one * 40f;
                // TODO: Replace with boss emoji sprite
            }
        }

        private Color GetColorForCode(char code)
        {
            if (code == '1' || code == '3') return enemyColor; // Enemy/Boss
            if (code == '2') return resourceColor; // Resource
            return emptyColor; // Empty
        }

        public void AdvanceDot()
        {
            if (currentDotIndex < spawnDots.Count - 1)
            {
                currentDotIndex++;
                UpdateActiveState();
            }
        }

        private void UpdateActiveState()
        {
            for (int i = 0; i < spawnDots.Count; i++)
            {
                Image dotImage = spawnDots[i].GetComponent<Image>();

                if (i == currentDotIndex)
                {
                    // Active: Pulse animation
                    StartCoroutine(PulseDot(dotImage.transform));
                }
                else if (i < currentDotIndex)
                {
                    // Completed: Fade
                    Color c = dotImage.color;
                    c.a = 0.5f;
                    dotImage.color = c;
                    dotImage.transform.localScale = Vector3.one;
                }
                else
                {
                    // Upcoming: Full opacity
                    Color c = dotImage.color;
                    c.a = 1f;
                    dotImage.color = c;
                    dotImage.transform.localScale = Vector3.one;
                }
            }

            // Fade completed lines
            for (int i = 0; i < connectionLines.Count; i++)
            {
                if (i < currentDotIndex)
                {
                    Color c = connectionLines[i].color;
                    c.a = 0.4f;
                    connectionLines[i].color = c;
                }
            }
        }

        private IEnumerator PulseDot(Transform dotTransform)
        {
            float duration = 0.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                float scale = Mathf.Lerp(1f, 1.3f, Mathf.Sin(t * Mathf.PI));
                dotTransform.localScale = Vector3.one * scale;

                elapsed += Time.deltaTime;
                yield return null;
            }

            dotTransform.localScale = Vector3.one;
        }

        public void UpdateCountdown(int ticksUntilNextSpawn)
        {
            if (countdownText != null)
            {
                countdownText.text = $"Next spawn: {ticksUntilNextSpawn} ticks";
            }
        }

        public void ShowPeacePeriod(int ticksRemaining, int nextWaveNumber, string nextSpawnCode)
        {
            waveNumberText.text = "Peace Period";
            waveNumberText.color = Color.white;

            if (countdownText != null)
            {
                countdownText.text = $"{ticksRemaining} ticks until Wave {nextWaveNumber}";
            }

            ClearTimeline();
        }

        private void ClearTimeline()
        {
            foreach (GameObject dot in spawnDots)
            {
                if (dot != null) Destroy(dot);
            }
            spawnDots.Clear();

            foreach (Image line in connectionLines)
            {
                if (line != null) Destroy(line.gameObject);
            }
            connectionLines.Clear();

            currentDotIndex = 0;
        }
    }
}
```

### EnemySpawner Integration

**Modify:** `Assets/Scripts/Systems/EnemySpawner.cs`

```csharp
public Vector2Int FindSpawnPosition(bool useAIPriority = true)
{
    if (!useAIPriority)
    {
        return FindRandomEmptyCell();
    }

    // Priority 1: Adjacent to player units
    List<Vector2Int> priority1 = GetCellsAdjacentToPlayerUnits();
    if (priority1.Count > 0)
        return priority1[Random.Range(0, priority1.Count)];

    // Priority 2: Fogged cells
    List<Vector2Int> priority2 = FogManager.Instance.GetFoggedCells();
    if (priority2.Count > 0)
        return priority2[Random.Range(0, priority2.Count)];

    // Priority 3: Distant revealed cells
    List<Vector2Int> priority3 = GetDistantRevealedCells();
    if (priority3.Count > 0)
        return priority3[Random.Range(0, priority3.Count)];

    // Priority 4: Grid edges
    List<Vector2Int> priority4 = GetGridEdgeCells();
    if (priority4.Count > 0)
        return priority4[Random.Range(0, priority4.Count)];

    // Priority 5: Adjacent to resources
    List<Vector2Int> priority5 = GetCellsAdjacentToResources();
    if (priority5.Count > 0)
        return priority5[Random.Range(0, priority5.Count)];

    // Priority 6: Random empty cell
    List<Vector2Int> priority6 = GetAllEmptyCells();
    if (priority6.Count > 0)
        return priority6[Random.Range(0, priority6.Count)];

    // Priority 7: Fail
    Debug.LogWarning("No valid spawn position found!");
    return Vector2Int.one * -1; // Failure sentinel
}

public void SpawnEnemyAtPosition(Vector2Int gridPos, int level)
{
    GameObject enemyPrefab = GetEnemyPrefabByLevel(level);
    Vector3 worldPos = GridManager.Instance.GridToWorldPosition(gridPos.x, gridPos.y);

    GameObject enemyObj = Instantiate(enemyPrefab, worldPos, Quaternion.identity);
    Unit enemy = enemyObj.GetComponent<Unit>();
    enemy.Initialize(Team.Enemy, gridPos.x, gridPos.y);

    GridManager.Instance.PlaceUnit(gridPos.x, gridPos.y, enemyObj, CellState.EnemyUnit);

    Debug.Log($"Spawned Level {level} enemy at ({gridPos.x}, {gridPos.y})");
}
```

---

## Resource Spawner Changes

**Remove:** Automatic interval-based spawning
**Keep:** Spawn-on-demand functionality for timeline system

**New Method in ResourceSpawner:**
```csharp
public void SpawnResourceAtPosition(Vector2Int gridPos, int level)
{
    GameObject resourcePrefab = GetResourcePrefabByLevel(level);
    Vector2Int size = GetResourceSize(level); // (1,1), (2,1), or (2,2)

    // Validate multi-cell occupation
    if (!CanPlaceResourceAt(gridPos, size))
    {
        Debug.LogWarning($"Cannot place Level {level} resource at ({gridPos.x}, {gridPos.y})");
        return;
    }

    Vector3 worldPos = GridManager.Instance.GridToWorldPosition(gridPos.x, gridPos.y);
    GameObject resourceObj = Instantiate(resourcePrefab, worldPos, Quaternion.identity);

    ResourceNode resource = resourceObj.GetComponent<ResourceNode>();
    resource.Initialize(level, gridPos);

    // Occupy all cells
    for (int dx = 0; dx < size.x; dx++)
    {
        for (int dy = 0; dy < size.y; dy++)
        {
            int x = gridPos.x + dx;
            int y = gridPos.y + dy;
            GridManager.Instance.PlaceResource(x, y, resourceObj, CellState.Resource);
        }
    }

    Debug.Log($"Spawned Level {level} resource at ({gridPos.x}, {gridPos.y})");
}

private bool CanPlaceResourceAt(Vector2Int anchor, Vector2Int size)
{
    for (int dx = 0; dx < size.x; dx++)
    {
        for (int dy = 0; dy < size.y; dy++)
        {
            int x = anchor.x + dx;
            int y = anchor.y + dy;

            if (!GridManager.Instance.IsValidCell(x, y) ||
                !GridManager.Instance.IsCellEmpty(x, y))
            {
                return false;
            }
        }
    }
    return true;
}
```

---

## Inspector Configuration

**GameMaster Component (Wave Settings):**
```csharp
[Header("Wave System")]
[SerializeField] private int peacePeriodMultiplier = 5;
[Tooltip("Peace period = peacePeriodMultiplier × 4 ticks (default: 20 ticks)")]

[SerializeField] private List<WaveConfig> waveSequence = new List<WaveConfig>();

// Default wave sequence
private void Reset()
{
    waveSequence = new List<WaveConfig>
    {
        new WaveConfig { waveNumber = 1, spawnCode = "1", enemyLevel = 1, resourceLevel = 1 },
        new WaveConfig { waveNumber = 2, spawnCode = "101", enemyLevel = 1, resourceLevel = 1 },
        new WaveConfig { waveNumber = 3, spawnCode = "10102", enemyLevel = 1, resourceLevel = 1 },
        new WaveConfig { waveNumber = 4, spawnCode = "1010102", enemyLevel = 1, resourceLevel = 2 },
        new WaveConfig { waveNumber = 5, spawnCode = "10101020102", enemyLevel = 2, resourceLevel = 2 },
    };
}
```

---

## Migration Path

### Phase 1: Core Wave Logic
1. Refactor WaveManager to use spawn codes
2. Implement 4-tick spacing between spawns
3. Add peace period system (4×5 ticks)
4. Remove wave completion based on enemy death

### Phase 2: AI Placement
1. Implement 7-level priority system in EnemySpawner
2. Add priority helpers (adjacent to units, fogged cells, etc.)
3. Extend ResourceSpawner with priority system
4. Test placement distribution

### Phase 3: Timeline UI
1. Create TimelineUI component
2. Implement dot visualization
3. Add wave preview during peace periods
4. Connect to WaveManager events

### Phase 4: Resource Integration
1. Remove automatic resource spawning
2. Integrate ResourceSpawner with timeline
3. Implement multi-cell validation
4. Test resource spawn codes (`2`)

### Phase 5: Polish
1. Add countdown timers to UI
2. Implement dot animations (pulse, fade)
3. Add audio feedback for spawn events
4. Playtest wave balance

---

## Balance Considerations

**Early Game (Waves 1-5):**
- Short spawn codes (1-5 events)
- Level 1 enemies/resources only
- Sparse enemy density ("101" not "111")
- Focus: Learn timeline system

**Mid Game (Waves 6-10):**
- Medium codes (5-10 events)
- Level 2 introduction
- Mixed spawns ("1010202")
- Focus: Resource management

**Late Game (Waves 11+):**
- Long codes (10-15 events)
- Dense enemy waves ("11111")
- Level 3 bosses
- Mixed level enemies in same wave

**Resource Frequency:**
- Target: 1 resource per 3-4 enemies
- Early waves: More resources (learning phase)
- Late waves: Fewer resources (scarcity)

---

## Testing Checklist

**Functional Tests:**
- [ ] Spawn code "10102" executes correctly (enemy, nothing, enemy, nothing, resource)
- [ ] 4-tick spacing between spawn events
- [ ] Peace period lasts exactly 20 ticks (default)
- [ ] Wave ends when spawn sequence completes (not when enemies die)
- [ ] AI placement follows priority hierarchy
- [ ] Multi-cell resources validate all cells before spawning
- [ ] Timeline UI displays correct dot colors
- [ ] Active dot pulses at 1.2× scale
- [ ] Completed dots fade to 40% opacity
- [ ] Peace period shows next wave preview

**Edge Cases:**
- [ ] Empty spawn code ("") → Instant wave completion
- [ ] All-zero code ("0000") → Wave with no spawns
- [ ] Grid full → Priority 7 fails gracefully
- [ ] Level 2 resource blocked → Falls back to next priority
- [ ] Wave triggers during grid expansion → Pauses correctly

**Integration:**
- [ ] Grid expansion doesn't break wave state
- [ ] Fog reveal doesn't affect ongoing wave
- [ ] Old automatic resource spawner removed
- [ ] Token costs still balanced with new resource rate

---

## Open Questions for Future Iterations

1. **Boss Waves:** How to represent Level 3 bosses in spawn codes?
   - Option: Use `3` for boss spawns ("10301" = 2 enemies + 1 boss)

2. **Dynamic Difficulty:** Should spawn codes adapt to player performance?
   - Could lengthen/shorten codes based on win/loss streaks

3. **Wave Modifiers:** Should some waves have special rules?
   - Example: "Fast Wave" (2-tick spacing instead of 4)
   - Example: "Horde Wave" (all enemies spawn simultaneously)

4. **Player Choice:** Should players vote on next wave difficulty?
   - Could offer 3 spawn code options with varying rewards

---

## Approval Required

This amendment requires approval before implementation. Key stakeholders should review:
- **Game Design:** Wave pacing and balance
- **Technical:** Implementation complexity and performance
- **UI/UX:** Timeline visualization and clarity
- **QA:** Testing scope and edge cases

**Sign-off:**
- [ ] Game Designer
- [ ] Lead Programmer
- [ ] UI Designer
- [ ] QA Lead

---

**End of Amendment**
