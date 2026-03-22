#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;

/// <summary>
/// Centralized audio manager for object placement sounds.
/// Plays drop/placement sounds when objects are placed on the grid.
/// More efficient than having AudioSource on every prefab.
/// </summary>
public class PlacementAudioManager : MonoBehaviour
{
    public static PlacementAudioManager Instance { get; private set; }

    [Header("Audio Settings")]
    [Tooltip("Sound to play when any object is placed")]
    public AudioClip placementSound;

    [Tooltip("Volume for placement sounds (0-1)")]
    [Range(0f, 1f)]
    public float volume = 1f;

    [Tooltip("Pitch variation range (adds randomness)")]
    [Range(0f, 0.2f)]
    public float pitchVariation = 0.05f;

    private AudioSource audioSource;

    void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Create AudioSource component
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D sound
    }

    /// <summary>
    /// Play placement sound with optional pitch variation
    /// </summary>
    public void PlayPlacementSound()
    {
        if (placementSound == null)
        {
            Debug.LogWarning("PlacementAudioManager: No placement sound assigned!");
            return;
        }

        // Add slight pitch variation for variety
        float pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        audioSource.pitch = pitch;

        audioSource.PlayOneShot(placementSound, volume);
    }

    /// <summary>
    /// Play placement sound at specific world position (for 3D spatial audio)
    /// </summary>
    public void PlayPlacementSoundAt(Vector3 worldPosition)
    {
        if (placementSound == null) return;

        AudioSource.PlayClipAtPoint(placementSound, worldPosition, volume);
    }
}
