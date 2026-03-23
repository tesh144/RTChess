#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace LittleCafe
{
    /// <summary>
    /// ScriptableObject database containing all corruption entity configurations.
    /// Create via: Right-click → Create → LittleCafe → Corruption Database
    /// </summary>
    [CreateAssetMenu(fileName = "CorruptionDatabase", menuName = "LittleCafe/Corruption Database")]
    public class CorruptionDatabase : ScriptableObject
    {
        [Header("All Corruption Entities")]
        [SerializeField] private List<CorruptionData> entries = new List<CorruptionData>();

        public List<CorruptionData> AllEntries => entries;

        /// <summary>
        /// Get corruption entity data by name.
        /// </summary>
        public CorruptionData GetByName(string entityName)
        {
            return entries.FirstOrDefault(e => e.entityName == entityName);
        }

        /// <summary>
        /// Add an entry to the database. Does not add duplicates by name.
        /// </summary>
        public void AddEntry(CorruptionData data)
        {
            if (data == null) return;
            if (entries.Any(e => e.entityName == data.entityName)) return;
            entries.Add(data);
        }

        /// <summary>
        /// Clear all entries.
        /// </summary>
        public void Clear()
        {
            entries.Clear();
        }

        /// <summary>
        /// Number of entries in the database.
        /// </summary>
        public int Count => entries.Count;
    }
}
