using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ClockworkGrid
{
    /// <summary>
    /// Displays interval counter and a progress bar for the current interval.
    /// </summary>
    public class IntervalUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI intervalText;
        [SerializeField] private Image progressBar;

        private void Update()
        {
            if (IntervalTimer.Instance == null) return;

            if (intervalText != null)
            {
                intervalText.text = $"Interval: {IntervalTimer.Instance.CurrentInterval}";
            }

            if (progressBar != null)
            {
                progressBar.fillAmount = IntervalTimer.Instance.IntervalProgress;
            }
        }
    }
}
