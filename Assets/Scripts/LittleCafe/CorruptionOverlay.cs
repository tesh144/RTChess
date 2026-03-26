#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// Added at runtime to a tile's GameObject when corruption spreads to that tile.
    /// Holds corruption HP and owns the visual overlay. Workers must destroy this before
    /// they can interact with whatever occupant is underneath.
    ///
    /// OwnerHeart and GridPosition must be set by CorruptionManager immediately after AddComponent.
    /// Call InitWithOccupant() to pause any building on this tile.
    /// Call Cleanup() before Destroy() to resume buildings and remove visuals.
    /// </summary>
    public class CorruptionOverlay : MonoBehaviour
    {
        [Header("Stats")]
        [SerializeField] private int maxHP = 3;

        /// <summary>The heart that owns this corrupted tile. Set by CorruptionManager.</summary>
        public CorruptionHeart OwnerHeart { get; set; }

        /// <summary>Grid position of this tile. Set by CorruptionManager.</summary>
        public Vector2Int GridPosition { get; set; }

        /// <summary>The GridEntityHealth component that workers attack.</summary>
        public GridEntityHealth Health { get; private set; }

        private GameObject visualChild;
        private bool _subscribedToFog;
        private GameObject pausedOccupant;
        private Transform occupantTransform; // Cached for visual positioning
        private System.Action<GridEntityHealth> occupantDeathHandler;

        // ── Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            Health = gameObject.AddComponent<GridEntityHealth>();
            // workerCanInteract=true, isAllied=false so workers target it
            // isSlotTakeable=true — worker advances into the tile after clearing corruption
            Health.Initialize(maxHP, atkPower: 0, canInteract: true, allied: false, slotTakeable: true);
        }

        private void Start()
        {
            Health.OnEntityDestroyed += OnOverlayDestroyed;
            Health.OnDamaged += OnOverlayDamaged;
            SpawnVisual();

            // Hide visual if tile is in fog — show when revealed
            if (visualChild != null && FogManager.Instance != null &&
                !FogManager.Instance.IsCellRevealed(GridPosition.x, GridPosition.y))
            {
                visualChild.SetActive(false);
                FogManager.Instance.OnCellRevealed += OnFogRevealed;
                _subscribedToFog = true;
            }
        }

        private void OnDestroy()
        {
            if (Health != null)
            {
                Health.OnEntityDestroyed -= OnOverlayDestroyed;
                Health.OnDamaged -= OnOverlayDamaged;
            }
            if (_subscribedToFog && FogManager.Instance != null)
                FogManager.Instance.OnCellRevealed -= OnFogRevealed;
        }

        private void OnFogRevealed(int x, int y)
        {
            if (x != GridPosition.x || y != GridPosition.y) return;
            if (visualChild != null) visualChild.SetActive(true);
            if (_subscribedToFog && FogManager.Instance != null)
            {
                FogManager.Instance.OnCellRevealed -= OnFogRevealed;
                _subscribedToFog = false;
            }
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Called by CorruptionManager after GridPosition/OwnerHeart are set.
        /// Handles occupants based on type:
        ///   - Buildings: pause production
        ///   - Workers (allied actors): pause behavior
        ///   - Neutral creatures (non-allied actors): kill immediately
        /// </summary>
        // Corruption tint color — applied to occupants sharing a tile with corruption
        private static readonly Color CorruptionTint = new Color(1f, 0.4f, 0.7f); // pink
        private const float CorruptionTintStrength = 0.5f;

        public void InitWithOccupant(GameObject occupant)
        {
            if (occupant == null) return;
            occupantTransform = occupant.transform;

            // Tint the occupant pink to show corruption influence
            var desat = occupant.GetComponent<ClockworkCraft.EnvironmentDesaturation>();
            if (desat != null)
                desat.SetTint(CorruptionTint, CorruptionTintStrength);

            var occupantHealth = occupant.GetComponent<GridEntityHealth>();
            var actor = occupant.GetComponent<GridEntityActor>();

            // Neutral creatures (non-allied with an actor) — corruption kills them instantly
            if (actor != null && occupantHealth != null && !occupantHealth.IsAllied)
            {
                occupantHealth.TakeDamage(occupantHealth.MaxHP);
                return;
            }

            // Workers (allied with an actor) — pause their behavior
            if (actor != null && occupantHealth != null && occupantHealth.IsAllied)
            {
                actor.PauseForCorruption();
                pausedOccupant = occupant;

                occupantDeathHandler = (_) =>
                {
                    pausedOccupant = null;
                    occupantDeathHandler = null;
                };
                occupantHealth.OnEntityDestroyed += occupantDeathHandler;
                return;
            }

            // Buildings — pause production
            if (BuildingProductionManager.Instance != null)
            {
                BuildingProductionManager.Instance.PauseBuilding(occupant);
                pausedOccupant = occupant;

                if (occupantHealth != null)
                {
                    occupantDeathHandler = (_) =>
                    {
                        pausedOccupant = null;
                        occupantDeathHandler = null;
                    };
                    occupantHealth.OnEntityDestroyed += occupantDeathHandler;
                }
            }
        }

        /// <summary>
        /// Called by CorruptionManager.ClearTile() BEFORE Destroy(overlay).
        /// Resumes paused building, cleans up visual, and unsubscribes occupant death handler.
        /// </summary>
        public void Cleanup()
        {
            // Resume the occupant if it was paused by this overlay
            if (pausedOccupant != null)
            {
                // Clear corruption tint
                var desat = pausedOccupant.GetComponent<ClockworkCraft.EnvironmentDesaturation>();
                if (desat != null)
                    desat.ClearTint();

                // Resume worker actor
                var actor = pausedOccupant.GetComponent<GridEntityActor>();
                if (actor != null)
                    actor.ResumeFromCorruption();

                // Resume building production
                if (BuildingProductionManager.Instance != null)
                    BuildingProductionManager.Instance.ResumeBuilding(pausedOccupant);

                // Unsubscribe occupant death handler
                var occupantHealth = pausedOccupant.GetComponent<GridEntityHealth>();
                if (occupantHealth != null && occupantDeathHandler != null)
                    occupantHealth.OnEntityDestroyed -= occupantDeathHandler;

                pausedOccupant = null;
                occupantDeathHandler = null;
            }

            // Destroy visual child
            if (visualChild != null)
            {
                Destroy(visualChild);
                visualChild = null;
            }
        }

        // ── Private ───────────────────────────────────────────────────────

        private void OnOverlayDamaged(int damageDealt, int currentHP, int maxHP)
        {
            // Pause corruption spreading when a worker hits a corruption tile
            if (CorruptionManager.Instance != null)
                CorruptionManager.Instance.PauseSpread();
        }

        private void OnOverlayDestroyed(GridEntityHealth _)
        {
            if (CorruptionManager.Instance != null)
                CorruptionManager.Instance.ClearTile(GridPosition.x, GridPosition.y, OwnerHeart);
        }

        private void SpawnVisual()
        {
            GameObject prefab = CorruptionManager.Instance != null
                ? CorruptionManager.Instance.CorruptionOverlayPrefab : null;

            if (prefab == null)
            {
                Debug.LogWarning($"[CorruptionOverlay] No corruption overlay prefab assigned on CorruptionManager. Tile ({GridPosition}) has no visual.");
                return;
            }

            float height = CorruptionManager.Instance != null
                ? CorruptionManager.Instance.CorruptionVisualHeight : 0.05f;
            visualChild = Instantiate(prefab, transform);
            visualChild.transform.localPosition = new Vector3(0f, height, 0f);
            float scale = CorruptionManager.Instance != null
                ? CorruptionManager.Instance.CorruptionVisualScale : 1f;
            visualChild.transform.localScale = Vector3.one * scale;
            visualChild.name = "CorruptionVisual";
            visualChild.SetActive(true);
        }

        /// <summary>Find a child named "RefHeight" in the hierarchy.</summary>
        private static Transform FindRefHeight(Transform root)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                if (root.GetChild(i).name == "RefHeight")
                    return root.GetChild(i);
            }
            // Recursive search
            foreach (Transform child in root)
            {
                var found = FindRefHeight(child);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>Estimate object height from renderer bounds when no RefHeight exists.</summary>
        private static float EstimateHeight(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return 0.5f;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return bounds.max.y - root.position.y;
        }
    }
}
