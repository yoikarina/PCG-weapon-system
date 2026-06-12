// ScriptableObject asset defining a gun body's tags, slots, dynamic stats, and scene prefab.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GunAssemblyTool
{
    [CreateAssetMenu(fileName = "NewGunBody", menuName = "GunAssemblyTool/Gun Body Data")]
    public class GunBodyData : ScriptableObject
    {
        [Header("Basic Info")]
        // Unique identifier used for serialisation and config restore.
        public string bodyId;

        // Human-readable name shown in the Weapon Workbench UI.
        public string displayName;

        [Header("Scene Model")]
        // Instantiated at runtime by RandomGunSpawner.
        // Must have GunAssemblyController on root and GunAttachmentPoint on children.
        public GameObject bodyPrefab;

        [Header("Editor Tool")]
        // Calibrated prefab used by WeaponWindowTool (has Socket_ children).
        public GameObject partObject;

        [Header("Compatibility Tags")]
        // Semantic tags (e.g. "smg", "rifle"). Normalised to lowercase by OnValidate.
        public List<string> tags = new List<string>();

        [Header("Media")]
        // Animation, SFX and VFX references for this gun body.
        // Attachments can override individual fields via their own mediaOverride.
        public GunMediaData mediaData;

        [Header("Available Slots")]
        // Each SlotData defines slot type and optional physical interface whitelists.
        public List<SlotData> slots = new List<SlotData>
        {
            new SlotData { slotType = AttachmentType.Magazine },
            new SlotData { slotType = AttachmentType.Barrel   },
            new SlotData { slotType = AttachmentType.Muzzle   },
            new SlotData { slotType = AttachmentType.Stock     },
            new SlotData { slotType = AttachmentType.Scope     }
        };

        [Header("Stats")]
        // Dynamic stat list. Only stats the designer explicitly adds are present.
        // Use StatKeys constants for preset stat names.
        // CompatibilityResolver.ComputeStats reads these as base values.
        public List<StatEntry> stats = new List<StatEntry>();

        // ── Runtime caches ────────────────────────────────────────────────────
        private HashSet<string>                      _tagCache;
        private Dictionary<AttachmentType, SlotData> _slotCache;

        private void OnEnable()  => RebuildCaches();

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(bodyId))
                bodyId = name.ToLowerInvariant().Replace(" ", "_");
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = name;
            for (int i = 0; i < tags.Count; i++)
                tags[i] = tags[i]?.Trim().ToLowerInvariant();
            RebuildCaches();
        }

        private void RebuildCaches()
        {
            _tagCache  = new HashSet<string>(tags);
            _slotCache = slots.Where(s => s != null)
                              .ToDictionary(s => s.slotType, s => s);
        }

        // Returns true if this body carries the given tag. O(1).
        public bool HasTag(string tag)
        {
            if (_tagCache == null) RebuildCaches();
            return _tagCache.Contains(tag);
        }

        // Returns true if this body has a slot of the given type. O(1).
        public bool HasSlot(AttachmentType type)
        {
            if (_slotCache == null) RebuildCaches();
            return _slotCache.ContainsKey(type);
        }

        // Returns the SlotData for the given slot type, or null if unsupported.
        public SlotData GetSlot(AttachmentType type)
        {
            if (_slotCache == null) RebuildCaches();
            _slotCache.TryGetValue(type, out var slot);
            return slot;
        }

        // Enumerates all supported slot types. Used by GunGenerator.
        public IEnumerable<AttachmentType> AvailableSlotTypes
        {
            get
            {
                if (_slotCache == null) RebuildCaches();
                return _slotCache.Keys;
            }
        }

        // Returns the value of a named stat, or defaultValue if not present.
        public float GetStat(string key, float defaultValue = 0f)
        {
            var entry = stats.FirstOrDefault(s => s.key == key);
            return entry != null ? entry.value : defaultValue;
        }

        // Returns true if this data has the named stat defined.
        public bool HasStat(string key) => stats.Any(s => s.key == key);
    }
}
