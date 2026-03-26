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

        [Header("Prefab")]
        [Tooltip("The bubble popup prefab (must have or will get a POIBubble component).")]
        [SerializeField] private GameObject bubblePrefab;

        [Header("Points of Interest")]
        [Tooltip("POI entries — synced from Google Sheets via SheetSyncEditor.")]
        [SerializeField] private List<POITypeData> poiEntries = new List<POITypeData>();

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

        /// <summary>Public access to POI entries (for SheetSyncEditor).</summary>
        public List<POITypeData> Entries => poiEntries;

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

        /// <summary>Find POI data by asset name (case-insensitive substring match).</summary>
        private POITypeData GetPOIData(string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) return null;
            foreach (var entry in poiEntries)
            {
                if (!entry.active || string.IsNullOrEmpty(entry.typeName)) continue;
                if (assetName.IndexOf(entry.typeName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return entry;
            }
            return null;
        }

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

            // Hearts always get a bubble immediately — use POI_Red
            ShowBubble(pos, "Corruption", BubbleType.POI_Red);
        }

        /// <summary>Called by CorruptionHeart.OnDestroy().</summary>
        public void UnregisterHeart(CorruptionHeart heart)
        {
            if (heart == null) return;
            var pos = heart.GridPosition;
            DismissBubble(pos);
            heartRegistry.Remove(pos);
        }

        /// <summary>
        /// Called by MapGeneratorV2 after each env object spawn.
        /// Only registers Singular-type POIs here — Cluster/Area types are handled
        /// by RegisterGatherings() which checks quantityMinimum.
        /// </summary>
        public void RegisterEnvPOI(Vector2Int gridPos, string assetName)
        {
            if (poiEntries == null || poiEntries.Count == 0) return;
            var data = GetPOIData(assetName);
            if (data == null || !data.active) return;

            // Cluster/Area types are handled by RegisterGatherings — skip per-object registration
            if (data.groupingType != POIGrouping.Singular) return;

            if (envRegistry.ContainsKey(gridPos)) return;
            envRegistry[gridPos] = new EnvPOIEntry
            {
                gridPos = gridPos,
                assetName = assetName
            };
        }

        /// <summary>
        /// Called by MapGeneratorV2 after spawning, before Initialize().
        /// Filters gatherings against POIDatabase: only Cluster/Area types that meet
        /// quantityMinimum get a POI registered at the gathering centroid.
        /// Singular-type POIs are still handled per-object via RegisterEnvPOI().
        /// </summary>
        public void RegisterGatherings(IReadOnlyList<EnvironmentGathering> gatherings)
        {
            if (poiEntries == null || poiEntries.Count == 0 || gatherings == null) return;

            int registered = 0;
            foreach (var gathering in gatherings)
            {
                var data = GetPOIData(gathering.assetName);
                if (data == null || !data.active) continue;

                // Singular POIs are registered per-object by RegisterEnvPOI — skip here
                if (data.groupingType == POIGrouping.Singular) continue;

                // Must meet the minimum size threshold
                if (gathering.size < data.quantityMinimum) continue;

                // Register at the gathering centroid
                if (envRegistry.ContainsKey(gathering.centroid)) continue;
                envRegistry[gathering.centroid] = new EnvPOIEntry
                {
                    gridPos = gathering.centroid,
                    assetName = gathering.assetName
                };
                registered++;
            }

            Debug.Log($"[POIManager] Registered {registered} gathering POIs from {gatherings.Count} total gatherings.");
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
                AwardReward("Corruption");
                DismissBubble(coord);
                heartRegistry.Remove(coord);
            }

            // Env POI discovered
            if (envRegistry.TryGetValue(coord, out var entry))
            {
                AwardReward(entry.assetName);
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
                var data = GetPOIData(candidates[i].entry.assetName);
                var bubbleType = data != null ? data.GetBubbleType() : BubbleType.POI_Grey;
                ShowBubble(candidates[i].pos, candidates[i].entry.assetName, bubbleType);
                filled++;
            }
        }

        private int MinManhattanDistToRevealed(Vector2Int pos)
        {
            // Bounded scan around pos — more efficient than iterating all revealed cells
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

        private void ShowBubble(Vector2Int gridPos, string assetName, BubbleType bubbleType)
        {
            if (activeBubbles.ContainsKey(gridPos)) return;

            var bubble = GetFromPool();
            if (bubble == null) return;

            var data = GetPOIData(assetName);
            string text = data != null ? data.label : assetName;

            Vector3 worldPos = GridManager.Instance != null
                ? GridManager.Instance.GridToWorldPosition(gridPos.x, gridPos.y)
                : new Vector3(gridPos.x, 0f, gridPos.y);
            worldPos.y += heightAboveGround;

            bubble.Setup(bubbleType, text, worldPos);
            activeBubbles[gridPos] = bubble;
        }

        private void DismissBubble(Vector2Int gridPos)
        {
            if (!activeBubbles.TryGetValue(gridPos, out var bubble)) return;
            bubble.Dismiss();
            activeBubbles.Remove(gridPos);
        }

        private void AwardReward(string assetName)
        {
            if (poiEntries == null || poiEntries.Count == 0) return;
            var data = GetPOIData(assetName);
            if (data == null || data.rewardQuantity <= 0) return;

            if (ResourceManager.Instance != null)
                ResourceManager.Instance.AddResource(data.rewardType, data.rewardQuantity);
        }

        // ── Pool ────────────────────────────────────────────────────────

        private void CreatePool()
        {
            if (bubblePrefab == null) return;
            int total = maxEnvBubbles + heartPoolSize;

            // Parent under the world canvas if set, otherwise under this transform
            Transform parent = transform;

            for (int i = 0; i < total; i++)
            {
                var obj = Instantiate(bubblePrefab, parent);
                var bubble = obj.GetComponent<POIBubble>();
                if (bubble == null) bubble = obj.AddComponent<POIBubble>();
                bubble.SetAnimParams(popInDuration, bobHeight, bobDuration, fadeOutDuration);
                obj.SetActive(false);
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
            Transform parent = transform;
            var obj = Instantiate(bubblePrefab, parent);
            var overflow = obj.GetComponent<POIBubble>();
            if (overflow == null) overflow = obj.AddComponent<POIBubble>();
            overflow.SetAnimParams(popInDuration, bobHeight, bobDuration, fadeOutDuration);
            obj.SetActive(false);
            pool.Add(overflow);
            return overflow;
        }
    }
}
