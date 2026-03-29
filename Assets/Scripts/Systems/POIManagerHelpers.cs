using System.Collections.Generic;
using UnityEngine;
using LittleCafe;
using ClockworkGrid;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ClockworkCraft
{
    /// <summary>
    /// Partial class — pool management, rolling window, icon lookup, and diagnostics.
    /// </summary>
    public partial class POIManager
    {
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

        // ── Icon Lookup ─────────────────────────────────────────────────

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
