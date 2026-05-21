// Barrel attachment; occupies the Barrel slot and carries hpBonus for the player.

using UnityEngine;

namespace GunAssemblyTool
{
    [CreateAssetMenu(fileName = "NewBarrel", menuName = "GunAssemblyTool/Parts/Barrel")]
    public class BarrelData : AttachmentData
    {
        [Header("Barrel")]
        [Tooltip("Display-only barrel length in millimetres. Has no effect on stats.")]
        public float barrelLengthMM = 400f;

        [Tooltip("HP bonus granted to the player while this barrel is equipped. " +
                 "Wire GunAssemblyController.onHpBonusChanged to Player.BuffAttributes.")]
        public int hpBonus = 0;

        // Locks attachType to Barrel and runs the base validation.
        protected override void OnValidate()
        {
            attachType = AttachmentType.Barrel;
            base.OnValidate();
        }
    }
}
