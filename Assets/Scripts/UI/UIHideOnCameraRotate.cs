using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Hides UI when the player right-click rotates the camera.
    /// Attach to any persistent GameObject (e.g. the camera).
    /// </summary>
    public class UIHideOnCameraRotate : MonoBehaviour
    {
        private CanvasGroup canvasGroup;

        private void Start()
        {
            // Find the main UI canvas and ensure it has a CanvasGroup
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                canvasGroup = canvas.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            }

            if (CameraController.Instance != null)
            {
                CameraController.Instance.OnRotationStarted += HideUI;
                CameraController.Instance.OnRotationEnded += ShowUI;
            }
        }

        private void OnDestroy()
        {
            if (CameraController.Instance != null)
            {
                CameraController.Instance.OnRotationStarted -= HideUI;
                CameraController.Instance.OnRotationEnded -= ShowUI;
            }
        }

        private void HideUI()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
            }
        }

        private void ShowUI()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = true;
            }
        }
    }
}
