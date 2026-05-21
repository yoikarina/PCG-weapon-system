using NUnit.Framework;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using static UnityEngine.InputSystem.LowLevel.InputStateHistory;

public class GunData : MonoBehaviour
{
    public List<PartData> equippedParts = new();

    [Header("Stats")]
    public int magSize;
    public int hitPoints;

    [Header("Current count ammo")]
    public int currentAmmo;
    public int swappedAmmo; // When swapped forces gun to continue from this value

    private bool reload = false;
    private bool gunSwapped = false;

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
    }
    public void SwapGun()
    {
        gunSwapped = true;
        swappedAmmo = currentAmmo;
        BuildStats();
        SetAmmo();
    }

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
            SetAmmo();
            reload = false;
        }
    }

    public void Reload()
    {
        SetAmmo();
    }

    public void SetAmmo()
    {
        currentAmmo = magSize;
    }
}
