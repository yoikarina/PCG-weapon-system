// GunSetup.cs
// Attached to every weapon prefab saved by the Weapon Workbench.
// On Awake, injects all scene references that cannot be baked into a prefab,
// then destroys itself so it leaves no runtime overhead.
//
// Requirements:
//   • Player GameObject must have tag "Player"
//   • WeaponContainer (the hand socket) must have tag "WeaponContainer"
//   • Main Camera must have tag "MainCamera" (Unity default)
//   • DamageCalculation component must exist somewhere in the scene

using UnityEngine;

public class GunSetup : MonoBehaviour
{
    private void Awake()
    {
        var gunData   = GetComponent<GunData>();
        var pickUp    = GetComponent<PickUpController>();
        var weaponCol = GetComponent<WeaponCollection>();

        // ── Find scene objects ────────────────────────────────────────────────
        GameObject playerGO    = GameObject.FindWithTag("Player");
        GameObject containerGO = GameObject.FindWithTag("WeaponContainer");
        Camera     mainCam     = Camera.main;

        // ── Wire PickUpController ─────────────────────────────────────────────
        if (pickUp != null)
        {
            pickUp.gun = gunData;
            pickUp.rb  = GetComponent<Rigidbody>();

            // Trigger collider (WeaponDetection pickup range)
            foreach (var col in GetComponents<BoxCollider>())
            {
                if (col.isTrigger) { pickUp.coll = col; break; }
            }

            if (playerGO != null)
                pickUp.player = playerGO.transform;
            else
                Debug.LogWarning($"[GunSetup] No GameObject with tag 'Player' found. " +
                                 $"Assign PickUpController.player manually on {name}.");

            if (containerGO != null)
                pickUp.gunContainer = containerGO.transform;
            else
                Debug.LogWarning($"[GunSetup] No GameObject with tag 'WeaponContainer' found. " +
                                 $"Assign PickUpController.gunContainer manually on {name}.");

            // PickUpController stores Camera as a Transform field named 'Camera'
            if (mainCam != null)
                pickUp.Camera = mainCam.transform;
            else
                Debug.LogWarning($"[GunSetup] No MainCamera found. " +
                                 $"Assign PickUpController.Camera manually on {name}.");
        }

        // ── Wire WeaponCollection ─────────────────────────────────────────────
        if (weaponCol != null)
        {
            weaponCol.gunData          = gunData;
            weaponCol.pickUpController = pickUp;
        }

        // ── Wire GunData fallback assets ──────────────────────────────────────
        // If the weapon prefab was saved without a bullet / spawn point / impact,
        // fall back to a default Bullet prefab + the scene's RifleBulletSpawn and
        // Impact GameObjects. This lets users immediately playtest a fresh weapon
        // without needing to assign anything by hand.
        if (gunData != null)
        {
            // Default bullet — loaded from Resources/Bullet_B.prefab
            // Drop your Bullet_B prefab into a "Resources" folder so Unity can
            // find it at runtime by name.
            if (gunData.bulletPrefab == null)
            {
                var bulletGO = Resources.Load<GameObject>("Bullet_B");
                if (bulletGO != null)
                    gunData.bulletPrefab = bulletGO.GetComponent<Bullet>();
                else
                    Debug.LogWarning("[GunSetup] No 'Bullet_B' prefab found in any " +
                                     "Resources folder. Add one to enable default bullets.");
            }

            // Default bullet spawn location — find RifleBulletSpawn in the scene.
            // Includes inactive objects in case it lives under a disabled parent.
            if (gunData.bulletSpawnLocation == null)
            {
                var spawn = GameObject.Find("RifleBulletSpawn");
                if (spawn != null)
                    gunData.bulletSpawnLocation = spawn;
                else
                    Debug.LogWarning("[GunSetup] No GameObject named 'RifleBulletSpawn' " +
                                     "found in scene.");
            }

            // Default impact effect — find the Impact GameObject in the scene.
            // GunData.impactEffect is instantiated on hit, so we want the prefab
            // (or reference) to the impact effect template.
            if (gunData.impactEffect == null)
            {
                var impact = GameObject.Find("Impact");
                if (impact != null)
                    gunData.impactEffect = impact;
                else
                    Debug.LogWarning("[GunSetup] No GameObject named 'Impact' found in scene.");
            }
        }

        // ── Wire GunData.dmgCalc ──────────────────────────────────────────────
        if (gunData != null && gunData.dmgCalc == null)
        {
            var dmgCalc = Object.FindFirstObjectByType<DamageCalculation>();
            if (dmgCalc != null)
                gunData.dmgCalc = dmgCalc;
            else
                Debug.LogWarning($"[GunSetup] No DamageCalculation found in scene. " +
                                 $"Assign GunData.dmgCalc manually on {name}.");
        }

        // ── Done — self-destruct ──────────────────────────────────────────────
        Destroy(this);
    }
}