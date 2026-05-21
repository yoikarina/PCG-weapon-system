using UnityEngine;

public class CompatibilityTest : MonoBehaviour
{
    public static bool Compatible(PartData receiver, PartData part)
    {
        if (receiver == null || part == null) 
            return false;

        if (receiver is not ReceiverData receiverData)
            return false;

        if (part is not Attachments attachmentData)
            return false;

        if (!attachmentData.weaponDatas.Contains(receiverData.weaponData))
            return false;

        if (part is MagazineData mag) {
            if (!receiverData.weaponData.allowedMagazines.Contains(mag.magazineCategory))
                return false;
        }

        return true;
    }
}
