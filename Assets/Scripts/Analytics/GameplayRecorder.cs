#pragma warning disable CS0414, CS0219, CS0618
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using ClockworkCraft;
using ClockworkGrid;
using LittleCafe;

namespace LittleCafe
{
    /// <summary>
    /// Records gameplay economics data to CSV for balancing analysis.
    ///
    /// When enabled (via title screen toggle or Inspector), captures:
    ///   - Resource events: every gain and spend with timestamp, tick, type, delta, and new total
    ///   - Periodic snapshots: all resource totals every N ticks
    ///
    /// CSV is written to Application.persistentDataPath on session end.
    /// Feed the file to Claude for data-driven balancing suggestions.
    ///
    /// Singleton — auto-created by MapGeneratorV2.EnsureManagers or placed manually.
    /// </summary>
    public class GameplayRecorder : MonoBehaviour
    {
        public static GameplayRecorder Instance { get; private set; }

        [Header("Settings")]
        [Tooltip("Snapshot all resource totals every N interval ticks.")]
        [SerializeField] private int snapshotEveryNTicks = 4;

        // ── State ────────────────────────────────────────────────────

        /// <summary>
        /// Whether recording is currently active.
        /// Set to true via the title screen toggle before game start.
        /// </summary>
        public bool IsRecording { get; private set; }

        /// <summary>
        /// Static flag set by the title screen toggle.
        /// GameplayRecorder reads this on game start to decide whether to record.
        /// Persists across the title→game transition since it's static.
        /// </summary>
        public static bool RecordNextSession { get; set; }

        private readonly List<string> eventLines = new List<string>();
        private readonly List<string> snapshotLines = new List<string>();
        private readonly Dictionary<ResourceType, int> previousTotals = new Dictionary<ResourceType, int>();
        private float sessionStartTime;
        private int ticksSinceLastSnapshot;
        private string sessionFileName;

        // Scarcity tracking: how many times each resource hit zero, and total ticks spent at zero
        private readonly Dictionary<ResourceType, int> zeroHitCount = new Dictionary<ResourceType, int>();
        private readonly Dictionary<ResourceType, int> ticksAtZero = new Dictionary<ResourceType, int>();
        private readonly HashSet<ResourceType> currentlyAtZero = new HashSet<ResourceType>();

        // Tracked resource types (the ones that matter for economy)
        private static readonly ResourceType[] TrackedTypes = {
            ResourceType.Gold, ResourceType.Wood, ResourceType.Stone,
            ResourceType.Food, ResourceType.Water, ResourceType.Clay,
            ResourceType.Gem, ResourceType.Copper, ResourceType.Ore,
            ResourceType.Flowers, ResourceType.Leaf, ResourceType.Grass,
            ResourceType.Bark, ResourceType.Twig, ResourceType.Acorn,
            ResourceType.Petal, ResourceType.Fish, ResourceType.Meat,
        };

        // ── Lifecycle ────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Check static flag from title screen
            if (RecordNextSession)
            {
                StartRecording();
                RecordNextSession = false;
            }
        }

        private void OnDestroy()
        {
            if (IsRecording)
                StopRecording();
        }

        private void OnApplicationQuit()
        {
            if (IsRecording)
                StopRecording();
        }

        // ── Public API ───────────────────────────────────────────────

