using System.Collections.Generic;
using UnityEngine;

namespace GunAssemblyTool
{
    // Stores data for a single attachment (like scope, magazine, etc.)
    [CreateAssetMenu(fileName = "NewAttachment", menuName = "GunAssemblyTool/Attachment Data")]
    public class AttachmentData : ScriptableObject
    {
        [Header("Basic info")]

        // Unique ID for this attachment (used for identification)
        public string attachmentId;

        // What type of attachment this is (scope, barrel, etc.)
        public AttachmentType attachType;

        [Header("Tag rules")]

        // The gun body MUST have all of these tags to allow this attachment
        // If empty, this attachment can fit any gun body
        [Tooltip("The gun body must contain all of these tags to be considered compatible. Leaving it blank means it will fit all gun bodies.")]
        public List<string> requiredTags = new List<string>();

        // If the gun body has ANY of these tags, this attachment cannot be used
        [Tooltip("Gun bodies containing any of these tags are incompatible.")]
        public List<string> forbiddenTags = new List<string>();

        [Header("Attribute bonus")]

        // Extra damage added by this attachment
        public float damageBonus = 0f;

        // Fire rate change (can be positive or negative)
        public float fireRateBonus = 0f;

        // Accuracy change
        public float accuracyBonus = 0f;

        // Reload time change (negative = faster reload)
        public float reloadTimeBonus = 0f;

        [Header("PCG weight")]

        // Used for random generation (higher = more likely to appear)
        // 0 means it will NOT be picked in random generation
        [Range(0f, 100f)]
        [Tooltip("The sampling weight during PCG random generation. 0 = not participating in random generation.")]
        public float spawnWeight = 10f;

        [Header("Prefab")]

        // The visual prefab of this attachment
        public GameObject attachmentPrefab;

        // Cached sets for faster lookup (used instead of List during runtime)
        private HashSet<string> _requiredCache;
        private HashSet<string> _forbiddenCache;

        // Called when the object is loaded
        private void OnEnable() => RebuildCaches();

        private void OnValidate()
        {
            // Auto-generate ID if empty
            if (string.IsNullOrWhiteSpace(attachmentId))
                attachmentId = name.ToLowerInvariant().Replace(" ", "_");

            // Clean up tag strings (remove spaces + make lowercase)
            for (int i = 0; i < requiredTags.Count; i++)
                requiredTags[i] = requiredTags[i]?.Trim().ToLowerInvariant();

            for (int i = 0; i < forbiddenTags.Count; i++)
                forbiddenTags[i] = forbiddenTags[i]?.Trim().ToLowerInvariant();

            // Rebuild cache after changes
            RebuildCaches();
        }

        // Convert lists to HashSet for faster checking
        private void RebuildCaches()
        {
            _requiredCache = new HashSet<string>(requiredTags);
            _forbiddenCache = new HashSet<string>(forbiddenTags);
        }

        // Get required tags as HashSet (auto rebuild if needed)
        public HashSet<string> RequiredTagSet
        {
            get { if (_requiredCache == null) RebuildCaches(); return _requiredCache; }
        }

        // Get forbidden tags as HashSet
        public HashSet<string> ForbiddenTagSet
        {
            get { if (_forbiddenCache == null) RebuildCaches(); return _forbiddenCache; }
        }
    }
}