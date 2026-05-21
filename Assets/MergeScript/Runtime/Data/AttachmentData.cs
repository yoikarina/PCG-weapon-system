// Abstract base class for all attachment ScriptableObjects.

using System.Collections.Generic;
using UnityEngine;

namespace GunAssemblyTool
{
    // Do not create this directly. Use a concrete subclass:
    //   Barrel / Magazine / Muzzle / Scope / Stock / Grip / Underbarrel / Skin
    public abstract class AttachmentData : ScriptableObject
    {
        [Header("Basic Info")]
        // Unique string identifier used for serialisation and config restore.
        // Auto-populated from the asset file name if left empty.
        public string attachmentId;

        // The slot type this attachment occupies on the gun body.
        // Locked to a fixed value by each concrete subclass via OnValidate.
        public AttachmentType attachType;

        [Header("Physical Interface")]
        // Thread size of this attachment.
        // Checked against the gun body slot's allowedThreads whitelist.
        // Set to None for attachments that do not use a threaded interface.
        [Tooltip("Thread size for barrel/muzzle slots. Leave None for non-threaded attachments.")]
        public ThreadType threadType = ThreadType.None;

        // Magazine format of this attachment.
        // Checked against the gun body slot's allowedMags whitelist.
        // Set to None for attachments that are not magazines.
        [Tooltip("Magazine format for magazine slots. Leave None for non-magazine attachments.")]
        public MagType magType = MagType.None;

        [Header("Tag Rules")]
        // The gun body must carry ALL of these tags for this attachment to be compatible.
        // Leave empty to allow this attachment on any gun body.
        // Multiple tags use AND logic, not OR — to support both "smg" and "rifle",
        // create two separate attachment assets.
        [Tooltip("Gun body must contain all of these tags. Leave empty to allow all gun bodies.")]
        public List<string> requiredTags = new List<string>();

        // The gun body must carry NONE of these tags for this attachment to be compatible.
        // Leave empty to apply no tag-based exclusions.
        [Tooltip("Gun bodies with any of these tags are incompatible with this attachment.")]
        public List<string> forbiddenTags = new List<string>();

        [Header("Attribute Bonus")]
        // All values are added on top of the gun body's base stats.
        // Negative values apply a debuff.
        public float damageBonus     = 0f;
        public float fireRateBonus   = 0f;   // RPM delta
        public float accuracyBonus   = 0f;   // 0-to-1 delta
        public float reloadTimeBonus = 0f;   // Seconds delta; negative = faster reload

        [Header("PCG Weight")]
        // Relative probability weight used during random generation.
        // Higher values make this attachment more likely to be selected.
        // 0 = never selected by PCG, but can still be equipped manually.
        // Suggested scale: Common = 60, Rare = 30, Legendary = 10.
        [Range(0f, 100f)]
        [Tooltip("Spawn weight for PCG. 0 = excluded from random generation.")]
        public float spawnWeight = 10f;

        [Header("Visual")]
        // Prefab instantiated at the matching GunAttachmentPoint when equipped.
        // If null, the attachment affects data only — no visual change occurs.
        [Tooltip("Prefab placed at the GunAttachmentPoint when equipped. Null = data only.")]
        public GameObject attachmentPrefab;

        // Internal HashSet caches for O(1) tag lookup.
        // Rebuilt on load and after every Inspector modification.
        private HashSet<string> _requiredCache;
        private HashSet<string> _forbiddenCache;

        // Called by Unity when the asset is loaded into memory.
        // Initialises the tag lookup caches.
        private void OnEnable() => RebuildCaches();

        // Called by Unity whenever the asset is modified in the Inspector.
        // Auto-fills attachmentId, normalises tags to lowercase,
        // then rebuilds the tag caches.
        protected virtual void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(attachmentId))
                attachmentId = name.ToLowerInvariant().Replace(" ", "_");

            for (int i = 0; i < requiredTags.Count; i++)
                requiredTags[i]  = requiredTags[i]?.Trim().ToLowerInvariant();
            for (int i = 0; i < forbiddenTags.Count; i++)
                forbiddenTags[i] = forbiddenTags[i]?.Trim().ToLowerInvariant();

            RebuildCaches();
        }

        // Rebuilds the required and forbidden tag HashSets from the serialised lists.
        private void RebuildCaches()
        {
            _requiredCache  = new HashSet<string>(requiredTags);
            _forbiddenCache = new HashSet<string>(forbiddenTags);
        }

        // Returns the required tags as a HashSet for O(1) membership testing.
        // Consumed by CompatibilityResolver.IsCompatible.
        public HashSet<string> RequiredTagSet
        {
            get { if (_requiredCache  == null) RebuildCaches(); return _requiredCache; }
        }

        // Returns the forbidden tags as a HashSet for O(1) membership testing.
        // Consumed by CompatibilityResolver.IsCompatible.
        public HashSet<string> ForbiddenTagSet
        {
            get { if (_forbiddenCache == null) RebuildCaches(); return _forbiddenCache; }
        }
    }
}
