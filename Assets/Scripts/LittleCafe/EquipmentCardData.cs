using UnityEngine;

namespace LittleCafe
{
    /// <summary>
    /// ScriptableObject defining an equipment card for the cafe builder dock bar.
    /// Create via: Right-click > Create > LittleCafe > Equipment Card
    /// Similar to RTChess's UnitStats but for cafe equipment.
    /// </summary>
    [CreateAssetMenu(fileName = "New Equipment Card", menuName = "LittleCafe/Equipment Card")]
    public class EquipmentCardData : ScriptableObject
    {
        [Header("Identity")]
        public EquipmentType equipmentType;
        public string displayName;

        [Header("Visuals")]
        public Color cardColor = Color.white;
        public Sprite iconSprite;

        [Header("Prefab")]
        [Tooltip("The prefab instantiated when this card is placed on the grid")]
        public GameObject equipmentPrefab;

        [Header("Placement Rules")]
        [Tooltip("Which zones this equipment can be placed in (empty = any zone)")]
        public CafeZone[] allowedZones;

        [Tooltip("If true, unlimited placement. If false, limited by maxCount.")]
        public bool unlimited = true;

        [Tooltip("Max number of this equipment allowed on the grid (if not unlimited)")]
        public int maxCount = 99;

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(displayName))
                displayName = EquipmentData.GetDisplayName(equipmentType);
            if (cardColor == Color.white)
                cardColor = EquipmentData.GetColor(equipmentType);
        }
    }
}
