# Phase 1 Implementation Guide — Procedural Map Generation

**Read first:** `Assets/Docs/ClockworkCraft/ClockworkCraft-GDD.md` (Sections 2, 3, 6, 9)
**Scene to create:** `Assets/ClockworkCraft/Scenes/ClockworkCraft.unity`
**All new code:** `Assets/ClockworkCraft/` — do NOT modify anything in `Assets/Scripts/` (RTChess)

---

## What You're Building

A procedurally generated 40×40 grid map where every run is different. The map has:
- Trees in Perlin-noise-based forests (strings of trees that workers can chain-clear)
- Gold Mines scattered sparsely, more common further from the center
- Wild Farms scattered for Food income
- Rivers of Water tiles (impassable for now, generated via random walk)
- Locked Rock tiles (impassable and non-interactable for now)
- Guaranteed starting resources near the Town Hall
- Fog of war covering the full map, revealed only around the starting base

All density/probability values are tunable in the Unity Inspector via a ScriptableObject — no magic numbers in code.

---

## Step 1 — Folder Structure

Create this folder structure under `Assets/ClockworkCraft/`:

```
Assets/ClockworkCraft/
├── Scripts/
│   ├── Core/
│   │   ├── MapGenerator.cs
│   │   ├── NodeManager.cs
│   │   └── FogManager.cs          (RTChess already has this — reuse if possible)
│   ├── Data/
│   │   └── MapGenerationSettings.cs
│   └── World/
│       ├── ResourceNode.cs        (new version — extends or replaces RTChess ResourceNode)
│       └── TileType.cs            (enum)
├── Data/
│   └── MapGenerationSettings.asset   (the ScriptableObject instance)
├── Prefabs/
│   ├── Nodes/
│   │   ├── TreeNode.prefab
│   │   ├── GoldMineNode.prefab
│   │   ├── WildFarmNode.prefab
│   │   ├── RockNode.prefab
│   │   └── WaterTile.prefab
│   └── TownHall.prefab
└── Scenes/
    └── ClockworkCraft.unity
```

---

## Step 2 — TileType Enum

Create `Assets/ClockworkCraft/Scripts/World/TileType.cs`:

```csharp
namespace ClockworkCraft
{
    public enum TileType
    {
        Empty,          // Grass — passable, buildable
        Tree,           // Wood resource — interactable, chain-clearable
        GoldMine,       // Gold resource — interactable, chain-clearable
        WildFarm,       // Food resource — interactable, chain-clearable
        Rock,           // Stone resource — LOCKED (not interactable at start)
        Water,          // Barrier — LOCKED (not interactable at start)
        TownHall,       // Pre-placed anchor building
        Building,       // Player-placed building (set at runtime)
    }

    public enum ResourceType
    {
        None,
        Gold,
        Wood,
        Food,
        Stone
    }
}
```

---

## Step 3 — MapGenerationSettings ScriptableObject

Create `Assets/ClockworkCraft/Scripts/Data/MapGenerationSettings.cs`:

