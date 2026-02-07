using System;
using UnityEngine;
using TMPro;

namespace ClockworkGrid
{
    /// <summary>
    /// Manages the player's resource token economy.
    /// Tracks token count and spawns floating text popups on gain.
    /// </summary>
    public class ResourceTokenManager : MonoBehaviour
    {
        public static ResourceTokenManager Instance { get; private set; }

        private int currentTokens;

        [Header("Starting Tokens")]
        [SerializeField] private int startingTokens = 10;

        public int CurrentTokens => currentTokens;

        /// <summary>
        /// Fired when token count changes. Passes new total.
        /// </summary>
        public event Action<int> OnTokensChanged;

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
            // Give player starting tokens
            if (startingTokens > 0)
            {
                currentTokens = startingTokens;
                OnTokensChanged?.Invoke(currentTokens);
            }
        }

        public void AddTokens(int amount, Vector3? worldPosition = null)
        {
            if (amount <= 0) return;

            currentTokens += amount;
            OnTokensChanged?.Invoke(currentTokens);

            // Spawn floating text at the world position
            if (worldPosition.HasValue)
            {
                SpawnFloatingText($"+{amount}", worldPosition.Value);
            }
        }

        public bool SpendTokens(int amount)
        {
            if (amount > currentTokens) return false;

            currentTokens -= amount;
            OnTokensChanged?.Invoke(currentTokens);
            return true;
        }

        public bool HasEnoughTokens(int cost)
        {
            return currentTokens >= cost;
        }

        private void SpawnFloatingText(string text, Vector3 worldPos)
        {
            GameObject floatObj = new GameObject("FloatingText");
            floatObj.transform.position = worldPos + Vector3.up * 1.5f;

            // Create a world-space canvas for the text
            Canvas canvas = floatObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 200;

            RectTransform canvasRect = floatObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(2f, 0.5f);
            canvasRect.localScale = Vector3.one * 0.02f;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(floatObj.transform, false);

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 36;
            tmp.color = new Color(1f, 0.9f, 0.2f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            // Add floating animation component
            FloatingTextAnimation anim = floatObj.AddComponent<FloatingTextAnimation>();
            anim.Initialize(tmp);
        }
    }

    /// <summary>
    /// Animates floating text: drifts upward and fades out over ~1 second.
    /// </summary>
    public class FloatingTextAnimation : MonoBehaviour
    {
        private TextMeshProUGUI textComponent;
        private float elapsed;
        private const float Duration = 1.0f;
        private const float RiseSpeed = 1.5f;
        private Vector3 startPos;

        public void Initialize(TextMeshProUGUI text)
        {
            textComponent = text;
            startPos = transform.position;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = elapsed / Duration;

            // Rise upward
            transform.position = startPos + Vector3.up * (RiseSpeed * t);

            // Fade out
            if (textComponent != null)
            {
                Color c = textComponent.color;
                c.a = 1f - t;
                textComponent.color = c;
            }

            // Billboard to face camera
            Camera cam = Camera.main;
            if (cam != null)
            {
                transform.LookAt(transform.position + cam.transform.forward);
            }

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
