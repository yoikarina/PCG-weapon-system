using UnityEngine;

[CreateAssetMenu(menuName = "Gun/Parts/Receiver-Body")]
public class ReceiverData : PartData
{
    public string name;
    public WeaponData weaponData;           // Receiver only has one type of weaponData, example AK-47


    // Base
    public float critDmgBase;

    // Additional buffs
    public float critDmg;
    public float critChange;

    // Additional debuffs
}