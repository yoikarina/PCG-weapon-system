// Skin attachment; occupies the Skin slot and applies a material to the gun body mesh.

using UnityEngine;

namespace GunAssemblyTool
{
    [CreateAssetMenu(fileName = "NewSkin", menuName = "GunAssemblyTool/Parts/Skin")]
    public class SkinData : AttachmentData
    {
        [Header("Skin")]
        [Tooltip("Material applied to the gun body mesh renderer when this skin is equipped.")]
        public Material skinMaterial;

        // Locks attachType to Skin and runs the base validation.
        protected override void OnValidate()
        {
            attachType = AttachmentType.Skin;
            base.OnValidate();
        }
    }
}
