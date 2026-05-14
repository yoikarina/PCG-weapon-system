using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class GunData : MonoBehaviour
{
    public List<PartData> equippedParts = new();

    public int magSize;
    public int hitPoints;

    public void Initialize(PartData[] part)
    {
        equippedParts.Clear();

        foreach (PartData partData in part) {
            if (partData != null)
                equippedParts.Add(partData);
        }

        BuildStats();
    }

    public void StatReset()
    {
        //Weapon
        magSize = 0;

        //Player
        hitPoints = 0;
    }

    public void BuildStats()
    {
        StatReset();

        foreach (PartData part in equippedParts) {
            if (part is MagazineData mag) {
                magSize = mag.magSize;
            }

            if (part is BarrelData bar) {
                hitPoints = bar.buffHP;
            }

            
        }
    }

    public void SwapGun()
    {
        BuildStats();
    }
}
