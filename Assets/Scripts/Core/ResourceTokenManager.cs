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

            // Track gold collection for objectives
            if (WaveManager.Instance != null)
                WaveManager.Instance.OnGoldCollected(amount);

            // Spawn coin fly effect toward the token UI
            if (worldPosition.HasValue && CoinFlyEffect.Instance != null)
            {
                CoinFlyEffect.Instance.SpawnCoins(worldPosition.Value, amount);
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

            // Use world-space TextMeshPro (not TextMeshProUGUI which needs a canvas)
            TextMeshPro tmp = floatObj.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 6;
            tmp.color = new Color(1f, 0.9f, 0.2f); // Gold
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.sortingOrder = 200;

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
        private TextMeshPro textComponent;
        private float elapsed;
        private const float Duration = 1.0f;
        private const float RiseSpeed = 1.5f;
        private Vector3 startPos;

        public void Initialize(TextMeshPro text)
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
