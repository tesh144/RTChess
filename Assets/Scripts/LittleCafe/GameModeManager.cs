#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;

namespace LittleCafe
{
    public enum GameMode
    {
        Build,
        Play
    }

    /// <summary>
    /// Manages switching between Build Mode and Play Mode.
    /// Assign UI root GameObjects in the Inspector — they get toggled on/off.
    /// </summary>
    public class GameModeManager : MonoBehaviour
    {
        public static GameModeManager Instance { get; private set; }

        [Header("UI References (assign in Inspector)")]
        [SerializeField] private GameObject buildModeUI;
        [SerializeField] private GameObject playModeUI;

        [Header("System References (optional)")]
        [SerializeField] private GameObject buildModeSystems;
        [SerializeField] private GameObject playModeSystems;

        public GameMode CurrentMode { get; private set; }

        public event System.Action<GameMode> OnModeChanged;

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
            SwitchToMode(GameMode.Build);
        }

        public void SwitchToMode(GameMode mode)
        {
            CurrentMode = mode;

            if (mode == GameMode.Build)
            {
                if (buildModeUI != null) buildModeUI.SetActive(true);
                if (playModeUI != null) playModeUI.SetActive(false);
                if (buildModeSystems != null) buildModeSystems.SetActive(true);
                if (playModeSystems != null) playModeSystems.SetActive(false);
            }
            else
            {
                if (buildModeUI != null) buildModeUI.SetActive(false);
                if (playModeUI != null) playModeUI.SetActive(true);
                if (buildModeSystems != null) buildModeSystems.SetActive(false);
                if (playModeSystems != null) playModeSystems.SetActive(true);
            }

            OnModeChanged?.Invoke(mode);
            Debug.Log($"[GameModeManager] Switched to {mode} Mode");
        }

        // UI button callbacks
        public void OnStartServiceClicked() => SwitchToMode(GameMode.Play);
        public void OnEditKitchenClicked() => SwitchToMode(GameMode.Build);
    }
}
