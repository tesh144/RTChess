#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// Configuration data for a single furniture asset.
    /// Stored in FurnitureDatabase ScriptableObject.
    /// </summary>
    [System.Serializable]
    public class FurnitureData : ISerializationCallbackReceiver
    {
        [Header("Asset Identity")]
        public string assetName;              // e.g., "MyTable"

        [Header("Furniture Properties")]
        public FurnitureType type = FurnitureType.Decoration;
        public bool isFunctional = false;     // Has special behavior (Table/Chair/Wall)
        public bool isWalkable = false;       // Default: false (blocks movement)
        [Tooltip("Active objects perform an action each interval tick. Furniture is passive by default.")]
        public bool isActive = false;

        [Header("Draw Weight")]
        [Tooltip("Relative likelihood of being drawn. Default 1. Set to 2 for 2x more likely, 3 for 3x, etc.")]
        public float drawWeight = 1f;

        [Header("Grid Shape")]
        [Tooltip("Cell footprint for this furniture. Replaces the legacy gridSize field.")]
        public GridShape shape;

        [Header("Visual")]
        public float visualScale = 1.0f;      // Scale multiplier for visual appearance

        [Header("Prefab & Icon")]
        public GameObject prefab;             // Assign your custom prefab directly
        public Sprite icon;                   // Icon for dock bar (optional)

        // ── Legacy field (hidden but kept serialised for migration) ─────────
        [HideInInspector]
        public Vector2Int gridSize = Vector2Int.one;

        // ── ISerializationCallbackReceiver ──────────────────────────────────
        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            // Auto-migrate: if shape is null/empty but gridSize was set to something
            // meaningful, convert it to a GridShape rectangle.
            if ((shape == null || shape.IsEmpty) && gridSize.x > 0 && gridSize.y > 0)
            {
                shape = GridShape.Rectangle(gridSize.x, gridSize.y);
            }
        }

        /// <summary>
        /// Get clean asset name without file extension.
        /// </summary>
        public string GetCleanName()
        {
            if (string.IsNullOrEmpty(assetName))
                return "Unknown";

            // Remove .fbx extension if present
            return assetName.Replace(".fbx", "").Replace(".FBX", "");
        }

    }
}
