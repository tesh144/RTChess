using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Singleton that manages sound effects.
    /// Plays placement SFX with pitch variation for player vs enemy units.
    /// </summary>
    public class SFXManager : MonoBehaviour
    {
        public static SFXManager Instance { get; private set; }

        [Header("Audio")]
        [SerializeField] private AudioClip placementClip;
        [SerializeField] private float volume = 0.7f;

        [Header("Pitch")]
        [SerializeField] private float playerPitch = 1.0f;
        [SerializeField] private float enemyPitch = 0.7f;
        [SerializeField] private float resourcePitch = 1.2f;

        private AudioSource audioSource;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        /// <summary>
        /// Play placement sound for a player unit.
        /// </summary>
        public void PlayPlayerPlacement()
        {
            PlayClip(placementClip, playerPitch);
        }

        /// <summary>
        /// Play placement sound for an enemy unit (lower pitch).
        /// </summary>
        public void PlayEnemyPlacement()
        {
            PlayClip(placementClip, enemyPitch);
        }

        /// <summary>
        /// Play placement sound for a resource node.
        /// </summary>
        public void PlayResourcePlacement()
        {
            PlayClip(placementClip, resourcePitch);
        }

        private void PlayClip(AudioClip clip, float pitch)
        {
            if (clip == null || audioSource == null) return;
            audioSource.pitch = pitch;
            audioSource.PlayOneShot(clip, volume);
        }
    }
}
