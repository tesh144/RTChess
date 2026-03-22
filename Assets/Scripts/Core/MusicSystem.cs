#pragma warning disable CS0414, CS0219, CS0618
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Background music controller with lobby/battle crossfade.
///
/// Respects GameStateManager:
///   TitleScreen → silent (or optional title track)
///   Playing     → starts lobby track, then SwitchToBattleTrack() when ready
///
/// If no GameStateManager exists, falls back to auto-playing lobby track.
/// </summary>
public class MusicSystem : MonoBehaviour
{
    public static MusicSystem instance;

    public AudioSource source;

    [Header("Music Tracks")]
    [Tooltip("Plays during early gameplay (before battle wave).")]
    public AudioClip lobbyTrack;
    [Tooltip("Main gameplay track. Crossfades from lobby when battle starts.")]
    public AudioClip battleTrack;
    [Tooltip("Optional: ambient track for the title screen. Leave empty to use procedural ambient pad.")]
    public AudioClip titleTrack;

    [Header("Crossfade")]
    public float crossfadeDuration = 1f;

    [Header("Sound Effects")]
    public AudioClip attack_sfx;
    public AudioClip mine_hit_sfx;
    public AudioClip mine_destroyed_sfx;

    private bool isPlayingBattle = false;
    private AudioClip generatedTitleTrack = null;

    private void Awake()
    {
        instance = this;

        // Ensure music doesn't auto-play
        if (source != null)
        {
            source.playOnAwake = false;
            source.Stop();
        }

        // Pre-generate ambient title track if no dedicated one is assigned
        if (titleTrack == null)
        {
            generatedTitleTrack = TitleMusicGenerator.Generate();
            Debug.Log("[MusicSystem] Generated procedural ambient title track");
        }
    }

    private void Start()
    {
        // Subscribe to game state changes
        if (ClockworkGrid.GameStateManager.Instance != null)
        {
            ClockworkGrid.GameStateManager.Instance.OnStateChanged += OnGameStateChanged;

            // Handle current state
            if (ClockworkGrid.GameStateManager.Instance.IsTitleScreen)
            {
                PlayTitleMusic();
            }
            else if (ClockworkGrid.GameStateManager.Instance.IsPlaying)
            {
                StartMusic();
            }

            Debug.Log("[MusicSystem] Listening to GameStateManager — music respects game state");
        }
        else
        {
            // No GameStateManager — fall back to old behavior
            Debug.Log("[MusicSystem] No GameStateManager found — auto-starting lobby track");
            StartMusic();
        }
    }

    private void OnDestroy()
    {
        if (ClockworkGrid.GameStateManager.Instance != null)
            ClockworkGrid.GameStateManager.Instance.OnStateChanged -= OnGameStateChanged;
    }

    private void OnGameStateChanged(ClockworkGrid.GameState oldState, ClockworkGrid.GameState newState)
    {
        if (newState == ClockworkGrid.GameState.Playing)
        {
            Debug.Log("[MusicSystem] Game state → Playing — starting lobby track");
            StartMusic();
        }
        else if (newState == ClockworkGrid.GameState.TitleScreen)
        {
            PlayTitleMusic();
        }
    }

    /// <summary>
    /// Play title screen music (ambient/quiet) or stay silent.
    /// </summary>
    public void PlayTitleMusic()
    {
        if (source == null) return;

        if (titleTrack != null)
        {
            // Dedicated title track assigned in Inspector
            source.clip = titleTrack;
            source.loop = true;
            source.volume = 0.6f;
            source.Play();
            Debug.Log("[MusicSystem] Playing assigned title track");
        }
        else if (generatedTitleTrack != null)
        {
            // No dedicated title track — use procedural ambient pad (distinct from lobby/battle)
            source.clip = generatedTitleTrack;
            source.loop = true;
            source.volume = 0.5f;
            source.Play();
            Debug.Log("[MusicSystem] Playing procedural ambient title music");
        }
        else
        {
            source.Stop();
            Debug.Log("[MusicSystem] Title screen — no music available");
        }
    }

    /// <summary>
    /// Start playing the lobby track. Called when game transitions to Playing state.
    /// </summary>
    public void StartMusic()
    {
        if (source == null) return;

        // Restore full volume (title screen may have lowered it)
        source.volume = 1f;

        if (lobbyTrack != null)
        {
            source.clip = lobbyTrack;
            source.loop = true;
            source.Play();
        }
        else if (battleTrack != null)
        {
            // Fallback: no lobby track, just play battle track
            source.clip = battleTrack;
            source.loop = true;
            source.Play();
            isPlayingBattle = true;
        }
    }

    /// <summary>
    /// Crossfade from lobby track to battle track. Called when wave starts.
    /// </summary>
    public void SwitchToBattleTrack()
    {
        if (isPlayingBattle || battleTrack == null || source == null) return;
        isPlayingBattle = true;
        StartCoroutine(CrossfadeToBattle());
    }

    private IEnumerator CrossfadeToBattle()
    {
        float startVolume = source.volume;

        // Fade out lobby track
        float elapsed = 0f;
        float halfDuration = crossfadeDuration * 0.5f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, elapsed / halfDuration);
            yield return null;
        }

        // Switch clip
        source.Stop();
        source.clip = battleTrack;
        source.volume = 0f;
        source.loop = true;
        source.Play();

        // Fade in battle track
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(0f, startVolume, elapsed / halfDuration);
            yield return null;
        }

        source.volume = startVolume;
    }
}
