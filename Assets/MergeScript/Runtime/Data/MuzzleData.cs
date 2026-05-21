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

        [Tooltip("Noise reduction factor (0 = no reduction, 1 = silent). " +
                 "Only meaningful when isSuppressor is true.")]
        [Range(0f, 1f)]
        public float noiseReduction = 0f;

        // Locks attachType to Muzzle and runs the base validation.
        protected override void OnValidate()
        {
            attachType = AttachmentType.Muzzle;
            base.OnValidate();
        }
    }
}
