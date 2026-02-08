using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ClockworkGrid
{
    /// <summary>
    /// Shows the custom Victory Screen prefab at the end of each wave.
    /// Tapping the screen proceeds to the next wave.
    /// Also handles defeat with a simple programmatic overlay.
    /// </summary>
    public class GameOverManager : MonoBehaviour
    {
        public static GameOverManager Instance { get; private set; }

        [Header("Victory Screen")]
        [SerializeField] private GameObject victoryScreenPrefab;

        private Canvas canvas;
        private GameObject activeVictoryScreen;
        private bool isGameOver = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Initialize(Canvas parentCanvas)
        {
            canvas = parentCanvas;

            if (WaveManager.Instance != null)
            {
                WaveManager.Instance.OnDefeat += ShowDefeat;
            }
        }

        private void OnDestroy()
        {
            if (WaveManager.Instance != null)
            {
                WaveManager.Instance.OnDefeat -= ShowDefeat;
            }
        }

        /// <summary>
        /// Called by WaveManager when a wave is cleared.
        /// Shows the victory screen and pauses game until player taps.
        /// </summary>
        public void ShowWaveComplete()
        {
            if (isGameOver) return;
            if (canvas == null || victoryScreenPrefab == null)
            {
                // No prefab assigned - just proceed immediately
                Debug.LogWarning("GameOverManager: No victory screen prefab assigned, proceeding automatically");
                if (WaveManager.Instance != null)
                    WaveManager.Instance.ProceedAfterWaveComplete();
                return;
            }

            // Instantiate the victory screen under the canvas
            activeVictoryScreen = Instantiate(victoryScreenPrefab, canvas.transform, false);
            activeVictoryScreen.SetActive(true);

            // Wire up all buttons in the prefab to dismiss and proceed
            Button[] buttons = activeVictoryScreen.GetComponentsInChildren<Button>(true);
            foreach (Button btn in buttons)
            {
                btn.onClick.AddListener(OnVictoryScreenTapped);
            }

            // Add a full-screen invisible button so tapping anywhere dismisses it
            if (buttons.Length == 0)
            {
                // Ensure the root has a RectTransform that covers the screen
                RectTransform rt = activeVictoryScreen.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                }

                // Add Image as raycast target (fully transparent)
                Image tapTarget = activeVictoryScreen.GetComponent<Image>();
                if (tapTarget == null)
                    tapTarget = activeVictoryScreen.AddComponent<Image>();
                tapTarget.color = new Color(0, 0, 0, 0); // invisible
                tapTarget.raycastTarget = true;

                // Add Button component for the tap
                Button tapButton = activeVictoryScreen.AddComponent<Button>();
                tapButton.onClick.AddListener(OnVictoryScreenTapped);
            }

            // Pause the interval timer while showing
            if (IntervalTimer.Instance != null)
                IntervalTimer.Instance.Pause();

            Debug.Log($"[GameOverManager] Wave complete screen shown (Wave {(WaveManager.Instance != null ? WaveManager.Instance.CurrentWaveNumber : 0)})");
        }

        private void OnVictoryScreenTapped()
        {
            if (activeVictoryScreen == null) return;

            // Destroy the victory screen
            Destroy(activeVictoryScreen);
            activeVictoryScreen = null;

            // Resume timer and proceed to next wave
            if (IntervalTimer.Instance != null)
                IntervalTimer.Instance.Resume();

            if (WaveManager.Instance != null)
                WaveManager.Instance.ProceedAfterWaveComplete();

            Debug.Log("[GameOverManager] Victory screen dismissed, proceeding to next wave");
        }

        /// <summary>
        /// Show defeat screen (simple programmatic overlay).
        /// </summary>
        private void ShowDefeat()
        {
            if (isGameOver) return;
            isGameOver = true;

            if (canvas == null) return;

            // Create simple defeat overlay
            GameObject defeatPanel = new GameObject("DefeatPanel");
            RectTransform panelRect = defeatPanel.AddComponent<RectTransform>();
            panelRect.SetParent(canvas.transform, false);
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            Image panelBg = defeatPanel.AddComponent<Image>();
            panelBg.color = new Color(0f, 0f, 0f, 0.85f);

            // Defeat title
            GameObject titleObj = new GameObject("DefeatTitle");
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.SetParent(panelRect, false);
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.sizeDelta = new Vector2(600f, 100f);
            titleRect.anchoredPosition = new Vector2(0f, 50f);

            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "DEFEAT";
            titleText.fontSize = 72;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = new Color(1f, 0.2f, 0.2f);

            // Restart button
            GameObject restartObj = new GameObject("RestartButton");
            RectTransform restartRect = restartObj.AddComponent<RectTransform>();
            restartRect.SetParent(panelRect, false);
            restartRect.anchorMin = new Vector2(0.5f, 0.5f);
            restartRect.anchorMax = new Vector2(0.5f, 0.5f);
            restartRect.sizeDelta = new Vector2(200f, 50f);
            restartRect.anchoredPosition = new Vector2(0f, -50f);

            Image restartBg = restartObj.AddComponent<Image>();
            restartBg.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

            Button restartBtn = restartObj.AddComponent<Button>();
            restartBtn.targetGraphic = restartBg;
            restartBtn.onClick.AddListener(() =>
            {
                Time.timeScale = 1f;
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
                );
            });

            GameObject btnTextObj = new GameObject("Text");
            RectTransform btnTextRect = btnTextObj.AddComponent<RectTransform>();
            btnTextRect.SetParent(restartRect, false);
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;

            TextMeshProUGUI btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnText.text = "Play Again";
            btnText.fontSize = 20;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.color = Color.white;
            btnText.fontStyle = FontStyles.Bold;

            Debug.Log("[GameOverManager] Defeat screen displayed");
        }
    }
}
