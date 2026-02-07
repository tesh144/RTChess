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
            if (IntervalTimer.Instance == null)
            {
                Debug.LogWarning("IntervalTimer.Instance is null!");
                return;
            }

            float progress = IntervalTimer.Instance.IntervalProgress;

            // Update interval count text (optional, small number at top)
            if (intervalText != null)
            {
                intervalText.text = IntervalTimer.Instance.CurrentInterval.ToString();
            }

            // Update vertical fill bar (fills upward)
            if (verticalBar != null)
            {
                verticalBar.fillAmount = progress;

                // Debug every 10 frames to avoid spam
                if (Time.frameCount % 10 == 0)
                {
                    Debug.Log($"IntervalUI: progress={progress:F2}, fillAmount={verticalBar.fillAmount:F2}, interval={IntervalTimer.Instance.CurrentInterval}");
                }
            }
            else
            {
                Debug.LogWarning("verticalBar is null!");
            }
        }
    }
}
