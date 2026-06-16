// Skin attachment; occupies the Skin slot.

using UnityEngine;

namespace GunAssemblyTool
{
    [CreateAssetMenu(fileName = "NewSkin", menuName = "GunAssemblyTool/Parts/Skin")]
    public class SkinData : AttachmentData
    {
        [Header("Skin")]
        [Tooltip("Material applied to the gun body mesh when this skin is equipped.")]
        public Material skinMaterial;
        protected override void OnValidate()
        {
            attachType = AttachmentType.Skin;
            base.OnValidate();
        }
    }
}
