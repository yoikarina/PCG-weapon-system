// Tracks the current gun body and equipped attachments at runtime.
// Call Build() after assembly to get a GunInstanceData snapshot
// that your minigame can use directly.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GunAssemblyTool
{
    public class GunAssemblyState
    {
        private GunBodyData _body;
        private Dictionary<AttachmentType, AttachmentData> _slots
            = new Dictionary<AttachmentType, AttachmentData>();

        // ── Assembly ──────────────────────────────────────────────────────────

        public void SetBody(GunBodyData body)
        {
            _body = body;
        }

        public bool TryEquip(AttachmentData attachment)
        {
            if (!CompatibilityResolver.IsCompatible(_body, attachment)) return false;
            _slots[attachment.attachType] = attachment;
            return true;
        }

        public void Unequip(AttachmentType slot)
        {
            _slots.Remove(slot);
        }

        public void Clear()
        {
            _body = null;
            _slots.Clear();
        }

        // ── Queries ───────────────────────────────────────────────────────────

        public GunBodyData Body => _body;

        public AttachmentData GetAttachment(AttachmentType slot)
        {
            _slots.TryGetValue(slot, out var a);
            return a;
        }

        public IEnumerable<AttachmentData> AllAttachments => _slots.Values;

        // ── Build — main output ───────────────────────────────────────────────

        // Creates a complete GunInstanceData snapshot from the current state.
        // Call this after assembly is done. The result is safe to cache and
        // pass around in your minigame — it has no dependency on this state object.
        //
        // Usage:
        //   GunInstanceData gun = assemblyState.Build();
        //   player.walkSpeed   -= gun.stats.weight * 0.05f;
        //   audioSource.clip    = gun.media.GetSFX("ShootSFX");
        public GunInstanceData Build()
        {
            var instance = new GunInstanceData
            {
                body = _body,
                attachments = _slots.Values.ToList(),
                stats = CompatibilityResolver.ComputeStats(_body, _slots.Values),
                media = ResolveMedia()
            };
            return instance;
        }

        // ── Media resolution ──────────────────────────────────────────────────

        // Merges media from the gun body first, then each attachment override on top.
        // Any key set by an attachment replaces the matching key from the body.
        public ResolvedMedia ResolveMedia()
        {
            var result = new ResolvedMedia();
            result.Merge(_body?.mediaData);
            foreach (var kv in _slots)
                result.Merge(kv.Value?.mediaOverride);
            return result;
        }
    }
}