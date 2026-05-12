using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Gun/Weapon")]
public class WeaponData : ScriptableObject
{
    public string name;
    public List<SlotData> slots;
}