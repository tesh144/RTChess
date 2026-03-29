#pragma warning disable CS0414, CS0219, CS0618

namespace LittleCafe
{
    /// <summary>
    /// Compatibility shim. The actual implementation is now in PlacedObjectRemovalHandler.
    ///
    /// Kept so CafeSceneSetupV2 can still call AddComponent&lt;FurnitureRemovalHandler&gt;()
    /// and MapGeneratorV2 references continue to compile without changes.
    ///
    /// REMOVE once CafeSceneSetupV2 is migrated to use PlacedObjectRemovalHandler.
    /// </summary>
    public class FurnitureRemovalHandler : PlacedObjectRemovalHandler { }
}
