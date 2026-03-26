using UnityEngine;

namespace ClockworkCraft
{
    public enum POIGrouping
    {
        Singular,
        Cluster,
        Area
    }

    public enum POITier
    {
        Gold,
        Grey,
        Red
    }

    /// <summary>Which database the linked object belongs to.</summary>
    public enum POISourceType
    {
        Environment,
        Unit,
        Building
    }

    [System.Serializable]
    public class POITypeData
    {
        public bool active = true;        // mirrors Active column in sheet

        [Header("Identity")]
        public string typeName;           // exact assetName from the source database (e.g. "Tree", "CorruptedHeart")
        public string label;              // displayed on bubble (e.g. "Forest", "!!!")
        public POISourceType sourceType;  // which database this object belongs to

        [Header("Grouping")]
        public POIGrouping groupingType;  // Singular, Cluster, Area
        public int quantityMinimum;       // minimum count to qualify as this POI

        [Header("Visual")]
        public POITier tier;              // Gold, Grey, Red — selects BubbleType child

        [Header("Reward")]
        public ResourceType rewardType;   // currency type awarded on discovery
        public int rewardQuantity;        // amount awarded

        // Legacy field kept for backwards compat — maps to tier
        [HideInInspector] public Color bubbleColor;
        [HideInInspector] public int approvalReward;

        /// <summary>Returns the BubbleType that corresponds to this POI's tier.</summary>
        public BubbleType GetBubbleType()
        {
            switch (tier)
            {
                case POITier.Gold: return BubbleType.POI_Gold;
                case POITier.Red:  return BubbleType.POI_Red;
                default:           return BubbleType.POI_Grey;
            }
        }
    }
}
