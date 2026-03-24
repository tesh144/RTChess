#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;

namespace LittleCafe
{
    /// <summary>
    /// Placed on the map by map generation code. Represents the source of a corruption cluster.
    /// Dormant until the player reveals a tile within heartActivationRadius. Owns a set of
    /// corrupted tiles tracked by CorruptionManager. When destroyed, its entire cluster is cleared.
    ///
    /// Stats are serialized directly on the prefab — no CorruptionData/CorruptionDatabase needed.
    /// GridPosition must be set by map gen before Start().
    /// </summary>
    public class CorruptionHeart : MonoBehaviour
    {
        // ── Stats — serialized on the prefab ─────────────────────────────
        [Header("Stats")]
        [SerializeField] private int maxHP = 10;
        [SerializeField] private int attackPower = 1;

        [Header("Visuals")]
        [Tooltip("Billboard sprite prefab that floats above the heart. A magenta placeholder quad is used if null.")]
        [SerializeField] private GameObject floatingIndicatorPrefab;

        public bool IsActive { get; private set; } = false;

        /// <summary>Grid coordinates of this heart. Set by map generation before Start().</summary>
        public Vector2Int GridPosition { get; set; }

        public GridEntityHealth Health { get; private set; }

        private GameObject floatingIndicatorInstance;

        private void Awake()
        {
            Health = gameObject.AddComponent<GridEntityHealth>();
        }

        private void Start()
        {
            // workerCanInteract=true so players can attack hearts
            // isAllied=false so workers will target it
            // isSlotTakeable=false so workers don't advance into the heart's cell on kill
            Health.Initialize(maxHP, atkPower: attackPower, canInteract: true, allied: false, slotTakeable: false);
            Health.OnEntityDestroyed += OnHeartDestroyed;

            if (CorruptionManager.Instance != null)
                CorruptionManager.Instance.RegisterHeart(this);

            SpawnFloatingIndicator();
        }

        private void OnDestroy()
        {
            if (Health != null)
                Health.OnEntityDestroyed -= OnHeartDestroyed;

            if (floatingIndicatorInstance != null)
                Destroy(floatingIndicatorInstance);
        }

        // ── Public ────────────────────────────────────────────────────────

        /// <summary>Called by CorruptionManager when the player explores within activation radius.</summary>
        public void Activate()
        {
            if (IsActive) return;
            IsActive = true;
            Debug.Log($"[CorruptionHeart] Heart at {GridPosition} activated.");
        }

        // ── Private ───────────────────────────────────────────────────────

        private void OnHeartDestroyed(GridEntityHealth _)
        {
            if (CorruptionManager.Instance != null)
                CorruptionManager.Instance.ClearHeartCluster(this);

            Destroy(gameObject);
        }

        private void SpawnFloatingIndicator()
        {
            if (floatingIndicatorPrefab != null)
            {
                floatingIndicatorInstance = Instantiate(floatingIndicatorPrefab, transform.position + Vector3.up * 2.5f, Quaternion.identity);
                floatingIndicatorInstance.name = "CorruptionHeartIndicator";
            }
            else
            {
                // Placeholder: a magenta quad floating above the heart
                floatingIndicatorInstance = GameObject.CreatePrimitive(PrimitiveType.Quad);
                floatingIndicatorInstance.name = "CorruptionHeartIndicator_Placeholder";
                floatingIndicatorInstance.transform.position = transform.position + Vector3.up * 2.5f;
                floatingIndicatorInstance.transform.localScale = Vector3.one * 0.6f;

                var r = floatingIndicatorInstance.GetComponent<MeshRenderer>();
                if (r != null)
                {
                    r.material = new Material(Shader.Find("Sprites/Default"));
                    r.material.color = new Color(0.8f, 0f, 1f, 0.9f);
                    r.sortingOrder = 200;
                }

                var col = floatingIndicatorInstance.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }
        }
    }
}
