namespace ClockworkGrid
{
    /// <summary>
    /// Static service locator for the active camera system.
    /// Returns the registered ICameraSystem (GridCamera).
    /// </summary>
    public static class CameraSystemLocator
    {
        private static ICameraSystem _override;

        public static ICameraSystem Current => _override;

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
