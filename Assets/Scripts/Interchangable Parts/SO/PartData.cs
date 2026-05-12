using UnityEngine;
using GunSystem;

//[CreateAssetMenu(menuName = "Gun/Part")]
public class PartData : ScriptableObject
{
    public SlotType slotType;
    public ThreadType threadType;
    public MagType magType;
}