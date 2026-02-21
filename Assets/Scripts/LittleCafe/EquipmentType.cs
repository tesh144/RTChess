using UnityEngine;

namespace LittleCafe
{
    public enum EquipmentType
    {
        CookingStation,
        ServingCounter,
        WashingStation,
        PlateRack,
        Wall,
        Door,
        Table,
        Chair
    }

    public enum CafeZone
    {
        Kitchen,   // rows 0-4
        WallRow,   // rows 5 and 12
        Dining,    // rows 6-11
        Open       // rows 13-14
    }

    public static class EquipmentData
    {
        public static Color GetColor(EquipmentType type)
        {
            switch (type)
            {
                case EquipmentType.CookingStation:  return new Color(1f, 0.42f, 0.42f);       // #FF6B6B
                case EquipmentType.ServingCounter:   return new Color(0.31f, 0.69f, 0.36f);    // #4FB05D
                case EquipmentType.WashingStation:   return new Color(0.42f, 0.80f, 1f);       // #6BCBFF
                case EquipmentType.PlateRack:        return new Color(1f, 0.41f, 0.71f);       // #FF69B4
                case EquipmentType.Wall:             return new Color(0.18f, 0.18f, 0.18f);    // #2D2D2D
                case EquipmentType.Door:             return new Color(1f, 0.85f, 0.24f);       // #FFD93D
                case EquipmentType.Table:            return new Color(0.55f, 0.36f, 0.96f);    // #8B5CF6
                case EquipmentType.Chair:            return new Color(0.56f, 0.93f, 0.56f);    // #90EE90
                default:                             return Color.white;
            }
        }

        public static string GetDisplayName(EquipmentType type)
        {
            switch (type)
            {
                case EquipmentType.CookingStation:  return "Cooking Station";
                case EquipmentType.ServingCounter:   return "Serving Counter";
                case EquipmentType.WashingStation:   return "Washing Station";
                case EquipmentType.PlateRack:        return "Plate Rack";
                case EquipmentType.Wall:             return "Wall";
                case EquipmentType.Door:             return "Door";
                case EquipmentType.Table:            return "Table";
                case EquipmentType.Chair:            return "Chair";
                default:                             return type.ToString();
            }
        }

        public static CafeZone GetZone(int row)
        {
            if (row >= 0 && row <= 4)  return CafeZone.Kitchen;
            if (row == 5 || row == 12) return CafeZone.WallRow;
            if (row >= 6 && row <= 11) return CafeZone.Dining;
            return CafeZone.Open;
        }

        public static Color GetZoneColor(CafeZone zone)
        {
            switch (zone)
            {
                case CafeZone.Kitchen:  return new Color(1f, 0.85f, 0.72f);
                case CafeZone.WallRow:  return new Color(0.6f, 0.6f, 0.6f);
                case CafeZone.Dining:   return new Color(0.75f, 1f, 0.75f);
                case CafeZone.Open:     return new Color(0.9f, 0.9f, 0.9f);
                default:                return Color.white;
            }
        }
    }
}
