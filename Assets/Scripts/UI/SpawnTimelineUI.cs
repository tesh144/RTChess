using UnityEngine;
using UnityEngine.UI;
using TMPro;
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

        [Header("Prefab References")]
        [SerializeField] private GameObject nodePointPrefab; // Custom timeline dot prefab with toggle children

        [Header("UI References (Assign in Inspector)")]
        [SerializeField] private GameObject timelineHolder; // The timeline panel/GameObject to show/hide
        [SerializeField] private TextMeshProUGUI waveNumberText;
        [SerializeField] private Transform dotContainer;

        [Header("Visual Settings")]
        [SerializeField] private float dotSpacing = 60f;
        [SerializeField] private float lineWidth = 3f;
        [SerializeField] private Color enemyColor = new Color(0.85f, 0.25f, 0.25f); // #D84040 red
        [SerializeField] private Color resourceColor = new Color(0.25f, 0.85f, 0.31f); // #40D850 green
        [SerializeField] private Color emptyColor = new Color(0.5f, 0.5f, 0.5f); // Gray

        [Header("Animation Settings")]
        [SerializeField] private bool enableSlideAnimation = true;
        [SerializeField] private float slideDownDistance = 100f; // Distance to slide from
        [SerializeField] private float slideDownDuration = 0.5f; // Animation duration

        // State
        private List<GameObject> spawnDots = new List<GameObject>();
        private List<Image> connectionLines = new List<Image>();
        private int currentDotIndex = 0;
        private string currentSpawnCode;
        private Coroutine pulseCoroutine;
        private RectTransform rectTransform;
        private Vector2 originalAnchoredPosition;
        private static Sprite cachedCircleSprite; // Cached to avoid recreating every wave

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Cache RectTransform and original position for animation
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                originalAnchoredPosition = rectTransform.anchoredPosition;
            }
        }

        private void Start()
        {
            // Hide UI container at runtime start (allows UI to stay visible in Editor for design work)
            // Only activates after player places first unit
            if (timelineHolder != null && (WaveManager.Instance == null || !WaveManager.Instance.HasWaveStarted))
            {
                Debug.Log("[SpawnTimelineUI] Hiding timeline holder at start");
                timelineHolder.SetActive(false);
            }
            else if (timelineHolder == null)
            {
                Debug.LogError("[SpawnTimelineUI] timelineHolder is not assigned! Please assign it in the Inspector.");
            }
        }

        /// <summary>
        /// Show countdown UI when player places first unit with slide-down animation.
        /// </summary>
        public void ShowCountdown(int waveNumber, int startingCount)
        {
            ClearTimeline(); // Remove any dots from previous wave

            if (timelineHolder != null)
            {
                timelineHolder.SetActive(true);
            }

            // Hide dot container during countdown
            if (dotContainer != null)
            {
                dotContainer.gameObject.SetActive(false);
            }

            if (waveNumberText != null)
            {
                waveNumberText.text = $"WAVE {waveNumber} in... {startingCount}";
                waveNumberText.fontSize = 48;
                waveNumberText.color = Color.white;
            }

            // Trigger slide-down animation
            if (enableSlideAnimation && rectTransform != null)
            {
                StartCoroutine(SlideDownAnimation());
            }
        }

        /// <summary>
        /// Update countdown display each tick.
        /// </summary>
        public void UpdateCountdown(int waveNumber, int remaining)
        {
            if (waveNumberText != null && remaining > 0)
            {
                waveNumberText.text = $"WAVE {waveNumber} in... {remaining}";
            }
        }

        /// <summary>
        /// Animate panel sliding down from above the screen.
        /// </summary>
        private IEnumerator SlideDownAnimation()
        {
            if (rectTransform == null) yield break;

            // Start position (above screen)
            Vector2 startPos = originalAnchoredPosition + new Vector2(0, slideDownDistance);
            Vector2 endPos = originalAnchoredPosition;

            rectTransform.anchoredPosition = startPos;

            float elapsed = 0f;
            while (elapsed < slideDownDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / slideDownDuration;

                // Smooth ease-out curve
                float smoothT = 1f - Mathf.Pow(1f - t, 3f);

                rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, smoothT);
                yield return null;
            }

            // Ensure final position is exact
            rectTransform.anchoredPosition = endPos;
        }

        /// <summary>
        /// Initialize wave timeline with spawn code.
        /// Called by WaveManager when a wave starts executing.
        /// </summary>
        public void InitializeWave(int waveNumber, string spawnCode)
        {
            ClearTimeline();

            // Activate timeline holder (may be hidden at start)
            if (timelineHolder != null)
            {
                timelineHolder.SetActive(true);
            }

            // Show dot container
            if (dotContainer != null)
            {
                dotContainer.gameObject.SetActive(true);
            }

            // Trigger slide-down animation
            if (enableSlideAnimation && rectTransform != null)
            {
                StartCoroutine(SlideDownAnimation());
            }

            // IMPORTANT: Clear any placeholder/design-time children from dotContainer
            // This removes manually-placed dots from the Unity scene
            if (dotContainer != null)
            {
                foreach (Transform child in dotContainer)
                {
                    Destroy(child.gameObject);
                }
            }

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
                waveNumberText.fontStyle = FontStyles.Bold;
                waveNumberText.color = enemyColor;
            }

            // Calculate offset to center dots
            float totalWidth = (spawnCode.Length - 1) * dotSpacing;
            float startOffset = -totalWidth / 2f;

            // Create dots and connecting lines
            for (int i = 0; i < spawnCode.Length; i++)
            {
                char code = spawnCode[i];

                GameObject dotObj;

                // Use prefab if assigned, otherwise create runtime UI
                if (nodePointPrefab != null)
                {
                    // Instantiate custom prefab
                    dotObj = Instantiate(nodePointPrefab, dotContainer, false);
                    dotObj.name = $"Dot_{i}_{code}";

                    RectTransform dotRect = dotObj.GetComponent<RectTransform>();
                    if (dotRect != null)
                    {
                        dotRect.anchoredPosition = new Vector2(startOffset + i * dotSpacing, 0);
                    }

                    // Toggle correct child based on code
                    ToggleDotChildren(dotObj, code);
                }
                else
                {
                    // Fallback: Create runtime UI (legacy behavior)
                    dotObj = new GameObject($"Dot_{i}_{code}");
                    dotObj.transform.SetParent(dotContainer, false);

                    RectTransform dotRect = dotObj.AddComponent<RectTransform>();
                    dotRect.anchorMin = new Vector2(0.5f, 0.5f);
                    dotRect.anchorMax = new Vector2(0.5f, 0.5f);
                    dotRect.pivot = new Vector2(0.5f, 0.5f);
                    dotRect.anchoredPosition = new Vector2(startOffset + i * dotSpacing, 0);

                    Image dotImage = dotObj.AddComponent<Image>();
                    ConfigureDot(dotImage, code);
                }

                spawnDots.Add(dotObj);
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
        /// Toggle correct child in nodePointPrefab based on spawn code.
        /// Prefab should have children: Blank (0), Enemy (1), Resource (2), Boss (3)
        /// </summary>
        private void ToggleDotChildren(GameObject dotObj, char code)
        {
            // Deactivate all children first
            for (int i = 0; i < dotObj.transform.childCount; i++)
            {
                dotObj.transform.GetChild(i).gameObject.SetActive(false);
            }

            // Activate correct child based on code
            int childIndex = -1;
            if (code == '0') childIndex = 0; // Blank
            else if (code == '1') childIndex = 1; // Enemy
            else if (code == '2') childIndex = 2; // Resource
            else if (code == '3') childIndex = 3; // Boss

            if (childIndex >= 0 && childIndex < dotObj.transform.childCount)
            {
                dotObj.transform.GetChild(childIndex).gameObject.SetActive(true);
            }
            else
            {
                Debug.LogWarning($"SpawnTimelineUI: NodePointPrefab missing child at index {childIndex} for code '{code}'");
            }
        }

        /// <summary>
        /// Create a circle sprite procedurally (cached to avoid allocations).
        /// </summary>
        private Sprite CreateCircleSprite()
        {
            // Return cached sprite if already created
            if (cachedCircleSprite != null)
                return cachedCircleSprite;

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

            cachedCircleSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return cachedCircleSprite;
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
                if (spawnDots[i] == null) continue; // Skip null entries

                Image dotImage = spawnDots[i].GetComponent<Image>();
                if (dotImage == null) continue; // Skip if no Image component

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
        /// Show peace period UI with grayed-out next wave preview.
        /// </summary>
        public void ShowPeacePeriod(int ticksRemaining, int nextWaveNumber, string nextSpawnCode)
        {
            ClearTimeline();

            // Hide dots during peace period
            if (dotContainer != null)
            {
                dotContainer.gameObject.SetActive(false);
            }

            if (waveNumberText != null)
            {
                waveNumberText.text = $"WAVE {nextWaveNumber} in... {ticksRemaining}";
                waveNumberText.fontSize = 48;
                waveNumberText.color = Color.white;
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
