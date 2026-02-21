using UnityEngine;
using System.Collections.Generic;
using ClockworkGrid;   // GridManager, FogManager
using LittleCafe;      // EnvironmentDatabase, EnvironmentData

namespace ClockworkCraft
{
    /// <summary>
    /// Procedural map generator for ClockworkCraft.
    /// Runs 8 ordered passes on a flat TileType grid, then spawns prefabs and
    /// registers them with GridManager and NodeManager.
    ///
    /// All density / probability values live in MapGenerationSettings (ScriptableObject)
    /// so designers can tune without touching code.
    ///
    /// [DefaultExecutionOrder(100)] ensures this Start() runs after GridManager.Start()
    /// and FogManager.Awake(), which both run at the default execution order.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class MapGenerator : MonoBehaviour
    {
        public static MapGenerator Instance { get; private set; }

        [Header("Settings")]
        public MapGenerationSettings settings;

        [Header("Environment Database")]
        [Tooltip("Drag EnvironmentDatabase.asset here. Prefabs and HP for all resource nodes.")]
        public EnvironmentDatabase environmentDatabase;

        [Header("Building Database")]
        [Tooltip("Drag BuildingDatabase.asset here. Used for the Town Hall prefab.")]
        public BuildingDatabase buildingDatabase;

        [Header("Database Entry Names")]
        [Tooltip("Must match the assetName field of each database entry.")]
        public string treeNodeName         = "Tree";
        public string goldMineNodeName     = "GoldMine";
        public string wildFarmNodeName     = "WildFarm";
        public string rockNodeName         = "Rock";
        public string waterNodeName        = "Water";
        public string townHallBuildingName = "TownHall";

        // Internal grid — stores tile type at each cell
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
            if (settings == null)
            {
                Debug.LogError("[MapGenerator] MapGenerationSettings not assigned!");
                return;
            }

            // Auto-create dependency managers if not already present in the scene
            if (NodeManager.Instance == null)
            {
                new GameObject("NodeManager").AddComponent<NodeManager>();
                Debug.Log("[MapGenerator] Auto-created NodeManager");
            }
            if (ResourceManager.Instance == null)
            {
                new GameObject("ResourceManager").AddComponent<ResourceManager>();
                Debug.Log("[MapGenerator] Auto-created ResourceManager");
            }

            // If a GameStartGate is present (LittleCafe scene deferred-init pattern),
            // wait for the player to start before generating — so the grid is fully
            // re-initialized by CafeSceneSetupV2.OnGameStarted() first.
            // If no gate exists (standalone ClockworkCraft scene), generate immediately.
            var gate = FindObjectOfType<LittleCafe.GameStartGate>();
            if (gate != null)
            {
                gate.OnGameStart += RunGenerate;
                Debug.Log("[MapGenerator] Waiting for GameStartGate before generating map...");
            }
            else
            {
                RunGenerate();
            }
        }

