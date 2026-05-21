// ScriptableObject asset that defines a gun body's tags, slots, base stats, and scene prefab.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GunAssemblyTool
{
    [CreateAssetMenu(fileName = "NewGunBody", menuName = "GunAssemblyTool/Gun Body Data")]
    public class GunBodyData : ScriptableObject
    {
        [Header("Basic Info")]
        // Unique string identifier used for serialisation and config restore.
        // Auto-populated from the asset file name if left empty.
        public string bodyId;

        [Header("Scene Model")]
        // The 3D prefab instantiated in the scene when this body is selected.
        // Must have GunAssemblyController on its root and
        // GunAttachmentPoint components on the appropriate child nodes.
        public GameObject bodyPrefab;

        [Header("Compatibility Tags")]
        // Semantic feature tags describing this gun body (e.g. "smg", "compact").
        // CompatibilityResolver checks whether an attachment's requiredTags
        // are a subset of these tags. All entries are normalised to lowercase.
        public List<string> tags = new List<string>();

        [Header("Base Stats")]
        public float baseDamage       = 20f;
        public float baseFireRate     = 600f;   // Rounds per minute
        [Range(0f, 1f)]
        public float baseAccuracy     = 0.8f;
        public float baseReloadTime   = 2.5f;   // Seconds

        // Default ammo count when no magazine attachment is equipped.
        // Overridden by MagazineData.magSize when a magazine is present.
        public int baseAmmoCapacity = 30;

        [Header("Available Slots")]
        // List of slots this gun body exposes to the attachment system.
        // Each SlotData entry defines the slot type and optional physical
        // interface whitelists (allowedThreads, allowedMags).
        public List<SlotData> slots = new List<SlotData>
        {
            new SlotData { slotType = AttachmentType.Magazine },
            new SlotData { slotType = AttachmentType.Barrel   },
            new SlotData { slotType = AttachmentType.Muzzle   },
            new SlotData { slotType = AttachmentType.Stock     },
            new SlotData { slotType = AttachmentType.Scope     }
        };

        // Internal caches for fast O(1) lookup at runtime.
        // Rebuilt by RebuildCaches() whenever the asset is modified or loaded.
        private HashSet<string>                      _tagCache;
        private Dictionary<AttachmentType, SlotData> _slotCache;

        // Called by Unity when the asset is loaded into memory.
        // Initialises the runtime lookup caches.
        private void OnEnable() => RebuildCaches();

        // Called by Unity whenever the asset is modified in the Inspector.
        // Auto-fills bodyId from the asset name, normalises tags to lowercase,
        // then rebuilds the lookup caches.
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(bodyId))
                bodyId = name.ToLowerInvariant().Replace(" ", "_");

            for (int i = 0; i < tags.Count; i++)
                tags[i] = tags[i]?.Trim().ToLowerInvariant();

            RebuildCaches();
        }

        // Rebuilds the tag HashSet and the slot Dictionary from the serialised lists.
        // Called on load and after every Inspector modification.
        private void RebuildCaches()
        {
            _tagCache  = new HashSet<string>(tags);
            _slotCache = slots
                .Where(s => s != null)
                .ToDictionary(s => s.slotType, s => s);
        }

        // Returns true if this gun body carries the specified tag.
        // Called frequently by CompatibilityResolver; must be O(1).
        public bool HasTag(string tag)
        {
            if (_tagCache == null) RebuildCaches();
            return _tagCache.Contains(tag);
        }

        // Returns true if this gun body has a slot of the specified type.
        // Called by CompatibilityResolver to enforce slot-type support.
        public bool HasSlot(AttachmentType type)
        {
            if (_slotCache == null) RebuildCaches();
            return _slotCache.ContainsKey(type);
        }

        // Returns the SlotData for the specified slot type, or null if unsupported.
        // Used by CompatibilityResolver to retrieve the physical interface whitelists.
        public SlotData GetSlot(AttachmentType type)
        {
            if (_slotCache == null) RebuildCaches();
            _slotCache.TryGetValue(type, out var slot);
            return slot;
        }

        // Enumerates all slot types this gun body supports.
        // Used by GunGenerator to iterate available slots during PCG generation.
        public IEnumerable<AttachmentType> AvailableSlotTypes
        {
            get
            {
                if (_slotCache == null) RebuildCaches();
                return _slotCache.Keys;
            }
        }
    }
}
