// GunRuntimeData.cs
// The PRODUCER side of IGunDataProvider.
//
// The Weapon Workbench bakes this component onto every assembled weapon prefab at
// save time (see WeaponWindowTool.SaveEquipAssembly). It stores the
// ScriptableObject references that define the loadout ¡ª which serialize cleanly
// into the prefab ¡ª and rebuilds the runtime GunInstanceData on demand via
// GunAssemblyState.
//
// Only SO references are serialized (NOT the computed GunInstanceData, which uses
// Dictionaries and is rebuilt each play session). That makes a dropped / spawned /
// saved weapon prefab fully self-describing: GetGunData() reproduces its stats
// anywhere, with no Inspector wiring and no reverse prefab->data lookup.

using System.Collections.Generic;
using UnityEngine;

namespace GunAssemblyTool
{
    [DisallowMultipleComponent]
    public class GunRuntimeData : MonoBehaviour, IGunDataProvider
    {
        [Tooltip("Gun body this loadout is built on. Serialized into the prefab.")]
        public GunBodyData body;

        [Tooltip("Equipped attachments. Serialized into the prefab.")]
        public List<AttachmentData> attachments = new List<AttachmentData>();

        // Cached snapshot ¡ª built on the first GetGunData() call, cleared by Invalidate().
        private GunInstanceData _cached;

        /// <inheritdoc/>
        public GunInstanceData GetGunData()
        {
            if (_cached == null) _cached = Build();
            return _cached;
        }

        /// Rebuild from the current SO references. Call this after swapping parts at runtime.
        public GunInstanceData Rebuild()
        {
            _cached = Build();
            return _cached;
        }

        /// Drop the cache without rebuilding (next GetGunData() will rebuild).
        public void Invalidate() => _cached = null;

        private GunInstanceData Build()
        {
            if (body == null)
            {
                Debug.LogError("[GunRuntimeData] No GunBodyData assigned.", this);
                return null;
            }

            var state = new GunAssemblyState();
            state.SetBody(body);
            foreach (var a in attachments)
                if (a != null) state.TryEquip(a);   // compatibility is re-validated here
            return state.Build();
        }
    }
}