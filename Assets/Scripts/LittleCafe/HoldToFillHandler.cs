#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System.Collections.Generic;
using ClockworkGrid;
using ClockworkCraft;

namespace LittleCafe
{
    public class HoldToFillHandler : MonoBehaviour
    {
        public static HoldToFillHandler Instance { get; private set; }

        [Header("Drain Timing")]
        [SerializeField] private float baseChunkInterval = 0.5f;
        [SerializeField] private float chunkDecayFactor = 0.85f;
        [SerializeField] private float minChunkInterval = 0.08f;

        // State
        private GameObject activeBuilding;
        private float chunkTimer;
        private float currentChunkInterval;
        private int chunksThisSession;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void LateUpdate()
        {
            // LateUpdate ensures HandlePopupTap (in Update) runs first and consumes clicks.
            if (Input.GetMouseButtonDown(0) && !BuildingProductionManager.Instance.ClickConsumedThisFrame)
            {
                TryStartHold();
            }

            if (Input.GetMouseButton(0) && activeBuilding != null)
            {
                UpdateHold();
            }

            if (Input.GetMouseButtonUp(0))
            {
                StopHold();
            }
        }

        private void TryStartHold()
        {
            // Input priority: don't activate if dragging
            if (DragDropHandler.Instance != null && DragDropHandler.Instance.IsDragging)
                return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 100f))
                return;

            GameObject hitObj = hit.collider.gameObject;
            var bpm = BuildingProductionManager.Instance;
            if (bpm == null) return;

            if (bpm.HasReadyPopupAt(hitObj))
                return;

            if (!bpm.IsWaitingForHoldFill(hitObj))
                return;

            if (bpm.IsBuildingPaused(hitObj))
                return;

            // Stop any existing hold before starting new one
            StopHold();

            activeBuilding = hitObj;
            chunksThisSession = 0;
            currentChunkInterval = baseChunkInterval;
            chunkTimer = 0f;
        }

        private void UpdateHold()
        {
            if (activeBuilding == null) return;

            var bpm = BuildingProductionManager.Instance;
            if (bpm == null || !bpm.IsWaitingForHoldFill(activeBuilding))
            {
                StopHold();
                return;
            }

            chunkTimer += Time.deltaTime;
            if (chunkTimer >= currentChunkInterval)
            {
                chunkTimer -= currentChunkInterval;
                TryDrainChunk();
            }
        }

        private void TryDrainChunk()
        {
            var bpm = BuildingProductionManager.Instance;
            var info = bpm.GetHoldFillInfo(activeBuilding);

            // Check if player can afford 1 unit
            var rm = ResourceManager.Instance;
            if (rm == null || rm.GetResource(info.resourceType) < 1)
                return; // Pause — no resources, but don't stop hold

            // Spend 1 resource
            rm.SpendResources(new Dictionary<ResourceType, int>
            {
                { info.resourceType, 1 }
            });

            // Increment fill
            bool fillComplete = bpm.IncrementHoldFill(activeBuilding);

            chunksThisSession++;

            // TODO Task 5: trigger VFX per chunk
            // TODO Task 4: update fill bar UI
            // TODO Task 6: play chunk SFX

            // Accelerate
            currentChunkInterval = Mathf.Max(
                minChunkInterval,
                currentChunkInterval * chunkDecayFactor
            );

            if (fillComplete)
            {
                // TODO Task 6: play completion SFX
                StopHold();
            }
        }

        private void StopHold()
        {
            activeBuilding = null;
            chunksThisSession = 0;
        }

        public void InterruptIfActive(GameObject building)
        {
            if (activeBuilding == building)
                StopHold();
        }
    }
}
