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
        private const float COIN_COLLECT_DEBOUNCE = 0.08f;

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
        }

        // ─────────────────────────────────────────────────────────────────
        // Placement Sounds
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Play when an object is successfully placed on the grid.</summary>
        public void PlayPlacement()
        {
            PlaySFX(placementDrop, randomPitch: true);
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

        /// <summary>Play when a single loot particle arrives at the resource bar (debounced).</summary>
        public void PlayCoinCollect()
        {
            if (Time.unscaledTime - lastCoinCollectTime < COIN_COLLECT_DEBOUNCE) return;
            lastCoinCollectTime = Time.unscaledTime;
            PlaySFX(coinCollect, randomPitch: true, pitchRange: 0.1f);
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
    }
}
