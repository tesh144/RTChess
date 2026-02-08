using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace ClockworkGrid
{
    /// <summary>
    /// Spawns coin icons at a world position that burst upward, pause,
    /// then fly to the TokenUI icon. One coin per token earned.
    /// Also handles "lost" coins (enemy mining) that shatter and fall.
    /// </summary>
    public class CoinFlyEffect : MonoBehaviour
    {
        public static CoinFlyEffect Instance { get; private set; }

        [Header("Animation Timing")]
        [SerializeField] private float burstDuration = 0.3f;
        [SerializeField] private float pauseDuration = 0.25f;
        [SerializeField] private float flyDuration = 0.45f;

        [Header("Burst Settings")]
        [SerializeField] private float burstRadius = 80f; // Spread in screen pixels
        [SerializeField] private float burstUpward = 120f; // Upward burst distance in pixels

        [Header("Coin Size")]
        [SerializeField] private float coinSize = 32f;

        [Header("Stagger")]
        [SerializeField] private float staggerDelay = 0.05f; // Delay between each coin spawning

        [Header("Shatter Settings")]
        [SerializeField] private float shatterDuration = 0.6f;
        [SerializeField] private int fragmentsPerCoin = 3;
        [SerializeField] private float fragmentSpread = 100f;
        [SerializeField] private float fragmentGravity = 400f; // Pixels per second squared
        [SerializeField] private Color lostCoinColor = new Color(0.8f, 0.2f, 0.2f); // Dark red

        private Canvas overlayCanvas;
        private RectTransform canvasRect;

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
            // Find or create a screen-space overlay canvas for the coins
            overlayCanvas = GetComponentInParent<Canvas>();
            if (overlayCanvas == null)
            {
                // Try to find an existing overlay canvas
                Canvas[] canvases = FindObjectsOfType<Canvas>();
                foreach (Canvas c in canvases)
                {
                    if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        overlayCanvas = c;
                        break;
                    }
                }
            }

            if (overlayCanvas != null)
            {
                canvasRect = overlayCanvas.GetComponent<RectTransform>();
            }
        }

        /// <summary>
        /// Spawn coin fly effect from a world position toward the token UI (player earned).
        /// </summary>
        public void SpawnCoins(Vector3 worldPosition, int coinCount)
        {
            if (overlayCanvas == null || coinCount <= 0) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 screenPos = cam.WorldToScreenPoint(worldPosition + Vector3.up * 1.0f);
            if (screenPos.z < 0) return;

            StartCoroutine(SpawnCoinsSequence(screenPos, coinCount));
        }

        /// <summary>
        /// Spawn "lost" coins that burst up then shatter and fall (enemy mined).
        /// </summary>
        public void SpawnLostCoins(Vector3 worldPosition, int coinCount)
        {
            if (overlayCanvas == null || coinCount <= 0) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 screenPos = cam.WorldToScreenPoint(worldPosition + Vector3.up * 1.0f);
            if (screenPos.z < 0) return;

            StartCoroutine(SpawnLostCoinsSequence(screenPos, coinCount));
        }

        private IEnumerator SpawnCoinsSequence(Vector3 screenStartPos, int coinCount)
        {
            int visualCount = Mathf.Min(coinCount, 8);
            for (int i = 0; i < visualCount; i++)
            {
                SpawnSingleCoin(screenStartPos, i, visualCount);
                if (staggerDelay > 0f && i < visualCount - 1)
                    yield return new WaitForSeconds(staggerDelay);
            }
        }

        private IEnumerator SpawnLostCoinsSequence(Vector3 screenStartPos, int coinCount)
        {
            int visualCount = Mathf.Min(coinCount, 8);
            for (int i = 0; i < visualCount; i++)
            {
                SpawnSingleLostCoin(screenStartPos, i, visualCount);
                if (staggerDelay > 0f && i < visualCount - 1)
                    yield return new WaitForSeconds(staggerDelay);
            }
        }

        #region Player Coins (fly to UI)

        private void SpawnSingleCoin(Vector3 screenStartPos, int index, int total)
        {
            GameObject coinObj = new GameObject($"FlyingCoin_{index}");
            coinObj.transform.SetParent(overlayCanvas.transform, false);

            RectTransform coinRect = coinObj.AddComponent<RectTransform>();
            coinRect.sizeDelta = new Vector2(coinSize, coinSize);

            Image coinImage = coinObj.AddComponent<Image>();

            // Use natural sprite color (white tint preserves original colors)
            if (TokenUI.Instance != null)
            {
                Sprite iconSprite = TokenUI.Instance.GetIconSprite();
                if (iconSprite != null)
                    coinImage.sprite = iconSprite;
            }
            coinImage.color = Color.white;

            coinRect.position = screenStartPos;

            // Calculate random burst offset
            float angle = (360f / total) * index + Random.Range(-20f, 20f);
            float rad = angle * Mathf.Deg2Rad;
            float spreadX = Mathf.Cos(rad) * burstRadius * Random.Range(0.6f, 1f);
            float spreadY = burstUpward + Mathf.Sin(rad) * burstRadius * 0.5f + Random.Range(-20f, 20f);

            Vector3 burstTarget = screenStartPos + new Vector3(spreadX, spreadY, 0f);

            Vector3 flyTarget = Vector3.zero;
            if (TokenUI.Instance != null)
            {
                flyTarget = TokenUI.Instance.GetIconScreenPosition();
            }

            StartCoroutine(AnimateCoin(coinObj, coinRect, coinImage, screenStartPos, burstTarget, flyTarget));
        }

        private IEnumerator AnimateCoin(GameObject coinObj, RectTransform coinRect, Image coinImage,
            Vector3 startPos, Vector3 burstPos, Vector3 flyTarget)
        {
            // Phase 1: Burst upward with ease-out
            float elapsed = 0f;
            while (elapsed < burstDuration)
            {
                if (coinObj == null) yield break;
                float t = elapsed / burstDuration;
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                coinRect.position = Vector3.Lerp(startPos, burstPos, eased);

                float scale = Mathf.Lerp(0.3f, 1f, eased);
                coinRect.localScale = Vector3.one * scale;

                elapsed += Time.deltaTime;
                yield return null;
            }
            if (coinObj == null) yield break;
            coinRect.position = burstPos;
            coinRect.localScale = Vector3.one;

            // Phase 2: Pause (slight hover)
            elapsed = 0f;
            while (elapsed < pauseDuration)
            {
                if (coinObj == null) yield break;
                float bob = Mathf.Sin(elapsed / pauseDuration * Mathf.PI) * 5f;
                coinRect.position = burstPos + new Vector3(0f, bob, 0f);
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Phase 3: Fly to token UI with accelerating ease-in
            if (coinObj == null) yield break;
            Vector3 flyStart = coinRect.position;
            elapsed = 0f;
            while (elapsed < flyDuration)
            {
                if (coinObj == null) yield break;
                float t = elapsed / flyDuration;
                float eased = t * t * t;
                coinRect.position = Vector3.Lerp(flyStart, flyTarget, eased);

                float scale = Mathf.Lerp(1f, 0.5f, eased);
                coinRect.localScale = Vector3.one * scale;

                if (t > 0.7f)
                {
                    Color c = coinImage.color;
                    c.a = Mathf.Lerp(1f, 0.6f, (t - 0.7f) / 0.3f);
                    coinImage.color = c;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (coinObj != null)
                Destroy(coinObj);
        }

        #endregion

        #region Lost Coins (shatter & fall)

        private void SpawnSingleLostCoin(Vector3 screenStartPos, int index, int total)
        {
            GameObject coinObj = new GameObject($"LostCoin_{index}");
            coinObj.transform.SetParent(overlayCanvas.transform, false);

            RectTransform coinRect = coinObj.AddComponent<RectTransform>();
            coinRect.sizeDelta = new Vector2(coinSize, coinSize);

            Image coinImage = coinObj.AddComponent<Image>();

            // Use natural sprite, no tint
            if (TokenUI.Instance != null)
            {
                Sprite iconSprite = TokenUI.Instance.GetIconSprite();
                if (iconSprite != null)
                    coinImage.sprite = iconSprite;
            }
            coinImage.color = Color.white;

            coinRect.position = screenStartPos;

            // Calculate random burst offset (same as player coins)
            float angle = (360f / total) * index + Random.Range(-20f, 20f);
            float rad = angle * Mathf.Deg2Rad;
            float spreadX = Mathf.Cos(rad) * burstRadius * Random.Range(0.6f, 1f);
            float spreadY = burstUpward + Mathf.Sin(rad) * burstRadius * 0.5f + Random.Range(-20f, 20f);

            Vector3 burstTarget = screenStartPos + new Vector3(spreadX, spreadY, 0f);

            StartCoroutine(AnimateLostCoin(coinObj, coinRect, coinImage, screenStartPos, burstTarget));
        }

        private IEnumerator AnimateLostCoin(GameObject coinObj, RectTransform coinRect, Image coinImage,
            Vector3 startPos, Vector3 burstPos)
        {
            // Phase 1: Burst upward (same as player coins)
            float elapsed = 0f;
            while (elapsed < burstDuration)
            {
                if (coinObj == null) yield break;
                float t = elapsed / burstDuration;
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                coinRect.position = Vector3.Lerp(startPos, burstPos, eased);

                float scale = Mathf.Lerp(0.3f, 1f, eased);
                coinRect.localScale = Vector3.one * scale;

                elapsed += Time.deltaTime;
                yield return null;
            }
            if (coinObj == null) yield break;
            coinRect.position = burstPos;
            coinRect.localScale = Vector3.one;

            // Phase 2: Brief hold, then tint red
            elapsed = 0f;
            float holdDuration = pauseDuration * 1.5f; // Hold slightly longer for dramatic effect
            while (elapsed < holdDuration)
            {
                if (coinObj == null) yield break;
                float t = elapsed / holdDuration;

                // Gentle bob
                float bob = Mathf.Sin(t * Mathf.PI) * 5f;
                coinRect.position = burstPos + new Vector3(0f, bob, 0f);

                // Tint toward red during hold
                coinImage.color = Color.Lerp(Color.white, lostCoinColor, t);

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Phase 3: Shatter - spawn fragments, destroy original
            if (coinObj == null) yield break;
            Vector3 shatterPos = coinRect.position;
            Sprite coinSprite = coinImage.sprite;

            // Destroy the original coin
            Destroy(coinObj);

            // Spawn fragments
            for (int f = 0; f < fragmentsPerCoin; f++)
            {
                SpawnFragment(shatterPos, coinSprite, f);
            }
        }

        private void SpawnFragment(Vector3 startPos, Sprite sprite, int index)
        {
            GameObject fragObj = new GameObject($"CoinFragment_{index}");
            fragObj.transform.SetParent(overlayCanvas.transform, false);

            RectTransform fragRect = fragObj.AddComponent<RectTransform>();
            float fragSize = coinSize * Random.Range(0.3f, 0.55f);
            fragRect.sizeDelta = new Vector2(fragSize, fragSize);

            Image fragImage = fragObj.AddComponent<Image>();
            if (sprite != null)
                fragImage.sprite = sprite;
            fragImage.color = lostCoinColor;

            fragRect.position = startPos;

            // Random velocity: mostly sideways and downward
            float angle = Random.Range(200f, 340f); // Below horizontal arc
            float rad = angle * Mathf.Deg2Rad;
            float speed = fragmentSpread * Random.Range(0.5f, 1.2f);
            Vector2 velocity = new Vector2(Mathf.Cos(rad) * speed, Mathf.Sin(rad) * speed);

            // Random spin
            float spinSpeed = Random.Range(-360f, 360f);

            StartCoroutine(AnimateFragment(fragObj, fragRect, fragImage, velocity, spinSpeed));
        }

        private IEnumerator AnimateFragment(GameObject fragObj, RectTransform fragRect, Image fragImage,
            Vector2 velocity, float spinSpeed)
        {
            float elapsed = 0f;
            Vector3 pos = fragRect.position;

            while (elapsed < shatterDuration)
            {
                if (fragObj == null) yield break;
                float t = elapsed / shatterDuration;
                float dt = Time.deltaTime;

                // Apply gravity to velocity
                velocity.y -= fragmentGravity * dt;

                // Move
                pos.x += velocity.x * dt;
                pos.y += velocity.y * dt;
                fragRect.position = pos;

                // Spin
                fragRect.Rotate(0f, 0f, spinSpeed * dt);

                // Shrink and fade
                float fadeT = Mathf.Clamp01((t - 0.3f) / 0.7f); // Start fading after 30%
                float scale = Mathf.Lerp(1f, 0f, fadeT * fadeT);
                fragRect.localScale = Vector3.one * scale;

                Color c = fragImage.color;
                c.a = 1f - fadeT;
                fragImage.color = c;

                elapsed += dt;
                yield return null;
            }

            if (fragObj != null)
                Destroy(fragObj);
        }

        #endregion
    }
}
