using GunSystem;
using UnityEngine;
[CreateAssetMenu(menuName = "Gun/Parts/Magazine")]
public class MagazineData : Attachments
{
    public string name;
    //public GameObject magazineObject;
    public int magSize;
    public MagType magazineCategory;

    // Base
    public float attackBase;

    // Additional buffs
    public float critDmg;
    public float critChange;
    public float attackFlat;
    public float attackPercentage;


    // Additional debuffs

}