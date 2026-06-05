// Runtime hub on the gun prefab root; bridges GunAssemblyState with scene visuals, GunData, and external systems.

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
        // Injected at runtime by RandomGunSpawner via ApplyConfiguration(overrideRegistry).
        public AttachmentRegistry registry;

        // Optional body to load automatically on Start.
        public GunBodyData initialBody;

        [Header("Events")]
        // Fired after any configuration change. Bind UI refresh methods here.
        public UnityEvent onAssemblyChanged;

        // Fired by ValidateAndNotify when the config contains errors.
        public UnityEvent<string> onValidationFailed;

        // Fired when the barrel HP bonus changes.
        // Wire to Player.BuffAttributes in the Inspector.
        [Tooltip("Fired when barrel HP bonus changes. Wire to Player.BuffAttributes.")]
        public UnityEvent<int> onHpBonusChanged;

        private GunAssemblyState                               _state;
        private Dictionary<AttachmentType, GunAttachmentPoint> _points;

        // Optional GunData component on this GameObject.
        // When present it is kept in sync with the current attachment stats
        // so Player.Shoot / Reload / Swap logic has accurate ammo and HP values.
        private GunData _gunData;

        public GunAssemblyState State             => _state;
        public GunBodyData      CurrentBody       => _state?.Body;
        public GunStats         CurrentStats      => _state?.ComputeStats() ?? default;
        public int CurrentAmmoCapacity => _state?.ComputeStats().ammoCapacity ?? 0;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _state = new GunAssemblyState(registry);
            _state.OnStateChanged      += HandleStateChanged;
            _state.OnAttachmentChanged += SyncAttachmentPoint;

            _points = GetComponentsInChildren<GunAttachmentPoint>(includeInactive: true)
                      .ToDictionary(p => p.slotType, p => p);

            // Cache GunData if one exists on this prefab (placed by WeaponWindowTool).
            _gunData = GetComponent<GunData>();
        }

        private void Start()
        {
            if (initialBody != null) _state.SetBody(initialBody);
        }

        // ── Public API ────────────────────────────────────────────────────────

        // Replaces the active gun body; incompatible attachments are removed automatically.
        public void SetBody(GunBodyData body) => _state.SetBody(body);

        // Equips an attachment after a compatibility check. Returns false if incompatible.
        public bool Equip(AttachmentType type, AttachmentData att) => _state.Equip(type, att);

        // Removes the attachment from a slot.
        public bool Unequip(AttachmentType type) => _state.Unequip(type);

        // Returns the attachment currently in the given slot, or null.
        public AttachmentData GetEquipped(AttachmentType type) => _state.GetEquipped(type);

        // Returns all attachments compatible with the current body for the given slot.
        public List<AttachmentData> GetCompatible(AttachmentType type)
            => registry != null && _state.Body != null
                ? registry.GetCompatible(_state.Body, type)
                : new List<AttachmentData>();

        // Applies a GunConfiguration snapshot.
        // Accepts an optional overrideRegistry for freshly instantiated prefabs
        // whose own registry field is still null.
        public void ApplyConfiguration(
            GunConfiguration         config,
            IEnumerable<GunBodyData> allBodies,
            AttachmentRegistry       overrideRegistry = null)
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

        // Validates the config and fires onValidationFailed with any errors found.
        public bool ValidateAndNotify()
        {
            var errors = _state.Validate();
            if (errors.Count > 0) { onValidationFailed?.Invoke(string.Join("\n", errors)); return false; }
            return true;
        }

        // ── Internal handlers ─────────────────────────────────────────────────

        private void HandleStateChanged()
        {
            onAssemblyChanged?.Invoke();
            onHpBonusChanged?.Invoke(_state.ComputeBarrelHpBonus());

            // Keep GunData in sync so Player ammo logic stays accurate.
            SyncGunData();
        }

        private void SyncAttachmentPoint(AttachmentType type, AttachmentData _, AttachmentData newAttach)
        {
            if (!_points.TryGetValue(type, out var point)) return;
            if (newAttach != null) point.Attach(newAttach);
            else                  point.Detach();
        }

        // Pushes the current computed stats into GunData when one is present.
        // This allows Player.Shoot / Reload to read magSize and hitPoints without
        // depending directly on GunAssemblyState.
        private void SyncGunData()
        {
            if (_gunData == null) return;
            var stats = _state.ComputeStats();
            _gunData.magSize   = stats.ammoCapacity;
            _gunData.hitPoints = _state.ComputeBarrelHpBonus();
            _gunData.SetAmmo();
        }
    }
}
