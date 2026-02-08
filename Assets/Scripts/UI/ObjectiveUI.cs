using UnityEngine;
using TMPro;
using System.Collections.Generic;

namespace ClockworkGrid
{
    /// <summary>
    /// Displays multiple wave objectives with per-objective completion state.
    /// Completed objectives show a checkmark and gray/strikethrough text.
    /// </summary>
    public class ObjectiveUI : MonoBehaviour
    {
        public static ObjectiveUI Instance { get; private set; }

        public TextMeshProUGUI objectiveText;

        private int currentWave = 1;
        private List<WaveObjective> objectives = new List<WaveObjective>();
        private List<int> progress = new List<int>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

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
        /// Set all objectives for the current wave.
        /// </summary>
        public void SetObjectives(int waveNumber, List<WaveObjective> waveObjectives)
        {
            currentWave = waveNumber;
            objectives = new List<WaveObjective>(waveObjectives);
            progress = new List<int>();
            for (int i = 0; i < objectives.Count; i++)
                progress.Add(0);
            UpdateDisplay();
        }

        /// <summary>
        /// Update progress for a specific objective index.
        /// </summary>
        public void UpdateProgress(int objectiveIndex, int newProgress)
        {
            if (objectiveIndex >= 0 && objectiveIndex < progress.Count)
            {
                progress[objectiveIndex] = newProgress;
                UpdateDisplay();
            }
        }

        /// <summary>
        /// Check if all objectives are complete.
        /// </summary>
        public bool AreAllObjectivesComplete()
        {
            for (int i = 0; i < objectives.Count; i++)
            {
                if (progress[i] < objectives[i].target)
                    return false;
            }
            return true;
        }

        private void UpdateDisplay()
        {
            if (objectiveText == null) return;

            string display = $"<b>Wave {currentWave}</b>\n";
            for (int i = 0; i < objectives.Count; i++)
            {
                bool complete = progress[i] >= objectives[i].target;
                string verb = objectives[i].GetVerb();
                string name = objectives[i].GetDisplayName();
                string line = $"{verb} {progress[i]}/{objectives[i].target} {name}";

                if (complete)
                {
                    display += $"<color=#888888>\u2714 <s>{line}</s></color>";
                }
                else
                {
                    display += $"  {line}";
                }

                if (i < objectives.Count - 1)
                    display += "\n";
            }

            objectiveText.text = display;
        }

        public void Hide()
        {
            if (objectiveText != null)
            {
                objectiveText.gameObject.SetActive(false);
            }
        }

        public void Show()
        {
            if (objectiveText != null)
            {
                objectiveText.gameObject.SetActive(true);
            }
        }

        // Legacy compatibility
        public void SetObjective(int waveNumber, int target, string displayName = "Mines")
        {
            SetObjectives(waveNumber, new List<WaveObjective>
            {
                new WaveObjective(ObjectiveType.DestroyResources, target)
            });
        }

        public void IncrementCleared()
        {
            if (progress.Count > 0)
                UpdateProgress(0, progress[0] + 1);
        }

        public void ResetProgress()
        {
            for (int i = 0; i < progress.Count; i++)
                progress[i] = 0;
            UpdateDisplay();
        }
    }
}
