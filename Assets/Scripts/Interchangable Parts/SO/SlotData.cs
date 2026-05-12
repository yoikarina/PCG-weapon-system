using UnityEngine;
using GunSystem;
using System.Collections.Generic;

[System.Serializable]
public class SlotData
{
    public SlotType slotType;
    public List<ThreadType> allowedThreads;
    public List<MagType> allowedMags;
}