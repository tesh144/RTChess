using UnityEngine;
using UnityEngine.EventSystems;

namespace ClockworkGrid
{
    /// <summary>
    /// Invisible button in the top-right corner.
    /// Tap 5 times to reveal the debug panel.
    /// </summary>
    public class HiddenDebugButton : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private int requiredTaps = 5;
        [SerializeField] private float tapResetTime = 2f;

        private int tapCount = 0;
        private float lastTapTime = 0f;

        public void OnPointerClick(PointerEventData eventData)
        {
            float currentTime = Time.realtimeSinceStartup;

            // Reset if too much time has passed
            if (currentTime - lastTapTime > tapResetTime)
            {
                tapCount = 0;
            }

            tapCount++;
            lastTapTime = currentTime;

            Debug.Log($"Debug button tapped {tapCount}/{requiredTaps}");

            if (tapCount >= requiredTaps)
            {
                OpenDebugPanel();
                tapCount = 0;
            }
        }

        private void OpenDebugPanel()
        {
            if (DebugPanel.Instance != null)
            {
                DebugPanel.Instance.Toggle();
                Debug.Log("Debug panel toggled");
            }
            else
            {
                Debug.LogWarning("DebugPanel instance not found!");
            }
        }
    }
}
