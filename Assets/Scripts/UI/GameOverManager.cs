using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ClockworkGrid
{
    /// <summary>
    /// Manages game over UI for victory and defeat states.
    /// Displays full-screen overlay with results and statistics.
    /// </summary>
    public class GameOverManager : MonoBehaviour
    {
        // Singleton
        public static GameOverManager Instance { get; private set; }

        [Header("UI References")]
        private GameObject gameOverPanel;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI statsText;
        private TextMeshProUGUI messageText;
        private Button restartButton;
        private Button quitButton;

        [Header("Colors")]
        [SerializeField] private Color victoryColor = new Color(0.2f, 1f, 0.2f);
        [SerializeField] private Color defeatColor = new Color(1f, 0.2f, 0.2f);

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

        public void Initialize(Canvas canvas)
        {
            CreateGameOverUI(canvas);

            // Subscribe to WaveManager events
            if (WaveManager.Instance != null)
            {
                WaveManager.Instance.OnVictory += ShowVictory;
                WaveManager.Instance.OnDefeat += ShowDefeat;
            }

            // Hide initially
            gameOverPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (WaveManager.Instance != null)
            {
                WaveManager.Instance.OnVictory -= ShowVictory;
                WaveManager.Instance.OnDefeat -= ShowDefeat;
            }
        }

        /// <summary>
        /// Create the game over UI hierarchy
        /// </summary>
        private void CreateGameOverUI(Canvas canvas)
        {
            // Full-screen overlay panel
            gameOverPanel = new GameObject("GameOverPanel");
            RectTransform panelRect = gameOverPanel.AddComponent<RectTransform>();
            panelRect.SetParent(canvas.transform, false);
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            Image panelBg = gameOverPanel.AddComponent<Image>();
            panelBg.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            panelBg.color = new Color(0f, 0f, 0f, 0.9f);

            // Content container (centered)
            GameObject content = new GameObject("Content");
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.SetParent(panelRect, false);
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(600f, 500f);

            // Title text (VICTORY or DEFEAT)
            GameObject titleObj = new GameObject("Title");
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.SetParent(contentRect, false);
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -50f);
            titleRect.sizeDelta = new Vector2(600f, 100f);

            titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "VICTORY";
            titleText.fontSize = 72;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;

            // Message text
            GameObject messageObj = new GameObject("Message");
            RectTransform messageRect = messageObj.AddComponent<RectTransform>();
            messageRect.SetParent(contentRect, false);
            messageRect.anchorMin = new Vector2(0.5f, 1f);
            messageRect.anchorMax = new Vector2(0.5f, 1f);
            messageRect.pivot = new Vector2(0.5f, 1f);
            messageRect.anchoredPosition = new Vector2(0f, -170f);
            messageRect.sizeDelta = new Vector2(550f, 60f);

            messageText = messageObj.AddComponent<TextMeshProUGUI>();
            messageText.text = "You survived all 20 waves!";
            messageText.fontSize = 24;
            messageText.alignment = TextAlignmentOptions.Center;
            messageText.color = new Color(0.9f, 0.9f, 0.9f);

            // Statistics text
            GameObject statsObj = new GameObject("Stats");
            RectTransform statsRect = statsObj.AddComponent<RectTransform>();
            statsRect.SetParent(contentRect, false);
            statsRect.anchorMin = new Vector2(0.5f, 0.5f);
            statsRect.anchorMax = new Vector2(0.5f, 0.5f);
            statsRect.pivot = new Vector2(0.5f, 0.5f);
            statsRect.anchoredPosition = new Vector2(0f, 0f);
            statsRect.sizeDelta = new Vector2(500f, 150f);

            statsText = statsObj.AddComponent<TextMeshProUGUI>();
            statsText.text = "Wave: 20\nEnemies Defeated: 150\nTokens Earned: 75";
            statsText.fontSize = 20;
            statsText.alignment = TextAlignmentOptions.Center;
            statsText.color = new Color(0.8f, 0.8f, 0.8f);

            // Restart button
            GameObject restartObj = CreateButton("RestartButton", "Play Again");
            RectTransform restartRect = restartObj.GetComponent<RectTransform>();
            restartRect.SetParent(contentRect, false);
            restartRect.anchorMin = new Vector2(0.5f, 0f);
            restartRect.anchorMax = new Vector2(0.5f, 0f);
            restartRect.pivot = new Vector2(0.5f, 0f);
            restartRect.anchoredPosition = new Vector2(0f, 70f);
            restartRect.sizeDelta = new Vector2(200f, 50f);

            restartButton = restartObj.GetComponent<Button>();
            restartButton.onClick.AddListener(OnRestartClicked);

            // Quit button
            GameObject quitObj = CreateButton("QuitButton", "Quit");
            RectTransform quitRect = quitObj.GetComponent<RectTransform>();
            quitRect.SetParent(contentRect, false);
            quitRect.anchorMin = new Vector2(0.5f, 0f);
            quitRect.anchorMax = new Vector2(0.5f, 0f);
            quitRect.pivot = new Vector2(0.5f, 0f);
            quitRect.anchoredPosition = new Vector2(0f, 10f);
            quitRect.sizeDelta = new Vector2(200f, 50f);

            quitButton = quitObj.GetComponent<Button>();
            quitButton.onClick.AddListener(OnQuitClicked);
        }

        /// <summary>
        /// Create a UI button
        /// </summary>
        private GameObject CreateButton(string name, string text)
        {
            GameObject buttonObj = new GameObject(name);
            RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();

            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = buttonImage;

            // Button text
            GameObject textObj = new GameObject("Text");
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.SetParent(buttonRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI buttonText = textObj.AddComponent<TextMeshProUGUI>();
            buttonText.text = text;
            buttonText.fontSize = 20;
            buttonText.alignment = TextAlignmentOptions.Center;
            buttonText.color = Color.white;
            buttonText.fontStyle = FontStyles.Bold;

            return buttonObj;
        }

        /// <summary>
        /// Show victory screen
        /// </summary>
        private void ShowVictory()
        {
            if (isGameOver) return;
            isGameOver = true;

            // Set victory styling
            titleText.text = "VICTORY!";
            titleText.color = victoryColor;

            messageText.text = "You survived all 20 waves!";

            // Calculate and display statistics
            string stats = GenerateStatistics();
            statsText.text = stats;

            // Show panel
            gameOverPanel.SetActive(true);

            // Pause game time (optional)
            // Time.timeScale = 0f;

            Debug.Log("Victory screen displayed");
        }

        /// <summary>
        /// Show defeat screen
        /// </summary>
        private void ShowDefeat()
        {
            if (isGameOver) return;
            isGameOver = true;

            // Set defeat styling
            titleText.text = "DEFEAT";
            titleText.color = defeatColor;

            messageText.text = "Your defenses have fallen...";

            // Calculate and display statistics
            string stats = GenerateStatistics();
            statsText.text = stats;

            // Show panel
            gameOverPanel.SetActive(true);

            // Pause game time (optional)
            // Time.timeScale = 0f;

            Debug.Log("Defeat screen displayed");
        }

        /// <summary>
        /// Generate statistics text
        /// </summary>
        private string GenerateStatistics()
        {
            int wavesCompleted = WaveManager.Instance != null ? WaveManager.Instance.CurrentWaveNumber : 0;
            int currentTokens = ResourceTokenManager.Instance != null ? ResourceTokenManager.Instance.CurrentTokens : 0;
            int intervals = IntervalTimer.Instance != null ? IntervalTimer.Instance.CurrentInterval : 0;
            float timePlayed = intervals * 2f; // Each interval is 2 seconds

            string stats = $"Waves Completed: {wavesCompleted}\n";
            stats += $"Intervals Survived: {intervals}\n";
            stats += $"Time Played: {Mathf.FloorToInt(timePlayed / 60f)}:{(timePlayed % 60f):00}\n";
            stats += $"Tokens Remaining: {currentTokens}";

            return stats;
        }

        /// <summary>
        /// Restart button clicked
        /// </summary>
        private void OnRestartClicked()
        {
            Debug.Log("Restart clicked - reloading scene");
            // Reset time scale if paused
            Time.timeScale = 1f;

            // Reload current scene
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
            );
        }

        /// <summary>
        /// Quit button clicked
        /// </summary>
        private void OnQuitClicked()
        {
            Debug.Log("Quit clicked - exiting application");

            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }
    }
}
