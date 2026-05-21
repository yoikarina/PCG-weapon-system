// Stock attachment; occupies the Stock slot and modifies accuracy via the base class bonus fields.

using UnityEngine;

namespace GunAssemblyTool
{
    [CreateAssetMenu(fileName = "NewStock", menuName = "GunAssemblyTool/Parts/Stock")]
    public class StockData : AttachmentData
    {
        // Locks attachType to Stock and runs the base validation.
        protected override void OnValidate()
        {
            attachType = AttachmentType.Stock;
            base.OnValidate();
        }
    }
}
