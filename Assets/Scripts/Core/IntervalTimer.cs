#pragma warning disable CS0414, CS0219, CS0618
using System;
using UnityEngine;

namespace ClockworkGrid
{
    public class IntervalTimer : MonoBehaviour
    {
        [Header("Interval Settings")]
        [SerializeField] private float baseIntervalDuration = 2.0f;
        [SerializeField] private bool verboseLogging = false;

        private float timer;
        private int currentBeat;   // 1–4, or 0 before first tick
        private int currentBar;
        private bool isPaused = false; // Iteration 9: Pause during grid expansion

        public static IntervalTimer Instance { get; private set; }

        public float BeatDuration => baseIntervalDuration / 4f;
        public float IntervalDuration => baseIntervalDuration;
        public int CurrentBar => currentBar;
        public int CurrentBeat => currentBeat;
        public int CurrentInterval => currentBar;
        public float IntervalProgress =>
            (Math.Max(0, currentBeat - 1) * BeatDuration + timer) / baseIntervalDuration;

        /// <summary>Fired every beat. Passes (beat 1–4, barNumber).</summary>
        public event Action<int, int> OnBeat;

        /// <summary>Fired every half-bar (beats 1 and 3). Passes barNumber.</summary>
        public event Action<int> OnHalfBar;

        /// <summary>Fired every bar (beat 1). Passes barNumber.</summary>
        public event Action<int> OnBar;

        /// <summary>Backward-compat alias for OnBar — fires at the same moment.</summary>
        public event Action<int> OnIntervalTick;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            if (isPaused)
            {
                return;
            }

            timer += Time.deltaTime;

            if (timer >= BeatDuration)
            {
                timer -= BeatDuration;
                currentBeat++;
                if (currentBeat > 4)
                {
                    currentBeat = 1;
                    currentBar++;
                }

                if (verboseLogging)
                    Debug.Log($"[IntervalTimer] Beat {currentBeat} / Bar {currentBar}");

                try
                {
                    OnBeat?.Invoke(currentBeat, currentBar);

                    if (currentBeat == 1 || currentBeat == 3)
                        OnHalfBar?.Invoke(currentBar);

                    if (currentBeat == 1)
                    {
                        OnBar?.Invoke(currentBar);
                        OnIntervalTick?.Invoke(currentBar);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[IntervalTimer] EXCEPTION during beat tick: {e.Message}\n{e.StackTrace}");
                }
            }
        }

        /// <summary>
        /// Pause the interval timer (Iteration 9: for grid expansion).
        /// </summary>
        public void Pause()
        {
            isPaused = true;
            Debug.Log("IntervalTimer paused");
        }

        /// <summary>
        /// Resume the interval timer.
        /// </summary>
        public void Resume()
        {
            isPaused = false;
            Debug.Log("IntervalTimer resumed");
        }

        /// <summary>
        /// Check if timer is currently paused.
        /// </summary>
        public bool IsPaused => isPaused;
    }
}
