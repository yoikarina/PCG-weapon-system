using UnityEngine;
using System.Collections.Generic;
using GunSystem;

[CreateAssetMenu(menuName = "Gun/Weapon")]
public class WeaponData : ScriptableObject
{
    //public string name;
    //public List<SlotData> slots;
    public string weaponName;

    public List<MagType> allowedMagazines;

    public List<ThreadType> supportedOptics;

    public bool supportsSuppressors;
}