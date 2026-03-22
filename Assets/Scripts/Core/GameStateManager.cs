#pragma warning disable CS0414, CS0219, CS0618
using System;
using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Central authority for game state. Every system checks this before acting.
    ///
    /// States:
    ///   TitleScreen → Player sees the title screen with logo, buttons, no gameplay.
    ///                 Music: silent or title music. UI: title panel only.
    ///   Playing     → Game is active. Map generated, dock bar visible, clock ticking.
    ///                 Music: game/battle track. UI: gameplay HUD.
    ///
    /// Transition flow:
    ///   Scene loads → TitleScreen (default)
    ///   Player presses Play → Playing
    ///
    /// Usage:
    ///   GameStateManager.Instance.CurrentState
    ///   GameStateManager.Instance.OnStateChanged += (oldState, newState) => { ... }
    ///   GameStateManager.Instance.TransitionTo(GameState.Playing);
    ///
    /// This is a scene-level singleton — does NOT persist across scene loads.
    /// Execution order -110 ensures it runs before CafeSceneSetupV2 (-100).
    /// </summary>
    [DefaultExecutionOrder(-110)]
    public class GameStateManager : MonoBehaviour
    {
        public static GameStateManager Instance { get; private set; }

        /// <summary>
        /// Fires when the game state changes: (previousState, newState).
        /// </summary>
        public event Action<GameState, GameState> OnStateChanged;

        [Header("Initial State")]
        [Tooltip("The state the game starts in. Normally TitleScreen.")]
        [SerializeField] private GameState initialState = GameState.TitleScreen;

        /// <summary>Current game state.</summary>
        public GameState CurrentState { get; private set; }

        /// <summary>True when the game is actively being played.</summary>
        public bool IsPlaying => CurrentState == GameState.Playing;

        /// <summary>True when on the title screen.</summary>
        public bool IsTitleScreen => CurrentState == GameState.TitleScreen;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            CurrentState = initialState;
            Debug.Log($"[GameStateManager] Initialized — state: {CurrentState}");
        }

        /// <summary>
        /// Transition to a new game state. Fires OnStateChanged.
        /// Safe to call multiple times with the same state (no-op).
        /// </summary>
        public void TransitionTo(GameState newState)
        {
            if (CurrentState == newState) return;

            GameState previous = CurrentState;
            CurrentState = newState;

            Debug.Log($"[GameStateManager] {previous} → {newState}");
            OnStateChanged?.Invoke(previous, newState);
        }

        /// <summary>Convenience: transition to Playing state.</summary>
        public void StartGame()
        {
            TransitionTo(GameState.Playing);
        }
    }

    /// <summary>
    /// Core game states. Add new states as needed (e.g., Loading, Paused, GameOver).
    /// </summary>
    public enum GameState
    {
        /// <summary>Title screen with logo and Play button. No gameplay active.</summary>
        TitleScreen,

        /// <summary>Game is active — map generated, workers moving, clock ticking.</summary>
        Playing
    }
}
