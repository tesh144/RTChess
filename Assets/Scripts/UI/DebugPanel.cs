using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ClockworkGrid
{
    /// <summary>
    /// Hidden debug panel with developer tools.
    /// Accessed by tapping the hidden button in top-right corner 5 times.
    /// </summary>
    public class DebugPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button clearAllButton;
        [SerializeField] private Button add100TokensButton;
        [SerializeField] private Button speed1xButton;
        [SerializeField] private Button speed2xButton;
        [SerializeField] private Button speed4xButton;
        [SerializeField] private TextMeshProUGUI pauseButtonText;
        [SerializeField] private TextMeshProUGUI speedText;

        private bool isPaused = false;
        private float currentSpeed = 1f;

        public static DebugPanel Instance { get; private set; }

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
            // Setup button listeners
            if (pauseButton != null)
                pauseButton.onClick.AddListener(TogglePause);

            if (clearAllButton != null)
                clearAllButton.onClick.AddListener(ClearAll);

            if (add100TokensButton != null)
                add100TokensButton.onClick.AddListener(() => AddTokens(100));

            if (speed1xButton != null)
                speed1xButton.onClick.AddListener(() => SetSpeed(1f));

            if (speed2xButton != null)
                speed2xButton.onClick.AddListener(() => SetSpeed(2f));

            if (speed4xButton != null)
                speed4xButton.onClick.AddListener(() => SetSpeed(4f));

            // Start hidden
            if (panelRoot != null)
                panelRoot.SetActive(false);

            UpdateSpeedDisplay();
        }

        public void Show()
        {
            if (panelRoot != null)
                panelRoot.SetActive(true);
        }

        public void Hide()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        public void Toggle()
        {
            if (panelRoot != null)
                panelRoot.SetActive(!panelRoot.activeSelf);
        }

        private void TogglePause()
        {
            isPaused = !isPaused;
            Time.timeScale = isPaused ? 0f : currentSpeed;

            if (pauseButtonText != null)
                pauseButtonText.text = isPaused ? "Resume" : "Pause";
        }

        private void SetSpeed(float speed)
        {
            currentSpeed = speed;
            if (!isPaused)
                Time.timeScale = speed;

            UpdateSpeedDisplay();
        }

        private void UpdateSpeedDisplay()
        {
            if (speedText != null)
                speedText.text = $"Speed: {currentSpeed}x";
        }

        private void ClearAll()
        {
            // Find all units and resource nodes
            Unit[] units = FindObjectsOfType<Unit>();
            foreach (Unit u in units)
            {
                if (GridManager.Instance != null)
                    GridManager.Instance.RemoveUnit(u.GridX, u.GridY);
                Destroy(u.gameObject);
            }

            ResourceNode[] nodes = FindObjectsOfType<ResourceNode>();
            foreach (ResourceNode n in nodes)
            {
                if (GridManager.Instance != null)
                    GridManager.Instance.RemoveUnit(n.GridX, n.GridY);
                Destroy(n.gameObject);
            }

            Debug.Log("Cleared all units and resources");
        }

        private void AddTokens(int amount)
        {
            if (ResourceTokenManager.Instance != null)
            {
                ResourceTokenManager.Instance.AddTokens(amount);
                Debug.Log($"Added {amount} tokens");
            }
        }

        private void OnDestroy()
        {
            // Reset time scale when destroyed
            Time.timeScale = 1f;
        }
    }
}
