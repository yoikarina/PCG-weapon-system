// Muzzle attachment; occupies the Muzzle slot with optional suppressor properties.

using UnityEngine;

namespace GunAssemblyTool
{
    [CreateAssetMenu(fileName = "NewMuzzle", menuName = "GunAssemblyTool/Parts/Muzzle")]
    public class MuzzleData : AttachmentData
    {
        protected override void OnValidate()
        {
            attachType = AttachmentType.Muzzle;
            base.OnValidate();
        }
    }
}