```csharp
using UnityEngine;

namespace ClockworkCraft
{
    [CreateAssetMenu(fileName = "MapGenerationSettings", menuName = "ClockworkCraft/Map Generation Settings")]
    public class MapGenerationSettings : ScriptableObject
    {
        [Header("Map Size")]
        public int mapWidth = 40;
        public int mapHeight = 40;
        [Tooltip("0 = random seed each run. Any other value = deterministic map.")]
        public int seed = 0;

        [Header("Clear Zone (around Town Hall)")]
        [Tooltip("Cells within this radius of the Town Hall center are always kept empty.")]
        public int clearRadius = 3;

        [Header("Guaranteed Starting Resources")]
        [Tooltip("Min distance from Town Hall for the guaranteed starting Gold Mine.")]
        public int goldMineMinDist = 4;
        [Tooltip("Max distance from Town Hall for the guaranteed starting Gold Mine.")]
        public int goldMineMaxDist = 7;
        [Tooltip("How many tree clusters to guarantee near the starting area.")]
        [Range(1, 5)] public int guaranteedTreeClusters = 3;
        [Tooltip("How many Wild Farm nodes to guarantee near the starting area.")]
        [Range(1, 3)] public int guaranteedFarms = 1;

        [Header("Trees (Perlin Noise)")]
        [Tooltip("Perlin noise threshold. Higher = fewer trees. 0.4 = ~40% coverage.")]
        [Range(0f, 1f)] public float treeDensityThreshold = 0.42f;
        [Tooltip("Noise scale. Larger value = bigger forest blobs.")]
        public float treeNoiseScale = 6f;
        [Tooltip("Post-process pass that fills gaps between nearby trees to create strings.")]
        public bool enableStringPass = true;
        [Tooltip("Max gap length to fill during the string pass (in cells).")]
        [Range(1, 3)] public int stringFillGap = 1;

        [Header("Rivers / Water")]
        [Range(0, 4)] public int riverCount = 2;
        [Tooltip("Minimum river length in tiles.")]
        public int riverMinLength = 12;
        [Tooltip("Maximum river length in tiles.")]
        public int riverMaxLength = 30;
        [Tooltip("Chance each river step also marks an adjacent cell as Water (widens the river).")]
        [Range(0f, 0.5f)] public float riverWidenChance = 0.15f;

        [Header("Gold Mines")]
        [Tooltip("Per-cell probability of spawning a Gold Mine (outside clear zone).")]
        [Range(0f, 0.1f)] public float goldMineDensity = 0.015f;
        [Tooltip("Minimum distance between any two Gold Mine nodes.")]
        public int goldMineMinSpacing = 6;

        [Header("Wild Farms")]
        [Range(0f, 0.1f)] public float farmDensity = 0.020f;
        public int farmMinSpacing = 5;

        [Header("Rocks (Locked)")]
        [Range(0f, 0.1f)] public float rockDensity = 0.012f;
        public int rockMinSpacing = 4;

        [Header("Fog of War")]
        [Tooltip("Number of cells revealed around the Town Hall at run start.")]
        public int startingRevealRadius = 4;
    }
}
```

After creating the script, in the Unity Editor go to:
**Assets → ClockworkCraft → Data → right-click → Create → ClockworkCraft → Map Generation Settings**
This creates the `MapGenerationSettings.asset` file. Assign the asset to MapGenerator in the scene.

---

## Step 4 — ResourceNode (ClockworkCraft version)

Create `Assets/ClockworkCraft/Scripts/World/ResourceNode.cs`.

This is a new version of the ResourceNode concept. Do NOT modify `Assets/Scripts/Core/ResourceNode.cs` (RTChess). Create a fresh one in the ClockworkCraft namespace:

```csharp
using UnityEngine;
using ClockworkGrid;  // for IntervalTimer if reusing it

namespace ClockworkCraft
{
    public class ResourceNode : MonoBehaviour
    {
        [Header("Node Settings")]
        public TileType tileType;
        public ResourceType resourceType;
        [Range(1, 3)] public int tier = 1;
        public int hp = 5;
        public bool isInteractable = true;   // false for Rock and Water at start

        [Header("Loot Drop")]
        public int lootBonusAmount = 3;      // bonus resources dropped on death

        public int GridX { get; private set; }
        public int GridY { get; private set; }

        private int currentHp;

        public void Initialize(int x, int y)
        {
            GridX = x;
            GridY = y;
            currentHp = hp;
        }

        // Returns yield per tick (based on tier)
        public int GetYieldPerTick() => tier;

        // Called by a worker when it faces and acts on this node.
        // Returns true if the node was killed (so the worker can trigger takeover).
        public bool TakeDamage(int damage)
        {
            if (!isInteractable) return false;

            currentHp -= damage;
            if (currentHp <= 0)
            {
                OnDepleted();
                return true;
            }
            return false;
        }

        private void OnDepleted()
        {
            // Award loot bonus
            ResourceManager.Instance?.AddResource(resourceType, lootBonusAmount);

            // Notify NodeManager
            NodeManager.Instance?.OnNodeDepleted(this);

            // Remove from grid
            GridManager.Instance?.ClearCell(GridX, GridY);

            Destroy(gameObject);
        }
    }
}
```

