// Muzzle attachment; occupies the Muzzle slot with optional suppressor properties.

using UnityEngine;

namespace GunAssemblyTool
{
    [CreateAssetMenu(fileName = "NewMuzzle", menuName = "GunAssemblyTool/Parts/Muzzle")]
    public class MuzzleData : AttachmentData
    {
        [Header("Muzzle")]
        [Tooltip("Flag this as a suppressor to trigger audio swap logic in your game.")]
        public bool isSuppressor = false;

        [Range(0f, 1f)]
        [Tooltip("Noise reduction factor. Only relevant when isSuppressor is true.")]
        public float noiseReduction = 0f;

        protected override void OnValidate()
        {
            attachType = AttachmentType.Muzzle;
            base.OnValidate();
        }
    }
}
