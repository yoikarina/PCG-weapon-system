// Magazine attachment; occupies the Magazine slot and overrides the gun body's ammo capacity.

using UnityEngine;

namespace GunAssemblyTool
{
    [CreateAssetMenu(fileName = "NewMagazine", menuName = "GunAssemblyTool/Parts/Magazine")]
    public class MagazineData : AttachmentData
    {
        [Header("Magazine")]
        [Tooltip("Number of rounds this magazine holds. " +
                 "Replaces GunBodyData.baseAmmoCapacity when equipped.")]
        public int magSize = 30;

        // Locks attachType to Magazine and runs the base validation.
        protected override void OnValidate()
        {
            attachType = AttachmentType.Magazine;
            base.OnValidate();
        }
    }
}
