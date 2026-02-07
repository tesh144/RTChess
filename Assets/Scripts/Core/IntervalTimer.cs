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
            timer += Time.deltaTime;

            if (timer >= baseIntervalDuration)
            {
                timer -= baseIntervalDuration;
                currentInterval++;
                OnIntervalTick?.Invoke(currentInterval);
            }
        }
    }
}
