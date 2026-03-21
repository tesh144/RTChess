using UnityEngine;
using System.Collections.Generic;

namespace LittleCafe
{
    /// <summary>
    /// When a worker dies, this effect grabs the visible mesh pieces from the
    /// character model (shoulders, elbows, feet, shuriken, shield, etc.), spawns
    /// physics copies at their world positions, and launches them outward with
    /// random torque. The debris tumbles, bounces on the ground plane, then
    /// shrinks to nothing and self-destructs.
    ///
    /// Usage:  WorkerDeathBurstFX.Spawn(workerGameObject);
    ///
    /// Call this BEFORE Destroy() so the mesh references are still valid.
    /// The effect is fire-and-forget — the spawned debris is independent of
    /// the original worker.
    /// </summary>
    public class WorkerDeathBurstFX : MonoBehaviour
    {
        // ─── Tuning ─────────────────────────────────────────────────
        private const float BURST_FORCE_MIN = 0.8f;       // Tight burst — pieces stay close to the cell
        private const float BURST_FORCE_MAX = 2.0f;
        private const float UPWARD_BIAS = 3.0f;           // Mostly upward pop
        private const float TORQUE_MIN = 120f;             // Degrees/sec
        private const float TORQUE_MAX = 500f;
        private const float LIFETIME = 1.6f;               // Total effect duration
        private const float SHRINK_START = 0.50f;          // Fraction of lifetime when shrinking begins
        private const float DEBRIS_SCALE_MULT = 1.0f;      // Scale relative to original mesh
        private const float GROUND_Y = 0.01f;              // Ground plane Y
        private const float BOUNCE_DAMPING = 0.2f;         // Velocity retained after ground bounce (low = less scatter)
        private const float GRAVITY = 14f;                 // Stronger gravity pulls pieces down faster

        // ─── Internal state ─────────────────────────────────────────
        private DebrisPiece[] pieces;
        private float elapsed = 0f;

        // ─── Public API ─────────────────────────────────────────────

        /// <summary>
        /// Spawn a death burst effect from the given worker's mesh pieces.
        /// Call BEFORE destroying the worker GameObject.
        /// </summary>
        public static void Spawn(GameObject worker)
        {
            if (worker == null) return;

            // Find all MeshRenderers or SkinnedMeshRenderers in the character hierarchy
            // The worker hierarchy is: Root → CharacterHold/AnimatorHolder → Character → [body parts]
            var meshInfos = CollectMeshes(worker);

            if (meshInfos.Count == 0)
            {
                Debug.Log("[WorkerDeathBurstFX] No meshes found — skipping burst");
                return;
            }

            // Create effect root
            GameObject fxRoot = new GameObject("WorkerDeathBurst");
            fxRoot.transform.position = worker.transform.position;
            WorkerDeathBurstFX fx = fxRoot.AddComponent<WorkerDeathBurstFX>();
            fx.SpawnDebris(meshInfos, worker.transform.position);
        }

        // ─── Mesh Collection ────────────────────────────────────────

        private struct MeshInfo
        {
            public Mesh mesh;
            public Material[] materials;
            public Vector3 worldPos;
            public Quaternion worldRot;
            public Vector3 lossyScale;
        }

        /// <summary>
        /// Gather every visible mesh from the worker hierarchy.
        /// Handles both MeshRenderer (static mesh) and SkinnedMeshRenderer (skinned).
        /// </summary>
        private static List<MeshInfo> CollectMeshes(GameObject root)
        {
            var result = new List<MeshInfo>();

            // MeshFilter + MeshRenderer pairs
            var meshFilters = root.GetComponentsInChildren<MeshFilter>(false);
            foreach (var mf in meshFilters)
            {
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled || mf.sharedMesh == null) continue;

                result.Add(new MeshInfo
                {
                    mesh = mf.sharedMesh,
                    materials = mr.sharedMaterials,
                    worldPos = mf.transform.position,
                    worldRot = mf.transform.rotation,
                    lossyScale = mf.transform.lossyScale
                });
            }

