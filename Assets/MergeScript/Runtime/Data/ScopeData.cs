// Scope attachment; occupies the Scope slot and carries a zoom level value.

using UnityEngine;

namespace GunAssemblyTool
{
    [CreateAssetMenu(fileName = "NewScope", menuName = "GunAssemblyTool/Parts/Scope")]
    public class ScopeData : AttachmentData
    {
        [Header("Scope")]
        [Tooltip("Zoom multiplier. Consume in your ADS / camera system.")]
        [Range(1f, 20f)]
        public float zoomLevel = 4f;

        protected override void OnValidate()
        {
            attachType = AttachmentType.Scope;
            base.OnValidate();
        }
    }
}
