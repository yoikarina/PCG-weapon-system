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
    }
}
