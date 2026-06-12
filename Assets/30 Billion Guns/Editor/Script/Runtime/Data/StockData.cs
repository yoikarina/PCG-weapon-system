// Stock attachment; occupies the Stock slot.

using UnityEngine;

namespace GunAssemblyTool
{
    [CreateAssetMenu(fileName = "NewStock", menuName = "GunAssemblyTool/Parts/Stock")]
    public class StockData : AttachmentData
    {
        protected override void OnValidate()
        {
            attachType = AttachmentType.Stock;
            base.OnValidate();
        }
    }
}
