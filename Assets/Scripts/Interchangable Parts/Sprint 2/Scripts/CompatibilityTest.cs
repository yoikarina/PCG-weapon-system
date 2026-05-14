using UnityEngine;

public class CompatibilityTest : MonoBehaviour
{
    public static bool Compatible(PartData receiver, PartData part)
    {
        if (receiver == null || part == null) 
            return false;

        if (receiver.weaponData == null)
            return false;

        WeaponData weaponData = receiver.weaponData;

        if (part.weaponData != weaponData) 
            return false;

        if (part is MagazineData mag) {
            if (!weaponData.allowedMagazines.Contains(mag.magazineCategory))
                return false;
        }

        return true;
    }
}
