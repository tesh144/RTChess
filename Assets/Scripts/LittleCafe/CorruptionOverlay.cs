#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;

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
        private GameObject pausedOccupant;
        private Transform occupantTransform; // Cached for visual positioning
        private System.Action<GridEntityHealth> occupantDeathHandler;

        // ── Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            Health = gameObject.AddComponent<GridEntityHealth>();
            // workerCanInteract=true, isAllied=false so workers target it
            // isSlotTakeable=false — worker stays put after destroying overlay, re-targets next cycle
            Health.Initialize(maxHP, atkPower: 0, canInteract: true, allied: false, slotTakeable: false);
        }

        private void Start()
        {
            Health.OnEntityDestroyed += OnOverlayDestroyed;
            SpawnVisual();
        }

        private void OnDestroy()
        {
            if (Health != null)
                Health.OnEntityDestroyed -= OnOverlayDestroyed;
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Called by CorruptionManager after GridPosition/OwnerHeart are set.
        /// Pauses any building occupant and caches the reference for cleanup.
        /// </summary>
        public void InitWithOccupant(GameObject occupant)
        {
            if (occupant == null) return;
            occupantTransform = occupant.transform;

            if (BuildingProductionManager.Instance != null)
            {
                // Only pause if this building actually has a production entry
                // PauseBuilding is a no-op if the building isn't registered
                BuildingProductionManager.Instance.PauseBuilding(occupant);
                pausedOccupant = occupant;

                // Subscribe to occupant death so we clear the reference cleanly
                var occupantHealth = occupant.GetComponent<GridEntityHealth>();
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
            // Resume the building if it was paused by this overlay
            if (pausedOccupant != null)
            {
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

        private void OnOverlayDestroyed(GridEntityHealth _)
        {
            if (CorruptionManager.Instance != null)
                CorruptionManager.Instance.ClearTile(GridPosition.x, GridPosition.y, OwnerHeart);
        }

        private void SpawnVisual()
        {
            // Use prefab from CorruptionManager if assigned, otherwise fall back to placeholder
            GameObject prefab = CorruptionManager.Instance != null
                ? CorruptionManager.Instance.CorruptionOverlayPrefab : null;

            if (prefab != null)
            {
                // If there's an occupant (tree, building, etc.), parent to it and
                // position at RefHeight so the fire sits on top of the object.
                // If no occupant (empty ground), parent to the tile at ground level.
                if (occupantTransform != null)
                {
                    Transform refHeight = FindRefHeight(occupantTransform);
                    if (refHeight != null)
                    {
                        visualChild = Instantiate(prefab, refHeight);
                        visualChild.transform.localPosition = Vector3.zero;
                    }
                    else
                    {
                        // No RefHeight — place at top of occupant using renderer bounds
                        visualChild = Instantiate(prefab, occupantTransform);
                        float height = EstimateHeight(occupantTransform);
                        visualChild.transform.localPosition = new Vector3(0f, height, 0f);
                    }
                }
                else
                {
                    // Empty ground — parent to tile, sit on surface
                    visualChild = Instantiate(prefab, transform);
                    visualChild.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                }

                visualChild.name = "CorruptionVisual";
                visualChild.SetActive(true);
            }
            else
            {
                // Placeholder: purple quad sitting above the tile surface
                visualChild = GameObject.CreatePrimitive(PrimitiveType.Quad);
                visualChild.name = "CorruptionVisual_Placeholder";
                visualChild.transform.SetParent(transform);
                visualChild.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                visualChild.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                visualChild.transform.localScale = new Vector3(0.9f, 0.9f, 1f);

                var r = visualChild.GetComponent<MeshRenderer>();
                if (r != null)
                {
                    r.material = new Material(Shader.Find("Sprites/Default"));
                    r.material.color = new Color(0.45f, 0f, 0.7f, 0.75f);
                    r.sortingOrder = 10;
                }

                var col = visualChild.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }
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
