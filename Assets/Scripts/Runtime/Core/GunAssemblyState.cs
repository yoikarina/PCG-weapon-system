using System;
using System.Collections.Generic;
using System.Linq;

namespace GunAssemblyTool
{
    // Manages one gun assembly state (body + attachments)
    // Not a MonoBehaviour → can be used in both runtime and editor tools
    public class GunAssemblyState
    {
        // ── Events ─────────────────────────────────────────────

        // Called when gun body changes (old → new)
        public event Action<GunBodyData, GunBodyData> OnBodyChanged;

        // Called when an attachment changes (slot, old → new)
        public event Action<AttachmentType, AttachmentData, AttachmentData> OnAttachmentChanged;

        // Called whenever anything changes
        public event Action OnStateChanged;

        // ── Data ─────────────────────────────────────────────

        // Current gun body
        private GunBodyData _body;

        // Current equipped attachments (by slot type)
        private readonly Dictionary<AttachmentType, AttachmentData> _slots
            = new Dictionary<AttachmentType, AttachmentData>();

        // Reference to registry (used for loading by ID)
        private readonly AttachmentRegistry _registry;

        public GunAssemblyState(AttachmentRegistry registry)
        {
            _registry = registry;
        }

        // ── Gun Body ─────────────────────────────────────────

        public GunBodyData Body => _body;

        /// <summary>
        /// Set the gun body
        /// Automatically removes any attachments that are no longer valid
        /// </summary>
        public void SetBody(GunBodyData newBody)
        {
            var old = _body;
            _body = newBody;

            // Find attachments that are no longer compatible
            var toRemove = new List<AttachmentType>();

            foreach (var kv in _slots)
            {
                bool ok = newBody != null && CompatibilityResolver.IsCompatible(newBody, kv.Value);
                if (!ok) toRemove.Add(kv.Key);
            }

            // Remove them
            foreach (var t in toRemove)
            {
                var removed = _slots[t];
                _slots.Remove(t);

                OnAttachmentChanged?.Invoke(t, removed, null);
            }

            OnBodyChanged?.Invoke(old, newBody);
            OnStateChanged?.Invoke();
        }

        // ── Attachments ─────────────────────────────────────

        /// <summary>
        /// Equip an attachment
        /// Pass null to unequip
        /// Returns true if successful
        /// </summary>
        public bool Equip(AttachmentType type, AttachmentData attachment)
        {
            // Null = remove
            if (attachment == null) return Unequip(type);

            if (_body == null) return false;

            // Check compatibility
            if (!CompatibilityResolver.IsCompatible(_body, attachment)) return false;

            _slots.TryGetValue(type, out var old);
            _slots[type] = attachment;

            OnAttachmentChanged?.Invoke(type, old, attachment);
            OnStateChanged?.Invoke();

            return true;
        }

        /// <summary>
        /// Remove attachment from a slot
        /// </summary>
        public bool Unequip(AttachmentType type)
        {
            if (!_slots.TryGetValue(type, out var old)) return false;

            _slots.Remove(type);

            OnAttachmentChanged?.Invoke(type, old, null);
            OnStateChanged?.Invoke();

            return true;
        }

        // Get attachment in a slot
        public AttachmentData GetEquipped(AttachmentType type)
        {
            _slots.TryGetValue(type, out var a);
            return a;
        }

        // Check if a slot is empty
        public bool IsSlotEmpty(AttachmentType type) => !_slots.ContainsKey(type);

        // Read-only access to all slots
        public IReadOnlyDictionary<AttachmentType, AttachmentData> AllSlots => _slots;

        // ── Validation ─────────────────────────────────────

        /// <summary>
        /// Returns all problems in current setup
        /// Empty list = everything is valid
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (_body == null)
            {
                errors.Add("No gun body selected");
                return errors;
            }

            foreach (var kv in _slots)
            {
                if (!CompatibilityResolver.IsCompatible(_body, kv.Value))
                    errors.Add($"{kv.Key} slot: {kv.Value.attachmentId} is not compatible");
            }

            return errors;
        }

        // ── Stats ─────────────────────────────────────────

        // Calculate final gun stats
        public GunStats ComputeStats() =>
            CompatibilityResolver.ComputeStats(_body, _slots.Values);

        // ── Serialization ─────────────────────────────────

        // Convert current setup to a simple data format (for saving)
        public GunConfiguration ToConfiguration() => new GunConfiguration
        {
            bodyId = _body?.bodyId,

            // Convert enum → string, attachment → ID
            slots = _slots.ToDictionary(
                kv => kv.Key.ToString(),
                kv => kv.Value.attachmentId)
        };

        // Load setup from saved data
        public void FromConfiguration(GunConfiguration config, IEnumerable<GunBodyData> allBodies)
        {
            if (config == null) return;

            // Find matching gun body
            var body = allBodies?.FirstOrDefault(b => b.bodyId == config.bodyId);
            SetBody(body);

            if (_registry == null || config.slots == null) return;

            // Restore attachments
            foreach (var kv in config.slots)
            {
                if (!Enum.TryParse<AttachmentType>(kv.Key, out var type)) continue;

                var att = _registry.allAttachments
                    .FirstOrDefault(a => a.attachmentId == kv.Value);

                Equip(type, att);
            }
        }
    }

    [Serializable]
    public class GunConfiguration
    {
        // Saved gun body ID
        public string bodyId;

        // Saved attachments: slot → attachment ID
        public Dictionary<string, string> slots = new Dictionary<string, string>();
    }
}