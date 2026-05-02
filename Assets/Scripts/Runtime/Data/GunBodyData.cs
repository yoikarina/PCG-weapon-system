using System.Collections.Generic;
using UnityEngine;

namespace GunAssemblyTool
{
    // Stores base data for a gun body (the core weapon without attachments)
    [CreateAssetMenu(fileName = "NewGunBody", menuName = "GunAssemblyTool/Gun Body Data")]
    public class GunBodyData : ScriptableObject
    {
        // Unique ID for this gun body
        public string bodyId;

        // The main model/prefab of the gun
        public GameObject bodyPrefab;

        // Tags used for compatibility checks (e.g. "rifle", "sniper")
        public List<string> tags = new List<string>();

        // Base stats of the gun (before attachments)
        public float baseDamage = 20f;
        public float baseFireRate = 600f;
        public float baseAccuracy = 0.8f;
        public float baseReloadTime = 2.5f;

        // Which attachment types this gun supports
        public List<AttachmentType> availableSlots = new List<AttachmentType>
        {
            AttachmentType.Magazine,
            AttachmentType.Barrel,
            AttachmentType.Stock,
            AttachmentType.Scope
        };

        // Cached data for faster lookup
        private HashSet<string> _tagCache;
        private HashSet<AttachmentType> _slotCache;

        // Called when loaded
        private void OnEnable() => RebuildCaches();

        private void OnValidate()
        {
            // Auto-generate ID if empty
            if (string.IsNullOrWhiteSpace(bodyId))
                bodyId = name.ToLowerInvariant().Replace(" ", "_");

            // Clean up tag strings
            for (int i = 0; i < tags.Count; i++)
                tags[i] = tags[i]?.Trim().ToLowerInvariant();

            // Update caches
            RebuildCaches();
        }

        // Convert lists to HashSet for faster checks
        private void RebuildCaches()
        {
            _tagCache = new HashSet<string>(tags);
            _slotCache = new HashSet<AttachmentType>(availableSlots);
        }

        // Check if this gun has a specific tag
        public bool HasTag(string tag)
        {
            if (_tagCache == null) RebuildCaches();
            return _tagCache.Contains(tag);
        }

        // Check if this gun supports a certain attachment type
        public bool HasSlot(AttachmentType type)
        {
            if (_slotCache == null) RebuildCaches();
            return _slotCache.Contains(type);
        }
    }
}