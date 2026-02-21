using System;
using UnityEngine;

namespace ClockworkCraft
{
    /// <summary>
    /// Tracks all four resource currencies: Gold, Wood, Food, Stone.
    /// Fires OnResourceChanged when any total changes so UI can react.
    /// </summary>
    public class ResourceManager : MonoBehaviour
    {
        public static ResourceManager Instance { get; private set; }

        [Header("Starting Resources (GDD Section 7)")]
        public int startingGold  = 20;
        public int startingWood  = 10;
        public int startingFood  =  5;
        public int startingStone =  0;

        private int gold;
        private int wood;
        private int food;
        private int stone;

        public int Gold  => gold;
        public int Wood  => wood;
        public int Food  => food;
        public int Stone => stone;

        /// <summary>Fired whenever a resource total changes. Args: type, new total.</summary>
        public event Action<ResourceType, int> OnResourceChanged;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            gold  = startingGold;
            wood  = startingWood;
            food  = startingFood;
            stone = startingStone;

            // Broadcast initial values so UI initialises correctly
            OnResourceChanged?.Invoke(ResourceType.Gold,  gold);
            OnResourceChanged?.Invoke(ResourceType.Wood,  wood);
            OnResourceChanged?.Invoke(ResourceType.Food,  food);
            OnResourceChanged?.Invoke(ResourceType.Stone, stone);
        }

        public void AddResource(ResourceType type, int amount)
        {
            if (amount == 0) return;
            switch (type)
            {
                case ResourceType.Gold:  gold  += amount; OnResourceChanged?.Invoke(type, gold);  break;
                case ResourceType.Wood:  wood  += amount; OnResourceChanged?.Invoke(type, wood);  break;
                case ResourceType.Food:  food  += amount; OnResourceChanged?.Invoke(type, food);  break;
                case ResourceType.Stone: stone += amount; OnResourceChanged?.Invoke(type, stone); break;
            }
            Debug.Log($"[ResourceManager] +{amount} {type}  |  G={gold}  W={wood}  F={food}  S={stone}");
        }

        public int GetResource(ResourceType type) => type switch
        {
            ResourceType.Gold  => gold,
            ResourceType.Wood  => wood,
            ResourceType.Food  => food,
            ResourceType.Stone => stone,
            _                  => 0
        };

        public bool CanAfford(int goldCost, int woodCost, int foodCost, int stoneCost)
            => gold >= goldCost && wood >= woodCost && food >= foodCost && stone >= stoneCost;

        /// <summary>
        /// Deducts costs atomically. Returns false and makes no change if not affordable.
        /// </summary>
        public bool SpendResources(int goldCost, int woodCost, int foodCost, int stoneCost)
        {
            if (!CanAfford(goldCost, woodCost, foodCost, stoneCost)) return false;
            gold  -= goldCost;  OnResourceChanged?.Invoke(ResourceType.Gold,  gold);
            wood  -= woodCost;  OnResourceChanged?.Invoke(ResourceType.Wood,  wood);
            food  -= foodCost;  OnResourceChanged?.Invoke(ResourceType.Food,  food);
            stone -= stoneCost; OnResourceChanged?.Invoke(ResourceType.Stone, stone);
            return true;
        }
    }
}
