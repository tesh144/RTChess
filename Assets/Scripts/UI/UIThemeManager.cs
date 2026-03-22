#pragma warning disable CS0414, CS0219, CS0618
using System.Collections.Generic;
using UnityEngine;

namespace ClockworkGrid
{
    /// <summary>
    /// Manages light/dark mode switching for GUI Pro Kit panels.
    /// Pairs each LightMode panel with its DarkMode counterpart and
    /// provides access to the currently active set.
    ///
    /// Attach to the root Canvas or a manager object in the scene.
    /// Use the editor tool (Tools > ClockworkCraft > Setup UI Panels) to auto-populate.
    /// </summary>
    public class UIThemeManager : MonoBehaviour
    {
        public static UIThemeManager Instance { get; private set; }

        [Header("Theme Containers")]
        [Tooltip("The LightMode parent transform containing all light panels.")]
        public Transform lightModeRoot;

        [Tooltip("The DarkMode parent transform containing all dark panels.")]
        public Transform darkModeRoot;

        [Header("Settings")]
        [Tooltip("Start in dark mode?")]
        public bool startDarkMode = false;

        // ── State ─────────────────────────────────────────────────────

        private bool isDarkMode;

        /// <summary>Whether dark mode is currently active.</summary>
        public bool IsDarkMode => isDarkMode;

        // Paired panels: pageId → (lightPanel, darkPanel)
        private Dictionary<string, PanelPair> panelPairs = new Dictionary<string, PanelPair>();

        // Quick access to all panels by pageId
        private Dictionary<string, UIPanel> lightPanels = new Dictionary<string, UIPanel>();
        private Dictionary<string, UIPanel> darkPanels = new Dictionary<string, UIPanel>();

        public struct PanelPair
        {
            public UIPanel light;
            public UIPanel dark;

            /// <summary>Returns the currently active panel based on theme.</summary>
            public UIPanel Active(bool darkMode) => darkMode ? dark : light;
        }

        // ── Lifecycle ─────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            isDarkMode = startDarkMode;
            DiscoverPanels();
            ApplyTheme();

            // DON'T hide all panels here. The scene's initial state should be correct —
            // Title_common (or whatever the startup panel is) should be active in the editor.
            // Individual controllers (TitleScreenController, etc.) manage their own panels.
            // HideAll() was causing Title_common to disappear when no controller existed
            // to re-show it.
        }

        // ── Panel Discovery ───────────────────────────────────────────

        /// <summary>
        /// Scans LightMode and DarkMode roots for UIPanel components,
        /// pairs them by pageId, and sets up cross-references.
        /// </summary>
        public void DiscoverPanels()
        {
            panelPairs.Clear();
            lightPanels.Clear();
            darkPanels.Clear();

            // Index light panels
            if (lightModeRoot != null)
            {
                foreach (Transform child in lightModeRoot)
                {
                    var panel = child.GetComponent<UIPanel>();
                    if (panel != null)
                    {
                        panel.isDarkMode = false;
                        panel.BuildRuntimeDictionaries();
                        lightPanels[panel.pageId] = panel;
                    }
                }
            }

            // Index dark panels
            if (darkModeRoot != null)
            {
                foreach (Transform child in darkModeRoot)
                {
                    var panel = child.GetComponent<UIPanel>();
                    if (panel != null)
                    {
                        panel.isDarkMode = true;
                        panel.BuildRuntimeDictionaries();
                        darkPanels[panel.pageId] = panel;
                    }
                }
            }

            // Build pairs
            var allPageIds = new HashSet<string>();
            foreach (var id in lightPanels.Keys) allPageIds.Add(id);
            foreach (var id in darkPanels.Keys) allPageIds.Add(id);

            foreach (var id in allPageIds)
            {
                lightPanels.TryGetValue(id, out var lightPanel);
                darkPanels.TryGetValue(id, out var darkPanel);

                var pair = new PanelPair { light = lightPanel, dark = darkPanel };
                panelPairs[id] = pair;

                // Cross-reference
                if (lightPanel != null) lightPanel.pairedPanel = darkPanel;
                if (darkPanel != null) darkPanel.pairedPanel = lightPanel;
            }

            Debug.Log($"[UIThemeManager] Discovered {panelPairs.Count} panel pairs " +
                      $"({lightPanels.Count} light, {darkPanels.Count} dark)");
        }

        // ── Theme Switching ───────────────────────────────────────────

        /// <summary>Toggle between light and dark mode.</summary>
        public void ToggleTheme()
        {
            isDarkMode = !isDarkMode;
            ApplyTheme();
        }

        /// <summary>Set a specific theme.</summary>
        public void SetTheme(bool dark)
        {
            if (isDarkMode == dark) return;
            isDarkMode = dark;
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            if (lightModeRoot != null)
                lightModeRoot.gameObject.SetActive(!isDarkMode);

            if (darkModeRoot != null)
                darkModeRoot.gameObject.SetActive(isDarkMode);
        }

        // ── Panel Access ──────────────────────────────────────────────

        /// <summary>
        /// Get the currently active panel for a given pageId.
        /// Returns the light or dark variant based on current theme.
        /// </summary>
        public UIPanel GetPanel(string pageId)
        {
            if (panelPairs.TryGetValue(pageId, out var pair))
                return pair.Active(isDarkMode);

            Debug.LogWarning($"[UIThemeManager] Panel not found: '{pageId}'");
            return null;
        }

        /// <summary>Get both light and dark panels for a pageId.</summary>
        public PanelPair GetPanelPair(string pageId)
        {
            panelPairs.TryGetValue(pageId, out var pair);
            return pair;
        }

        /// <summary>Get all registered page IDs.</summary>
        public IEnumerable<string> AllPageIds => panelPairs.Keys;

        /// <summary>
        /// Show a specific panel by pageId (hides nothing else — caller manages visibility).
        /// </summary>
        public void ShowPanel(string pageId)
        {
            var panel = GetPanel(pageId);
            if (panel != null) panel.Show();
        }

        /// <summary>Hide a specific panel by pageId.</summary>
        public void HidePanel(string pageId)
        {
            // Hide both variants to be safe
            if (panelPairs.TryGetValue(pageId, out var pair))
            {
                if (pair.light != null) pair.light.Hide();
                if (pair.dark != null) pair.dark.Hide();
            }
        }

        /// <summary>Hide all panels.</summary>
        public void HideAll()
        {
            foreach (var pair in panelPairs.Values)
            {
                if (pair.light != null) pair.light.Hide();
                if (pair.dark != null) pair.dark.Hide();
            }
        }

        /// <summary>
        /// Execute an action on both light and dark variants of a panel.
        /// Useful for syncing state across themes.
        /// </summary>
        public void ForBoth(string pageId, System.Action<UIPanel> action)
        {
            if (panelPairs.TryGetValue(pageId, out var pair))
            {
                if (pair.light != null) action(pair.light);
                if (pair.dark != null) action(pair.dark);
            }
        }
    }
}
