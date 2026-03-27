using System.Collections;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ClockworkGrid;
using LittleCafe;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
        [Tooltip("The bubble popup prefab (must have or will get a POIBubble component).")]
        [SerializeField] private GameObject bubblePrefab;
        [SerializeField] private POIDatabase poiDatabase;

        [Header("Databases (for icon lookup)")]
        [SerializeField] private EnvironmentDatabase environmentDatabase;
        [SerializeField] private UnitDatabase unitDatabase;

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

        [Tooltip("World-space scale applied to each bubble Canvas. The prefab is 2560×1440px — at 0.005 each pixel ≈ 0.005 world units, giving a ~5-unit-wide bubble.")]
        [SerializeField] private float bubbleWorldScale = 0.005f;

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

        // Stagger queue — only one bubble appears at a time
        private readonly Queue<System.Action> bubbleQueue = new Queue<System.Action>();
        private Coroutine staggerCoroutine;
        private const float STAGGER_DELAY = 1.2f; // seconds between each bubble appearance

        // Last player activity position — bubbles closer to this appear first
        private Vector2Int lastActivityPos;

        /// <summary>Find POI data by exact asset name match (case-insensitive).</summary>
        private POITypeData GetPOIData(string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) return null;
            foreach (var entry in poiEntries)
            {
                if (!entry.active || string.IsNullOrEmpty(entry.typeName)) continue;
                if (string.Equals(entry.typeName, assetName, System.StringComparison.OrdinalIgnoreCase))
                    return entry;
            }
            return null;
        }

        // ── Diagnostics ────────────────────────────────────────────────

        /// <summary>Log the full state of the POI system to the console.</summary>
        [ContextMenu("Diagnose POI System")]
        public void DiagnosePOISystem()
        {
            Debug.Log("═══════════ POI System Diagnostic ═══════════");
            Debug.Log($"  bubblePrefab: {(bubblePrefab != null ? bubblePrefab.name : "NULL ← ASSIGN THIS")}");
            Debug.Log($"  poiDatabase:  {(poiDatabase != null ? poiDatabase.name : "NULL")}");
            Debug.Log($"  poiEntries:   {poiEntries?.Count ?? 0} entries");
            if (poiEntries != null)
            {
                foreach (var e in poiEntries)
                    Debug.Log($"    [{(e.active ? "ON" : "off")}] {e.typeName} → \"{e.label}\" | {e.sourceType} | {e.groupingType} | min={e.quantityMinimum} | tier={e.tier}");
            }
            Debug.Log($"  bubbleWorldScale: {bubbleWorldScale}");
            Debug.Log($"  Pool size:    {pool.Count}");
            if (pool.Count > 0 && pool[0].Panel != null)
                Debug.Log($"  UIPanel elements on first bubble: {pool[0].Panel.ElementCount}");
            Debug.Log($"  heartRegistry:  {heartRegistry.Count}");
            Debug.Log($"  envRegistry:    {envRegistry.Count}");
            Debug.Log($"  activeBubbles:  {activeBubbles.Count}");
            foreach (var kvp in activeBubbles)
                Debug.Log($"    grid {kvp.Key} → {kvp.Value.CurrentType} active={kvp.Value.IsActive}");
            Debug.Log($"  FogManager: {(FogManager.Instance != null ? "OK" : "NULL")}");
            Debug.Log($"  GridManager: {(GridManager.Instance != null ? "OK" : "NULL")}");
            Debug.Log("═══════════════════════════════════════════════");
        }

        // ── Sync ────────────────────────────────────────────────────────

        /// <summary>
        /// Copy entries from the assigned POIDatabase into the inline poiEntries list.
        /// Called from Inspector context menu or editor button.
        /// </summary>
        [ContextMenu("Sync from Database")]
        public void SyncFromDatabase()
        {
            if (poiDatabase == null)
            {
                Debug.LogWarning("[POIManager] No POIDatabase assigned — can't sync.");
                return;
            }

            poiEntries.Clear();
            foreach (var src in poiDatabase.Entries)
            {
                poiEntries.Add(new POITypeData
                {
                    active           = src.active,
                    typeName         = src.typeName,
                    label            = src.label,
                    sourceType       = src.sourceType,
                    groupingType     = src.groupingType,
                    quantityMinimum  = src.quantityMinimum,
                    tier             = src.tier,
                    rewardType       = src.rewardType,
                    rewardQuantity   = src.rewardQuantity
                });
            }

            Debug.Log($"[POIManager] Synced {poiEntries.Count} entries from POIDatabase.");
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
            Debug.Log($"[POIManager] Awake. Pool size: {pool.Count}, poiEntries: {poiEntries?.Count ?? 0}, bubblePrefab: {(bubblePrefab != null ? bubblePrefab.name : "NULL")}");
        }

        private void OnDestroy()
        {
            if (FogManager.Instance != null)
                FogManager.Instance.OnCellRevealed -= OnCellRevealed;
        }

        // ── Public API ──────────────────────────────────────────────────

        /// <summary>UnitDatabase assetName for corruption hearts — used for POI lookup.</summary>
        private const string HEART_ASSET_NAME = "CorruptedHeart";

        /// <summary>Called by CorruptionHeart after it registers with CorruptionManager.</summary>
        public void RegisterHeart(CorruptionHeart heart)
        {
            if (heart == null) return;
            var pos = heart.GridPosition;
            if (heartRegistry.ContainsKey(pos)) return;
            heartRegistry[pos] = heart;

            // Hearts always get a bubble immediately — look up POI data for label/tier
            var data = GetPOIData(HEART_ASSET_NAME);
            var bubbleType = data != null ? data.GetBubbleType() : BubbleType.POI_Red;
            QueueBubble(pos, HEART_ASSET_NAME, bubbleType);
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

        /// <summary>Delay between celebration burst and loot fly (seconds).</summary>
        private const float LOOT_FLY_DELAY = 0.25f;

        /// <summary>Call when the player places something to update bubble priority sorting.</summary>
        public void SetLastActivityPosition(Vector2Int gridPos)
        {
            lastActivityPos = gridPos;
        }

        private void OnCellRevealed(int x, int y)
        {
            var coord = new Vector2Int(x, y);
            lastActivityPos = coord; // Fog reveal = player activity

            // Heart discovered
            if (heartRegistry.TryGetValue(coord, out var heart) && heart != null)
            {
                Vector3 bubblePos = GetBubbleOrGridPos(coord);
                BubbleType bType = activeBubbles.TryGetValue(coord, out var hBub) && hBub != null
                    ? hBub.CurrentType : BubbleType.POI_Red;

                // 1. Celebration burst + SFX (immediate)
                POIDiscoveryFX.Play(bubblePos, bType);

                // 2. Loot fly (delayed so the burst lands first)
                StartCoroutine(DelayedAwardReward(HEART_ASSET_NAME, bubblePos));

                // 3. Dismiss bubble (starts fade-out alongside burst)
                DismissBubble(coord);
                heartRegistry.Remove(coord);
            }

            // Env POI discovered
            if (envRegistry.TryGetValue(coord, out var entry))
            {
                Vector3 bubblePos = GetBubbleOrGridPos(coord);
                BubbleType bType = activeBubbles.TryGetValue(coord, out var eBub) && eBub != null
                    ? eBub.CurrentType : BubbleType.POI_Grey;

                // 1. Celebration burst + SFX (immediate)
                POIDiscoveryFX.Play(bubblePos, bType);

                // 2. Loot fly (delayed so the burst lands first)
                StartCoroutine(DelayedAwardReward(entry.assetName, bubblePos));

                // 3. Dismiss bubble (starts fade-out alongside burst)
                DismissBubble(coord);
                envRegistry.Remove(coord);
            }

            // Border expanded — refresh which env POIs qualify
            RefreshEnvWindow();
        }

        private IEnumerator DelayedAwardReward(string assetName, Vector3 worldPos)
        {
            yield return new WaitForSeconds(LOOT_FLY_DELAY);
            AwardReward(assetName, worldPos);
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

            // Sort by distance to last player activity (closest to action first)
            candidates.Sort((a, b) =>
            {
                int distA = Mathf.Abs(a.pos.x - lastActivityPos.x) + Mathf.Abs(a.pos.y - lastActivityPos.y);
                int distB = Mathf.Abs(b.pos.x - lastActivityPos.x) + Mathf.Abs(b.pos.y - lastActivityPos.y);
                return distA.CompareTo(distB);
            });

            // Fill open slots
            int filled = 0;
            for (int i = 0; i < candidates.Count && filled < openSlots; i++)
            {
                var data = GetPOIData(candidates[i].entry.assetName);
                var bubbleType = data != null ? data.GetBubbleType() : BubbleType.POI_Grey;
                QueueBubble(candidates[i].pos, candidates[i].entry.assetName, bubbleType);
                filled++;
            }

            if (candidates.Count > 0 || envRegistry.Count > 0)
                Debug.Log($"[POIManager] RefreshEnvWindow: {envRegistry.Count} registered, {candidates.Count} candidates near border, {filled} new bubbles shown, {activeEnvCount + filled}/{maxEnvBubbles} slots used.");
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

        /// <summary>Look up the icon sprite for a POI by asset name from the source database.</summary>
        private Sprite LookupIcon(string assetName, POITypeData data)
        {
            if (data == null) return null;
            switch (data.sourceType)
            {
                case POISourceType.Environment:
                    if (environmentDatabase != null)
                    {
                        var env = environmentDatabase.GetByName(assetName);
                        if (env != null) return env.icon;
                    }
                    break;
                case POISourceType.Unit:
                    if (unitDatabase != null)
                    {
                        var unit = unitDatabase.GetByName(assetName);
                        if (unit != null) return unit.icon;
                    }
                    break;
                case POISourceType.Building:
                    // Buildings use BuildingDatabase — add if needed
                    break;
            }
            return null;
        }

        /// <summary>Queue a bubble to appear — staggered so only one pops in at a time.</summary>
        private void QueueBubble(Vector2Int gridPos, string assetName, BubbleType bubbleType)
        {
            bubbleQueue.Enqueue(() => ShowBubbleImmediate(gridPos, assetName, bubbleType));

            if (staggerCoroutine == null)
                staggerCoroutine = StartCoroutine(StaggerCoroutine());
        }

        private IEnumerator StaggerCoroutine()
        {
            while (bubbleQueue.Count > 0)
            {
                var action = bubbleQueue.Dequeue();
                action();
                yield return new WaitForSeconds(STAGGER_DELAY);
            }
            staggerCoroutine = null;
        }

        private void ShowBubbleImmediate(Vector2Int gridPos, string assetName, BubbleType bubbleType)
        {
            if (activeBubbles.ContainsKey(gridPos)) return;

            var bubble = GetFromPool();
            if (bubble == null)
            {
                Debug.LogWarning($"[POIManager] ShowBubble failed — pool exhausted and no prefab. gridPos={gridPos}, asset={assetName}");
                return;
            }

            var data = GetPOIData(assetName);
            string text = data != null ? data.label : assetName;
            Sprite icon = LookupIcon(assetName, data);

            Vector3 worldPos = GridManager.Instance != null
                ? GridManager.Instance.GridToWorldPosition(gridPos.x, gridPos.y)
                : new Vector3(gridPos.x, 0f, gridPos.y);
            worldPos.y += heightAboveGround;

            bubble.Setup(bubbleType, text, worldPos, icon);
            activeBubbles[gridPos] = bubble;
            Debug.Log($"[POIManager] Bubble shown: '{text}' ({bubbleType}) at grid {gridPos} → world {worldPos}");
        }

        private void DismissBubble(Vector2Int gridPos)
        {
            if (!activeBubbles.TryGetValue(gridPos, out var bubble)) return;
            bubble.Dismiss();
            activeBubbles.Remove(gridPos);
        }

        /// <summary>Dismiss any POI bubble at this grid position. Called by BuildingProductionManager to prevent stacking.</summary>
        public void DismissBubbleAt(Vector2Int gridPos)
        {
            DismissBubble(gridPos);
        }

        /// <summary>
        /// Returns the bubble's world position if one is active for this coord,
        /// otherwise falls back to the grid-cell world position so loot fly
        /// still has a valid origin even when no bubble was shown.
        /// </summary>
        private Vector3 GetBubbleOrGridPos(Vector2Int coord)
        {
            if (activeBubbles.TryGetValue(coord, out var bubble) && bubble != null)
                return bubble.transform.position;

            // No active bubble — use grid world position instead
            if (GridManager.Instance != null)
                return GridManager.Instance.GridToWorldPosition(coord.x, coord.y);

            return new Vector3(coord.x, 0f, coord.y);
        }

        private void AwardReward(string assetName, Vector3 worldPos)
        {
            if (poiEntries == null || poiEntries.Count == 0) return;
            var data = GetPOIData(assetName);
            if (data == null || data.rewardQuantity <= 0) return;

            // Visual: loot fly from bubble/grid position to resource bar.
            // SpawnLoot adds the resource per-particle on arrival, so no
            // direct AddResource call — avoids double-counting.
            var lootFX = ClockworkCraft.ResourceLootFX.Instance;
            if (lootFX != null)
            {
                lootFX.SpawnLoot(worldPos, data.rewardType, data.rewardQuantity);
            }
            else
            {
                // Fallback: no loot FX available, add silently
                if (ResourceManager.Instance != null)
                    ResourceManager.Instance.AddResource(data.rewardType, data.rewardQuantity);
            }

            Debug.Log($"[POIManager] Awarded {data.rewardQuantity}x {data.rewardType} for discovering '{assetName}'");
        }

        // ── Pool ────────────────────────────────────────────────────────

        private void CreatePool()
        {
            if (bubblePrefab == null)
            {
                Debug.LogError("[POIManager] bubblePrefab is NULL — no bubbles will appear. Assign WorldCanvas_Popups in the Inspector.");
                return;
            }

            int total = maxEnvBubbles + heartPoolSize;
            Transform parent = transform;
            Vector3 scale = Vector3.one * bubbleWorldScale;

            for (int i = 0; i < total; i++)
            {
                var bubble = CreateBubbleInstance(parent, scale);
                if (bubble != null) pool.Add(bubble);
            }

            // Validate first bubble has UIPanel elements
            if (pool.Count > 0 && pool[0].Panel != null && pool[0].Panel.ElementCount == 0)
                Debug.LogWarning("[POIManager] Bubble prefab UIPanel has 0 elements — run Tools > ClockworkCraft > Setup UI Panels on the prefab.");
        }

        private POIBubble CreateBubbleInstance(Transform parent, Vector3 scale)
        {
            var obj = Instantiate(bubblePrefab, parent);
            var bubble = obj.GetComponent<POIBubble>();
            if (bubble == null) bubble = obj.AddComponent<POIBubble>();
            bubble.SetAnimParams(popInDuration, bobHeight, bobDuration, fadeOutDuration);
            bubble.SetTargetScale(scale);
            obj.SetActive(false);
            return bubble;
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
            var overflow = CreateBubbleInstance(transform, Vector3.one * bubbleWorldScale);
            pool.Add(overflow);
            return overflow;
        }
    }
}

#if UNITY_EDITOR
namespace ClockworkCraft
{
    [CustomEditor(typeof(POIManager))]
    public class POIManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            POIManager mgr = (POIManager)target;

            // Sync button at top — same as MapGeneratorV2
            EditorGUILayout.Space(4);
            if (GUILayout.Button("Sync from Database"))
            {
                Undo.RecordObject(mgr, "Sync POI from Database");
                mgr.SyncFromDatabase();
                EditorUtility.SetDirty(mgr);
            }

            // Runtime diagnostic button (play mode only)
            if (Application.isPlaying)
            {
                if (GUILayout.Button("Diagnose POI System"))
                    mgr.DiagnosePOISystem();
            }

            EditorGUILayout.Space(4);
            DrawDefaultInspector();
        }
    }
}
#endif
