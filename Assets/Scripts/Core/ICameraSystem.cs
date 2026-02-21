using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Shared camera API implemented by both CameraController (RTChess) and GridCamera (Cafe).
    /// Allows DragDropHandler and other UI to work with either camera system.
    /// </summary>
    public interface ICameraSystem
    {
        Vector3 CurrentTarget { get; }
        float CurrentDistance { get; }
        bool IsAutoRotating { get; }

        void SetAutoRotate(bool enabled, float speed = -1f);
        void ZoomTo(float distance);
        void SetTarget(Vector3 worldPos);
        void FocusOnCell(int gridX, int gridY);
        void FocusOnPosition(Vector3 worldPos);
        void Shake(float intensity = 0.15f, float duration = 0.25f);
    }
}