---

## Step 5 — MapGenerator

Create `Assets/ClockworkCraft/Scripts/Core/MapGenerator.cs`.

This is the main system. It runs 8 passes in order to build the map. It reads all settings from the `MapGenerationSettings` ScriptableObject.

```csharp
using UnityEngine;
using System.Collections.Generic;

namespace ClockworkCraft
{
    public class MapGenerator : MonoBehaviour
    {
        public static MapGenerator Instance { get; private set; }

        [Header("Settings")]
        public MapGenerationSettings settings;

        [Header("Prefabs")]
        public GameObject townHallPrefab;
        public GameObject treePrefab;
        public GameObject goldMinePrefab;
        public GameObject wildFarmPrefab;
        public GameObject rockPrefab;
        public GameObject waterPrefab;

        // Internal grid — stores what tile type is at each cell
        private TileType[,] tileGrid;
        private int width;
        private int height;
        private Vector2Int center;
        private System.Random rng;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            GenerateMap(settings.seed == 0 ? Random.Range(1, 999999) : settings.seed);
        }

        public void GenerateMap(int seed)
        {
            rng = new System.Random(seed);
            width = settings.mapWidth;
            height = settings.mapHeight;
            center = new Vector2Int(width / 2, height / 2);
            tileGrid = new TileType[width, height];

            // Run passes in order
            Pass1_ClearZone();
            Pass2_GuaranteedStartingResources();
            Pass3_Rivers();
            Pass4_ForestNoise(seed);
            Pass5_GoldMines();
            Pass6_WildFarms();
            Pass7_Rocks();
            Pass8_Validate();

            // Spawn all tiles
            SpawnTiles();

            // Place Town Hall
            PlaceTownHall();

            // Apply fog of war
            FogManager.Instance?.RevealRadius(center, settings.startingRevealRadius);

            Debug.Log($"[MapGenerator] Map generated. Seed: {seed}");
        }

        // ── PASS 1: Clear Zone ───────────────────────────────────────────────
        void Pass1_ClearZone()
        {
            // Mark center cells as Empty — no nodes may be placed here
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                tileGrid[x, y] = TileType.Empty;
            }
            // Town Hall cell
            tileGrid[center.x, center.y] = TileType.TownHall;
        }

        bool IsInClearZone(int x, int y)
        {
            int dx = x - center.x;
            int dy = y - center.y;
            return (dx * dx + dy * dy) <= settings.clearRadius * settings.clearRadius;
        }

        // ── PASS 2: Guaranteed Starting Resources ────────────────────────────
        void Pass2_GuaranteedStartingResources()
        {
            // Place guaranteed Gold Mine at distance 4–7 from center
            PlaceGuaranteedNode(TileType.GoldMine, settings.goldMineMinDist, settings.goldMineMaxDist);

            // Place guaranteed Tree clusters
            for (int i = 0; i < settings.guaranteedTreeClusters; i++)
            {
                Vector2Int pos = GetRandomEmptyCell(settings.clearRadius + 1, settings.clearRadius + 5);
                if (pos.x >= 0) PlaceCluster(TileType.Tree, pos, 2, 4);
            }

            // Place guaranteed Wild Farm
            for (int i = 0; i < settings.guaranteedFarms; i++)
            {
                PlaceGuaranteedNode(TileType.WildFarm, settings.clearRadius + 1, settings.clearRadius + 6);
            }
        }

        void PlaceGuaranteedNode(TileType type, int minDist, int maxDist)
        {
            for (int attempt = 0; attempt < 100; attempt++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2);
                float dist = minDist + (float)(rng.NextDouble() * (maxDist - minDist));
                int x = center.x + Mathf.RoundToInt(Mathf.Cos(angle) * dist);
                int y = center.y + Mathf.RoundToInt(Mathf.Sin(angle) * dist);
                if (IsInBounds(x, y) && tileGrid[x, y] == TileType.Empty && !IsInClearZone(x, y))
                {
                    tileGrid[x, y] = type;
                    return;
                }
            }
            Debug.LogWarning($"[MapGenerator] Could not place guaranteed {type} node after 100 attempts.");
        }

        void PlaceCluster(TileType type, Vector2Int origin, int minSize, int maxSize)
        {
            int size = rng.Next(minSize, maxSize + 1);
            // Place origin
            if (IsInBounds(origin.x, origin.y) && tileGrid[origin.x, origin.y] == TileType.Empty)
                tileGrid[origin.x, origin.y] = type;

            // Grow in random orthogonal directions
            Vector2Int current = origin;
            for (int i = 1; i < size; i++)
            {
                int[] dirs = { 0, 1, 2, 3 };
                ShuffleFY(dirs);
                foreach (int dir in dirs)
                {
                    Vector2Int next = current + DirectionToVector(dir);
                    if (IsInBounds(next.x, next.y) && tileGrid[next.x, next.y] == TileType.Empty && !IsInClearZone(next.x, next.y))
                    {
                        tileGrid[next.x, next.y] = type;
                        current = next;
                        break;
                    }
                }
            }
        }

        // ── PASS 3: Rivers ───────────────────────────────────────────────────
        void Pass3_Rivers()
        {
            for (int r = 0; r < settings.riverCount; r++)
            {
                GenerateRiver();
            }
        }

        void GenerateRiver()
        {
            // Start from a random edge
            int side = rng.Next(0, 4); // 0=top, 1=bottom, 2=left, 3=right
            Vector2Int pos = GetEdgeStart(side);
            Vector2Int targetDir = GetOppositeDirection(side);

            int length = rng.Next(settings.riverMinLength, settings.riverMaxLength + 1);
            for (int i = 0; i < length; i++)
            {
                if (!IsInBounds(pos.x, pos.y)) break;
                if (!IsInClearZone(pos.x, pos.y))
                    tileGrid[pos.x, pos.y] = TileType.Water;

                // Widen
                if (rng.NextDouble() < settings.riverWidenChance)
                {
                    Vector2Int side1 = pos + new Vector2Int(targetDir.y, -targetDir.x);
                    if (IsInBounds(side1.x, side1.y) && !IsInClearZone(side1.x, side1.y))
                        tileGrid[side1.x, side1.y] = TileType.Water;
                }

                // Move: 70% continue in main direction, 30% drift
                if (rng.NextDouble() < 0.7)
                    pos += targetDir;
                else
                    pos += rng.NextDouble() < 0.5
                        ? new Vector2Int(targetDir.y, -targetDir.x)
                        : new Vector2Int(-targetDir.y, targetDir.x);
            }
        }

        // ── PASS 4: Forest (Perlin Noise) ────────────────────────────────────
        void Pass4_ForestNoise(int seed)
        {
            float offsetX = (float)(rng.NextDouble() * 1000);
            float offsetY = (float)(rng.NextDouble() * 1000);

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (tileGrid[x, y] != TileType.Empty) continue;
                if (IsInClearZone(x, y)) continue;

                float nx = (x + offsetX) / settings.treeNoiseScale;
                float ny = (y + offsetY) / settings.treeNoiseScale;
                float value = Mathf.PerlinNoise(nx, ny);

                if (value > settings.treeDensityThreshold)
                    tileGrid[x, y] = TileType.Tree;
            }

            // String fill pass
            if (settings.enableStringPass)
                StringFillPass();
        }

        void StringFillPass()
        {
            // For each Empty cell, check if there are Trees on both sides
            // (orthogonally) within stringFillGap cells. If so, fill the gap.
            int gap = settings.stringFillGap;
            Vector2Int[] axes = { new Vector2Int(1, 0), new Vector2Int(0, 1) };

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (tileGrid[x, y] != TileType.Empty) continue;

                foreach (var axis in axes)
                {
                    // Check both sides along this axis
                    bool foundA = false, foundB = false;
                    for (int d = 1; d <= gap + 1; d++)
                    {
                        int ax = x + axis.x * d, ay = y + axis.y * d;
                        if (IsInBounds(ax, ay) && tileGrid[ax, ay] == TileType.Tree) { foundA = true; break; }
                        if (!IsInBounds(ax, ay) || tileGrid[ax, ay] != TileType.Empty) break;
                    }
                    for (int d = 1; d <= gap + 1; d++)
                    {
                        int bx = x - axis.x * d, by = y - axis.y * d;
                        if (IsInBounds(bx, by) && tileGrid[bx, by] == TileType.Tree) { foundB = true; break; }
                        if (!IsInBounds(bx, by) || tileGrid[bx, by] != TileType.Empty) break;
                    }
                    if (foundA && foundB && !IsInClearZone(x, y))
                    {
                        tileGrid[x, y] = TileType.Tree;
                        break;
                    }
                }
            }
        }

        // ── PASS 5: Gold Mines ───────────────────────────────────────────────
        void Pass5_GoldMines()
        {
            ScatterNodes(TileType.GoldMine, settings.goldMineDensity, settings.goldMineMinSpacing);
        }

        // ── PASS 6: Wild Farms ───────────────────────────────────────────────
        void Pass6_WildFarms()
        {
            ScatterNodes(TileType.WildFarm, settings.farmDensity, settings.farmMinSpacing);
        }

        // ── PASS 7: Rocks ────────────────────────────────────────────────────
        void Pass7_Rocks()
        {
            ScatterNodes(TileType.Rock, settings.rockDensity, settings.rockMinSpacing);
        }

        void ScatterNodes(TileType type, float density, int minSpacing)
        {
            List<Vector2Int> placed = new List<Vector2Int>();
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (tileGrid[x, y] != TileType.Empty) continue;
                if (IsInClearZone(x, y)) continue;
                if (rng.NextDouble() > density) continue;

                // Check min spacing
                bool tooClose = false;
                foreach (var p in placed)
                {
                    int dx = x - p.x, dy = y - p.y;
                    if (dx * dx + dy * dy < minSpacing * minSpacing) { tooClose = true; break; }
                }
                if (tooClose) continue;

                tileGrid[x, y] = type;
                placed.Add(new Vector2Int(x, y));
            }
        }

        // ── PASS 8: Validation ───────────────────────────────────────────────
        void Pass8_Validate()
        {
            // Ensure Town Hall cell is correct
            tileGrid[center.x, center.y] = TileType.TownHall;

            // Ensure the 8 cells directly adjacent to center are empty
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int x = center.x + dx, y = center.y + dy;
                if (IsInBounds(x, y) && tileGrid[x, y] != TileType.Empty)
                    tileGrid[x, y] = TileType.Empty;
            }
        }

        // ── SPAWN ────────────────────────────────────────────────────────────
        void SpawnTiles()
        {
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                GameObject prefab = GetPrefabForTile(tileGrid[x, y]);
                if (prefab == null) continue;

                Vector3 worldPos = GridManager.Instance.GridToWorldPosition(x, y);
                GameObject obj = Instantiate(prefab, worldPos, Quaternion.identity);

                // Initialize ResourceNode component if present
                if (obj.TryGetComponent<ResourceNode>(out var node))
                {
                    node.Initialize(x, y);
                    // Assign interactability
                    node.isInteractable = (tileGrid[x, y] == TileType.Rock || tileGrid[x, y] == TileType.Water)
                        ? false : true;
                    // Tier based on distance from center
                    float dist = Vector2Int.Distance(new Vector2Int(x, y), center);
                    node.tier = dist < 10 ? 1 : dist < 20 ? (rng.NextDouble() < 0.5 ? 1 : 2) : (rng.NextDouble() < 0.4 ? 2 : 3);

                    NodeManager.Instance?.RegisterNode(node);
                }

                // Register with GridManager
                GridManager.Instance?.RegisterTile(x, y, tileGrid[x, y], obj);
            }
        }

        void PlaceTownHall()
        {
            Vector3 worldPos = GridManager.Instance.GridToWorldPosition(center.x, center.y);
            Instantiate(townHallPrefab, worldPos, Quaternion.identity);
        }

        // ── HELPERS ──────────────────────────────────────────────────────────
        bool IsInBounds(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;

        GameObject GetPrefabForTile(TileType type) => type switch
        {
            TileType.Tree      => treePrefab,
            TileType.GoldMine  => goldMinePrefab,
            TileType.WildFarm  => wildFarmPrefab,
            TileType.Rock      => rockPrefab,
            TileType.Water     => waterPrefab,
            _                  => null
        };

        Vector2Int GetEdgeStart(int side) => side switch
        {
            0 => new Vector2Int(rng.Next(0, width), 0),
            1 => new Vector2Int(rng.Next(0, width), height - 1),
            2 => new Vector2Int(0, rng.Next(0, height)),
            _ => new Vector2Int(width - 1, rng.Next(0, height))
        };

        Vector2Int GetOppositeDirection(int side) => side switch
        {
            0 => new Vector2Int(0, 1),
            1 => new Vector2Int(0, -1),
            2 => new Vector2Int(1, 0),
            _ => new Vector2Int(-1, 0)
        };

        Vector2Int DirectionToVector(int dir) => dir switch
        {
            0 => Vector2Int.up,
            1 => Vector2Int.right,
            2 => Vector2Int.down,
            _ => Vector2Int.left
        };

        Vector2Int GetRandomEmptyCell(int minDist, int maxDist)
        {
            for (int attempt = 0; attempt < 200; attempt++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2);
                float dist = minDist + (float)(rng.NextDouble() * (maxDist - minDist));
                int x = center.x + Mathf.RoundToInt(Mathf.Cos(angle) * dist);
                int y = center.y + Mathf.RoundToInt(Mathf.Sin(angle) * dist);
                if (IsInBounds(x, y) && tileGrid[x, y] == TileType.Empty)
                    return new Vector2Int(x, y);
            }
            return new Vector2Int(-1, -1);
        }

        void ShuffleFY(int[] arr)
        {
            for (int i = arr.Length - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }
        }
    }
}
```

