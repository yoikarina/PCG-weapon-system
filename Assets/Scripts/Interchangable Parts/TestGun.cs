using GunSystem;
using System.Collections.Generic;
using UnityEngine;

public class TestGun : MonoBehaviour
{
    public WeaponData weapon;
    public List<EquippedPart> equippedParts;

    public void CurrentMagazineSize()
    {
        var equip = equippedParts.Find(equip => equip.slotType == SlotType.Magazine);
        var mag = equip?.part as MagazineData;

        if (mag != null)
            Debug.Log("Magazine Size: " + mag.magSize);
        else
            Debug.Log("No magazine equipped");
    }

    public void Check()
    {
        bool allCompatible = true;

        foreach (var slot in weapon.slots) {
            var equipped = equippedParts.Find(equip => equip.slotType == slot.slotType);

            if (equipped == null || equipped.part == null) {
                Debug.Log($"Slot {slot.slotType} is empty");
                continue;
                //return;
            }

            bool result = CompatibilityCheck.IsCompatible(slot, equipped.part);

            if (!result) {
                Debug.Log($"Invalid part in {slot.slotType}");
                allCompatible = false;
            }
        }
        Debug.Log("Every slot is valid?: " + allCompatible);
    }
}