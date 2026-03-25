#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Unified sound effects manager for all game audio.
    /// Replaces the old SFXManager and PlacementAudioManager with a single
    /// categorized system. All AudioClip fields are assignable in the Inspector.
    ///
    /// Usage: GameSFXManager.Instance.PlayPlacement(); // etc.
    ///
    /// Supports pitch variation for natural-sounding repetitive effects,
    /// and debouncing for rapid-fire events (fog reveal, coin collect).
    /// </summary>
    public class GameSFXManager : MonoBehaviour
    {
        public static GameSFXManager Instance { get; private set; }

        // ─── Audio Sources ──────────────────────────────────────────────
        private AudioSource sfxSource;       // Main SFX channel
        private AudioSource uiSource;        // UI-specific channel (unaffected by game pause)
        private AudioSource coinSource;      // Dedicated channel for coin collect escalation (isolated pitch)

        // ─── Volume ─────────────────────────────────────────────────────
        [Header("Volume")]
        [Range(0f, 1f)] public float masterVolume = 0.8f;
        [Range(0f, 1f)] public float uiVolume = 0.9f;

        // ─── Placement ─────────────────────────────────────────────────
        [Header("Placement")]
        public AudioClip placementDrop;
        public AudioClip placementError;
        public AudioClip objectRemove;

        // ─── UI / Cards ─────────────────────────────────────────────────
        [Header("UI & Cards")]
        public AudioClip buttonClick;
        public AudioClip cardDraw;
        public AudioClip cardSlideIn;
        public AudioClip dragStart;
        public AudioClip dragCancel;
        public AudioClip errorBuzz;
        public AudioClip successChime;
        public AudioClip drawReady;            // draw button cooldown finished

        // ─── Combat ─────────────────────────────────────────────────────
        [Header("Combat")]
        public AudioClip hitImpact;         // interact_strong landing
        public AudioClip hitWeak;           // interact_weak / light tap
        public AudioClip entityDeath;       // entity HP reaches 0
        public AudioClip damageImpact;      // target takes damage (jiggle feedback)

        // ─── Resources & Loot ───────────────────────────────────────────
        [Header("Resources & Loot")]
        public AudioClip coinCollect;       // single coin jingle
        public AudioClip lootBurst;         // loot particles spawn
        public AudioClip lootArrival;       // particle reaches bar
        public AudioClip resourceDepleted;  // resource node fully harvested

        // ─── Production ─────────────────────────────────────────────────
        [Header("Production")]
        public AudioClip timerComplete;     // building timer done
        public AudioClip popupAppear;       // reward popup bounces in
        public AudioClip rewardCollect;     // player taps to collect reward
        public AudioClip handFull;          // can't collect, hand is full

        // ─── Discovery ──────────────────────────────────────────────────
        [Header("Discovery")]
        public AudioClip fogReveal;         // fog cleared (debounced per batch)

        // ─── Ambient ────────────────────────────────────────────────────
        [Header("Ambient (Optional)")]
        public AudioClip clockTick;         // subtle tick on each interval

        // ─── Pitch Variation ────────────────────────────────────────────
        [Header("Pitch Variation")]
        [Range(0f, 0.2f)] public float pitchVariation = 0.05f;

        // ─── Debouncing ─────────────────────────────────────────────────
        private float lastFogRevealTime = -1f;
        private const float FOG_REVEAL_DEBOUNCE = 0.15f; // Only play once per 150ms

        private float lastCoinCollectTime = -1f;
        private int coinCollectBurstCount = 0;
        private const int MAX_COIN_COLLECT_BURST = 15;
        private const float COIN_BURST_RESET_TIME = 1.5f;
        private const float COIN_STAGGER_INTERVAL = 0.06f;   // 60ms between queued sounds — guarantees distinct dings
        private readonly System.Collections.Generic.Queue<int> coinSoundQueue = new System.Collections.Generic.Queue<int>();
        private float nextCoinPlayTime = 0f;

        // ─────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Main SFX source (affected by Time.timeScale)
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;

            // UI source (ignoreListenerPause so UI sounds play even when game is paused)
            uiSource = gameObject.AddComponent<AudioSource>();
            uiSource.playOnAwake = false;
            uiSource.spatialBlend = 0f;
            uiSource.ignoreListenerPause = true;

            // Dedicated coin collect source — isolated from sfxSource so other SFX
            // can't stomp the escalating pitch mid-burst
            coinSource = gameObject.AddComponent<AudioSource>();
            coinSource.playOnAwake = false;
            coinSource.spatialBlend = 0f;

            // Generate procedural fallback clips for critical sounds
            GenerateProceduralFallbacks();
        }

        private void Update()
        {
            DrainCoinSoundQueue();
        }

        // ─────────────────────────────────────────────────────────────────
        // Placement Sounds
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Play when an object is successfully placed on the grid.</summary>
        public void PlayPlacement()
        {
            AudioClip clip = placementDrop != null ? placementDrop : proceduralPlacementClip;
            PlaySFX(clip, randomPitch: true);
        }

        /// <summary>Play when placement validation fails (invalid cell, can't afford).</summary>
        public void PlayPlacementError()
        {
            PlayUI(placementError);
        }

        /// <summary>Play when an object is removed from the grid.</summary>
        public void PlayRemoval()
        {
            PlaySFX(objectRemove, randomPitch: true);
        }

        // ─────────────────────────────────────────────────────────────────
        // UI & Card Sounds
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Play on any UI button press.</summary>
        public void PlayButtonClick()
        {
            PlayUI(buttonClick);
        }

        /// <summary>Play when a card is drawn from the deck.</summary>
        public void PlayCardDraw()
        {
            PlayUI(cardDraw);
        }

        /// <summary>Play when a card slides into the dock bar.</summary>
        public void PlayCardSlideIn()
        {
            PlayUI(cardSlideIn);
        }

        /// <summary>Play when draw button cooldown finishes and becomes available.</summary>
        public void PlayDrawReady()
        {
            PlayUI(drawReady);
        }

        /// <summary>Play when player starts dragging a card.</summary>
        public void PlayDragStart()
        {
            PlayUI(dragStart);
        }

        /// <summary>Play when drag is cancelled (released in invalid area).</summary>
        public void PlayDragCancel()
        {
            PlayUI(dragCancel);
        }

        /// <summary>Play for generic error feedback (can't afford, hand full, etc.).</summary>
        public void PlayError()
        {
            PlayUI(errorBuzz);
        }

        /// <summary>Play for positive feedback (worker acquired, successful action).</summary>
        public void PlaySuccess()
        {
            PlayUI(successChime);
        }

        // ─────────────────────────────────────────────────────────────────
        // Combat Sounds
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Play when interact_strong animation lands (worker attacks target).</summary>
        public void PlayHitImpact()
        {
            PlaySFX(hitImpact, randomPitch: true);
        }

        /// <summary>Play for interact_weak (light tap, idle interaction).</summary>
        public void PlayHitWeak()
        {
            PlaySFX(hitWeak, randomPitch: true);
        }

        /// <summary>Play when an entity's HP reaches zero.</summary>
        public void PlayEntityDeath()
        {
            PlaySFX(entityDeath);
        }

        /// <summary>Play when a target takes damage (hit feedback, separate from attack animation).</summary>
        public void PlayDamageImpact()
        {
            PlaySFX(damageImpact, randomPitch: true);
        }

        // ─────────────────────────────────────────────────────────────────
        // Resource & Loot Sounds
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Play when loot particles burst from a resource node.</summary>
        public void PlayLootBurst()
        {
            PlaySFX(lootBurst);
        }

        /// <summary>
        /// Called when a loot particle arrives at the resource bar.
        /// Enqueues the sound for staggered playback — even if 10 particles arrive
        /// on the same frame, each produces a distinct, ascending-pitch ding
        /// spaced 60ms apart. No debounce needed; the queue handles spacing.
        /// </summary>
        public void PlayCoinCollect()
        {
            float timeSinceLast = Time.unscaledTime - lastCoinCollectTime;

            // Reset burst after a quiet period
            if (timeSinceLast > COIN_BURST_RESET_TIME)
                coinCollectBurstCount = 0;

            // Cap how many sounds queue per batch
            if (coinCollectBurstCount >= MAX_COIN_COLLECT_BURST) return;

            lastCoinCollectTime = Time.unscaledTime;
            coinCollectBurstCount++;

            // Enqueue the burst index — Update() drains at staggered intervals
            coinSoundQueue.Enqueue(coinCollectBurstCount);
        }

        /// <summary>
        /// Drains one queued coin sound per COIN_STAGGER_INTERVAL on the dedicated
        /// coinSource AudioSource. Guarantees every arrival is audibly distinct.
        /// </summary>
        private void DrainCoinSoundQueue()
        {
            if (coinSoundQueue.Count == 0) return;
            if (coinCollect == null || coinSource == null) return;
            if (Time.unscaledTime < nextCoinPlayTime) return;

            int burstIndex = coinSoundQueue.Dequeue();

            float pitch = 0.8f + (burstIndex * 0.12f) + Random.Range(-0.02f, 0.02f);
            float volume = masterVolume * Mathf.Lerp(0.85f, 1.6f, (float)burstIndex / MAX_COIN_COLLECT_BURST);

            coinSource.pitch = pitch;
            coinSource.PlayOneShot(coinCollect, volume);

            nextCoinPlayTime = Time.unscaledTime + COIN_STAGGER_INTERVAL;
        }

        /// <summary>Play when a loot particle reaches its final destination.</summary>
        public void PlayLootArrival()
        {
            PlaySFX(lootArrival);
        }

        /// <summary>Play when a resource node is fully depleted.</summary>
        public void PlayResourceDepleted()
        {
            PlaySFX(resourceDepleted);
        }

        // ─────────────────────────────────────────────────────────────────
        // Production Sounds
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Play when a building's production timer completes.</summary>
        public void PlayTimerComplete()
        {
            PlaySFX(timerComplete);
        }

        /// <summary>Play when a reward popup bounces into view.</summary>
        public void PlayPopupAppear()
        {
            PlaySFX(popupAppear);
        }

        /// <summary>Play when player taps popup to collect reward.</summary>
        public void PlayRewardCollect()
        {
            PlayUI(rewardCollect);
        }

        /// <summary>Play when player tries to collect but hand is full.</summary>
        public void PlayHandFull()
        {
            PlayUI(handFull);
        }

        // ─────────────────────────────────────────────────────────────────
        // Discovery Sounds
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Play when fog cells are revealed (debounced — one sound per batch).</summary>
        public void PlayFogReveal()
        {
            if (Time.unscaledTime - lastFogRevealTime < FOG_REVEAL_DEBOUNCE) return;
            lastFogRevealTime = Time.unscaledTime;
            PlaySFX(fogReveal, randomPitch: true, pitchRange: 0.08f);
        }

        // ─────────────────────────────────────────────────────────────────
        // Ambient Sounds
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Play a subtle tick on each interval timer beat.</summary>
        public void PlayClockTick()
        {
            PlaySFX(clockTick, volume: 0.3f);
        }

        // ─────────────────────────────────────────────────────────────────
        // Internal Playback
        // ─────────────────────────────────────────────────────────────────

        private void PlaySFX(AudioClip clip, bool randomPitch = false, float pitchRange = -1f, float volume = -1f)
        {
            if (clip == null || sfxSource == null) return;

            float vol = (volume > 0f) ? volume : masterVolume;
            float range = (pitchRange >= 0f) ? pitchRange : pitchVariation;
            sfxSource.pitch = randomPitch ? (1f + Random.Range(-range, range)) : 1f;
            sfxSource.PlayOneShot(clip, vol);
        }

        private void PlayUI(AudioClip clip, float volume = -1f)
        {
            if (clip == null || uiSource == null) return;

            float vol = (volume > 0f) ? volume : uiVolume;
            uiSource.pitch = 1f;
            uiSource.PlayOneShot(clip, vol);
        }

        // ─────────────────────────────────────────────────────────────────
        // Procedural Fallback Clips
        // ─────────────────────────────────────────────────────────────────

        private AudioClip proceduralPlacementClip;

        /// <summary>
        /// Generate simple procedural audio clips for critical sounds that
        /// MUST play even when Inspector AudioClip fields are unassigned.
        /// </summary>
        private void GenerateProceduralFallbacks()
        {
            proceduralPlacementClip = GeneratePlacementSound();
        }

        /// <summary>
        /// Generates a satisfying "thump + sparkle" placement sound.
        /// Short percussive hit (60ms) with a bright high-frequency tail (100ms).
        /// </summary>
        private static AudioClip GeneratePlacementSound()
        {
            int sampleRate = 44100;
            float duration = 0.18f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float tNorm = t / duration;

                // Layer 1: Low thump (80 Hz sine, fast decay)
                float thumpEnv = Mathf.Exp(-t * 35f);
                float thump = Mathf.Sin(2f * Mathf.PI * 80f * t) * thumpEnv * 0.5f;

                // Layer 2: Mid knock (200 Hz sine, medium decay)
                float knockEnv = Mathf.Exp(-t * 50f);
                float knock = Mathf.Sin(2f * Mathf.PI * 200f * t) * knockEnv * 0.3f;

                // Layer 3: High sparkle (800 Hz + 1200 Hz, delayed start, slow decay)
                float sparkleDelay = Mathf.Max(0f, t - 0.03f);
                float sparkleEnv = Mathf.Exp(-sparkleDelay * 20f) * Mathf.Clamp01(sparkleDelay * 50f);
                float sparkle = (Mathf.Sin(2f * Mathf.PI * 800f * t) +
                                 Mathf.Sin(2f * Mathf.PI * 1200f * t) * 0.5f) * sparkleEnv * 0.15f;

                samples[i] = Mathf.Clamp(thump + knock + sparkle, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("ProceduralPlacement", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
