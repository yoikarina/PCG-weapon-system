using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace GunAssemblyTool
{
    // Main bridge between data (state) and scene (GameObject)
    // Responsible for applying attachments visually to the gun
    [AddComponentMenu("GunAssemblyTool/Gun Assembly Controller")]
    public class GunAssemblyController : MonoBehaviour
    {
        [Header("Data")]

        // Attachment database
        public AttachmentRegistry registry;

        // Optional starting gun body
        public GunBodyData initialBody;

        [Header("Event")]

        // Called when assembly changes (for UI refresh, etc.)
        public UnityEvent onAssemblyChanged;

        // Called when validation fails (returns error message)
        public UnityEvent<string> onValidationFailed;

        private GunAssemblyState _state;

        // All attachment points on this prefab (slot → transform)
        private Dictionary<AttachmentType, GunAttachmentPoint> _points;

        public GunAssemblyState State => _state;
        public GunBodyData CurrentBody => _state?.Body;

        // Get current final stats
        public GunStats CurrentStats => _state?.ComputeStats() ?? default;

        private void Awake()
        {
            // Note:
            // registry might still be null at this point (assigned externally)
            // So we initialize with current value, and rebuild later if needed
            _state = new GunAssemblyState(registry);

            // When state changes → notify listeners
            _state.OnStateChanged += () => onAssemblyChanged?.Invoke();

            // When attachment changes → update visual
            _state.OnAttachmentChanged += SyncAttachmentPoint;

            // Find all attachment points on this prefab
            _points = GetComponentsInChildren<GunAttachmentPoint>(includeInactive: true)
                .ToDictionary(p => p.slotType, p => p);
        }

        private void Start()
        {
            // Apply initial body if set
            if (initialBody != null)
                _state.SetBody(initialBody);
        }

        // ── Basic API ─────────────────────────────

        public void SetBody(GunBodyData body) => _state.SetBody(body);

        public bool Equip(AttachmentType type, AttachmentData attachment)
            => _state.Equip(type, attachment);

        public bool Unequip(AttachmentType type) => _state.Unequip(type);

        public AttachmentData GetEquipped(AttachmentType type)
            => _state.GetEquipped(type);

        // Get all compatible attachments for a slot
        public List<AttachmentData> GetCompatible(AttachmentType type)
            => registry != null && _state.Body != null
                ? registry.GetCompatible(_state.Body, type)
                : new List<AttachmentData>();

        /// <summary>
        /// Apply a saved configuration (from generator or load)
        /// You can pass a registry to override the current one
        /// </summary>
        public void ApplyConfiguration(
            GunConfiguration config,
            IEnumerable<GunBodyData> allBodies,
            AttachmentRegistry overrideRegistry = null)
        {
            // If a different registry is provided → rebuild state
            var reg = overrideRegistry ?? registry;

            if (reg != null && reg != registry)
            {
                registry = reg;

                // Recreate state to ensure correct data source
                _state = new GunAssemblyState(registry);

                _state.OnStateChanged += () => onAssemblyChanged?.Invoke();
                _state.OnAttachmentChanged += SyncAttachmentPoint;
            }

            _state.FromConfiguration(config, allBodies);
        }

        // Validate current setup and notify if invalid
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

        // ── Internal: sync visual with data ─────────────────

        private void SyncAttachmentPoint(
            AttachmentType type, AttachmentData _, AttachmentData newAttach)
        {
            if (!_points.TryGetValue(type, out var point)) return;

            if (newAttach != null)
                point.Attach(newAttach);
            else
                point.Detach();
        }
    }
}