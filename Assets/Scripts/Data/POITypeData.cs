using UnityEngine;

namespace ClockworkCraft
{
    [System.Serializable]
    public class POITypeData
    {
        public string typeName;       // keyword matched against assetName (e.g. "Tree", "Corruption")
        public string label;          // displayed on bubble (e.g. "Forest", "Gold")
        public Color bubbleColor;     // bubble background tint
        public int approvalReward;    // Approval currency awarded on discovery
    }
}
