using UnityEngine;
using System.Collections.Generic;

namespace LittleCafe
{
    /// <summary>
    /// Chair furniture with dynamic walkability and auto-rotation behavior.
    /// - Walkable when unoccupied (NPCs can walk through)
    /// - Non-walkable when occupied (NPC sitting blocks the cell)
    /// - Auto-rotates to face adjacent tables when placed
    /// </summary>
    public class ChairObject : FurnitureObject
    {
        [Header("Chair State")]
        [SerializeField] private bool isOccupied = false;
        [SerializeField] private bool isActive = false; // Active = adjacent to table

        private FurnitureObject facingTable;
        private ChairPositionController positionController;

        /// <summary>
        /// Dynamic walkability: walkable when unoccupied, blocked when occupied.
        /// </summary>
        public override bool IsWalkable => !isOccupied;

        public bool IsActive => isActive;
        public bool IsOccupied => isOccupied;
        public FurnitureObject FacingTable => facingTable;
        public ChairPositionController PositionController => positionController;

        /// <summary>
        /// Called after chair is placed. Auto-rotates to face adjacent table.
        /// </summary>
        public override void OnPlaced(int x, int y, Vector2Int size)
        {
            base.OnPlaced(x, y, size);

            CheckIfActive();
            if (isActive)
            {
                AutoRotateToFaceTable();
            }

            // Initialize position controller for visual state management
            positionController = GetComponent<ChairPositionController>();
            if (positionController == null)
            {
                positionController = gameObject.AddComponent<ChairPositionController>();
            }
            positionController.Initialize(transform.position, facingTable);

            // Register with TableSeatingManager to track seating attachments
            if (TableSeatingManager.Instance != null)
            {
                TableSeatingManager.Instance.UpdateChairAttachment(this);
            }
        }

        /// <summary>
        /// Check if chair is adjacent to a table (becomes "active").
        /// </summary>
        private void CheckIfActive()
        {
            isActive = HasAdjacentType(FurnitureType.Table);

            if (isActive)
            {
                // Find the table this chair is facing
                List<FurnitureObject> adjacentTables = GetAdjacentOfType(FurnitureType.Table);
                if (adjacentTables.Count > 0)
                {
                    facingTable = adjacentTables[0]; // Face the first adjacent table
                }
            }

            Debug.Log($"[ChairObject] {gameObject.name} isActive={isActive}");
        }

        /// <summary>
        /// Auto-rotate chair to face the adjacent table.
        /// </summary>
        private void AutoRotateToFaceTable()
        {
            if (facingTable == null) return;

            // Calculate direction from chair to table
            Vector2Int directionToTable = new Vector2Int(
                facingTable.GridX - GridX,
                facingTable.GridY - GridY
            );

            // Determine rotation based on direction
            float targetRotation = 0f;
            if (directionToTable.x > 0) // Table to the East
                targetRotation = 90f;
            else if (directionToTable.x < 0) // Table to the West
                targetRotation = -90f;
            else if (directionToTable.y > 0) // Table to the North
                targetRotation = 0f;
            else if (directionToTable.y < 0) // Table to the South
                targetRotation = 180f;

            // Apply rotation to the visual model (not root, to avoid grid conflicts)
            Transform visualRoot = transform.GetChild(0); // Assumes first child is AnimatorHolder or similar
            if (visualRoot != null)
            {
                visualRoot.localRotation = Quaternion.Euler(0f, targetRotation, 0f);
                Debug.Log($"[ChairObject] {gameObject.name} rotated to {targetRotation}° to face table at ({facingTable.GridX}, {facingTable.GridY})");
            }
        }

        /// <summary>
        /// Set chair occupancy state. Triggers visual position change.
        /// </summary>
        public void SetOccupied(bool occupied)
        {
            if (isOccupied == occupied) return;

            isOccupied = occupied;
            UpdateGridCellState(); // Update walkability in GridManager

            // Animate chair position based on occupancy
            if (positionController != null)
            {
                if (occupied)
                    positionController.PullOut();
                else
                    positionController.TuckIn();
            }

            Debug.Log($"[ChairObject] {gameObject.name} occupancy changed: {isOccupied}");
        }

        /// <summary>
        /// Refresh active state (call this when adjacent furniture changes).
        /// </summary>
        public void RefreshActiveState()
        {
            DetectAdjacentFurniture();
            CheckIfActive();
        }

        /// <summary>
        /// Called when chair is removed. Detach from seating system.
        /// </summary>
        public override void OnRemoved()
        {
            base.OnRemoved();

            // Unregister from TableSeatingManager
            if (TableSeatingManager.Instance != null)
            {
                TableSeatingManager.Instance.DetachChairFromTable(this);
            }
        }
    }
}
