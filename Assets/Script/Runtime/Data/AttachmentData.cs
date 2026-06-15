// Abstract base class for all attachment ScriptableObjects.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GunAssemblyTool
{
    public abstract class AttachmentData : ScriptableObject
    {
        [Header("Basic Info")]
        public string attachmentId;
        public string displayName;
        public AttachmentType attachType;

        [Header("Tag Rules")]
        // OR logic: gun body must have AT LEAST ONE of these tags to be compatible.
        // Leave empty to match any gun body.
        [Tooltip("Gun body must have at least one of these tags (OR logic). Leave empty to allow all gun bodies.")]
        public List<string> requiredTags = new List<string>();

        // Gun body must have NONE of these tags.
        [Tooltip("Gun bodies with any of these tags are incompatible.")]
        public List<string> forbiddenTags = new List<string>();

        [Header("Media Override")]
        // Optional — leave null to use the gun body's default media.
        // Set this to override specific animations, sounds or effects when
        // this attachment is equipped. For example a suppressor can replace
        // shootSFX with a quieter clip without touching the gun body data.
        public GunMediaData mediaOverride;

        [Header("Stats")]
        // Dynamic stat bonuses. MagSize overrides the body's base value; all others are additive.
        public List<StatEntry> stats = new List<StatEntry>();

        [Header("Visual")]
        [Tooltip("Prefab placed at the GunAttachmentPoint when equipped. Null = data only.")]
        public GameObject attachmentPrefab;

        // Runtime caches
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

        // OR logic: returns true when at least one required tag is present,
        // or when no required tags are defined (matches all gun bodies).
        public HashSet<string> RequiredTagSet
        {
            get { if (_requiredCache == null) RebuildCaches(); return _requiredCache; }
        }

        public HashSet<string> ForbiddenTagSet
        {
            get { if (_forbiddenCache == null) RebuildCaches(); return _forbiddenCache; }
        }

        public float GetStat(string key, float defaultValue = 0f)
        {
            var entry = stats.FirstOrDefault(s => s.key == key);
            return entry != null ? entry.AsFloat() : defaultValue;
        }

        public bool HasStat(string key) => stats.Any(s => s.key == key);
    }
}