// Describes one attachment slot on a gun body, including optional physical interface constraints.

using System.Collections.Generic;
using UnityEngine;

namespace GunAssemblyTool
{
    [System.Serializable]
    public class SlotData
    {
        // The attachment type this slot accepts (e.g. Magazine, Barrel).
        // Only one attachment of this type can be equipped at a time.
        public AttachmentType slotType;

        // Optional whitelist of thread sizes that fit this slot.
        // If the list is empty, any thread type is accepted.
        // Used for Barrel and Muzzle slots to enforce physical interface matching.
        [Tooltip("Whitelist of allowed thread sizes. Leave empty for no restriction.")]
        public List<ThreadType> allowedThreads = new List<ThreadType>();

        // Optional whitelist of magazine formats that fit this slot.
        // If the list is empty, any magazine format is accepted.
        // Used for Magazine slots to enforce physical interface matching.
        [Tooltip("Whitelist of allowed magazine formats. Leave empty for no restriction.")]
        public List<MagType> allowedMags = new List<MagType>();
    }
}
