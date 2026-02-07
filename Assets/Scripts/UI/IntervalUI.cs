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
        // Singleton
        public static IntervalUI Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private GameObject tickCounterHolder; // The tick counter panel/GameObject to show/hide
        [SerializeField] private TextMeshProUGUI intervalText;
        [SerializeField] private Image verticalBar;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // Hide tick counter at runtime start (allows UI to stay visible in Editor for design work)
            // Only activates after player places first unit and wave starts
            if (tickCounterHolder != null && (WaveManager.Instance == null || !WaveManager.Instance.HasWaveStarted))
            {
                Debug.Log("[IntervalUI] Hiding tick counter at start");
                tickCounterHolder.SetActive(false);
            }
            else if (tickCounterHolder == null)
            {
                Debug.LogError("[IntervalUI] tickCounterHolder is not assigned! Please assign it in the Inspector.");
            }
        }

        /// <summary>
        /// Show the tick counter when wave starts.
        /// </summary>
        public void ShowTickCounter()
        {
            if (tickCounterHolder != null)
            {
                tickCounterHolder.SetActive(true);
            }
        }

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
