#pragma warning disable CS0414, CS0219, CS0618

namespace LittleCafe
{
    /// <summary>
    /// Compatibility shim. FurnitureObject is now a thin wrapper over PlacedObject.
    ///
    /// This file is kept so that:
    ///  - Existing prefabs that have a FurnitureObject MonoBehaviour component continue to work
    ///  - MapGeneratorV2 (Will's file) continues to compile without changes
    ///  - Legacy subclasses (ChairObject, TableObject, WallObject) continue to inherit correctly
    ///
    /// REMOVE this shim once:
    ///  1. MapGeneratorV2 no longer references FurnitureObject
    ///  2. FurnitureConnectionDebugVisualizer and TableSeatingManager are retired or migrated
    ///  3. All prefabs are migrated from FurnitureObject → PlacedObject component
    /// </summary>
    public class FurnitureObject : PlacedObject { }
}
