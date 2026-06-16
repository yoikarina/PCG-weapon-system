// Grip attachment; occupies the Grip slot.

using UnityEngine;

namespace GunAssemblyTool
{
    [CreateAssetMenu(fileName = "NewGrip", menuName = "GunAssemblyTool/Parts/Grip")]
    public class GripData : AttachmentData
    {
        protected override void OnValidate()
        {
            attachType = AttachmentType.Grip;
            base.OnValidate();
        }
    }
}
