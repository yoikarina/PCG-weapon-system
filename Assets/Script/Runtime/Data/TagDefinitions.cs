// Global registry of all valid tag strings used in the compatibility system.

using System.Collections.Generic;
using UnityEngine;

namespace GunAssemblyTool
{
    [CreateAssetMenu(fileName = "TagDefinitions", menuName = "GunAssemblyTool/Tag Definitions")]
    public class TagDefinitions : ScriptableObject
    {
        // Master list of all valid tags in the project.
        // GunBodyDataEditor reads this list to populate the tag dropdown,
        // preventing designers from entering freeform strings that could
        // silently break compatibility checks.
        public List<string> tags = new List<string>
        {
            "pistol", "smg", "rifle", "shotgun", "sniper",
            "compact", "bullpup", "full-size",
            "polymer", "metal",
            "pistol-grip", "no-stock"
        };

        // Internal HashSet built from the tags list for O(1) lookup.
        // Rebuilt whenever OnEnable or OnValidate fires.
        private HashSet<string> _tagSet;

        // Called by Unity when the asset is loaded into memory.
        // Builds the HashSet cache from the current tags list.
        private void OnEnable() => RebuildSet();

        // Called by Unity whenever the asset is modified in the Inspector.
        // Strips duplicates, normalises all entries to lowercase,
        // then rebuilds the HashSet cache.
        private void OnValidate()
        {
            var seen  = new HashSet<string>();
            var clean = new List<string>();
            foreach (var t in tags)
            {
                var trimmed = t?.Trim().ToLowerInvariant();
                if (!string.IsNullOrEmpty(trimmed) && seen.Add(trimmed))
                    clean.Add(trimmed);
            }
            tags = clean;
            RebuildSet();
        }

        // Constructs the internal HashSet from the current tags list.
        private void RebuildSet() => _tagSet = new HashSet<string>(tags);

        // Returns true if the given tag exists in the registry.
        // Used by GunBodyDataEditor to highlight invalid tags in red.
        public bool IsValid(string tag)
        {
            if (_tagSet == null) RebuildSet();
            return _tagSet.Contains(tag);
        }

        // Read-only access to the full tag list.
        // Used by GunBodyDataEditor to populate the add-tag dropdown.
        public IReadOnlyList<string> AllTags => tags;
    }
}
