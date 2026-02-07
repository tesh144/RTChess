using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace ClockworkGrid
{
    /// <summary>
    /// Displays spawn code timeline with colored dots at top-center.
    /// Attach this to a UI GameObject in the scene hierarchy.
    /// Iteration 10: Wave System Redesign
    /// </summary>
    public class SpawnTimelineUI : MonoBehaviour
    {
        // Singleton
        public static SpawnTimelineUI Instance { get; private set; }

        [Header("UI References (Assign in Inspector)")]
        [SerializeField] private Text waveNumberText;
        [SerializeField] private Text countdownText;
        [SerializeField] private Transform dotContainer;

        [Header("Visual Settings")]
        [SerializeField] private float dotSpacing = 60f;
        [SerializeField] private float lineWidth = 3f;
        [SerializeField] private Color enemyColor = new Color(0.85f, 0.25f, 0.25f); // #D84040 red
        [SerializeField] private Color resourceColor = new Color(0.25f, 0.85f, 0.31f); // #40D850 green
        [SerializeField] private Color emptyColor = new Color(0.5f, 0.5f, 0.5f); // Gray

        // State
        private List<GameObject> spawnDots = new List<GameObject>();
        private List<Image> connectionLines = new List<Image>();
        private int currentDotIndex = 0;
        private string currentSpawnCode;
        private Coroutine pulseCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Initialize wave timeline with spawn code.
        /// Called by WaveManager when a wave starts.
        /// </summary>
        public void InitializeWave(int waveNumber, string spawnCode)
        {
            ClearTimeline();

            if (string.IsNullOrEmpty(spawnCode))
            {
                Debug.LogWarning("SpawnTimelineUI: Empty spawn code");
                return;
            }

            currentSpawnCode = spawnCode;

            // Update wave title
            if (waveNumberText != null)
            {
                waveNumberText.text = $"WAVE {waveNumber}";
                waveNumberText.fontSize = 36;
                waveNumberText.fontStyle = FontStyle.Bold;
                waveNumberText.color = enemyColor;
            }

            // Calculate offset to center dots
            float totalWidth = (spawnCode.Length - 1) * dotSpacing;
            float startOffset = -totalWidth / 2f;

            // Create dots and connecting lines
            for (int i = 0; i < spawnCode.Length; i++)
            {
                char code = spawnCode[i];

                // Create dot
                GameObject dotObj = new GameObject($"Dot_{i}_{code}");
                dotObj.transform.SetParent(dotContainer, false);

                RectTransform dotRect = dotObj.AddComponent<RectTransform>();
                dotRect.anchorMin = new Vector2(0.5f, 0.5f);
                dotRect.anchorMax = new Vector2(0.5f, 0.5f);
                dotRect.pivot = new Vector2(0.5f, 0.5f);
                dotRect.anchoredPosition = new Vector2(startOffset + i * dotSpacing, 0);

                Image dotImage = dotObj.AddComponent<Image>();
                ConfigureDot(dotImage, code);

                spawnDots.Add(dotObj);

                // Create connecting line (except after last dot)
                if (i < spawnCode.Length - 1)
                {
                    GameObject lineObj = new GameObject($"Line_{i}");
                    lineObj.transform.SetParent(dotContainer, false);

                    RectTransform lineRect = lineObj.AddComponent<RectTransform>();
                    lineRect.anchorMin = new Vector2(0.5f, 0.5f);
                    lineRect.anchorMax = new Vector2(0.5f, 0.5f);
                    lineRect.pivot = new Vector2(0.5f, 0.5f);
                    lineRect.sizeDelta = new Vector2(dotSpacing, lineWidth);
                    lineRect.anchoredPosition = new Vector2(startOffset + i * dotSpacing + dotSpacing / 2, 0);

                    Image lineImage = lineObj.AddComponent<Image>();
                    lineImage.color = GetColorForCode(code);

                    connectionLines.Add(lineImage);
                }
            }

            currentDotIndex = 0;
            UpdateActiveState();
        }

        /// <summary>
        /// Configure dot appearance based on spawn code.
        /// </summary>
        private void ConfigureDot(Image dotImage, char code)
        {
            RectTransform rect = dotImage.GetComponent<RectTransform>();

            if (code == '0')
            {
                // Empty tick: smaller gray circle (outline style)
                dotImage.color = emptyColor;
                rect.sizeDelta = Vector2.one * 16f;
                dotImage.sprite = CreateCircleSprite();
            }
            else if (code == '1')
            {
                // Enemy: red filled circle
                dotImage.color = enemyColor;
                rect.sizeDelta = Vector2.one * 20f;
                dotImage.sprite = CreateCircleSprite();
            }
            else if (code == '2')
            {
                // Resource: green filled circle
                dotImage.color = resourceColor;
                rect.sizeDelta = Vector2.one * 20f;
                dotImage.sprite = CreateCircleSprite();
            }
            else if (code == '3')
            {
                // Boss (future): large red dot
                dotImage.color = enemyColor;
                rect.sizeDelta = Vector2.one * 40f;
                dotImage.sprite = CreateCircleSprite();
            }
        }

        /// <summary>
        /// Create a circle sprite procedurally.
        /// </summary>
        private Sprite CreateCircleSprite()
        {
            int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 1;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    pixels[y * size + x] = distance <= radius ? Color.white : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// Get line color based on spawn code.
        /// </summary>
        private Color GetColorForCode(char code)
        {
            if (code == '1' || code == '3') return enemyColor; // Enemy/Boss
            if (code == '2') return resourceColor; // Resource
            return emptyColor; // Empty
        }

        /// <summary>
        /// Advance to next dot in the sequence.
        /// Called by WaveManager when a spawn event occurs.
        /// </summary>
        public void AdvanceDot()
        {
            if (currentDotIndex < spawnDots.Count - 1)
            {
                currentDotIndex++;
                UpdateActiveState();
            }
        }

        /// <summary>
        /// Update visual state of all dots (active/completed/upcoming).
        /// </summary>
        private void UpdateActiveState()
        {
            // Stop any existing pulse
            if (pulseCoroutine != null)
            {
                StopCoroutine(pulseCoroutine);
                pulseCoroutine = null;
            }

            for (int i = 0; i < spawnDots.Count; i++)
            {
                Image dotImage = spawnDots[i].GetComponent<Image>();

                if (i == currentDotIndex)
                {
                    // Active: Pulse animation
                    pulseCoroutine = StartCoroutine(PulseDot(spawnDots[i].transform));
                }
                else if (i < currentDotIndex)
                {
                    // Completed: Fade to 50% opacity
                    Color c = dotImage.color;
                    c.a = 0.5f;
                    dotImage.color = c;
                    spawnDots[i].transform.localScale = Vector3.one;
                }
                else
                {
                    // Upcoming: Full opacity
                    Color c = dotImage.color;
                    c.a = 1f;
                    dotImage.color = c;
                    spawnDots[i].transform.localScale = Vector3.one;
                }
            }

            // Fade completed lines to 40% opacity
            for (int i = 0; i < connectionLines.Count; i++)
            {
                if (i < currentDotIndex)
                {
                    Color c = connectionLines[i].color;
                    c.a = 0.4f;
                    connectionLines[i].color = c;
                }
                else
                {
                    Color c = connectionLines[i].color;
                    c.a = 1f;
                    connectionLines[i].color = c;
                }
            }
        }

        /// <summary>
        /// Pulse animation for active dot (scale 1.0 → 1.3 → 1.0).
        /// </summary>
        private IEnumerator PulseDot(Transform dotTransform)
        {
            while (true)
            {
                float duration = 0.5f;
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    float t = elapsed / duration;
                    float scale = Mathf.Lerp(1f, 1.3f, Mathf.Sin(t * Mathf.PI));
                    dotTransform.localScale = Vector3.one * scale;

                    elapsed += Time.deltaTime;
                    yield return null;
                }

                dotTransform.localScale = Vector3.one;
                yield return new WaitForSeconds(0.2f);
            }
        }

        /// <summary>
        /// Update countdown text showing ticks until next spawn.
        /// </summary>
        public void UpdateCountdown(int ticksUntilNextSpawn)
        {
            if (countdownText != null)
            {
                countdownText.text = $"Next spawn: {ticksUntilNextSpawn} ticks";
            }
        }

        /// <summary>
        /// Show peace period UI with grayed-out next wave preview.
        /// </summary>
        public void ShowPeacePeriod(int ticksRemaining, int nextWaveNumber, string nextSpawnCode)
        {
            ClearTimeline();

            if (waveNumberText != null)
            {
                waveNumberText.text = "Peace Period";
                waveNumberText.color = Color.white;
            }

            if (countdownText != null)
            {
                countdownText.text = $"{ticksRemaining} ticks until Wave {nextWaveNumber}";
            }
        }

        /// <summary>
        /// Clear all dots and lines from the timeline.
        /// </summary>
        private void ClearTimeline()
        {
            if (pulseCoroutine != null)
            {
                StopCoroutine(pulseCoroutine);
                pulseCoroutine = null;
            }

            foreach (GameObject dot in spawnDots)
            {
                if (dot != null) Destroy(dot);
            }
            spawnDots.Clear();

            foreach (Image line in connectionLines)
            {
                if (line != null) Destroy(line.gameObject);
            }
            connectionLines.Clear();

            currentDotIndex = 0;
        }

        private void OnDestroy()
        {
            ClearTimeline();
        }
    }
}
