using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ClockworkGrid
{
    /// <summary>
    /// Displays a vertical interval timer bar on the left edge of the screen.
    /// Fills upward over the interval duration.
    /// </summary>
    public class IntervalUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI intervalText;
        [SerializeField] private Image verticalBar;

        private void Update()
        {
            if (IntervalTimer.Instance == null) return;

            // Update interval count text (optional, small number at top)
            if (intervalText != null)
            {
                intervalText.text = IntervalTimer.Instance.CurrentInterval.ToString();
            }

            // Update vertical fill bar (fills upward)
            if (verticalBar != null)
            {
                verticalBar.fillAmount = IntervalTimer.Instance.IntervalProgress;
            }
        }
    }
}
