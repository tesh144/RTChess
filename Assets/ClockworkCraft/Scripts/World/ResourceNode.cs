#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using ClockworkGrid;

namespace ClockworkCraft
{
    /// <summary>
    /// ClockworkCraft resource node — Trees, Gold Mines, Wild Farms, Rocks.
    /// Distinct from ClockworkGrid.ResourceNode (RTChess).  Do NOT merge.
    ///
    /// Workers deal damage each tick they face this node.
    /// Loot drops use an HP-to-loot conversion: every lootHpCost HP removed
    /// triggers lootYield particles. On depletion, any remaining accumulated
    /// damage also triggers a final loot burst plus a bonus.
    /// </summary>
    public class ResourceNode : MonoBehaviour
    {
        [Header("Node Settings")]
        public TileType tileType;
        public ResourceType resourceType;
        [Range(1, 3)] public int tier = 1;
        public int hp = 5;
        public bool isInteractable = true;   // false for Rock and Water at start

        [Header("Loot Drop — HP Conversion")]
        [Tooltip("HP removed per loot trigger. e.g. 2 means every 2 HP of damage = 1 loot burst.")]
        [Min(1)] public int lootHpCost = 1;
        [Tooltip("How many particles burst out each time lootHpCost is met.")]
        [Range(1, 10)] public int lootYield = 1;
        [Tooltip("Bonus resources awarded on node death (in addition to normal conversion loot).")]
        public int lootBonusAmount = 3;

        public int GridX { get; private set; }
        public int GridY { get; private set; }

        private int currentHp;
        private int accumulatedDamage;   // Tracks HP damage since last loot trigger

        public void Initialize(int x, int y)
        {
            GridX = x;
            GridY = y;
            currentHp = hp;
            accumulatedDamage = 0;
        }

        /// <summary>Returns yield per tick (equals tier — Tier 1: 1/tick, Tier 2: 2/tick, Tier 3: 3/tick).</summary>
        public int GetYieldPerTick() => tier;

        /// <summary>
        /// Called externally when this node takes damage (via GridEntityHealth.TakeDamage).
        /// Accumulates damage and returns how many loot bursts should trigger.
        /// The caller (GridEntityActor) is responsible for spawning the particles.
        /// </summary>
        public int AccumulateDamage(int damage)
        {
            if (!isInteractable || resourceType == ResourceType.None) return 0;

            accumulatedDamage += damage;

            // How many times did we cross the lootHpCost threshold?
            int triggers = accumulatedDamage / lootHpCost;
            accumulatedDamage %= lootHpCost;

            return triggers * lootYield;
        }

        /// <summary>
        /// Called by a Worker when it faces and acts on this node.
        /// Returns true if the node was killed (Worker should trigger tile takeover).
        /// NOTE: Damage is dealt via GridEntityHealth — this only tracks node HP for depletion.
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
            // Award any remaining accumulated damage as loot + bonus
            int finalLoot = lootBonusAmount;

            // Convert any leftover accumulated damage
            if (accumulatedDamage > 0 && lootHpCost > 0)
            {
                // Be generous on death — round up the remainder
                finalLoot += lootYield;
                accumulatedDamage = 0;
            }

            if (ResourceLootFX.Instance != null && finalLoot > 0)
            {
                Vector3 hitPos = transform.position + Vector3.up * 0.5f;
                ResourceLootFX.Instance.SpawnLoot(hitPos, resourceType, finalLoot);
            }
            else if (finalLoot > 0)
            {
                ResourceManager.Instance?.AddResource(resourceType, finalLoot);
            }

            // SFX: resource fully depleted
            if (ClockworkGrid.GameSFXManager.Instance != null)
                ClockworkGrid.GameSFXManager.Instance.PlayResourceDepleted();

            // Notify NodeManager (removes from registry)
            NodeManager.Instance?.OnNodeDepleted(this);

            // Free grid cell — RemoveUnit is the existing public API on GridManager
            GridManager.Instance?.RemoveUnit(GridX, GridY);

            Destroy(gameObject);
        }
    }
}
