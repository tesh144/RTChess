using System;
using UnityEngine;

namespace ClockworkGrid
{
    public class IntervalTimer : MonoBehaviour
    {
        [Header("Interval Settings")]
        [SerializeField] private float baseIntervalDuration = 2.0f;

        private float timer;
        private int currentInterval;
        private bool isPaused = false; // Iteration 9: Pause during grid expansion

        public static IntervalTimer Instance { get; private set; }

        public int CurrentInterval => currentInterval;
        public float IntervalDuration => baseIntervalDuration;
        public float IntervalProgress => timer / baseIntervalDuration;

        /// <summary>
        /// Fired every interval tick. Passes the current interval count.
        /// </summary>
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
            if (isPaused) return; // Iteration 9: Don't tick while paused

            timer += Time.deltaTime;

            if (timer >= baseIntervalDuration)
            {
                timer -= baseIntervalDuration;
                currentInterval++;
                OnIntervalTick?.Invoke(currentInterval);
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
