using UnityEngine;
using System.Collections.Generic;
using ClockworkGrid;

namespace LittleCafe
{
    /// <summary>
    /// Manages seating relationships: which chairs are attached to which furniture groups,
    /// available seating positions, and seating capacity calculations.
    ///
    /// Subscribes to FurnitureConnectivityManager to update chair attachments when
    /// furniture groups change (merge, split, etc).
    /// </summary>
    public class TableSeatingManager : MonoBehaviour
    {
        public static TableSeatingManager Instance { get; private set; }

        // Track which furniture group each chair is attached to
        private Dictionary<ChairObject, FurnitureGroup> chairToGroup = new Dictionary<ChairObject, FurnitureGroup>();

        // Track which chairs are attached to each furniture group
        private Dictionary<FurnitureGroup, List<ChairObject>> groupToChairs = new Dictionary<FurnitureGroup, List<ChairObject>>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            if (FurnitureConnectivityManager.Instance != null)
            {
                FurnitureConnectivityManager.Instance.ConnectivityChanged += HandleConnectivityChange;
            }
        }

        private void OnDisable()
        {
            if (FurnitureConnectivityManager.Instance != null)
            {
                FurnitureConnectivityManager.Instance.ConnectivityChanged -= HandleConnectivityChange;
            }
        }

        /// <summary>
        /// Called when furniture connectivity changes (groups merge, split, etc).
        /// Updates chair attachments based on new furniture groups.
        /// </summary>
        private void HandleConnectivityChange()
        {
            Debug.Log("[TableSeatingManager] Connectivity changed, updating chair attachments...");

            // Refresh all chairs to see if they should be attached to new groups
            RefreshAllChairAttachments();
        }

        /// <summary>
        /// Scan all chairs and update their group attachments based on current connectivity.
        /// </summary>
        private void RefreshAllChairAttachments()
        {
            ChairObject[] allChairs = FindObjectsOfType<ChairObject>();

            foreach (ChairObject chair in allChairs)
            {
                UpdateChairAttachment(chair);
            }

            Debug.Log($"[TableSeatingManager] ✓ Refreshed {allChairs.Length} chairs");
        }

        /// <summary>
        /// Update a single chair's group attachment based on adjacent furniture.
        /// </summary>
        public void UpdateChairAttachment(ChairObject chair)
        {
            if (chair == null) return;

            FurnitureConnectivityManager fcm = FurnitureConnectivityManager.Instance;
            if (fcm == null) return;

            // Get the chair's current group attachment (if any)
            FurnitureGroup oldGroup = null;
            if (chairToGroup.TryGetValue(chair, out oldGroup) && oldGroup != null)
            {
                // Remove from old group
                if (groupToChairs.TryGetValue(oldGroup, out var chairList))
                {
                    chairList.Remove(chair);
                    if (chairList.Count == 0)
                    {
                        groupToChairs.Remove(oldGroup);
                    }
                }
            }

            // Find which furniture group this chair should attach to
            FurnitureGroup newGroup = FindAdjacentFurnitureGroup(chair, fcm);

            if (newGroup != null)
            {
                // Attach to new group
                chairToGroup[chair] = newGroup;

                if (!groupToChairs.TryGetValue(newGroup, out var newChairList))
                {
                    newChairList = new List<ChairObject>();
                    groupToChairs[newGroup] = newChairList;
                }

                if (!newChairList.Contains(chair))
                {
                    newChairList.Add(chair);
                }

                Debug.Log($"[TableSeatingManager] Chair '{chair.gameObject.name}' attached to {newGroup.GroupType} group with {newGroup.TotalSeatingCapacity} seats");
            }
            else
            {
                // Detach from any group
                chairToGroup[chair] = null;
                Debug.Log($"[TableSeatingManager] Chair '{chair.gameObject.name}' detached (no adjacent furniture group)");
            }
        }

        /// <summary>
        /// Find which furniture group is adjacent to this chair.
        /// </summary>
        private FurnitureGroup FindAdjacentFurnitureGroup(ChairObject chair, FurnitureConnectivityManager fcm)
        {
            // Get all adjacent furniture pieces
            List<FurnitureObject> adjacentFurniture = chair.GetAdjacentOfType(FurnitureType.Table);

            if (adjacentFurniture.Count == 0)
                return null;

            // Get the group of the first adjacent table
            FurnitureGroup group = fcm.GetFurnitureGroup(adjacentFurniture[0]);

            // Verify group is actually a Table group (should be since we filtered by type)
            if (group != null && group.GroupType == FurnitureType.Table)
            {
                return group;
            }

            return null;
        }

