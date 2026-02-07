using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Manages progressive grid expansion from 2×2 (tutorial) to 11×11 (endgame).
    /// Iteration 9: Grid Expansion & Tutorial
    /// </summary>
    public class GridExpansionManager : MonoBehaviour
    {
        // Singleton
        public static GridExpansionManager Instance { get; private set; }

        [Header("Grid Size Progression")]
        [SerializeField] private Vector2Int tutorialGridSize = new Vector2Int(2, 2);
        [SerializeField] private Vector2Int startingGridSize = new Vector2Int(4, 4);
        [SerializeField] private Vector2Int maxGridSize = new Vector2Int(11, 11);

        [Header("Expansion Milestones")]
        [SerializeField] private int wave3Size = 6; // Wave 3 complete → 6×6
        [SerializeField] private int wave6Size = 8; // Wave 6 complete → 8×8
        [SerializeField] private int wave10Size = 10; // Wave 10 complete → 10×10
        [SerializeField] private int wave15Size = 11; // Wave 15 complete → 11×11 (max)

        [Header("Expansion Animation")]
        [SerializeField] private float expansionDuration = 0.8f;

        // Current state
        private Vector2Int currentGridSize;
        private bool isExpanding = false;

        // Expansion milestones (waveNumber → gridSize)
        private Dictionary<int, int> expansionMilestones = new Dictionary<int, int>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Initialize expansion system. Called after tutorial complete.
        /// </summary>
        public void Initialize(bool skipTutorial = false)
        {
            // Set starting grid size
            currentGridSize = skipTutorial ? startingGridSize : tutorialGridSize;

            // Set up expansion milestones
            expansionMilestones[3] = wave3Size;
            expansionMilestones[6] = wave6Size;
            expansionMilestones[10] = wave10Size;
            expansionMilestones[15] = wave15Size;

            // Subscribe to wave completion
            if (WaveManager.Instance != null)
            {
                WaveManager.Instance.OnWaveComplete += OnWaveComplete;
            }

            Debug.Log($"GridExpansionManager initialized: Starting size {currentGridSize}, max {maxGridSize}");
        }

        private void OnDestroy()
        {
            if (WaveManager.Instance != null)
            {
                WaveManager.Instance.OnWaveComplete -= OnWaveComplete;
            }
        }

        /// <summary>
        /// Get current grid size.
        /// </summary>
        public Vector2Int CurrentGridSize => currentGridSize;

        /// <summary>
        /// Check if grid is at max size.
        /// </summary>
        public bool IsAtMaxSize => currentGridSize.x >= maxGridSize.x && currentGridSize.y >= maxGridSize.y;

        /// <summary>
        /// Called when a wave is completed. Checks if grid should expand.
        /// </summary>
        private void OnWaveComplete(WaveData waveData)
        {
            if (isExpanding) return;
            if (IsAtMaxSize) return;

            int waveNumber = waveData.WaveNumber;

            // Check if this wave triggers an expansion
            if (expansionMilestones.ContainsKey(waveNumber))
            {
                int newSize = expansionMilestones[waveNumber];
                Vector2Int targetSize = new Vector2Int(newSize, newSize);

                // Only expand if new size is larger
                if (newSize > currentGridSize.x)
                {
                    StartCoroutine(ExpandGridCoroutine(targetSize));
                }
            }
        }

        /// <summary>
        /// Expand grid after tutorial completion.
        /// </summary>
        public void ExpandAfterTutorial()
        {
            if (isExpanding) return;
            if (currentGridSize != tutorialGridSize) return;

            StartCoroutine(ExpandGridCoroutine(startingGridSize));
        }

        /// <summary>
        /// Manually trigger grid expansion (for testing or special events).
        /// </summary>
        public void ExpandToSize(Vector2Int newSize)
        {
            if (isExpanding) return;
            if (newSize.x <= currentGridSize.x || newSize.y <= currentGridSize.y) return;

            StartCoroutine(ExpandGridCoroutine(newSize));
        }

        /// <summary>
        /// Coroutine that handles the expansion animation and updates.
        /// </summary>
        private IEnumerator ExpandGridCoroutine(Vector2Int newSize)
        {
            isExpanding = true;

            Debug.Log($"Expanding grid from {currentGridSize} to {newSize}");

            // Pause game systems
            if (IntervalTimer.Instance != null)
            {
                IntervalTimer.Instance.Pause();
            }

            // Store old size
            Vector2Int oldSize = currentGridSize;

            // Update grid manager
            if (GridManager.Instance != null)
            {
                GridManager.Instance.ResizeGrid(newSize);
            }

            // Update fog manager (new cells start fogged)
            if (FogManager.Instance != null)
            {
                FogManager.Instance.ExpandFog(newSize);
            }

            // Update current size
            currentGridSize = newSize;

            // Animate camera zoom out
            yield return StartCoroutine(AnimateCameraToGrid(newSize));

            // Show expansion notification (optional)
            Debug.Log($"Grid expanded to {newSize}!");

            // Resume game systems
            if (IntervalTimer.Instance != null)
            {
                IntervalTimer.Instance.Resume();
            }

            isExpanding = false;
        }

        /// <summary>
        /// Animate camera to frame the new grid size.
        /// </summary>
        private IEnumerator AnimateCameraToGrid(Vector2Int gridSize)
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) yield break;

            float startTime = Time.unscaledTime;
            float startSize = mainCam.orthographicSize;

            // Calculate target camera size to frame grid
            // Grid cell size is 1.5 (from GameSetup)
            float cellSize = GridManager.Instance != null ? GridManager.Instance.CellSize : 1.5f;
            float targetSize = (gridSize.y * cellSize) * 0.55f; // Fit grid with padding

            // Smooth zoom animation
            while (Time.unscaledTime - startTime < expansionDuration)
            {
                float t = (Time.unscaledTime - startTime) / expansionDuration;
                t = 1f - (1f - t) * (1f - t); // Ease out

                mainCam.orthographicSize = Mathf.Lerp(startSize, targetSize, t);

                yield return null;
            }

            mainCam.orthographicSize = targetSize;
        }
    }
}
