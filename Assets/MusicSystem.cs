using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MusicSystem : MonoBehaviour
{
    public static MusicSystem instance;

    public AudioSource source;

    [Header("Music Tracks")]
    public AudioClip lobbyTrack;   // Pared-down track that plays before wave starts
    public AudioClip battleTrack;  // Main theme that plays once wave begins

    [Header("Crossfade")]
    public float crossfadeDuration = 1f;

    [Header("Sound Effects")]
    public AudioClip attack_sfx;
    public AudioClip mine_hit_sfx;
    public AudioClip mine_destroyed_sfx;

    private bool isPlayingBattle = false;

    private void Awake()
    {
        instance = this;

        // Ensure music doesn't auto-play
        if (source != null)
        {
            source.playOnAwake = false;
            source.Stop();
        }
    }

    private void Start()
    {
        // Auto-start lobby music (Start runs after all Awake calls, so order is safe)
        StartMusic();
    }

    /// <summary>
    /// Start playing the lobby track.
    /// </summary>
    public void StartMusic()
    {
        if (source == null) return;

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
