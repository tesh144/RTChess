using System;
using System.Collections.Generic;

namespace LittleCafe
{
    [Serializable]
    public class KitchenLayoutData
    {
        public int gridWidth;
        public int gridHeight;
        public List<EquipmentPlacement> placements = new List<EquipmentPlacement>();
    }

    [Serializable]
    public class EquipmentPlacement
    {
        public int gridX;
        public int gridY;
        public EquipmentType equipmentType;

        public EquipmentPlacement() { }

        public EquipmentPlacement(int x, int y, EquipmentType type)
        {
            gridX = x;
            gridY = y;
            equipmentType = type;
        }
    }
}
