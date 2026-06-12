// State machine for one gun's active configuration; validates all changes and fires events on mutation.

using System;
using System.Collections.Generic;
using System.Linq;

namespace GunAssemblyTool
{
    public class GunAssemblyState
    {
        // ── Events ────────────────────────────────────────────────────────────

        // Fired when the gun body is replaced.
        // Parameters: (previousBody, newBody).
        public event Action<GunBodyData, GunBodyData> OnBodyChanged;

        // Fired when any slot's attachment changes (including equip, unequip, and
        // automatic removal triggered by a body swap).
        // Parameters: (slotType, previousAttachment, newAttachment).
        // Either attachment parameter may be null (null = empty slot).
        public event Action<AttachmentType, AttachmentData, AttachmentData> OnAttachmentChanged;

        // Fired after every mutation regardless of which field changed.
        // Subscribe here to refresh the UI with a single handler.
        public event Action OnStateChanged;

        // ── Internal data ─────────────────────────────────────────────────────

        private GunBodyData _body;

        // Current attachment in each slot. Dictionary gives O(1) slot lookup.
        private readonly Dictionary<AttachmentType, AttachmentData> _slots
            = new Dictionary<AttachmentType, AttachmentData>();

        // Registry reference used when restoring a config from a snapshot.
        private readonly AttachmentRegistry _registry;

        // Constructs a new state machine bound to the given registry.
        // The registry is used only in FromConfiguration to look up attachments by ID.
        public GunAssemblyState(AttachmentRegistry registry) { _registry = registry; }

        // ── Gun body ──────────────────────────────────────────────────────────

        // The currently active gun body, or null if none has been set.
        public GunBodyData Body => _body;

        // Replaces the active gun body.
        // Any equipped attachments that are no longer compatible with the new body
        // are automatically removed; OnAttachmentChanged fires for each removal.
        // OnBodyChanged and OnStateChanged fire once after all removals.
        public void SetBody(GunBodyData newBody)
        {
            var old = _body;
            _body = newBody;

            // Strip attachments that are incompatible with the new body
            var toRemove = new List<AttachmentType>();
            foreach (var kv in _slots)
            {
                if (newBody == null || !CompatibilityResolver.IsCompatible(newBody, kv.Value))
                    toRemove.Add(kv.Key);
            }
            foreach (var t in toRemove)
            {
                var removed = _slots[t];
                _slots.Remove(t);
                OnAttachmentChanged?.Invoke(t, removed, null);
            }

            OnBodyChanged?.Invoke(old, newBody);
            OnStateChanged?.Invoke();
        }

        // ── Attachments ───────────────────────────────────────────────────────

        // Equips the given attachment into the matching slot.
        // Runs a full four-step compatibility check before accepting the attachment.
        // Returns true on success, false if the attachment is incompatible.
        // Passing null is equivalent to calling Unequip(type).
        public bool Equip(AttachmentType type, AttachmentData attachment)
        {
            if (attachment == null) return Unequip(type);
            if (_body == null) return false;
            if (!CompatibilityResolver.IsCompatible(_body, attachment)) return false;

            _slots.TryGetValue(type, out var old);
            _slots[type] = attachment;

            OnAttachmentChanged?.Invoke(type, old, attachment);
            OnStateChanged?.Invoke();
            return true;
        }

        // Removes the attachment from the specified slot.
        // Returns true if an attachment was present and removed, false if the slot
        // was already empty.
        public bool Unequip(AttachmentType type)
        {
            if (!_slots.TryGetValue(type, out var old)) return false;
            _slots.Remove(type);
            OnAttachmentChanged?.Invoke(type, old, null);
            OnStateChanged?.Invoke();
            return true;
        }

        // Returns the attachment currently equipped in the given slot,
        // or null if the slot is empty.
        public AttachmentData GetEquipped(AttachmentType type)
        {
            _slots.TryGetValue(type, out var a);
            return a;
        }

        // Returns true if the specified slot contains no attachment.
        public bool IsSlotEmpty(AttachmentType type) => !_slots.ContainsKey(type);

