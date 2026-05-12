using GunSystem;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class TestGun : MonoBehaviour
{
    public WeaponData weapon;
    public List<EquippedPart> equippedParts;
    //[SerializeField] private
    public UIStats stats;
    public Player player;

    private EquippedPart Magazine;
    private EquippedPart Barrel;
    int current;
    bool reload = false;


    void Start()
    {     
        CheckData();
    }

    void Update()
    {

    }

    bool compatibility = true;
    public void EquipPart()
    {
        if (compatibility) {
            CheckData();
            compatibility = false;
            stats.ColorChecker(compatibility);
        } else {
            Debug.Log("Weapon parts are not compatible or you already updated the gun");
        }       
    }

    private void DataCollection()
    {
        Magazine = equippedParts.Find(equip => equip.slotType == SlotType.Magazine);
        Barrel = equippedParts.Find(equip => equip.slotType == SlotType.Barrel);
    }

    public void CheckData()
    {
        DataCollection();
        var mag = Magazine?.part as MagazineData;
        var barrel = Barrel?.part as BarrelData;
        if (mag == null) { return; }
        current = mag.magSize;

        // UI ammunition counter
        stats.UIMaxAmmo(mag.magSize);
        stats.UIAmmo(mag.magSize);

        // Player buff/debuffs
        player.BuffAttributes(barrel.buffHP);
        player.DebuffAttributes();

        // Gun attribute buff/debuffs

    }

    public void Reload()
    {
        CheckData();
    }

    public void Shoot()
    {
        current--;
        if (current == 0) {
            reload = true;
        }

        if (reload && current < 0) {
            CheckData();
            reload = false;
        }
        stats.UIAmmo(current);
    }

    public void Check()
    {
        bool allCompatible = true;
        compatibility = true;
        stats.ColorChecker(compatibility);

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
                compatibility = false;
                stats.ColorChecker(compatibility);
            }
        }
        Debug.Log("Every slot is valid?: " + allCompatible);
    }
}