            // SkinnedMeshRenderers (bake current pose into static mesh)
            var skinnedRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(false);
            foreach (var smr in skinnedRenderers)
            {
                if (!smr.enabled) continue;

                Mesh bakedMesh = new Mesh();
                smr.BakeMesh(bakedMesh);

                result.Add(new MeshInfo
                {
                    mesh = bakedMesh,
                    materials = smr.sharedMaterials,
                    worldPos = smr.transform.position,
                    worldRot = smr.transform.rotation,
                    lossyScale = smr.transform.lossyScale
                });
            }

            return result;
        }

        // ─── Debris Spawning ────────────────────────────────────────

        private struct DebrisPiece
        {
            public Transform transform;
            public Vector3 velocity;
            public Vector3 angularVelocity;    // Degrees per second
            public Vector3 startScale;
            public bool grounded;
        }

        private void SpawnDebris(List<MeshInfo> meshInfos, Vector3 center)
        {
            pieces = new DebrisPiece[meshInfos.Count];

            for (int i = 0; i < meshInfos.Count; i++)
            {
                MeshInfo info = meshInfos[i];

                // Create a debris piece with the same mesh + material
                GameObject piece = new GameObject($"Debris_{i}");
                piece.transform.SetParent(transform, false);
                piece.transform.position = info.worldPos;
                piece.transform.rotation = info.worldRot;
                piece.transform.localScale = info.lossyScale * DEBRIS_SCALE_MULT;

                MeshFilter mf = piece.AddComponent<MeshFilter>();
                mf.sharedMesh = info.mesh;

                MeshRenderer mr = piece.AddComponent<MeshRenderer>();
                mr.sharedMaterials = info.materials;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                // Calculate burst direction: outward from center + upward bias
                Vector3 outward = (info.worldPos - center);
                outward.y = 0f;
                if (outward.sqrMagnitude < 0.001f)
                    outward = Random.onUnitSphere;
                outward = outward.normalized;

                float force = Random.Range(BURST_FORCE_MIN, BURST_FORCE_MAX);
                Vector3 velocity = outward * force + Vector3.up * (UPWARD_BIAS * Random.Range(0.7f, 1.3f));

                // Random spin
                Vector3 angularVel = new Vector3(
                    Random.Range(-TORQUE_MAX, TORQUE_MAX),
                    Random.Range(-TORQUE_MAX, TORQUE_MAX),
                    Random.Range(-TORQUE_MIN, TORQUE_MIN)
                );

                pieces[i] = new DebrisPiece
                {
                    transform = piece.transform,
                    velocity = velocity,
                    angularVelocity = angularVel,
                    startScale = piece.transform.localScale,
                    grounded = false
                };
            }
        }

        // ─── Update ─────────────────────────────────────────────────

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / LIFETIME);

            if (elapsed >= LIFETIME)
            {
                Destroy(gameObject);
                return;
            }

            float dt = Time.deltaTime;

            for (int i = 0; i < pieces.Length; i++)
            {
                ref DebrisPiece p = ref pieces[i];
                if (p.transform == null) continue;

                // Gravity
                if (!p.grounded)
                    p.velocity.y -= GRAVITY * dt;

                // Move
                Vector3 pos = p.transform.position;
                pos += p.velocity * dt;

                // Ground bounce
                if (pos.y <= GROUND_Y && p.velocity.y < 0f)
                {
                    pos.y = GROUND_Y;
                    p.velocity.y = -p.velocity.y * BOUNCE_DAMPING;
                    p.velocity.x *= 0.7f;
                    p.velocity.z *= 0.7f;
                    p.angularVelocity *= 0.5f;

                    // Stop bouncing if velocity is tiny
                    if (Mathf.Abs(p.velocity.y) < 0.3f)
                    {
                        p.velocity.y = 0f;
                        p.grounded = true;
                    }
                }

                p.transform.position = pos;

                // Tumble rotation
                p.transform.Rotate(p.angularVelocity * dt, Space.World);

                // Shrink to zero in the second half of the lifetime
                if (t > SHRINK_START)
                {
                    float shrinkT = (t - SHRINK_START) / (1f - SHRINK_START);
                    // Ease-in: holds size then accelerates shrink
                    float shrinkFactor = 1f - (shrinkT * shrinkT);
                    p.transform.localScale = p.startScale * Mathf.Max(shrinkFactor, 0f);
                }
            }
        }
    }
}
