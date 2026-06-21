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

        // HP bonus forwarded to Player via GunAssemblyController.onHpBonusChanged.
        // This is kept as a dedicated field (not in the stat list) because it affects
        // the player character rather than the gun's combat stats.
        [Tooltip("Player HP bonus while this barrel is equipped. " +
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
