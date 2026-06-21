// Scope attachment; occupies the Scope slot and carries a zoom level value.

using UnityEngine;

namespace GunAssemblyTool
{
    [CreateAssetMenu(fileName = "NewScope", menuName = "GunAssemblyTool/Parts/Scope")]
    public class ScopeData : AttachmentData
    {

        protected override void OnValidate()
        {
            attachType = AttachmentType.Scope;
            base.OnValidate();
        }
    }
}