        // Read-only view of all currently equipped slot–attachment pairs.
        public IReadOnlyDictionary<AttachmentType, AttachmentData> AllSlots => _slots;

        // ── Validation ────────────────────────────────────────────────────────

        // Validates the entire current configuration and returns a list of error strings.
        // An empty list indicates a fully valid configuration.
        // Under normal operation Equip() prevents invalid states from forming; this
        // method is useful after loading external data or direct registry modifications.
        public List<string> Validate()
        {
            var errors = new List<string>();
            if (_body == null) { errors.Add("No gun body selected"); return errors; }
            foreach (var kv in _slots)
                if (!CompatibilityResolver.IsCompatible(_body, kv.Value))
                    errors.Add($"{kv.Key} slot: {kv.Value.attachmentId} is not compatible");
            return errors;
        }

        // ── Stats ─────────────────────────────────────────────────────────────

        // Computes and returns the final GunStats by stacking all equipped attachment
        // bonuses on top of the active gun body's base stats.
        public GunStats ComputeStats() =>
            CompatibilityResolver.ComputeStats(_body, _slots.Values);

        // Returns the total HP bonus contributed by all equipped BarrelData attachments.
        // GunAssemblyController broadcasts this value via onHpBonusChanged whenever
        // the gun configuration changes, allowing Player.BuffAttributes to update HP.
        public int ComputeBarrelHpBonus() =>
            CompatibilityResolver.ComputeBarrelHpBonus(_slots.Values);

        // ── Serialisation ─────────────────────────────────────────────────────

        // Exports the current configuration as a lightweight GunConfiguration snapshot.
        // The snapshot contains only string IDs — no Unity object references —
        // making it safe to serialise to JSON, store in save data, or send over a network.
        // ── Media resolution ──────────────────────────────────────────────────

        // Returns a ResolvedMedia snapshot by starting from the gun body's
        // mediaData and letting each equipped attachment's mediaOverride
        // replace individual fields that are not null.
        // Call this after any configuration change to refresh your animation,
        // audio and VFX references.
        //
        // Example usage in your own MonoBehaviour:
        //   var media = assemblyState.ResolveMedia();
        //   audioSource.clip = media.shootSFX;
        //   animator.runtimeAnimatorController = ...;
        //   Instantiate(media.muzzleFlashVFX, muzzleSocket);
        // Builds a ResolvedMedia snapshot from the current configuration.
        // The gun body's mediaData is merged first (base values), then each
        // equipped attachment's mediaOverride is merged on top (overrides).
        // Any key present in an attachment overrides the same key from the body.
        public ResolvedMedia ResolveMedia()
        {
            var result = new ResolvedMedia();
            result.Merge(_body?.mediaData);
            foreach (var kv in _slots)
                result.Merge(kv.Value?.mediaOverride);
            return result;
        }

        public GunConfiguration ToConfiguration() => new GunConfiguration
        {
            bodyId = _body?.bodyId,
            slots  = _slots.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value.attachmentId)
        };

        // Restores the state machine from a GunConfiguration snapshot.
        // Resolves body and attachment IDs against allBodies and the registry,
        // then calls SetBody and Equip for each entry, triggering all relevant events.
        // This automatically syncs the scene via GunAssemblyController's event handlers.
        public void FromConfiguration(
            GunConfiguration config,
            IEnumerable<GunBodyData> allBodies)
        {
            if (config == null) return;

            SetBody(allBodies?.FirstOrDefault(b => b.bodyId == config.bodyId));

            if (_registry == null || config.slots == null) return;
            foreach (var kv in config.slots)
            {
                if (!Enum.TryParse<AttachmentType>(kv.Key, out var type)) continue;
                Equip(type, _registry.FindById(kv.Value));
            }
        }
    }

    // ── GunConfiguration ──────────────────────────────────────────────────────

    // Lightweight snapshot of one gun configuration.
    // Stores only string IDs — safe to serialise to JSON.
    [Serializable]
    public class GunConfiguration
    {
        public string bodyId;
        public Dictionary<string, string> slots = new Dictionary<string, string>();
    }
}