---

## Step 6 — NodeManager

Create `Assets/ClockworkCraft/Scripts/Core/NodeManager.cs` as a lightweight singleton that tracks all active nodes and handles the tile takeover trigger:

```csharp
using UnityEngine;
using System.Collections.Generic;

namespace ClockworkCraft
{
    public class NodeManager : MonoBehaviour
    {
        public static NodeManager Instance { get; private set; }

        private Dictionary<Vector2Int, ResourceNode> nodes = new();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void RegisterNode(ResourceNode node)
        {
            nodes[new Vector2Int(node.GridX, node.GridY)] = node;
        }

        public ResourceNode GetNode(int x, int y) =>
            nodes.TryGetValue(new Vector2Int(x, y), out var n) ? n : null;

        public void OnNodeDepleted(ResourceNode node)
        {
            nodes.Remove(new Vector2Int(node.GridX, node.GridY));
            // Tile takeover is handled by the Worker that dealt the killing blow
            // (Worker.OnKilledNode is called from ResourceNode.TakeDamage)
        }
    }
}
```

---

## Step 7 — Integrate GridManager

The RTChess `GridManager` tracks cell occupants. You need it to know about ClockworkCraft tile types. Add a wrapper method if needed, OR ensure `GridManager` has a `RegisterTile(int x, int y, TileType type, GameObject obj)` method accessible from `ClockworkCraft` namespace.

