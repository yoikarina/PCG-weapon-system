// Underbarrel attachment; occupies the Underbarrel slot with foregrip and launcher flags.

using UnityEngine;

namespace GunAssemblyTool
{
    [CreateAssetMenu(fileName = "NewUnderbarrel", menuName = "GunAssemblyTool/Parts/Underbarrel")]
    public class UnderbarrelData : AttachmentData
    {
        [Header("Underbarrel")]
        [Tooltip("Mark as a foregrip to apply custom recoil or handling logic in your game.")]
        public bool isForegrip = false;

        [Tooltip("Mark as a launcher to enable grenade or secondary projectile mechanics.")]
        public bool isLauncher = false;

        // Locks attachType to Underbarrel and runs the base validation.
        protected override void OnValidate()
        {
            attachType = AttachmentType.Underbarrel;
            base.OnValidate();
        }
    }
}
