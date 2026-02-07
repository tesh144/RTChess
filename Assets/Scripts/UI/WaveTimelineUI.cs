using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace ClockworkGrid
{
    /// <summary>
    /// Displays a horizontal wave timeline showing upcoming waves.
    /// Shows wave markers, current progress, and warning indicators.
    /// </summary>
    public class WaveTimelineUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private RectTransform timelineContainer;
        [SerializeField] private Image timelineBackground;
        [SerializeField] private Image progressFill;
        [SerializeField] private RectTransform markersPanel;
        [SerializeField] private TextMeshProUGUI waveInfoText;

        [Header("Wave Markers")]
        private List<GameObject> waveMarkers = new List<GameObject>();

        [Header("Visual Settings")]
        [SerializeField] private Color easyColor = new Color(0.3f, 0.8f, 0.3f);
        [SerializeField] private Color mediumColor = new Color(0.8f, 0.8f, 0.3f);
        [SerializeField] private Color hardColor = new Color(0.9f, 0.5f, 0.2f);
        [SerializeField] private Color bossColor = new Color(0.9f, 0.2f, 0.2f);
        [SerializeField] private Color warningColor = new Color(1f, 0.3f, 0.3f);

        // Timeline configuration
        private int timelineSpan = 100; // Show next 100 intervals (~3-5 waves at 20 interval spacing)

        public void Initialize(Canvas canvas)
        {
            CreateTimelineUI(canvas);
        }

        /// <summary>
        /// Create the wave timeline UI hierarchy
        /// </summary>
        private void CreateTimelineUI(Canvas canvas)
        {
            // Container (top-right corner)
            GameObject containerObj = new GameObject("WaveTimelineContainer");
            timelineContainer = containerObj.AddComponent<RectTransform>();
            timelineContainer.SetParent(canvas.transform, false);
            timelineContainer.anchorMin = new Vector2(1f, 1f);
            timelineContainer.anchorMax = new Vector2(1f, 1f);
            timelineContainer.pivot = new Vector2(1f, 1f);
            timelineContainer.anchoredPosition = new Vector2(-20f, -20f);
            timelineContainer.sizeDelta = new Vector2(400f, 80f);

            // Background
            GameObject bgObj = new GameObject("Background");
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.SetParent(timelineContainer, false);
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            timelineBackground = bgObj.AddComponent<Image>();
            timelineBackground.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            timelineBackground.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);

            // Progress fill (shows current time position)
            GameObject fillObj = new GameObject("ProgressFill");
            RectTransform fillRect = fillObj.AddComponent<RectTransform>();
            fillRect.SetParent(bgRect, false);
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 0.3f);
            fillRect.offsetMin = new Vector2(5f, 5f);
            fillRect.offsetMax = new Vector2(-5f, 0f);

            progressFill = fillObj.AddComponent<Image>();
            progressFill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            progressFill.color = new Color(0f, 0.83f, 1f, 0.8f);
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;

            // Markers panel (where wave markers are placed)
            GameObject markersObj = new GameObject("MarkersPanel");
            markersPanel = markersObj.AddComponent<RectTransform>();
            markersPanel.SetParent(bgRect, false);
            markersPanel.anchorMin = new Vector2(0f, 0.3f);
            markersPanel.anchorMax = new Vector2(1f, 0.7f);
            markersPanel.offsetMin = new Vector2(5f, 0f);
            markersPanel.offsetMax = new Vector2(-5f, 0f);

            // Wave info text (shows countdown/status)
            GameObject infoObj = new GameObject("WaveInfoText");
            RectTransform infoRect = infoObj.AddComponent<RectTransform>();
            infoRect.SetParent(bgRect, false);
            infoRect.anchorMin = new Vector2(0f, 0.7f);
            infoRect.anchorMax = new Vector2(1f, 1f);
            infoRect.offsetMin = new Vector2(10f, 0f);
            infoRect.offsetMax = new Vector2(-10f, -5f);

            waveInfoText = infoObj.AddComponent<TextMeshProUGUI>();
            waveInfoText.text = "Preparing...";
            waveInfoText.fontSize = 16;
            waveInfoText.alignment = TextAlignmentOptions.Center;
            waveInfoText.color = Color.white;
            waveInfoText.fontStyle = FontStyles.Bold;
        }

        private void Start()
        {
            if (WaveManager.Instance != null)
            {
                WaveManager.Instance.OnWavesUpdated += OnWavesUpdated;
                WaveManager.Instance.OnWaveWarning += OnWaveWarning;
                WaveManager.Instance.OnWaveSpawned += OnWaveSpawned;

                // Initial update
                OnWavesUpdated(WaveManager.Instance.UpcomingWaves);
            }
        }

        private void OnDestroy()
        {
            if (WaveManager.Instance != null)
            {
                WaveManager.Instance.OnWavesUpdated -= OnWavesUpdated;
                WaveManager.Instance.OnWaveWarning -= OnWaveWarning;
                WaveManager.Instance.OnWaveSpawned -= OnWaveSpawned;
            }
        }

        private void Update()
        {
            UpdateProgress();
            UpdateWaveInfo();
        }

        /// <summary>
        /// Update the progress fill bar
        /// </summary>
        private void UpdateProgress()
        {
            if (progressFill == null || IntervalTimer.Instance == null || WaveManager.Instance == null)
                return;

            int currentInterval = IntervalTimer.Instance.CurrentInterval;
            int nextWaveInterval = WaveManager.Instance.NextWaveInterval;

            // Calculate progress within the timeline span
            float progress = (float)(currentInterval % timelineSpan) / timelineSpan;
            progressFill.fillAmount = progress;

            // Flash red if wave is imminent (< 5 intervals)
            int intervalsUntil = WaveManager.Instance.GetIntervalsUntilNextWave();
            if (intervalsUntil <= 5 && intervalsUntil > 0)
            {
                // Pulse effect
                float pulse = Mathf.PingPong(Time.time * 3f, 1f);
                progressFill.color = Color.Lerp(Color.white, warningColor, pulse);
            }
            else
            {
                progressFill.color = new Color(0f, 0.83f, 1f, 0.8f); // Cyan
            }
        }

        /// <summary>
        /// Update wave info text
        /// </summary>
        private void UpdateWaveInfo()
        {
            if (waveInfoText == null || WaveManager.Instance == null) return;

            WaveState currentState = WaveManager.Instance.CurrentState;

            if (currentState == WaveState.Preparation)
            {
                // Show downtime countdown
                float downtimeRemaining = WaveManager.Instance.DowntimeRemaining;
                waveInfoText.text = $"Downtime: {downtimeRemaining:F1}s";
                waveInfoText.color = new Color(0.3f, 0.8f, 1f); // Light blue
            }
            else if (currentState == WaveState.Active)
            {
                // Show current wave info
                int currentWaveNum = WaveManager.Instance.CurrentWaveNumber;
                waveInfoText.text = $"Wave {currentWaveNum} Active!";
                waveInfoText.color = warningColor; // Red for active wave
            }
            else if (currentState == WaveState.Complete)
            {
                // Show wave cleared message
                waveInfoText.text = "Wave Cleared!";
                waveInfoText.color = easyColor; // Green for cleared
            }
            else
            {
                // Fallback: Show next wave info
                int intervalsUntil = WaveManager.Instance.GetIntervalsUntilNextWave();
                float secondsUntil = intervalsUntil * 2f; // 2 seconds per interval

                if (WaveManager.Instance.UpcomingWaves.Count > 0)
                {
                    WaveData nextWave = WaveManager.Instance.UpcomingWaves[0];
                    waveInfoText.text = $"Wave {nextWave.WaveNumber}: {secondsUntil:F0}s ({nextWave.EnemyCount} enemies)";
                    waveInfoText.color = GetWaveColor(nextWave.WaveType);
                }
                else
                {
                    waveInfoText.text = "No waves incoming";
                    waveInfoText.color = Color.white;
                }
            }
        }

        /// <summary>
        /// Rebuild wave markers when waves are updated
        /// </summary>
        private void OnWavesUpdated(List<WaveData> waves)
        {
            // Clear existing markers
            foreach (GameObject marker in waveMarkers)
            {
                if (marker != null) Destroy(marker);
            }
            waveMarkers.Clear();

            if (markersPanel == null || IntervalTimer.Instance == null) return;

            int currentInterval = IntervalTimer.Instance.CurrentInterval;

            // Create markers for upcoming waves
            foreach (WaveData wave in waves)
            {
                // Only show waves within timeline span
                int relativeInterval = wave.SpawnInterval - currentInterval;
                if (relativeInterval < 0 || relativeInterval > timelineSpan) continue;

                CreateWaveMarker(wave, relativeInterval);
            }
        }

        /// <summary>
        /// Create a visual marker for a wave
        /// </summary>
        private void CreateWaveMarker(WaveData wave, int relativeInterval)
        {
            // Marker container
            GameObject markerObj = new GameObject($"WaveMarker_{wave.WaveNumber}");
            RectTransform markerRect = markerObj.AddComponent<RectTransform>();
            markerRect.SetParent(markersPanel, false);
            markerRect.sizeDelta = new Vector2(4f, 60f); // Thin vertical line

            // Position marker based on relative interval
            float xPos = (float)relativeInterval / timelineSpan;
            markerRect.anchorMin = new Vector2(xPos, 0f);
            markerRect.anchorMax = new Vector2(xPos, 1f);
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.anchoredPosition = Vector2.zero;

            // Marker line
            Image markerImage = markerObj.AddComponent<Image>();
            markerImage.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            markerImage.color = GetWaveColor(wave.WaveType);

            // Wave number label (above marker)
            GameObject labelObj = new GameObject("Label");
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.SetParent(markerRect, false);
            labelRect.anchorMin = new Vector2(0.5f, 1f);
            labelRect.anchorMax = new Vector2(0.5f, 1f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 5f);
            labelRect.sizeDelta = new Vector2(30f, 20f);

            TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = wave.WaveNumber.ToString();
            labelText.fontSize = 14;
            labelText.color = GetWaveColor(wave.WaveType);
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.fontStyle = FontStyles.Bold;

            waveMarkers.Add(markerObj);
        }

        /// <summary>
        /// Called when wave warning is triggered
        /// </summary>
        private void OnWaveWarning(WaveData wave)
        {
            // TODO: Play warning sound, flash UI, etc.
            Debug.Log($"[WaveTimelineUI] Wave {wave.WaveNumber} warning!");
        }

        /// <summary>
        /// Called when wave spawns
        /// </summary>
        private void OnWaveSpawned(WaveData wave)
        {
            // Refresh markers after wave spawns
            if (WaveManager.Instance != null)
            {
                OnWavesUpdated(WaveManager.Instance.UpcomingWaves);
            }
        }

        /// <summary>
        /// Get color for wave type
        /// </summary>
        private Color GetWaveColor(string waveType)
        {
            switch (waveType)
            {
                case "Easy": return easyColor;
                case "Medium": return mediumColor;
                case "Hard": return hardColor;
                case "Boss": return bossColor;
                default: return Color.white;
            }
        }
    }
}
