// Abstract base class for all attachment ScriptableObjects.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GunAssemblyTool
{
    // Do not create directly — use a concrete subclass:
    // Barrel / Magazine / Muzzle / Scope / Stock / Grip / Underbarrel / Skin
    public abstract class AttachmentData : ScriptableObject
    {
        [Header("Basic Info")]
        // Unique identifier used for serialisation and config restore.
        public string attachmentId;

        // Human-readable name shown in the Weapon Workbench UI.
        public string displayName;

        // Which slot this attachment occupies. Locked by each concrete subclass.
        public AttachmentType attachType;

        [Header("Tag Rules")]
        // Gun body must have ALL of these tags. Leave empty to allow all gun bodies.
        [Tooltip("Gun body must contain all of these tags. Leave empty to allow all gun bodies.")]
        public List<string> requiredTags = new List<string>();

        // Gun bodies with ANY of these tags are incompatible.
        [Tooltip("Gun bodies with any of these tags are incompatible with this attachment.")]
        public List<string> forbiddenTags = new List<string>();

        [Header("Stats")]
        // Dynamic stat list. Only stats the designer explicitly adds are present.
        // Values are treated as BONUSES added on top of the gun body base stats,
        // except MagSize which overrides the body's value directly.
        public List<StatEntry> stats = new List<StatEntry>();

        [Header("PCG Weight")]
        // Relative probability weight for random generation. 0 = excluded from PCG.
        [Range(0f, 100f)]
        [Tooltip("Spawn weight for PCG. 0 = excluded from random generation.")]
        public float spawnWeight = 10f;

        [Header("Visual")]
        // Prefab instantiated at the GunAttachmentPoint when equipped.
        [Tooltip("Prefab placed at the GunAttachmentPoint when equipped. Null = data only.")]
        public GameObject attachmentPrefab;

        // ── Runtime caches ────────────────────────────────────────────────────
        private HashSet<string> _requiredCache;
        private HashSet<string> _forbiddenCache;

        private void OnEnable() => RebuildCaches();

        protected virtual void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(attachmentId))
                attachmentId = name.ToLowerInvariant().Replace(" ", "_");
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = name;
            for (int i = 0; i < requiredTags.Count; i++)
                requiredTags[i] = requiredTags[i]?.Trim().ToLowerInvariant();
            for (int i = 0; i < forbiddenTags.Count; i++)
                forbiddenTags[i] = forbiddenTags[i]?.Trim().ToLowerInvariant();
            RebuildCaches();
        }

        private void RebuildCaches()
        {
            _requiredCache = new HashSet<string>(requiredTags);
            _forbiddenCache = new HashSet<string>(forbiddenTags);
        }

        // Returns required tags as a HashSet for O(1) lookup.
        public HashSet<string> RequiredTagSet
        {
            get { if (_requiredCache == null) RebuildCaches(); return _requiredCache; }
        }

        // Returns forbidden tags as a HashSet for O(1) lookup.
        public HashSet<string> ForbiddenTagSet
        {
            get { if (_forbiddenCache == null) RebuildCaches(); return _forbiddenCache; }
        }

        // Returns the value of a named stat bonus, or defaultValue if not present.
        public float GetStat(string key, float defaultValue = 0f)
        {
            var entry = stats.FirstOrDefault(s => s.key == key);
            return entry != null ? entry.value : defaultValue;
        }

        // Returns true if this attachment has the named stat defined.
        public bool HasStat(string key) => stats.Any(s => s.key == key);
    }
}