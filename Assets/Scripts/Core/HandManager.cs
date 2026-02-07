using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Data for a unit in the player's hand
    /// Iteration 6: Now uses UnitStats for full data
    /// </summary>
    [System.Serializable]
    public class UnitData
    {
        public UnitStats Stats; // Full unit stats
        public UnitType Type => Stats.unitType;
        public int Cost => Stats.resourceCost;
        public string DisplayName => Stats.unitName;
        public GameObject Prefab => Stats.unitPrefab;

        public UnitData(UnitStats stats)
        {
            Stats = stats;
        }
    }

    /// <summary>
    /// Manages the player's hand state (units available in dock).
    /// Handles drawing units with cost escalation and probability weighting.
    /// </summary>
    public class HandManager : MonoBehaviour
    {
        // Singleton
        public static HandManager Instance { get; private set; }

        // Hand state
        private List<UnitData> hand = new List<UnitData>();

        // Draw configuration
        [Header("Draw Costs")]
        [SerializeField] private int baseDealCost = 2; // Phase 2 spec: starts at 2
        private int drawCount = 0;

        // Events
        public event Action OnHandChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Initialize()
        {
            // Iteration 6: Now uses RaritySystem instead of direct prefab refs
            // RaritySystem should already be initialized by GameSetup
        }

        /// <summary>
        /// Calculate the current draw cost
        /// Linear escalation: 2, 3, 4, 5...
        /// </summary>
        public int CalculateDrawCost()
        {
            return baseDealCost + drawCount;
        }

        /// <summary>
        /// Draw a random unit into the hand
        /// Returns true if successful, false if can't afford
        /// Iteration 6: Uses RaritySystem for weighted random draws
        /// </summary>
        public bool DrawUnit()
        {
            int cost = CalculateDrawCost();

            // Check affordability
            if (ResourceTokenManager.Instance == null ||
                !ResourceTokenManager.Instance.HasEnoughTokens(cost))
            {
                Debug.Log($"Cannot afford draw! Need {cost}, have {ResourceTokenManager.Instance?.CurrentTokens ?? 0}");
                return false;
            }

            // Check RaritySystem is available
            if (RaritySystem.Instance == null)
            {
                Debug.LogError("RaritySystem not initialized!");
                return false;
            }

            // Spend tokens
            ResourceTokenManager.Instance.SpendTokens(cost);

            // Increment draw count
            drawCount++;

            // Draw random unit from RaritySystem
            UnitStats drawnStats = RaritySystem.Instance.DrawRandomUnit();
            if (drawnStats == null)
            {
                Debug.LogError("Failed to draw unit from RaritySystem!");
                return false;
            }

            UnitData unitData = new UnitData(drawnStats);

            // Add to hand
            hand.Add(unitData);

            // Fire event
            OnHandChanged?.Invoke();

            Debug.Log($"Drew {unitData.DisplayName} ({drawnStats.rarity})");
            return true;
        }

        /// <summary>
        /// Remove a unit from hand (called after placement)
        /// </summary>
        public void RemoveFromHand(int index)
        {
            if (index >= 0 && index < hand.Count)
            {
                hand.RemoveAt(index);
                OnHandChanged?.Invoke();
            }
        }

        /// <summary>
        /// Remove a specific unit data from hand
        /// </summary>
        public void RemoveFromHand(UnitData unitData)
        {
            if (hand.Contains(unitData))
            {
                hand.Remove(unitData);
                OnHandChanged?.Invoke();
            }
        }

        /// <summary>
        /// Get the current hand
        /// </summary>
        public List<UnitData> GetHand()
        {
            return new List<UnitData>(hand); // Return copy to prevent external modification
        }

        /// <summary>
        /// Get a specific unit from hand by index
        /// </summary>
        public UnitData GetUnit(int index)
        {
            if (index >= 0 && index < hand.Count)
                return hand[index];
            return null;
        }

        /// <summary>
        /// Get the number of units in hand
        /// </summary>
        public int GetHandSize()
        {
            return hand.Count;
        }

        /// <summary>
        /// Give the player starting units (for testing)
        /// Iteration 6: Give 3 Soldiers at game start
        /// </summary>
        public void GiveStartingHand()
        {
            if (RaritySystem.Instance == null)
            {
                Debug.LogError("Cannot give starting hand: RaritySystem not initialized!");
                return;
            }

            // Give 3 Soldiers at game start (from Phase 2 spec)
            UnitStats soldierStats = RaritySystem.Instance.GetUnitStats(UnitType.Soldier);
            if (soldierStats != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    UnitData soldier = new UnitData(soldierStats);
                    hand.Add(soldier);
                }

                OnHandChanged?.Invoke();
                Debug.Log("Starting hand: 3 Soldiers");
            }
        }
    }
}
