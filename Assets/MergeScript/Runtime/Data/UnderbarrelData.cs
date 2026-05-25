// Underbarrel attachment; occupies the Underbarrel slot.

using UnityEngine;

namespace GunAssemblyTool
{
    [CreateAssetMenu(fileName = "NewUnderbarrel", menuName = "GunAssemblyTool/Parts/Underbarrel")]
    public class UnderbarrelData : AttachmentData
    {
        [Header("Underbarrel")]
        public bool isForegrip = false;
        public bool isLauncher = false;
        protected override void OnValidate()
        {
            attachType = AttachmentType.Underbarrel;
            base.OnValidate();
        }
    }
}