        /// <summary>
        /// Begin recording. Can be called at any time.
        /// </summary>
        public void StartRecording()
        {
            if (IsRecording) return;
            IsRecording = true;
            sessionStartTime = Time.time;
            ticksSinceLastSnapshot = 0;

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            sessionFileName = $"session_{timestamp}.csv";

            eventLines.Clear();
            snapshotLines.Clear();
            previousTotals.Clear();
            zeroHitCount.Clear();
            ticksAtZero.Clear();
            currentlyAtZero.Clear();

            // Initialize previous totals and scarcity state
            if (ResourceManager.Instance != null)
            {
                foreach (var type in TrackedTypes)
                {
                    int amount = ResourceManager.Instance.GetResource(type);
                    previousTotals[type] = amount;
                    zeroHitCount[type] = 0;
                    ticksAtZero[type] = 0;
                    if (amount == 0) currentlyAtZero.Add(type);
                }
            }

            // Subscribe to events
            if (ResourceManager.Instance != null)
                ResourceManager.Instance.OnResourceChanged += OnResourceChanged;

            if (IntervalTimer.Instance != null)
                IntervalTimer.Instance.OnIntervalTick += OnTick;

            Debug.Log($"[GameplayRecorder] Recording started — will save to {sessionFileName}");
        }

        /// <summary>
        /// Stop recording and write the CSV file.
        /// </summary>
        public void StopRecording()
        {
            if (!IsRecording) return;
            IsRecording = false;

            // Unsubscribe
            if (ResourceManager.Instance != null)
                ResourceManager.Instance.OnResourceChanged -= OnResourceChanged;

            if (IntervalTimer.Instance != null)
                IntervalTimer.Instance.OnIntervalTick -= OnTick;

            // Take a final snapshot
            TakeSnapshot(IntervalTimer.Instance != null ? IntervalTimer.Instance.CurrentInterval : -1);

            // Write to disk
            WriteCsv();
        }

        // ── Event Handlers ───────────────────────────────────────────

        private void OnResourceChanged(ResourceType type, int newTotal)
        {
            if (!IsRecording) return;

            // Determine delta (gain or spend)
            int oldTotal = previousTotals.ContainsKey(type) ? previousTotals[type] : 0;
            int delta = newTotal - oldTotal;
            previousTotals[type] = newTotal;

            if (delta == 0) return;

            float elapsed = Time.time - sessionStartTime;
            int tick = IntervalTimer.Instance != null ? IntervalTimer.Instance.CurrentInterval : -1;
            string eventType = delta > 0 ? "GAIN" : "SPEND";

            // elapsed, tick, eventType, resourceType, delta, newTotal
            eventLines.Add($"{elapsed:F2},{tick},{eventType},{type},{delta},{newTotal}");

            // Track scarcity: resource just hit zero
            if (newTotal == 0 && !currentlyAtZero.Contains(type))
            {
                currentlyAtZero.Add(type);
                if (!zeroHitCount.ContainsKey(type)) zeroHitCount[type] = 0;
                zeroHitCount[type]++;
            }
            // Resource recovered from zero
            else if (newTotal > 0 && currentlyAtZero.Contains(type))
            {
                currentlyAtZero.Remove(type);
            }
        }

        private void OnTick(int tickNumber)
        {
            if (!IsRecording) return;

            // Accumulate ticks spent at zero for each resource
            foreach (var type in currentlyAtZero)
            {
                if (!ticksAtZero.ContainsKey(type)) ticksAtZero[type] = 0;
                ticksAtZero[type]++;
            }

            ticksSinceLastSnapshot++;
            if (ticksSinceLastSnapshot >= snapshotEveryNTicks)
            {
                ticksSinceLastSnapshot = 0;
                TakeSnapshot(tickNumber);
            }
        }

        private void TakeSnapshot(int tickNumber)
        {
            float elapsed = Time.time - sessionStartTime;
            StringBuilder sb = new StringBuilder();
            sb.Append($"{elapsed:F2},{tickNumber}");

            foreach (var type in TrackedTypes)
            {
                int amount = ResourceManager.Instance != null
                    ? ResourceManager.Instance.GetResource(type) : 0;
                sb.Append($",{amount}");
            }

            snapshotLines.Add(sb.ToString());
        }

        // ── CSV Output ───────────────────────────────────────────────

