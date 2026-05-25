// Runtime ammo and stat tracker placed on an assembled gun by WeaponWindowTool or RandomGunSpawner.

using System.Collections.Generic;
using UnityEngine;

namespace GunAssemblyTool
{
    public class GunData : MonoBehaviour
    {
        // Parts supplied after assembly. Each entry is an AttachmentData subclass.
        public List<AttachmentData> equippedParts = new List<AttachmentData>();

        [Header("Computed Stats")]
        // Populated by BuildStats(). Read-only at runtime.
        public int   magSize;
        public int   hitPoints;

        [Header("Ammo State")]
        public int currentAmmo;
        // Preserved across SwapGun calls so ammo count survives a weapon swap.
        public int swappedAmmo;

        private bool _reload     = false;
        private bool _gunSwapped = false;

        // Called by WeaponWindowTool after assembly.
        public void Initialize(AttachmentData[] parts)
        {
            equippedParts.Clear();
            foreach (var part in parts)
                if (part != null) equippedParts.Add(part);
            BuildStats();
        }

        private void Start() => SetAmmo();

        // Resets computed stats before rebuilding.
        public void StatReset()
        {
            magSize   = 0;
            hitPoints = 0;
        }

        // Reads MagSize from the dynamic stat list and hpBonus from BarrelData.
        public void BuildStats()
        {
            StatReset();
            foreach (var part in equippedParts)
            {
                // MagSize — read from StatEntry list
                if (part.HasStat(StatKeys.MagSize))
                    magSize = (int)part.GetStat(StatKeys.MagSize);

                // HP bonus — BarrelData dedicated field (affects player, not gun)
                if (part is BarrelData barrel)
                    hitPoints = barrel.hpBonus;
            }
        }

        // Called by Player.Swapping when swapping to this gun.
        public void SwapGun()
        {
            _gunSwapped = true;
            swappedAmmo = currentAmmo;
            BuildStats();
            SetAmmo();
        }

        // Decrements ammo by one. Auto-reloads when empty.
        public void CurrentAmmo()
        {
            if (_gunSwapped) { currentAmmo = swappedAmmo; _gunSwapped = false; }
            currentAmmo--;
            if (currentAmmo == 0)  _reload = true;
            if (_reload && currentAmmo < 0) { SetAmmo(); _reload = false; }
        }

        // Refills ammo to current magSize.
        public void Reload() => SetAmmo();

        // Sets currentAmmo to magSize.
        public void SetAmmo() => currentAmmo = magSize;
    }
}
