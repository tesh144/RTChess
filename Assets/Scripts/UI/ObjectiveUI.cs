using UnityEngine;
using TMPro;

namespace ClockworkGrid
{
    /// <summary>
    /// Displays wave objective progress ("Clear X/Y Mines").
    /// </summary>
    public class ObjectiveUI : MonoBehaviour
    {
        public static ObjectiveUI Instance { get; private set; }

        public TextMeshProUGUI objectiveText;

        private int currentCleared = 0;
        private int targetCount = 0;
        private int currentWave = 1;
        private string targetName = "Mines";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Try to find objective text if not assigned
            if (objectiveText == null)
            {
                objectiveText = GetComponent<TextMeshProUGUI>();
            }

            if (objectiveText == null)
            {
                Debug.LogWarning("[ObjectiveUI] No TextMeshProUGUI component found!");
            }
        }

        /// <summary>
        /// Set the objective for the current wave.
        /// </summary>
        public void SetObjective(int waveNumber, int target, string displayName = "Mines")
        {
            currentWave = waveNumber;
            targetCount = target;
            targetName = displayName;
            currentCleared = 0;
            UpdateDisplay();
        }

        /// <summary>
        /// Increment cleared count when a resource node is destroyed.
        /// </summary>
        public void IncrementCleared()
        {
            currentCleared++;
            UpdateDisplay();
        }

        /// <summary>
        /// Reset objective progress.
        /// </summary>
        public void ResetProgress()
        {
            currentCleared = 0;
            UpdateDisplay();
        }

        /// <summary>
        /// Check if objective is complete.
        /// </summary>
        public bool IsObjectiveComplete()
        {
            return currentCleared >= targetCount;
        }

        /// <summary>
        /// Update the displayed text.
        /// </summary>
        private void UpdateDisplay()
        {
            if (objectiveText == null) return;

            objectiveText.text = $"Wave {currentWave}: Clear {currentCleared}/{targetCount} {targetName}";
        }

        /// <summary>
        /// Hide the objective display.
        /// </summary>
        public void Hide()
        {
            if (objectiveText != null)
            {
                objectiveText.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Show the objective display.
        /// </summary>
        public void Show()
        {
            if (objectiveText != null)
            {
                objectiveText.gameObject.SetActive(true);
            }
        }
    }
}
