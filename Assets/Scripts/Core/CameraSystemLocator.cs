namespace ClockworkGrid
{
    /// <summary>
    /// Static service locator for the active camera system.
    /// Returns the registered ICameraSystem (GridCamera in cafe scene),
    /// or falls back to CameraController.Instance (RTChess scene).
    /// </summary>
    public static class CameraSystemLocator
    {
        private static ICameraSystem _override;

        public static ICameraSystem Current
        {
            get
            {
                if (_override != null) return _override;
                return CameraController.Instance;
            }
        }

        public static void Register(ICameraSystem system)
        {
            _override = system;
        }

        public static void Unregister(ICameraSystem system)
        {
            if (_override == system) _override = null;
        }
    }
}
