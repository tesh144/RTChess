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
        private Material _fogMaterial;
        private Texture2D _fogTexture;
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

            // Destroy fog material and texture to avoid memory leaks
            if (_fogMaterial != null) { Destroy(_fogMaterial); _fogMaterial = null; }
            if (_fogTexture  != null) { Destroy(_fogTexture);  _fogTexture  = null; }

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
            // Use prefab from CorruptionManager if assigned, otherwise fall back to particle fog
            GameObject prefab = CorruptionManager.Instance != null
                ? CorruptionManager.Instance.CorruptionOverlayPrefab : null;

            Debug.Log($"[CorruptionOverlay] SpawnVisual at {GridPosition}. prefab={(prefab != null ? prefab.name : "NULL")} — using {(prefab != null ? "PREFAB" : "PARTICLE")} path");

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
                // Code-driven purple fog particle system — no prefab or texture assets needed

                // Step 1: Generate soft-circle texture (radial alpha gradient)
                _fogTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                Vector2 center = new Vector2(15.5f, 15.5f);
                for (int px = 0; px < 32; px++)
                {
                    for (int py = 0; py < 32; py++)
                    {
                        float dist = Vector2.Distance(new Vector2(px, py), center);
                        float t = Mathf.Clamp01(1f - dist / 16f);
                        float alpha = t * t;
                        _fogTexture.SetPixel(px, py, new Color(1f, 1f, 1f, alpha));
                    }
                }
                _fogTexture.Apply();

                // Step 2: Create material (Particles/Standard Unlit, alpha-blended)
                var sh = Shader.Find("Particles/Standard Unlit");
                if (sh == null)
                {
                    Debug.LogError("[CorruptionOverlay] Shader 'Particles/Standard Unlit' not found.");
                    return;
                }
                _fogMaterial = new Material(sh);
                _fogMaterial.mainTexture = _fogTexture;
                _fogMaterial.SetFloat("_Mode", 2f);
                _fogMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _fogMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _fogMaterial.SetInt("_ZWrite", 0);
                _fogMaterial.EnableKeyword("_ALPHABLEND_ON");
                _fogMaterial.renderQueue = 3000;

                // Step 3: Create child GameObject and ParticleSystem
                visualChild = new GameObject("CorruptionFog");
                visualChild.transform.SetParent(transform);
                visualChild.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                visualChild.transform.localRotation = Quaternion.identity;
                visualChild.transform.localScale    = Vector3.one;

                var ps = visualChild.AddComponent<ParticleSystem>();

                // Step 4: Configure ParticleSystem modules
                var main = ps.main;
                main.duration        = 3f;
                main.loop            = true;
                main.startLifetime   = 2.5f;
                main.startSpeed      = 0.04f;
                main.startSize       = 0.45f;
                main.maxParticles    = 60;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.gravityModifier = 0f;
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.45f, 0f, 0.75f, 0.5f),
                    new Color(0.60f, 0f, 0.85f, 0.5f)
                );

                var emission = ps.emission;
                emission.enabled = true;
                emission.rateOverTime = 20f;

                var shape = ps.shape;
                shape.enabled   = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale     = new Vector3(0.85f, 0.05f, 0.85f);

                // Colour over lifetime: fade in → hold → fade out
                var gradient = new Gradient();
                gradient.alphaKeys = new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f, 0.0f),
                    new GradientAlphaKey(1f, 0.2f),
                    new GradientAlphaKey(1f, 0.8f),
                    new GradientAlphaKey(0f, 1.0f),
                };
                gradient.colorKeys = new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f),
                };
                var colOverLife = ps.colorOverLifetime;
                colOverLife.enabled = true;
                colOverLife.color   = new ParticleSystem.MinMaxGradient(gradient);

                // Size over lifetime: expand then shrink
                var sizeCurve = new AnimationCurve(
                    new Keyframe(0f,   0.8f),
                    new Keyframe(0.5f, 1.0f),
                    new Keyframe(1f,   0.6f)
                );
                var sizeOverLife = ps.sizeOverLifetime;
                sizeOverLife.enabled = true;
                sizeOverLife.size    = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

                // Renderer
                var psr = visualChild.GetComponent<ParticleSystemRenderer>();
                psr.material         = _fogMaterial;
                psr.renderMode       = ParticleSystemRenderMode.Billboard;
                psr.sortingOrder     = 10;
                psr.sortingLayerName = "Default";

                // Start playback
                visualChild.SetActive(true);
                ps.Play();
                Debug.Log($"[CorruptionOverlay] Particle fog created at {GridPosition}. PS isPlaying={ps.isPlaying}, particleCount={ps.particleCount}, material={_fogMaterial.name}, shader={_fogMaterial.shader.name}");
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
