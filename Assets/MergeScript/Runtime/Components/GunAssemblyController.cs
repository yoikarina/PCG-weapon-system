// Runtime hub placed on the gun prefab root; bridges GunAssemblyState with scene visuals and external systems.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace GunAssemblyTool
{
    [AddComponentMenu("GunAssemblyTool/Gun Assembly Controller")]
    public class GunAssemblyController : MonoBehaviour
    {
        [Header("Data")]
        // The global attachment registry. Injected at runtime by RandomGunSpawner
        // via the overrideRegistry parameter of ApplyConfiguration.
        public AttachmentRegistry registry;

        // Optional gun body to load automatically on Start.
        // Leave null if the body will be set programmatically.
        public GunBodyData initialBody;

        [Header("Events")]
        // Fired after any configuration change (body swap, equip, or unequip).
        // Bind UI refresh methods here to keep displays in sync.
        public UnityEvent onAssemblyChanged;

        // Fired by ValidateAndNotify when the current configuration contains errors.
        // The parameter is a newline-separated list of error descriptions.
        public UnityEvent<string> onValidationFailed;

        // Fired when the total barrel HP bonus changes.
        // Bind this to Player.BuffAttributes so the player's HP updates automatically
        // whenever a barrel is equipped or unequipped.
        [Tooltip("Fired when barrel HP bonus changes. Wire to Player.BuffAttributes.")]
        public UnityEvent<int> onHpBonusChanged;

        // ── Internal state ────────────────────────────────────────────────────

        private GunAssemblyState                               _state;

        // Map from slot type to the GunAttachmentPoint child component for that slot.
        // Built in Awake by scanning all child GameObjects.
        private Dictionary<AttachmentType, GunAttachmentPoint> _points;

        // ── Public properties ─────────────────────────────────────────────────

        public GunAssemblyState State        => _state;
        public GunBodyData      CurrentBody  => _state?.Body;
        public GunStats         CurrentStats => _state?.ComputeStats() ?? default;

        // Current ammo capacity: MagazineData.magSize when a magazine is equipped,
        // otherwise GunBodyData.baseAmmoCapacity.
        public int CurrentAmmoCapacity => _state?.ComputeStats().ammoCapacity ?? 0;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        // Initialises the internal state machine and builds the slot-to-mount-point map.
        // Note: registry may be null at this point; it is injected later by
        // RandomGunSpawner through ApplyConfiguration(overrideRegistry).
        private void Awake()
        {
            _state = new GunAssemblyState(registry);
            _state.OnStateChanged      += HandleStateChanged;
            _state.OnAttachmentChanged += SyncAttachmentPoint;

            // Scan all child nodes for GunAttachmentPoint components and map them
            // by slot type so SyncAttachmentPoint can look them up in O(1).
            _points = GetComponentsInChildren<GunAttachmentPoint>(includeInactive: true)
                .ToDictionary(p => p.slotType, p => p);
        }

        // Sets the initial gun body if one has been assigned in the Inspector.
        private void Start()
        {
            if (initialBody != null) _state.SetBody(initialBody);
        }

        // ── Public API ────────────────────────────────────────────────────────

        // Replaces the active gun body. Incompatible attachments are removed automatically.
        public void SetBody(GunBodyData body) => _state.SetBody(body);

        // Equips the given attachment into its slot after a compatibility check.
        // Returns true on success, false if the attachment is incompatible.
        public bool Equip(AttachmentType type, AttachmentData attachment) =>
            _state.Equip(type, attachment);

        // Removes the attachment from the specified slot.
        // Returns false if the slot was already empty.
        public bool Unequip(AttachmentType type) => _state.Unequip(type);

        // Returns the attachment currently in the specified slot, or null if empty.
        public AttachmentData GetEquipped(AttachmentType type) => _state.GetEquipped(type);

        // Returns all attachments of the given type that are compatible with the
        // current gun body. Used to populate UI slot picker lists.
        public List<AttachmentData> GetCompatible(AttachmentType type)
            => registry != null && _state.Body != null
                ? registry.GetCompatible(_state.Body, type)
                : new List<AttachmentData>();

        // Applies a GunConfiguration snapshot to this controller.
        // Accepts an optional overrideRegistry so RandomGunSpawner can inject the
        // registry into a freshly instantiated prefab whose own registry field is null.
        // When the override differs from the current registry the internal state machine
        // is rebuilt to ensure all subsequent calls use the correct data source.
        public void ApplyConfiguration(
            GunConfiguration     config,
            IEnumerable<GunBodyData> allBodies,
            AttachmentRegistry   overrideRegistry = null)
        {
            var reg = overrideRegistry ?? registry;
            if (reg != null && reg != registry)
            {
                registry = reg;
                _state   = new GunAssemblyState(registry);
                _state.OnStateChanged      += HandleStateChanged;
                _state.OnAttachmentChanged += SyncAttachmentPoint;
            }
            _state.FromConfiguration(config, allBodies);
        }

        // Validates the current configuration and fires onValidationFailed with a
        // description of any errors found. Returns true when the config is valid.
        public bool ValidateAndNotify()
        {
            var errors = _state.Validate();
            if (errors.Count > 0)
            {
                onValidationFailed?.Invoke(string.Join("\n", errors));
                return false;
            }
            return true;
        }

        // ── Internal event handlers ───────────────────────────────────────────

        // Invoked by GunAssemblyState.OnStateChanged after every mutation.
        // Notifies the UI via onAssemblyChanged and forwards the updated barrel
        // HP bonus to any listeners of onHpBonusChanged (typically Player.BuffAttributes).
        private void HandleStateChanged()
        {
            onAssemblyChanged?.Invoke();
            onHpBonusChanged?.Invoke(_state.ComputeBarrelHpBonus());
        }

        // Invoked by GunAssemblyState.OnAttachmentChanged whenever a slot changes.
        // Finds the matching GunAttachmentPoint child and calls Attach or Detach
        // to update the 3D scene to match the new data state.
        private void SyncAttachmentPoint(
            AttachmentType type, AttachmentData _, AttachmentData newAttach)
        {
            if (!_points.TryGetValue(type, out var point)) return;
            if (newAttach != null) point.Attach(newAttach);
            else                  point.Detach();
        }
    }
}
