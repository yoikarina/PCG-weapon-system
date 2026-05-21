// Grip attachment; occupies the Grip slot and modifies handling via the base class bonus fields.

using UnityEngine;

namespace GunAssemblyTool
{
    [CreateAssetMenu(fileName = "NewGrip", menuName = "GunAssemblyTool/Parts/Grip")]
    public class GripData : AttachmentData
    {
        // Locks attachType to Grip and runs the base validation.
        protected override void OnValidate()
        {
            attachType = AttachmentType.Grip;
            base.OnValidate();
        }
    }
}
