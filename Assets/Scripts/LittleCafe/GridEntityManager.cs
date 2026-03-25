#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ClockworkCraft;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// Central registry and factory for grid entities.
    /// Attaches GridEntityHealth and GridEntityActor components based on database data.
    /// Tracks all living entities and handles cleanup when they're destroyed.
    ///
    /// Created by CafeSceneSetupV2.SetupFurnitureSystems().
    /// Called by DragDropHandler after placement to wire up components.
    /// </summary>
    public class GridEntityManager : MonoBehaviour
    {
        public static GridEntityManager Instance { get; private set; }

        // Registry of all living entities
        private List<GridEntityHealth> allHealth = new List<GridEntityHealth>();
        private List<GridEntityActor> allActors = new List<GridEntityActor>();

        [Tooltip("EnvironmentDatabase reference — auto-found from MapGeneratorV2 if not assigned.")]
        public EnvironmentDatabase environmentDatabase;

        // --- Public Accessors ---
        public IReadOnlyList<GridEntityHealth> AllHealth => allHealth;
        public IReadOnlyList<GridEntityActor> AllActors => allActors;
        public int ActiveActorCount => allActors.Count;
        public int LivingEntityCount => allHealth.Count;

        // ---------------------------------------------------------------
        // Singleton
        // ---------------------------------------------------------------

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            GridEntityHealth.OnAnyEntityDestroyed += HandleEntityDestroyed;
        }

        private void OnDisable()
        {
            GridEntityHealth.OnAnyEntityDestroyed -= HandleEntityDestroyed;
        }

        // ---------------------------------------------------------------
        // Component Attachment (called after placement)
        // ---------------------------------------------------------------

        /// <summary>
        /// Attach GridEntityHealth and/or GridEntityActor to a placed object
        /// based on its database stats.
        /// </summary>
        /// <param name="go">The placed GameObject (has FurnitureObject component)</param>
        /// <param name="hp">Max hit points from database (0 = no health component)</param>
        /// <param name="attackPower">Attack power from database</param>
        /// <param name="isActive">Whether this entity acts each tick</param>
        /// <param name="registryName">Database assetName for InteractionRegistry lookup (avoids prefab name mismatch)</param>
        /// <param name="allied">True for player-owned entities (workers, buildings) that should never be attacked by own workers</param>
        /// <param name="killerAdvances">When killed, does the attacker advance into this cell? Maps to sheet "Killer's Behavior" (Advance/Stay).</param>
        public void AttachComponents(GameObject go, int hp, int attackPower, bool isActive,
            BehaviorType behaviorType = BehaviorType.RotateAndInteract,
            string registryName = null, bool allied = false, bool killerAdvances = true)
        {
            if (go == null) return;

            // Attach health if HP > 0
            if (hp > 0)
            {
                GridEntityHealth health = go.GetComponent<GridEntityHealth>();
                if (health == null)
                    health = go.AddComponent<GridEntityHealth>();

                // Query InteractionRegistry for unlock state
                // Use registryName (database assetName) if provided, fall back to cleaned GameObject name
                string lookupName = !string.IsNullOrEmpty(registryName)
                    ? registryName
                    : go.name.Replace("(Clone)", "").Trim();
                bool canInteract = true;
                if (ClockworkCraft.InteractionRegistry.Instance != null)
                    canInteract = ClockworkCraft.InteractionRegistry.Instance.IsUnlocked(lookupName);

                health.Initialize(hp, attackPower, canInteract, allied, killerAdvances);
                allHealth.Add(health);

                // Attach HP bar (shows on damage, fades after timeout)
                GridEntityHPBar hpBar = go.GetComponent<GridEntityHPBar>();
                if (hpBar == null)
                    hpBar = go.AddComponent<GridEntityHPBar>();
                hpBar.Initialize(health);

                Debug.Log($"[GridEntityManager] Attached health to {go.name}: HP={hp}, ATK={attackPower}");
            }

            // Attach actor if isActive
            if (isActive)
            {
                GridEntityActor actor = go.GetComponent<GridEntityActor>();
                if (actor == null)
                    actor = go.AddComponent<GridEntityActor>();

                actor.Initialize(clockwise: true, range: 1, intervalMultiplier: 1, behavior: behaviorType);
                allActors.Add(actor);

                Debug.Log($"[GridEntityManager] Attached actor to {go.name}: behavior={behaviorType}");
            }
            else if (allied)
            {
                // Allied non-active objects (buildings) get a subtle bounce each interval tick
                BuildingBounceEffect bounce = go.GetComponent<BuildingBounceEffect>();
                if (bounce == null)
                    go.AddComponent<BuildingBounceEffect>();
            }
        }

        /// <summary>
        /// Attach components with loot settings. Adds a ResourceNode for loot drops.
        /// </summary>
        public void AttachComponents(GameObject go, int hp, int attackPower, bool isActive,
            ResourceType lootResourceType, int lootHpCost = 1, int lootYield = 1,
            BehaviorType behaviorType = BehaviorType.RotateAndInteract,
            string registryName = null, bool allied = false, bool killerAdvances = true)
        {
            // Attach base health + actor
            AttachComponents(go, hp, attackPower, isActive, behaviorType, registryName, allied, killerAdvances);

            // Attach ResourceNode for loot if a resource type is specified
            if (lootResourceType != ResourceType.None && go != null)
            {
                ResourceNode node = go.GetComponent<ResourceNode>();
                if (node == null)
                    node = go.AddComponent<ResourceNode>();

                // Query InteractionRegistry for unlock state
                string lookupName = !string.IsNullOrEmpty(registryName)
                    ? registryName
                    : go.name.Replace("(Clone)", "").Trim();
                bool canInteract = true;
                if (ClockworkCraft.InteractionRegistry.Instance != null)
                    canInteract = ClockworkCraft.InteractionRegistry.Instance.IsUnlocked(lookupName);

                node.resourceType   = lootResourceType;
                node.hp             = hp;
                node.lootHpCost     = lootHpCost;
                node.lootYield      = lootYield;
                node.lootBonusAmount = lootYield; // Death bonus = one extra burst
                node.isInteractable = canInteract;

                Debug.Log($"[GridEntityManager] Attached loot to {go.name}: drops {lootResourceType} (cost={lootHpCost}, yield={lootYield}, interactable={canInteract})");
            }
        }

        /// <summary>
        /// Overload that takes FurnitureData directly.
        /// </summary>
        public void AttachFromFurnitureData(GameObject go, FurnitureData data)
        {
            if (data == null) return;
            // Furniture doesn't have hp/attackPower fields, only isActive
            // isActive defaults to false for furniture
            AttachComponents(go, hp: 0, attackPower: 0, isActive: data.isActive);
        }

        /// <summary>
        /// Overload that takes WorkerData directly. Workers are allied.
        /// </summary>
        public void AttachFromWorkerData(GameObject go, WorkerData data)
        {
            if (data == null) return;
            AttachComponents(go, data.hp, data.attackPower, data.isActive, data.behaviorType,
                registryName: data.assetName, allied: true, killerAdvances: data.killerAdvances);
        }

        /// <summary>
        /// Overload that takes UnitData directly. Units (beasts, enemies) are not allied.
        /// </summary>
        public void AttachFromUnitData(GameObject go, UnitData data)
        {
            if (data == null) return;
            AttachComponents(go, data.hp, data.attackPower, data.isActive,
                data.lootResourceType, data.lootHpCost, data.lootYield, data.behaviorType,
                registryName: data.assetName, allied: false, killerAdvances: data.killerAdvances);
        }

        /// <summary>
        /// Overload that takes BuildingData directly. Buildings are allied.
        /// </summary>
        public void AttachFromBuildingData(GameObject go, BuildingData data)
        {
            if (data == null) return;
            AttachComponents(go, data.hp, data.attackPower, data.isActive,
                registryName: data.assetName, allied: true, killerAdvances: data.killerAdvances);
        }

        /// <summary>
        /// Overload that takes EnvironmentData directly. Environment is not allied.
        /// </summary>
        public void AttachFromEnvironmentData(GameObject go, EnvironmentData data)
        {
            if (data == null) return;
            AttachComponents(go, data.hp, data.attackPower, data.isActive,
                data.lootResourceType, data.lootHpCost, data.lootYield,
                registryName: data.assetName, allied: false, killerAdvances: data.killerAdvances);

            // Environment objects start desaturated until first worker interaction
            if (go.GetComponent<ClockworkCraft.EnvironmentDesaturation>() == null)
                go.AddComponent<ClockworkCraft.EnvironmentDesaturation>();
        }

        // ---------------------------------------------------------------
        // Entity Destruction
        // ---------------------------------------------------------------

        private void HandleEntityDestroyed(GridEntityHealth health)
        {
            if (health == null) return;

            // CorruptionOverlay lives on a grid tile — don't destroy the tile.
            // The overlay handles its own cleanup via CorruptionManager.ClearTile().
            if (health.GetComponent<CorruptionOverlay>() != null)
            {
                Debug.Log($"[GridEntityManager] Skipping destroy for corruption overlay on tile {health.gameObject.name}");
                return;
            }

            Debug.Log($"[GridEntityManager] Entity destroyed: {health.gameObject.name}");

            // Check if this was a worker (allied entity with an actor)
            GridEntityActor actor = health.GetComponent<GridEntityActor>();
            bool isWorker = health.IsAllied && actor != null;
            // Workers get body-part debris burst; everything else gets the poof
            if (isWorker)
            {
                WorkerDeathBurstFX.Spawn(health.gameObject);
            }
            else
            {
                Vector3 poofPos = health.transform.position + Vector3.up * 0.5f;
                PoofEffect.Spawn(poofPos);
            }

            // Remove from registries
            FurnitureObject furniture = health.GetComponent<FurnitureObject>();
            allHealth.Remove(health);

            if (actor != null)
                allActors.Remove(actor);

            // Clean up grid state
            if (furniture != null)
            {
                furniture.OnRemoved();
            }
            else
            {
                // Environment objects (spawned by MapGeneratorV2) may not have FurnitureObject.
                // Find and free their grid cell by searching for this object in the grid.
                GridManager gm = GridManager.Instance;
                if (gm != null)
                {
                    for (int x = 0; x < gm.Width; x++)
                    for (int y = 0; y < gm.Height; y++)
                    {
                        if (gm.GetCellOccupant(x, y) == health.gameObject)
                        {
                            gm.RemoveUnit(x, y);
                            Debug.Log($"[GridEntityManager] Freed grid cell ({x},{y}) for destroyed '{health.gameObject.name}' (no FurnitureObject)");
                            // Don't break — multi-cell objects could occupy multiple cells
                        }
                    }
                }
            }

            // Workers: hide instantly (debris burst is the visual), destroy immediately
            // Everything else: delay 0.6s for the "remove" animation to play
            if (isWorker)
            {
                // Hide all renderers immediately so the intact model vanishes
                foreach (var rend in health.GetComponentsInChildren<Renderer>())
                    rend.enabled = false;
                Destroy(health.gameObject, 0.1f);
            }
            else
            {
                Destroy(health.gameObject, 0.6f);
            }

        }

        // ---------------------------------------------------------------
        // Manual Removal (when player removes via tap-and-hold)
        // ---------------------------------------------------------------

        /// <summary>
        /// Called when an object is manually removed by the player.
        /// Cleans up entity components from our registry.
        /// </summary>
        public void OnManualRemoval(GameObject go)
        {
            if (go == null) return;

            GridEntityHealth health = go.GetComponent<GridEntityHealth>();
            if (health != null)
                allHealth.Remove(health);

            GridEntityActor actor = go.GetComponent<GridEntityActor>();
            if (actor != null)
                allActors.Remove(actor);
        }

        // ---------------------------------------------------------------
        // Queries
        // ---------------------------------------------------------------

        /// <summary>
        /// Get all active actors currently alive.
        /// </summary>
        public List<GridEntityActor> GetLivingActors()
        {
            allActors.RemoveAll(a => a == null);
            return new List<GridEntityActor>(allActors);
        }

        /// <summary>
        /// Get all damageable entities currently alive.
        /// </summary>
        public List<GridEntityHealth> GetLivingEntities()
        {
            allHealth.RemoveAll(h => h == null);
            return new List<GridEntityHealth>(allHealth);
        }

        /// <summary>
        /// Get the number of currently active worker entities (allied + has actor).
        /// Used by EconomyManager to determine the correct placement cost tier.
        /// Automatically reflects deaths and manual removals since both clean up allActors.
        /// </summary>
        public int GetActiveWorkerCount()
        {
            int count = 0;
            foreach (var actor in allActors)
            {
                if (actor == null) continue;
                GridEntityHealth health = actor.GetComponent<GridEntityHealth>();
                if (health != null && health.IsAllied)
                    count++;
            }
            return count;
        }
    }
}
