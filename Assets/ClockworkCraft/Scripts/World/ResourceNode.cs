using UnityEngine;
using ClockworkGrid;

namespace ClockworkCraft
{
    /// <summary>
    /// ClockworkCraft resource node — Trees, Gold Mines, Wild Farms, Rocks.
    /// Distinct from ClockworkGrid.ResourceNode (RTChess).  Do NOT merge.
    ///
    /// Workers deal damage each tick they face this node.
    /// On depletion: awards loot, notifies NodeManager, frees grid cell.
    /// isInteractable=false on Rock and Water — workers skip interaction entirely.
    /// </summary>
    public class ResourceNode : MonoBehaviour
    {
        [Header("Node Settings")]
        public TileType tileType;
        public ResourceType resourceType;
        [Range(1, 3)] public int tier = 1;
        public int hp = 5;
        public bool isInteractable = true;   // false for Rock and Water at start

        [Header("Loot Drop")]
        public int lootBonusAmount = 3;      // bonus resources awarded on death

        public int GridX { get; private set; }
        public int GridY { get; private set; }

        private int currentHp;

        public void Initialize(int x, int y)
        {
            GridX = x;
            GridY = y;
            currentHp = hp;
        }

        /// <summary>Returns yield per tick (equals tier — Tier 1: 1/tick, Tier 2: 2/tick, Tier 3: 3/tick).</summary>
        public int GetYieldPerTick() => tier;

        /// <summary>
        /// Called by a Worker when it faces and acts on this node.
        /// Returns true if the node was killed (Worker should trigger tile takeover).
        /// </summary>
        public bool TakeDamage(int damage)
        {
            if (!isInteractable) return false;

            currentHp -= damage;
            if (currentHp <= 0)
            {
                OnDepleted();
                return true;
            }
            return false;
        }

        private void OnDepleted()
        {
            // Award loot bonus drop
            ResourceManager.Instance?.AddResource(resourceType, lootBonusAmount);

            // Notify NodeManager (removes from registry)
            NodeManager.Instance?.OnNodeDepleted(this);

            // Free grid cell — RemoveUnit is the existing public API on GridManager
            GridManager.Instance?.RemoveUnit(GridX, GridY);

            Destroy(gameObject);
        }
    }
}
