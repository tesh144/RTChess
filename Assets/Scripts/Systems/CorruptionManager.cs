#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System.Collections.Generic;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// Singleton manager for the corruption system.
    /// Tracks all corruption hearts and their owned tile clusters.
    /// Handles spread ticking, heart dormancy activation, fog-of-war reveal, and tile corruption/clearing.
    ///
    /// Auto-created by MapGeneratorV2.EnsureManagers(). No manual scene placement required.
    /// </summary>
    public class CorruptionManager : MonoBehaviour
    {
        public static CorruptionManager Instance { get; private set; }

        [Header("Spread Settings")]
        [Tooltip("Seconds between each spread tick. Each tick spreads every active heart's corruption one tile outward.")]
        [SerializeField] private float spreadInterval = 30f;

        [Tooltip("Chebyshev radius (in tiles) within which a revealed tile activates a dormant heart.")]
        [SerializeField] private int heartActivationRadius = 5;

        [Header("Visuals")]
        [Tooltip("When true, uses the code-driven particle fog. When false, uses the prefab below.")]
        [SerializeField] private bool useParticleFog = true;

        [Tooltip("Prefab instantiated on each corrupted tile. Only used when useParticleFog is false.")]
        [SerializeField] private GameObject corruptionOverlayPrefab;

        /// <summary>The overlay visual prefab. Returns null when particle fog is enabled (default).</summary>
        public GameObject CorruptionOverlayPrefab => useParticleFog ? null : corruptionOverlayPrefab;

        // ── Internal State ────────────────────────────────────────────────

        private readonly List<CorruptionHeart> allHearts = new List<CorruptionHeart>();

        // Per-heart tile ownership — each heart tracks its own cluster
        private readonly Dictionary<CorruptionHeart, HashSet<Vector2Int>> heartTiles
            = new Dictionary<CorruptionHeart, HashSet<Vector2Int>>();

        // Flat set for O(1) IsCorrupted() checks — always kept in sync with heartTiles
        private readonly HashSet<Vector2Int> allCorruptedTiles = new HashSet<Vector2Int>();

        private float spreadTimer;

        // ── Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
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

        private void OnDestroy()
        {
            if (FogManager.Instance != null)
                FogManager.Instance.OnCellRevealed -= OnCellRevealed;
        }

        // ── Registration ──────────────────────────────────────────────────

        /// <summary>Called by CorruptionHeart.Start() to register with the manager.</summary>
        public void RegisterHeart(CorruptionHeart heart)
        {
            if (heart == null || allHearts.Contains(heart)) return;
            allHearts.Add(heart);
            heartTiles[heart] = new HashSet<Vector2Int>();

            // Auto-activate if the surrounding area is already revealed.
            // Handles fog-disabled testing mode and hearts inside the starting reveal area —
            // in both cases all OnCellRevealed events fire before hearts register,
            // so the normal fog-gated activation never triggers.
            CheckActivateOnRegister(heart);

            Debug.Log($"[CorruptionManager] Registered heart at {heart.GridPosition}. Total hearts: {allHearts.Count}");
        }

        /// <summary>
        /// Checks whether any tile within heartActivationRadius of the heart is already revealed.
        /// If so, activates the heart immediately and seeds its first corrupted tile.
        /// This mirrors the logic in OnCellRevealed but runs at registration time.
        /// </summary>
        private void CheckActivateOnRegister(CorruptionHeart heart)
        {
            if (heart.IsActive) return;
            if (FogManager.Instance == null) return;

            var pos = heart.GridPosition;
            for (int dx = -heartActivationRadius; dx <= heartActivationRadius; dx++)
            {
                for (int dy = -heartActivationRadius; dy <= heartActivationRadius; dy++)
                {
                    if (FogManager.Instance.IsCellRevealed(pos.x + dx, pos.y + dy))
                    {
                        heart.Activate();
                        CorruptTile(pos.x, pos.y, heart);
                        Debug.Log($"[CorruptionManager] Auto-activated heart at {pos} — surrounding area already revealed.");
                        return;
                    }
                }
            }
        }

        // ── Corruption ────────────────────────────────────────────────────

        /// <summary>Mark a tile as corrupted and owned by the given heart.</summary>
        public void CorruptTile(int x, int y, CorruptionHeart owner)
        {
            var coord = new Vector2Int(x, y);
            if (allCorruptedTiles.Contains(coord)) return;
            if (!heartTiles.ContainsKey(owner)) return;

            var tile = GridManager.Instance != null ? GridManager.Instance.GetGridTile(x, y) : null;
            if (tile == null) return;

            // Add overlay to the tile GameObject
            var overlay = tile.AddComponent<CorruptionOverlay>();
            overlay.OwnerHeart = owner;
            overlay.GridPosition = coord;

            // Pass occupant for building pausing
            var occupant = GridManager.Instance.GetCellOccupant(x, y);
            overlay.InitWithOccupant(occupant);

            // Update both data structures together
            heartTiles[owner].Add(coord);
            allCorruptedTiles.Add(coord);

            Debug.Log($"[CorruptionManager] Corrupted tile ({x},{y}) owned by heart at {owner.GridPosition}.");
        }

        /// <summary>
        /// Clear corruption from a single tile. Cleans up overlay and resumes any paused building.
        /// Called by CorruptionOverlay.OnOverlayDestroyed or by ClearHeartCluster.
        /// </summary>
        public void ClearTile(int x, int y, CorruptionHeart owner)
        {
            var coord = new Vector2Int(x, y);

            var tile = GridManager.Instance != null ? GridManager.Instance.GetGridTile(x, y) : null;
            if (tile != null)
            {
                var overlay = tile.GetComponent<CorruptionOverlay>();
                if (overlay != null)
                {
                    overlay.Cleanup();
                    Destroy(overlay);
                }
            }

            // Update both data structures together
            if (heartTiles.ContainsKey(owner))
                heartTiles[owner].Remove(coord);
            allCorruptedTiles.Remove(coord);
        }

        /// <summary>
        /// Destroy a heart's entire cluster — called when the heart dies.
        /// Clears every tile owned by this heart only; other hearts are unaffected.
        /// </summary>
        public void ClearHeartCluster(CorruptionHeart heart)
        {
            if (!heartTiles.ContainsKey(heart)) return;

            Debug.Log($"[CorruptionManager] Clearing cluster for heart at {heart.GridPosition} ({heartTiles[heart].Count} tiles).");

            // Snapshot to avoid modifying the set during iteration
            var tiles = new List<Vector2Int>(heartTiles[heart]);
            foreach (var coord in tiles)
                ClearTile(coord.x, coord.y, heart);

            heartTiles.Remove(heart);
            allHearts.Remove(heart);
        }

        /// <summary>Returns true if the tile at (x, y) is currently corrupted by any heart.</summary>
        public bool IsCorrupted(int x, int y) => allCorruptedTiles.Contains(new Vector2Int(x, y));

        /// <summary>
        /// Returns the set of tiles currently owned by the given heart's cluster.
        /// Used by CorruptionHeart to find valid cells for spike growth.
        /// Returns null if the heart is not registered.
        /// </summary>
        public IReadOnlyCollection<Vector2Int> GetHeartTiles(CorruptionHeart heart)
        {
            if (heartTiles.TryGetValue(heart, out var tiles))
                return tiles;
            return null;
        }

        // ── Spread ────────────────────────────────────────────────────────

        private void SpreadAll()
        {
            if (allHearts.Count == 0) return;

            foreach (var heart in allHearts)
            {
                if (!heart.IsActive) continue;
                if (!heartTiles.ContainsKey(heart)) continue;

                // Snapshot — new tiles will be added mid-loop otherwise
                var snapshot = new List<Vector2Int>(heartTiles[heart]);
                foreach (var coord in snapshot)
                {
                    TrySpreadTo(coord.x + 1, coord.y, heart);
                    TrySpreadTo(coord.x - 1, coord.y, heart);
                    TrySpreadTo(coord.x,     coord.y + 1, heart);
                    TrySpreadTo(coord.x,     coord.y - 1, heart);
                }
            }
        }

        private void TrySpreadTo(int x, int y, CorruptionHeart owner)
        {
            if (GridManager.Instance == null) return;
            if (!GridManager.Instance.IsValidCell(x, y)) return;
            if (IsCorrupted(x, y)) return;
            CorruptTile(x, y, owner);
        }

        // ── Fog / Activation ─────────────────────────────────────────────

        private void OnCellRevealed(int x, int y)
        {
            // 1. Activate any dormant heart within the activation radius
            foreach (var heart in allHearts)
            {
                if (heart.IsActive) continue;
                int dx = Mathf.Abs(x - heart.GridPosition.x);
                int dy = Mathf.Abs(y - heart.GridPosition.y);
                if (dx <= heartActivationRadius && dy <= heartActivationRadius)
                {
                    heart.Activate();
                    // Seed the heart's first corrupted tile — its own position
                    CorruptTile(heart.GridPosition.x, heart.GridPosition.y, heart);
                }
            }

            // 2. If this revealed tile is corrupted, reveal the whole cluster instantly
            var coord = new Vector2Int(x, y);
            if (!allCorruptedTiles.Contains(coord)) return;
            if (FogManager.Instance == null) return;

            var tile = GridManager.Instance != null ? GridManager.Instance.GetGridTile(x, y) : null;
            if (tile == null) return;

            var overlay = tile.GetComponent<CorruptionOverlay>();
            if (overlay == null || overlay.OwnerHeart == null) return;

            var owner = overlay.OwnerHeart;
            if (!heartTiles.ContainsKey(owner)) return;

            // Reveal all tiles in this heart's cluster
            foreach (var c in heartTiles[owner])
                FogManager.Instance.RevealCell(c.x, c.y);

            // Also reveal the heart's own tile
            FogManager.Instance.RevealCell(owner.GridPosition.x, owner.GridPosition.y);
        }
    }
}