If GridManager does not have this method, add it in a `partial class` extension file or create a thin wrapper. **Do not modify the existing GridManager.cs directly.**

---

## Step 8 — Tile Takeover in Worker

When you implement the Worker unit (separate task), the tile takeover logic goes here:

```csharp
// Inside Worker.cs — called when a facing node is killed

void OnKilledNode(ResourceNode killedNode)
{
    int newX = killedNode.GridX;
    int newY = killedNode.GridY;

    // Move this worker to the killed node's tile
    GridManager.Instance.MoveUnit(GridX, GridY, newX, newY);
    GridX = newX;
    GridY = newY;
    transform.position = GridManager.Instance.GridToWorldPosition(newX, newY);

    // Reveal fog around new position
    FogManager.Instance?.RevealRadius(new Vector2Int(newX, newY), 2);

    // Note: facing direction stays the same — worker will face the next cell on the next tick
}
```

---

## Step 9 — Scene Setup

In the new `ClockworkCraft.unity` scene:

1. Create a GameObject named **MapGenerator** — add the `MapGenerator` component — assign `MapGenerationSettings.asset` to the Settings field — assign all node prefabs.
2. Create a GameObject named **NodeManager** — add the `NodeManager` component.
3. Add or reference **GridManager** from RTChess (or create a new instance configured for 40×40 grid).
4. Add or reference **FogManager** — configure with `startingRevealRadius = 4`.
5. Add **IntervalTimer** from RTChess (reuse directly).