        void RunGenerate()
        {
            GenerateMap(settings.seed == 0 ? Random.Range(1, 999999) : settings.seed);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        public void GenerateMap(int seed)
        {
            if (GridManager.Instance == null)
            {
                Debug.LogError("[MapGenerator] GridManager not found in scene!");
                return;
            }

            rng    = new System.Random(seed);
            width  = settings.mapWidth;
            height = settings.mapHeight;
            center = new Vector2Int(width / 2, height / 2);
            tileGrid = new TileType[width, height];

            // Initialise FogManager for this map size (all cells fogged)
            FogManager.Instance?.Initialize(width, height);

            // ── Run passes in order ──────────────────────────────────────────
            Pass1_ClearZone();
            Pass2_GuaranteedStartingResources();
            Pass3_Rivers();
            Pass4_ForestNoise(seed);
            Pass5_GoldMines();
            Pass6_WildFarms();
            Pass7_Rocks();
            Pass8_Validate();

            // ── Spawn ────────────────────────────────────────────────────────
            SpawnTiles();
            PlaceTownHall();

            // Reveal starting area around Town Hall
            FogManager.Instance?.RevealRadius(center.x, center.y, settings.startingRevealRadius);

            Debug.Log($"[MapGenerator] Map generated. Seed={seed}  Size={width}x{height}  Nodes={NodeManager.Instance?.NodeCount}");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Pass 1 — Clear Zone
        // ─────────────────────────────────────────────────────────────────────

        void Pass1_ClearZone()
        {
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                tileGrid[x, y] = TileType.Empty;

            tileGrid[center.x, center.y] = TileType.TownHall;
        }

        bool IsInClearZone(int x, int y)
        {
            int dx = x - center.x;
            int dy = y - center.y;
            return (dx * dx + dy * dy) <= settings.clearRadius * settings.clearRadius;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Pass 2 — Guaranteed Starting Resources
        // ─────────────────────────────────────────────────────────────────────

        void Pass2_GuaranteedStartingResources()
        {
            // Guaranteed Gold Mine at distance 4–7
            PlaceGuaranteedNode(TileType.GoldMine, settings.goldMineMinDist, settings.goldMineMaxDist);

            // Guaranteed tree clusters bordering the clear zone
            for (int i = 0; i < settings.guaranteedTreeClusters; i++)
            {
                Vector2Int pos = GetRandomEmptyCell(settings.clearRadius + 1, settings.clearRadius + 5);
                if (pos.x >= 0) PlaceCluster(TileType.Tree, pos, 2, 4);
            }

            // Guaranteed Wild Farm
            for (int i = 0; i < settings.guaranteedFarms; i++)
                PlaceGuaranteedNode(TileType.WildFarm, settings.clearRadius + 1, settings.clearRadius + 6);
        }

        void PlaceGuaranteedNode(TileType type, int minDist, int maxDist)
        {
            for (int attempt = 0; attempt < 100; attempt++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2);
                float dist  = minDist + (float)(rng.NextDouble() * (maxDist - minDist));
                int x = center.x + Mathf.RoundToInt(Mathf.Cos(angle) * dist);
                int y = center.y + Mathf.RoundToInt(Mathf.Sin(angle) * dist);
                if (IsInBounds(x, y) && tileGrid[x, y] == TileType.Empty && !IsInClearZone(x, y))
                {
                    tileGrid[x, y] = type;
                    return;
                }
            }
            Debug.LogWarning($"[MapGenerator] Could not place guaranteed {type} after 100 attempts.");
        }

        void PlaceCluster(TileType type, Vector2Int origin, int minSize, int maxSize)
        {
            int size = rng.Next(minSize, maxSize + 1);
            if (IsInBounds(origin.x, origin.y) && tileGrid[origin.x, origin.y] == TileType.Empty)
                tileGrid[origin.x, origin.y] = type;

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

        // ─────────────────────────────────────────────────────────────────────
        // Pass 3 — Rivers (random-walk Water tiles)
        // ─────────────────────────────────────────────────────────────────────

        void Pass3_Rivers()
        {
            for (int r = 0; r < settings.riverCount; r++)
                GenerateRiver();
        }

        void GenerateRiver()
        {
            int side = rng.Next(0, 4);          // 0=top, 1=bottom, 2=left, 3=right
            Vector2Int pos       = GetEdgeStart(side);
            Vector2Int targetDir = GetOppositeDirection(side);

            int length = rng.Next(settings.riverMinLength, settings.riverMaxLength + 1);
            for (int i = 0; i < length; i++)
            {
                if (!IsInBounds(pos.x, pos.y)) break;

                if (!IsInClearZone(pos.x, pos.y))
                    tileGrid[pos.x, pos.y] = TileType.Water;

                // Widen: occasionally mark perpendicular neighbour as Water
                if (rng.NextDouble() < settings.riverWidenChance)
                {
                    Vector2Int side1 = pos + new Vector2Int(targetDir.y, -targetDir.x);
                    if (IsInBounds(side1.x, side1.y) && !IsInClearZone(side1.x, side1.y))
                        tileGrid[side1.x, side1.y] = TileType.Water;
                }

                // Move: 70% straight, 30% perpendicular drift
                if (rng.NextDouble() < 0.7)
                    pos += targetDir;
                else
                    pos += rng.NextDouble() < 0.5
                        ? new Vector2Int( targetDir.y, -targetDir.x)
                        : new Vector2Int(-targetDir.y,  targetDir.x);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Pass 4 — Forest (Perlin Noise + String Fill)
        // ─────────────────────────────────────────────────────────────────────

        void Pass4_ForestNoise(int seed)
        {
            float offsetX = (float)(rng.NextDouble() * 1000);
            float offsetY = (float)(rng.NextDouble() * 1000);

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (tileGrid[x, y] != TileType.Empty) continue;
                if (IsInClearZone(x, y)) continue;

                float nx    = (x + offsetX) / settings.treeNoiseScale;
                float ny    = (y + offsetY) / settings.treeNoiseScale;
                float value = Mathf.PerlinNoise(nx, ny);

                if (value > settings.treeDensityThreshold)
                    tileGrid[x, y] = TileType.Tree;
            }

            if (settings.enableStringPass)
                StringFillPass();
        }

        void StringFillPass()
        {
            // For each Empty cell, fill it if trees exist on both sides along an axis
            // within stringFillGap distance — creates connected strings of trees.
            int gap = settings.stringFillGap;
            Vector2Int[] axes = { new Vector2Int(1, 0), new Vector2Int(0, 1) };

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (tileGrid[x, y] != TileType.Empty) continue;

                foreach (var axis in axes)
                {
                    bool foundA = false, foundB = false;

                    for (int d = 1; d <= gap + 1; d++)
                    {
                        int ax = x + axis.x * d, ay = y + axis.y * d;
                        if (!IsInBounds(ax, ay) || tileGrid[ax, ay] != TileType.Empty) { foundA = IsInBounds(ax, ay) && tileGrid[ax, ay] == TileType.Tree; break; }
                    }
                    for (int d = 1; d <= gap + 1; d++)
                    {
                        int bx = x - axis.x * d, by = y - axis.y * d;
                        if (!IsInBounds(bx, by) || tileGrid[bx, by] != TileType.Empty) { foundB = IsInBounds(bx, by) && tileGrid[bx, by] == TileType.Tree; break; }
                    }

                    if (foundA && foundB && !IsInClearZone(x, y))
                    {
                        tileGrid[x, y] = TileType.Tree;
                        break;
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Pass 5 — Gold Mines
        // ─────────────────────────────────────────────────────────────────────

        void Pass5_GoldMines()
        {
            ScatterNodes(TileType.GoldMine, settings.goldMineDensity, settings.goldMineMinSpacing);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Pass 6 — Wild Farms
        // ─────────────────────────────────────────────────────────────────────

        void Pass6_WildFarms()
        {
            ScatterNodes(TileType.WildFarm, settings.farmDensity, settings.farmMinSpacing);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Pass 7 — Rocks (Locked)
        // ─────────────────────────────────────────────────────────────────────

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

        // ─────────────────────────────────────────────────────────────────────
        // Pass 8 — Final Validation
        // ─────────────────────────────────────────────────────────────────────

        void Pass8_Validate()
        {
            // Town Hall cell is always correct
            tileGrid[center.x, center.y] = TileType.TownHall;

            // The 8 cells immediately adjacent to center must be clear
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int x = center.x + dx, y = center.y + dy;
                if (IsInBounds(x, y) && tileGrid[x, y] != TileType.Empty)
                    tileGrid[x, y] = TileType.Empty;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Spawn
        // ─────────────────────────────────────────────────────────────────────

        void SpawnTiles()
        {
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                GameObject prefab = GetPrefabForTile(tileGrid[x, y]);
                if (prefab == null) continue;   // Empty / TownHall handled separately

                Vector3 worldPos = GridManager.Instance.GridToWorldPosition(x, y);
                GameObject obj   = Instantiate(prefab, worldPos, Quaternion.identity);

                // Configure ResourceNode component if present
                if (obj.TryGetComponent<ResourceNode>(out var node))
                {
                    // Apply HP and resource type from database entry
                    EnvironmentData envData = GetEnvData(tileGrid[x, y]);
                    if (envData != null)
                    {
                        node.hp            = envData.hp;
                        node.lootBonusAmount = envData.hp; // bonus = base HP (generous drop)
                    }
                    node.resourceType = TileTypeToResourceType(tileGrid[x, y]);

                    node.Initialize(x, y);

                    // Rock and Water are not interactable at game start
                    node.isInteractable = tileGrid[x, y] != TileType.Rock
                                       && tileGrid[x, y] != TileType.Water;

                    // Tier based on distance from center (outer map = higher tier)
                    float dist = Vector2Int.Distance(new Vector2Int(x, y), center);
                    node.tier = dist < 10f ? 1
                              : dist < 20f ? (rng.NextDouble() < 0.5 ? 1 : 2)
                              :              (rng.NextDouble() < 0.4 ? 2 : 3);

                    NodeManager.Instance?.RegisterNode(node);
                }

                // Register with GridManager occupant table (all spawnable nodes = Resource state)
                GridManager.Instance?.PlaceUnit(x, y, obj, CellState.Resource);
            }
        }

        void PlaceTownHall()
        {
            if (buildingDatabase == null) return;
            BuildingData data = buildingDatabase.GetByName(townHallBuildingName);
            if (data == null || data.prefab == null)
            {
                Debug.LogWarning($"[MapGenerator] Town Hall entry '{townHallBuildingName}' not found in BuildingDatabase or has no prefab.");
                return;
            }
            Vector3 worldPos = GridManager.Instance.GridToWorldPosition(center.x, center.y);
            Instantiate(data.prefab, worldPos, Quaternion.identity);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        bool IsInBounds(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;

        // Maps TileType to the assetName configured in the Inspector
        string TileTypeToNodeName(TileType type) => type switch
        {
            TileType.Tree     => treeNodeName,
            TileType.GoldMine => goldMineNodeName,
            TileType.WildFarm => wildFarmNodeName,
            TileType.Rock     => rockNodeName,
            TileType.Water    => waterNodeName,
            _                 => null
        };

        // Looks up the EnvironmentData entry for a tile type
        EnvironmentData GetEnvData(TileType type)
        {
            if (environmentDatabase == null) return null;
            string name = TileTypeToNodeName(type);
            return name != null ? environmentDatabase.GetByName(name) : null;
        }

        // Gets the spawnable prefab from the database (null = don't spawn)
        GameObject GetPrefabForTile(TileType type)
        {
            EnvironmentData data = GetEnvData(type);
            if (data != null && data.prefab != null) return data.prefab;
            return null;   // Empty / TownHall / unmapped entries → skip
        }

        // Maps TileType to which resource currency this node yields
        static ResourceType TileTypeToResourceType(TileType type) => type switch
        {
            TileType.Tree     => ResourceType.Wood,
            TileType.GoldMine => ResourceType.Gold,
            TileType.WildFarm => ResourceType.Food,
            TileType.Rock     => ResourceType.Stone,
            _                 => ResourceType.None
        };

        Vector2Int GetEdgeStart(int side) => side switch
        {
            0 => new Vector2Int(rng.Next(0, width),  0),
            1 => new Vector2Int(rng.Next(0, width),  height - 1),
            2 => new Vector2Int(0,                   rng.Next(0, height)),
            _ => new Vector2Int(width - 1,           rng.Next(0, height))
        };

        Vector2Int GetOppositeDirection(int side) => side switch
        {
            0 =>  Vector2Int.up,
            1 =>  Vector2Int.down,
            2 =>  Vector2Int.right,
            _ =>  Vector2Int.left
        };

        Vector2Int DirectionToVector(int dir) => dir switch
        {
            0 =>  Vector2Int.up,
            1 =>  Vector2Int.right,
            2 =>  Vector2Int.down,
            _ =>  Vector2Int.left
        };

        Vector2Int GetRandomEmptyCell(int minDist, int maxDist)
        {
            for (int attempt = 0; attempt < 200; attempt++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2);
                float dist  = minDist + (float)(rng.NextDouble() * (maxDist - minDist));
                int x = center.x + Mathf.RoundToInt(Mathf.Cos(angle) * dist);
                int y = center.y + Mathf.RoundToInt(Mathf.Sin(angle) * dist);
                if (IsInBounds(x, y) && tileGrid[x, y] == TileType.Empty)
                    return new Vector2Int(x, y);
            }
            return new Vector2Int(-1, -1); // sentinel: no cell found
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
