using GunSystem;
using UnityEngine;
[CreateAssetMenu(menuName = "Gun/Parts/Magazine")]
public class MagazineData : PartData
{
    public string name;
    //public GameObject magazineObject;
    public int magSize;
    public MagType magazineCategory;

}