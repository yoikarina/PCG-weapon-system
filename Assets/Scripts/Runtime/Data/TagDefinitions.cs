using System.Collections.Generic;
using UnityEngine;

namespace GunAssemblyTool
{
    // Stores all valid tags used in the system (for validation and consistency)
    [CreateAssetMenu(fileName = "TagDefinitions", menuName = "GunAssemblyTool/Tag Definitions")]
    public class TagDefinitions : ScriptableObject
    {
        // List of all allowed tags
        public List<string> tags = new List<string>
        {
            "pistol", "smg", "rifle", "shotgun", "sniper",
            "compact", "bullpup", "full-size",
            "polymer", "metal",
            "pistol-grip", "no-stock"
        };

        // Cached set for fast lookup
        private HashSet<string> _tagSet;

        private void OnEnable() => RebuildSet();

        private void OnValidate()
        {
            // Clean and remove duplicates
            var seen = new HashSet<string>();
            var clean = new List<string>();

            foreach (var t in tags)
            {
                // Trim spaces and convert to lowercase
                var trimmed = t?.Trim().ToLowerInvariant();

                // Only keep valid and unique tags
                if (!string.IsNullOrEmpty(trimmed) && seen.Add(trimmed))
                    clean.Add(trimmed);
            }

            // Replace with cleaned list
            tags = clean;

            RebuildSet();
        }

        // Build HashSet for faster Contains() checks
        private void RebuildSet() => _tagSet = new HashSet<string>(tags);

        // Check if a tag exists in the system
        public bool IsValid(string tag)
        {
            if (_tagSet == null) RebuildSet();
            return _tagSet.Contains(tag);
        }

        // Read-only access to all tags
        public IReadOnlyList<string> AllTags => tags;
    }
}