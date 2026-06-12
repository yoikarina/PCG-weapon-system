// Magazine attachment; occupies the Magazine slot.
// Set MagSize via the Stats list in the Workbench — it will override the gun body's base value.

using UnityEngine;

namespace GunAssemblyTool
{
    [CreateAssetMenu(fileName = "NewMagazine", menuName = "GunAssemblyTool/Parts/Magazine")]
    public class MagazineData : AttachmentData
    {
        // Locks attachType to Magazine and runs the base validation.
        protected override void OnValidate()
        {
            attachType = AttachmentType.Magazine;
            base.OnValidate();
        }

        // Convenience property: reads MagSize from the dynamic stat list.
        // Returns 0 if MagSize has not been added to this attachment.
        public int MagSize => (int)GetStat(StatKeys.MagSize, 0f);
    }
}
