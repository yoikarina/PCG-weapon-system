using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.InputSystem.LowLevel.InputStateHistory;

public class GunData : MonoBehaviour
{
    public List<PartData> equippedParts = new();

    public int magSize;
    public int hitPoints;

    //public int gunCurrentMag;

    public UIStats stats;

    public void Initialize(PartData[] part)
    {
        equippedParts.Clear();

        foreach (PartData partData in part) {
            if (partData != null)
                equippedParts.Add(partData);
        }
        BuildStats();
    }

    public void Start()
    {
        stats = GetComponent<UIStats>();
        SetAmmo();
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

        SetAmmo();
    }
    public void SwapGun()
    {
        gunSwapped = true;
        swappedAmmo = currentAmmo;
        BuildStats();     
    }
    public int currentAmmo;
    public int swappedAmmo;
    bool reload = false;
    bool gunSwapped = false;

    public void CurrentAmmo()
    {
        if (gunSwapped) {
            currentAmmo = swappedAmmo;
            gunSwapped = false;
        }

        currentAmmo--;
        if (currentAmmo == 0) {
            reload = true;
        }

        if (reload && currentAmmo < 0) {
            currentAmmo = magSize;
            reload = false;
        }

        //stats.UIAmmo(gunCurrentMag);
    }

    public void SetAmmo()
    {
        currentAmmo = magSize;
    }
}
