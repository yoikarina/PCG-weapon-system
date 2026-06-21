using GunSystem;
using UnityEngine;

public static class CompatibilityCheck
{
    public static bool IsCompatible(SlotData slot, PartData part)
    {
        if (part == null)
            return false;

        if (slot.slotType != part.slotType)
            return false;

        if (slot.allowedThreads.Count > 0 &&
            !slot.allowedThreads.Contains(part.threadType))
            return false;

        if (slot.allowedMags.Count > 0 &&
            !slot.allowedMags.Contains(part.magType))
            return false;

        return true;
    }
}