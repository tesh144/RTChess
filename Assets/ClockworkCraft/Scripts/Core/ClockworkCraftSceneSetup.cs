#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using ClockworkGrid;
using LittleCafe;    // GridCamera, CameraPan live in LittleCafe namespace

namespace ClockworkCraft
{
    /// <summary>
    /// Lightweight bootstrap for the ClockworkCraft scene.
    ///
    /// Only handles camera setup. All grid, fog, and spawning responsibilities
    /// belong to MapGeneratorV2 (the single authority for map creation).
    ///
    /// [DefaultExecutionOrder(-100)] so camera is ready before anything else.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class ClockworkCraftSceneSetup : MonoBehaviour
    {
        private void Awake()
        {
            SetupCamera();
            EnsureIntervalTimer();
            Debug.Log("[ClockworkCraftSetup] Camera ready. Map generation handled by MapGeneratorV2.");
        }

        private void SetupCamera()
        {
            Camera cam = FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                Debug.LogError("[ClockworkCraftSetup] No Camera found in scene!");
                return;
            }

            // Replace any stale GridCamera with a fresh one
            var oldGC = cam.GetComponent<GridCamera>();
            if (oldGC != null) DestroyImmediate(oldGC);

            GridCamera gridCam = cam.gameObject.AddComponent<GridCamera>();
            gridCam.PointAtGrid();

            if (cam.GetComponent<CameraPan>() == null)
                cam.gameObject.AddComponent<CameraPan>();

            Debug.Log($"[ClockworkCraftSetup] GridCamera + CameraPan on '{cam.gameObject.name}'");
        }

        private void EnsureIntervalTimer()
        {
            if (IntervalTimer.Instance == null)
            {
                new GameObject("IntervalTimer").AddComponent<IntervalTimer>();
                Debug.Log("[ClockworkCraftSetup] Created IntervalTimer");
            }
        }
    }
}
