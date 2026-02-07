using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Unit types available in the dock/hand system
    /// </summary>
    public enum UnitType
    {
        Soldier,
        Ninja,
        Ogre
    }

    /// <summary>
    /// Data for a unit in the player's hand
    /// </summary>
    [System.Serializable]
    public class UnitData
    {
        public UnitType Type;
        public int Cost;
        public string DisplayName;
        public GameObject Prefab;

        public UnitData(UnitType type, int cost, string displayName, GameObject prefab)
        {
            Type = type;
            Cost = cost;
            DisplayName = displayName;
            Prefab = prefab;
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

        [Header("Unit Prefabs")]
        [SerializeField] private GameObject soldierPrefab;
        [SerializeField] private GameObject ninjaPrefab;
        [SerializeField] private GameObject ogrePrefab;

        // Unit costs (from Phase 2 spec)
        private const int SOLDIER_COST = 3;
        private const int NINJA_COST = 4;
        private const int OGRE_COST = 10;

        // Draw probabilities (from Phase 2 spec)
        private const float SOLDIER_WEIGHT = 0.6f; // 60%
        private const float NINJA_WEIGHT = 0.3f;   // 30%
        private const float OGRE_WEIGHT = 0.1f;    // 10%

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

        public void Initialize(GameObject soldier, GameObject ninja, GameObject ogre)
        {
            soldierPrefab = soldier;
            ninjaPrefab = ninja;
            ogrePrefab = ogre;
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

            // Spend tokens
            ResourceTokenManager.Instance.SpendTokens(cost);

            // Increment draw count
            drawCount++;

            // Pick random unit type based on weights
            UnitType type = PickRandomUnitType();
            UnitData unitData = CreateUnitData(type);

            // Add to hand
            hand.Add(unitData);

            // Fire event
            OnHandChanged?.Invoke();

            Debug.Log($"Drew {unitData.DisplayName} (cost: {unitData.Cost})");
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
        /// </summary>
        public void GiveStartingHand()
        {
            // Give 3 Soldiers at game start (from Phase 2 spec)
            for (int i = 0; i < 3; i++)
            {
                UnitData soldier = CreateUnitData(UnitType.Soldier);
                hand.Add(soldier);
            }

            OnHandChanged?.Invoke();
            Debug.Log("Starting hand: 3 Soldiers");
        }

        /// <summary>
        /// Pick a random unit type based on weighted probabilities
        /// </summary>
        private UnitType PickRandomUnitType()
        {
            float roll = UnityEngine.Random.value; // 0.0 to 1.0

            if (roll < SOLDIER_WEIGHT)
                return UnitType.Soldier; // 0.0 - 0.6 (60%)
            else if (roll < SOLDIER_WEIGHT + NINJA_WEIGHT)
                return UnitType.Ninja; // 0.6 - 0.9 (30%)
            else
                return UnitType.Ogre; // 0.9 - 1.0 (10%)
        }

        /// <summary>
        /// Create UnitData for a given type
        /// </summary>
        private UnitData CreateUnitData(UnitType type)
        {
            switch (type)
            {
                case UnitType.Soldier:
                    return new UnitData(UnitType.Soldier, SOLDIER_COST, "Soldier", soldierPrefab);
                case UnitType.Ninja:
                    return new UnitData(UnitType.Ninja, NINJA_COST, "Ninja", ninjaPrefab);
                case UnitType.Ogre:
                    return new UnitData(UnitType.Ogre, OGRE_COST, "Ogre", ogrePrefab);
                default:
                    return new UnitData(UnitType.Soldier, SOLDIER_COST, "Soldier", soldierPrefab);
            }
        }
    }
}
