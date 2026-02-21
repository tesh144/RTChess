using UnityEngine;
using System.Collections.Generic;

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
        public void AttachComponents(GameObject go, int hp, int attackPower, bool isActive)
        {
            if (go == null) return;

            // Attach health if HP > 0
            if (hp > 0)
            {
                GridEntityHealth health = go.GetComponent<GridEntityHealth>();
                if (health == null)
                    health = go.AddComponent<GridEntityHealth>();

                health.Initialize(hp, attackPower);
                allHealth.Add(health);

                Debug.Log($"[GridEntityManager] Attached health to {go.name}: HP={hp}, ATK={attackPower}");
            }

            // Attach actor if isActive
            if (isActive)
            {
                GridEntityActor actor = go.GetComponent<GridEntityActor>();
                if (actor == null)
                    actor = go.AddComponent<GridEntityActor>();

                actor.Initialize(clockwise: true, range: 1, intervalMultiplier: 1);
                allActors.Add(actor);

                Debug.Log($"[GridEntityManager] Attached actor to {go.name}: active clockwork entity");
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
        /// Overload that takes WorkerData directly.
        /// </summary>
        public void AttachFromWorkerData(GameObject go, WorkerData data)
        {
            if (data == null) return;
            AttachComponents(go, data.hp, data.attackPower, data.isActive);
        }

        /// <summary>
        /// Overload that takes UnitData directly.
        /// </summary>
        public void AttachFromUnitData(GameObject go, UnitData data)
        {
            if (data == null) return;
            AttachComponents(go, data.hp, data.attackPower, data.isActive);
        }

        /// <summary>
        /// Overload that takes BuildingData directly.
        /// </summary>
        public void AttachFromBuildingData(GameObject go, BuildingData data)
        {
            if (data == null) return;
            AttachComponents(go, data.hp, data.attackPower, data.isActive);
        }

        /// <summary>
        /// Overload that takes EnvironmentData directly.
        /// </summary>
        public void AttachFromEnvironmentData(GameObject go, EnvironmentData data)
        {
            if (data == null) return;
            AttachComponents(go, data.hp, data.attackPower, data.isActive);
        }

        // ---------------------------------------------------------------
        // Entity Destruction
        // ---------------------------------------------------------------

        private void HandleEntityDestroyed(GridEntityHealth health)
        {
            if (health == null) return;

            Debug.Log($"[GridEntityManager] Entity destroyed: {health.gameObject.name}");

            // Remove from registries
            allHealth.Remove(health);

            GridEntityActor actor = health.GetComponent<GridEntityActor>();
            if (actor != null)
                allActors.Remove(actor);

            // Clean up grid state via FurnitureObject
            FurnitureObject furniture = health.GetComponent<FurnitureObject>();
            if (furniture != null)
            {
                furniture.OnRemoved();
            }

            // Destroy the GameObject after a delay (allow remove animation to play)
            Destroy(health.gameObject, 0.6f);
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
    }
}