---

## Step 10 — Prefabs

Create simple placeholder prefabs for each node type. Use distinct colours:

| Node | Colour | Notes |
|------|--------|-------|
| TreeNode | Dark green cube | Should visually cluster well — use a slightly irregular shape if possible |
| GoldMineNode | Yellow/gold cube | |
| WildFarmNode | Light green flat cube | |
| RockNode | Grey cube | Visually distinct from trees — darker, rougher |
| WaterTile | Blue flat plane | Does not need a ResourceNode component |
| TownHall | Brown/orange cube, slightly taller | Pre-placed at center |

Each prefab except WaterTile and TownHall needs:
- A `ResourceNode` component
- A `BoxCollider` (for future mouse interaction)
- The `tileType` and `resourceType` fields set correctly in the Inspector

---

## Testing Checklist

After implementation, verify the following before moving to Phase 2:

- [ ] Map generates on scene play without errors
- [ ] Town Hall is always at the grid center
- [ ] Guaranteed Gold Mine always spawns within 4–7 cells of center
- [ ] Guaranteed trees always spawn near the starting area
- [ ] Trees appear in strings/clusters (not isolated single tiles)
- [ ] Rivers are visible as connected Water tiles crossing the map
- [ ] Rocks are scattered sparsely and do not cluster densely
- [ ] Gold Mines are rarer than trees
- [ ] No two Gold Mines are immediately adjacent (min spacing respected)
- [ ] The 8 cells directly adjacent to Town Hall are always empty
- [ ] Fog of war covers the full map at start; area around Town Hall is revealed
- [ ] Changing `seed` to a fixed value produces the same map every time
- [ ] Changing `treeDensityThreshold` in the Inspector changes forest density
- [ ] Pressing Play with `seed = 0` produces a different map each time
- [ ] No compile errors referencing RTChess internals from ClockworkCraft scripts
- [ ] RTChess scene still runs without changes

---

## Notes for Future Phases

- The `isInteractable` flag on `ResourceNode` is how Rock and Water get unlocked later. A future research system will call `node.isInteractable = true` on all nodes of a given type when the relevant tech is researched.
- The tier assignment in `SpawnTiles()` is currently distance-based. This is intentional for Phase 1 simplicity. A future pass can replace it with separate noise layers per resource type.
- The Worker's tile takeover (Step 8) is documented here for reference but should be implemented as part of the Worker system task (Phase 1B).
