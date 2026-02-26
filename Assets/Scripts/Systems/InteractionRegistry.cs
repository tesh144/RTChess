using UnityEngine;
using System.Collections.Generic;
using LittleCafe;

namespace ClockworkCraft
{
    /// <summary>
    /// Central registry controlling which objects workers can interact with.
    /// Part of the game's unlock/progression system — NOT stored on individual database entries.
    ///
    /// Assign EnvironmentDatabase and UnitDatabase in the Inspector and entries auto-populate.
    /// Each entry has a toggle controlling whether workers can interact with that object type.
    /// As the player progresses (places buildings, researches tech, etc.), entries get unlocked
    /// via Unlock().
    ///
    /// Spawned objects query this registry to set their per-instance isInteractable flag.
    /// GridEntityActor checks the per-instance flag on the target when scanning.
    ///
    /// Lives as a scene object — managed by CafeSceneSetupV2 (the game manager).
    /// </summary>
    public class InteractionRegistry : MonoBehaviour
    {
        public static InteractionRegistry Instance { get; private set; }

        [Header("Databases (assign to auto-populate entries)")]
        [Tooltip("Assign to auto-populate environment entries on Awake.")]
        [SerializeField] private EnvironmentDatabase environmentDatabase;
        [Tooltip("Assign to auto-populate unit entries on Awake.")]
        [SerializeField] private UnitDatabase unitDatabase;

        [Header("Interaction Entries")]
        [Tooltip("Each entry controls whether workers can interact with this object type. Toggle 'unlocked' to allow interaction from game start.")]
        [SerializeField] private List<InteractionEntry> entries = new List<InteractionEntry>();

        // Runtime lookup — built from entries list on Awake
        private Dictionary<string, InteractionEntry> lookup = new Dictionary<string, InteractionEntry>();

        /// <summary>
        /// Fired when an entry is unlocked at runtime. Arg: the entry name.
        /// </summary>
        public event System.Action<string> OnEntryUnlocked;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            RebuildLookup();

            // Auto-populate from assigned databases (only adds missing entries)
            if (environmentDatabase != null || unitDatabase != null)
            {
                PopulateFromDatabases(environmentDatabase, unitDatabase);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Check if workers can interact with an object by its database name.
        /// Returns true if the entry exists and is unlocked, or if no entry is found (default allow).
        /// </summary>
        public bool IsUnlocked(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return true;
            if (lookup.TryGetValue(objectName, out var entry))
                return entry.unlocked;
            return true; // Unknown objects are interactable by default
        }

        /// <summary>
        /// Unlock an object type so workers can interact with it.
        /// Called by buildings, research, or progression triggers.
        /// </summary>
        public void Unlock(string objectName)
        {
            if (lookup.TryGetValue(objectName, out var entry))
            {
                if (!entry.unlocked)
                {
                    entry.unlocked = true;
                    Debug.Log($"[InteractionRegistry] Unlocked '{objectName}' — workers can now interact with it.");
                    OnEntryUnlocked?.Invoke(objectName);
                }
            }
            else
            {
                // Add new entry as unlocked
                var newEntry = new InteractionEntry { objectName = objectName, unlocked = true };
                entries.Add(newEntry);
                lookup[objectName] = newEntry;
                Debug.Log($"[InteractionRegistry] Added and unlocked new entry '{objectName}'.");
                OnEntryUnlocked?.Invoke(objectName);
            }
        }

        /// <summary>
        /// Lock an object type so workers can no longer interact with it.
        /// </summary>
        public void Lock(string objectName)
        {
            if (lookup.TryGetValue(objectName, out var entry))
            {
                entry.unlocked = false;
                Debug.Log($"[InteractionRegistry] Locked '{objectName}'.");
            }
        }

        /// <summary>
        /// Get all entries (for editor/debug display).
        /// </summary>
        public List<InteractionEntry> AllEntries => entries;

        // ─────────────────────────────────────────────────────────────────
        // Population
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Populate entries from environment and unit databases.
        /// Only adds entries that don't already exist (preserves manually set unlock states).
        /// Called by MapGeneratorV2 during setup.
        /// </summary>
        public void PopulateFromDatabases(EnvironmentDatabase envDB, UnitDatabase unitDB)
        {
            bool added = false;

            if (envDB != null)
            {
                foreach (var env in envDB.AllEnvironment)
                {
                    if (string.IsNullOrEmpty(env.assetName)) continue;
                    if (!lookup.ContainsKey(env.assetName))
                    {
                        var entry = new InteractionEntry
                        {
                            objectName = env.assetName,
                            source = InteractionSource.Environment,
                            unlocked = true // Environment objects default to interactable
                        };
                        entries.Add(entry);
                        lookup[env.assetName] = entry;
                        added = true;
                    }
                }
            }

            if (unitDB != null)
            {
                foreach (var unit in unitDB.AllUnits)
                {
                    if (string.IsNullOrEmpty(unit.assetName)) continue;
                    if (!lookup.ContainsKey(unit.assetName))
                    {
                        var entry = new InteractionEntry
                        {
                            objectName = unit.assetName,
                            source = InteractionSource.Unit,
                            unlocked = true // Units default to interactable (toggle off in Inspector for locked ones)
                        };
                        entries.Add(entry);
                        lookup[unit.assetName] = entry;
                        added = true;
                    }
                }
            }

            if (added)
            {
                Debug.Log($"[InteractionRegistry] Populated {entries.Count} entries from databases.");
            }
        }

        private void RebuildLookup()
        {
            lookup.Clear();
            foreach (var entry in entries)
            {
                if (!string.IsNullOrEmpty(entry.objectName))
                    lookup[entry.objectName] = entry;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Data Types
    // ─────────────────────────────────────────────────────────────────────

    public enum InteractionSource
    {
        Environment,
        Unit
    }

    /// <summary>
    /// Single entry in the interaction registry.
    /// Visible in Inspector for designer control.
    /// </summary>
    [System.Serializable]
    public class InteractionEntry
    {
        [Tooltip("Name matching the database assetName (e.g. 'Tree', 'Rock', 'Dinosaur').")]
        public string objectName;

        [Tooltip("Where this entry came from (for display only).")]
        public InteractionSource source;

        [Tooltip("If true, workers can interact with this object type from the start.")]
        public bool unlocked = true;
    }
}