        /// <summary>
        /// Get all chairs attached to a furniture group.
        /// </summary>
        public List<ChairObject> GetAttachedChairsForGroup(FurnitureGroup group)
        {
            if (group == null)
                return new List<ChairObject>();

            if (groupToChairs.TryGetValue(group, out var chairs))
                return new List<ChairObject>(chairs);

            return new List<ChairObject>();
        }

        /// <summary>
        /// Get available seating positions around a furniture group.
        /// Returns all perimeter positions not currently occupied by chairs.
        /// </summary>
        public List<Vector3> GetAvailableSeatingPositions(FurnitureGroup group)
        {
            if (group == null)
                return new List<Vector3>();

            List<Vector3> allPositions = group.GetAllPerimeterPositions();
            List<ChairObject> attachedChairs = GetAttachedChairsForGroup(group);

            // Filter out positions where chairs are sitting
            List<Vector3> availablePositions = new List<Vector3>();

            foreach (Vector3 pos in allPositions)
            {
                bool isOccupied = false;

                foreach (ChairObject chair in attachedChairs)
                {
                    // Check if this position corresponds to this chair
                    if (Vector3.Distance(chair.transform.position, pos) < 0.5f)
                    {
                        isOccupied = true;
                        break;
                    }
                }

                if (!isOccupied)
                {
                    availablePositions.Add(pos);
                }
            }

            return availablePositions;
        }

        /// <summary>
        /// Get seating capacity of a furniture group.
        /// </summary>
        public int GetSeatingCapacity(FurnitureGroup group)
        {
            if (group == null)
                return 0;

            return group.TotalSeatingCapacity;
        }

        /// <summary>
        /// Attach a chair to a furniture group (called when chair is placed).
        /// </summary>
        public void AttachChairToTable(ChairObject chair, FurnitureGroup group)
        {
            if (chair == null || group == null)
                return;

            chairToGroup[chair] = group;

            if (!groupToChairs.TryGetValue(group, out var chairList))
            {
                chairList = new List<ChairObject>();
                groupToChairs[group] = chairList;
            }

            if (!chairList.Contains(chair))
            {
                chairList.Add(chair);
            }

            Debug.Log($"[TableSeatingManager] ✓ Chair '{chair.gameObject.name}' attached to group");
        }

        /// <summary>
        /// Detach a chair from its furniture group (called when chair is removed).
        /// </summary>
        public void DetachChairFromTable(ChairObject chair)
        {
            if (chair == null)
                return;

            if (chairToGroup.TryGetValue(chair, out FurnitureGroup group) && group != null)
            {
                if (groupToChairs.TryGetValue(group, out var chairList))
                {
                    chairList.Remove(chair);

                    if (chairList.Count == 0)
                    {
                        groupToChairs.Remove(group);
                    }
                }
            }

            chairToGroup[chair] = null;

            Debug.Log($"[TableSeatingManager] ✓ Chair '{chair.gameObject.name}' detached from group");
        }

        /// <summary>
        /// Get the furniture group that a chair is attached to.
        /// </summary>
        public FurnitureGroup GetChairGroup(ChairObject chair)
        {
            if (chair == null)
                return null;

            chairToGroup.TryGetValue(chair, out FurnitureGroup group);
            return group;
        }

        /// <summary>
        /// Get occupancy info for a group (how many chairs are occupied vs available).
        /// </summary>
        public (int occupied, int available, int capacity) GetGroupSeatingStatus(FurnitureGroup group)
        {
            if (group == null)
                return (0, 0, 0);

            int capacity = group.TotalSeatingCapacity;
            List<ChairObject> chairs = GetAttachedChairsForGroup(group);
            int occupied = 0;

            foreach (ChairObject chair in chairs)
            {
                if (chair != null && chair.IsOccupied)
                    occupied++;
            }

            int available = chairs.Count - occupied;

            return (occupied, available, capacity);
        }

        /// <summary>
        /// Debug: Log all current chair-group relationships.
        /// </summary>
        public void DebugLogSeatingStatus()
        {
            Debug.Log("=== SEATING STATUS ===");
            Debug.Log($"Total furniture groups: {groupToChairs.Count}");

            foreach (var kvp in groupToChairs)
            {
                FurnitureGroup group = kvp.Key;
                List<ChairObject> chairs = kvp.Value;

                var (occupied, available, capacity) = GetGroupSeatingStatus(group);

                Debug.Log($"{group.GroupType} Group: {group.Members.Count} furniture, {capacity} capacity, {chairs.Count} chairs attached, {occupied} occupied, {available} available");

                foreach (ChairObject chair in chairs)
                {
                    if (chair != null)
                    {
                        Debug.Log($"  └─ Chair: {chair.gameObject.name} (Occupied: {chair.IsOccupied})");
                    }
                }
            }

            Debug.Log("====================");
        }
    }
}
