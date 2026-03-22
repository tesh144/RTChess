#pragma warning disable CS0414, CS0219, CS0618
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
        [Tooltip("Assign to auto-populate building entries with per-faction interaction flags.")]
        [SerializeField] private LittleCafe.BuildingDatabase buildingDatabase;

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
            if (environmentDatabase != null || unitDatabase != null || buildingDatabase != null)
            {
                PopulateFromDatabases(environmentDatabase, unitDatabase);
                PopulateFromBuildingDatabase(buildingDatabase);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Check if workers can interact with an object by its database name.
        /// Returns true if the entry exists and is unlocked, or if no entry is found (default allow).
        /// Legacy method — prefer CanInteract() for faction-aware checks.
        /// </summary>
        public bool IsUnlocked(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return true;
            if (lookup.TryGetValue(objectName, out var entry))
                return entry.unlocked;
            return true; // Unknown objects are interactable by default
        }

        /// <summary>
        /// Faction-aware interaction check. Returns true if the given interactor type
        /// can interact with the target object, based on the sheet's per-faction columns.
        /// Falls back to legacy 'unlocked' for entries that haven't been updated.
        /// </summary>
        public bool CanInteract(string objectName, InteractorType interactorType)
        {
            if (string.IsNullOrEmpty(objectName)) return true;
            if (!lookup.TryGetValue(objectName, out var entry))
            {
                // Unknown objects: allies/enemies default allow (legacy), wild animals default deny
                return interactorType != InteractorType.WildAnimal;
            }

            switch (interactorType)
            {
                case InteractorType.Ally:
                    return entry.allyInteractible;
                case InteractorType.Enemy:
                    return entry.enemyInteractible;
                case InteractorType.WildAnimal:
                    return entry.wildAnimalInteractible;
                default:
                    return entry.unlocked; // Legacy fallback
            }
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
                    if (lookup.TryGetValue(env.assetName, out var existing))
                    {
                        // Update per-faction flags on existing entry
                        existing.allyInteractible = env.allyInteractible;
                        existing.enemyInteractible = env.enemyInteractible;
                        existing.wildAnimalInteractible = env.wildAnimalInteractible;
                    }
                    else
                    {
                        var entry = new InteractionEntry
                        {
                            objectName = env.assetName,
                            source = InteractionSource.Environment,
                            unlocked = env.allyInteractible, // Legacy compat: workers interact if ally-interactible
                            allyInteractible = env.allyInteractible,
                            enemyInteractible = env.enemyInteractible,
                            wildAnimalInteractible = env.wildAnimalInteractible
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

        /// <summary>
        /// Populate entries from the BuildingDatabase, setting per-faction interaction flags
        /// from the sheet columns (Ally Interactible, Enemy Interactible, Wild Animal Interactible).
        /// </summary>
        public void PopulateFromBuildingDatabase(LittleCafe.BuildingDatabase buildingDB)
        {
            if (buildingDB == null) return;
            bool added = false;

            foreach (var bld in buildingDB.AllBuildings)
            {
                if (string.IsNullOrEmpty(bld.assetName)) continue;
                string name = bld.GetCleanName();

                if (lookup.TryGetValue(name, out var existing))
                {
                    // Update per-faction flags on existing entry
                    existing.allyInteractible = bld.allyInteractible;
                    existing.enemyInteractible = bld.enemyInteractible;
                    existing.wildAnimalInteractible = bld.wildAnimalInteractible;
                }
                else
                {
                    var entry = new InteractionEntry
                    {
                        objectName = name,
                        source = InteractionSource.Building,
                        unlocked = bld.enemyInteractible, // Legacy compat: workers attack if enemy-interactible
                        allyInteractible = bld.allyInteractible,
                        enemyInteractible = bld.enemyInteractible,
                        wildAnimalInteractible = bld.wildAnimalInteractible
                    };
                    entries.Add(entry);
                    lookup[name] = entry;
                    added = true;
                }
            }

            if (added)
            {
                Debug.Log($"[InteractionRegistry] Populated building entries. Total: {entries.Count}");
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
        Unit,
        Building
    }

    /// <summary>
    /// What kind of entity is trying to interact.
    /// Used to look up the correct per-faction flag on the target.
    /// </summary>
    public enum InteractorType
    {
        Ally,       // Workers, Fighters — player-controlled
        Enemy,      // Hostile entities attacking the object
        WildAnimal  // Dinosaur, Mammoth — neutral wildlife
    }

    /// <summary>
    /// Single entry in the interaction registry.
    /// Visible in Inspector for designer control.
    /// Now supports per-faction interaction flags matching the Google Sheet columns:
    ///   Ally Interactible, Enemy Interactible, Wild Animal Interactible.
    /// </summary>
    [System.Serializable]
    public class InteractionEntry
    {
        [Tooltip("Name matching the database assetName (e.g. 'Tree', 'Rock', 'Dinosaur', 'Feast').")]
        public string objectName;

        [Tooltip("Where this entry came from (for display only).")]
        public InteractionSource source;

        [Tooltip("Legacy: if true, workers (allies) can interact with this object type from the start.")]
        public bool unlocked = true;

        [Header("Per-Faction Interaction (from sheet)")]
        [Tooltip("Can allied workers interact with this object? Maps to 'Ally Interactible' sheet column.")]
        public bool allyInteractible = false;
        [Tooltip("Can enemies attack/interact with this object? Maps to 'Enemy Interactible' sheet column.")]
        public bool enemyInteractible = true;
        [Tooltip("Can wild animals interact with this object? Maps to 'Wild Animal Interactible' sheet column.")]
        public bool wildAnimalInteractible = false;
    }
}