        private void WriteCsv()
        {
            string dir = Application.persistentDataPath;
            string path = Path.Combine(dir, sessionFileName);

            StringBuilder csv = new StringBuilder();

            // Header comment
            csv.AppendLine($"# ClockworkCraft Gameplay Recording");
            csv.AppendLine($"# Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            csv.AppendLine($"# Duration: {Time.time - sessionStartTime:F1}s");
            csv.AppendLine($"# Snapshot interval: every {snapshotEveryNTicks} ticks");
            csv.AppendLine();

            // Section 1: Events
            csv.AppendLine("# === RESOURCE EVENTS ===");
            csv.AppendLine("elapsed_s,tick,event_type,resource_type,delta,total_after");
            foreach (var line in eventLines)
                csv.AppendLine(line);

            csv.AppendLine();

            // Section 2: Snapshots
            csv.AppendLine("# === RESOURCE SNAPSHOTS ===");
            StringBuilder snapHeader = new StringBuilder("elapsed_s,tick");
            foreach (var type in TrackedTypes)
                snapHeader.Append($",{type}");
            csv.AppendLine(snapHeader.ToString());

            foreach (var line in snapshotLines)
                csv.AppendLine(line);

            csv.AppendLine();

            // Section 3: Resource Scarcity Report
            csv.AppendLine("# === RESOURCE SCARCITY ===");
            csv.AppendLine("# Times each resource hit zero and how many ticks it stayed there");
            csv.AppendLine("resource_type,times_hit_zero,ticks_at_zero,still_at_zero");
            foreach (var type in TrackedTypes)
            {
                int hits = zeroHitCount.ContainsKey(type) ? zeroHitCount[type] : 0;
                int ticks = ticksAtZero.ContainsKey(type) ? ticksAtZero[type] : 0;
                bool stillZero = currentlyAtZero.Contains(type);
                if (hits > 0 || ticks > 0 || stillZero)
                    csv.AppendLine($"{type},{hits},{ticks},{(stillZero ? "YES" : "NO")}");
            }

            csv.AppendLine();

            // Section 4: Cost Curve Snapshot (current costs per building at each escalation level)
            WriteCostCurveSection(csv);

            // Write
            try
            {
                File.WriteAllText(path, csv.ToString());
                Debug.Log($"[GameplayRecorder] CSV saved: {path}");
                Debug.Log($"[GameplayRecorder] {eventLines.Count} events, {snapshotLines.Count} snapshots recorded");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameplayRecorder] Failed to write CSV: {e.Message}");
            }
        }

        /// <summary>
        /// Write a cost curve section showing how each building's cost escalates
        /// at placement #0, #1, #2, ... up to current count + 3.
        /// </summary>
        private void WriteCostCurveSection(StringBuilder csv)
        {
            csv.AppendLine("# === COST CURVES ===");
            csv.AppendLine("# Shows escalating cost per building type at each placement number");
            csv.AppendLine("# Format: item_name,placements_so_far,resource:cost,resource:cost,...");

            if (EconomyManager.Instance == null || EconomyManager.Instance.balanceConfig == null)
            {
                csv.AppendLine("# (No EconomyManager or balance config available)");
                return;
            }

            var config = EconomyManager.Instance.balanceConfig;

            foreach (var entry in config.entries)
            {
                if (!entry.HasAnyCost()) continue;

                int currentCount = EconomyManager.Instance.GetPlacementCount(entry.itemName);
                int showUpTo = currentCount + 3; // Show a few placements ahead

                for (int n = 0; n <= showUpTo; n++)
                {
                    var costs = entry.GetEffectivePlacementCosts(n);
                    if (costs.Count == 0) continue;

                    StringBuilder line = new StringBuilder();
                    line.Append($"{entry.itemName},{n}");
                    string marker = n == currentCount ? ",<-- CURRENT" : "";

                    foreach (var cost in costs)
                        line.Append($",{cost.resourceType}:{cost.amount}");

                    line.Append(marker);
                    csv.AppendLine(line.ToString());
                }

                // Blank line between buildings
                csv.AppendLine();
            }
        }
    }
}
