using UnityEngine;
using GunSystem;
using NUnit.Framework;
using System.Collections.Generic;

//[CreateAssetMenu(menuName = "Gun/Part")]
public class PartData : ScriptableObject
{
    public SlotType slotType;
    public ThreadType threadType;
    public MagType magType;


    public GameObject partObject;
    
    //public WeaponData weaponData;           // Receiver only has one type of weaponData, example AK-47
    //public List<WeaponData> weaponDatas;    // Parts can be equipped to multiple weaponData, a magazine can be put on an AK-47 or a M16
}