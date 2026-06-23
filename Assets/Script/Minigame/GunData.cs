using GunAssemblyTool;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class GunData : MonoBehaviour
{
    [Header("Scene References")]
    public GameObject bulletSpawnLocation;
    public Bullet bulletPrefab;
    public DamageCalculation dmgCalc;
    public GameObject impactEffect;

    public GunRuntimeData runtimeData;

    [Header("Current count ammo")]
    public int currentAmmo;
    public int maxAmmo;

    [Header("Audio")]
    public List<AudioClip> sounds;
    public AudioSource audioSource;

    public float damage;
    public float weight;

    private bool reloadNeeded = false;
    private ObjectPool<Bullet> pool;
    private GunInstanceData gun;

    public void Awake()
    {
        gun = runtimeData.GetGunData();
        damage = gun.stats.damage;
        weight = gun.stats.weight;

        // Media sfx is optional ¡ª fall back to empty list if not provided
        sounds = gun.media != null && gun.media.sfx != null
               ? gun.media.sfx
               : new List<AudioClip>();

        SetAmmo();
        currentAmmo = gun.stats.ammoCapacity;
        maxAmmo = currentAmmo;
        ObjectPooling();
    }

    // ©¤©¤ Shooting ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    public void ShootingTheGun(Camera playerCamera)
    {
        int accumulatedDmg = dmgCalc != null ? dmgCalc.DamageCalc(gun) : 0;

        if (!reloadNeeded)
        {
            // Bullet visual is optional ¡ª only spawn if the prefab is assigned
            Bullet bullet = null;
            if (bulletPrefab != null && pool != null)
            {
                bullet = pool.Get();
                bullet.transform.position = bulletSpawnLocation != null
                    ? bulletSpawnLocation.transform.position
                    : transform.position;
                bullet.transform.rotation = transform.rotation;
            }

            // Raycast hit detection still runs regardless of bullet visual
            if (Physics.Raycast(playerCamera.transform.position,
                                playerCamera.transform.forward,
                                out RaycastHit hit,
                                gun.stats.fireRange))
            {
                Enemy enemy = hit.transform.GetComponent<Enemy>();
                Prop prop = hit.transform.GetComponent<Prop>();
                if (enemy != null) enemy.dealDamage(accumulatedDmg);
                if (prop != null) prop.HitProp(gun.stats.fireRange, playerCamera);

                // Impact VFX is optional ¡ª only spawn if assigned
                if (impactEffect != null)
                {
                    GameObject shoot = Instantiate(impactEffect, hit.point,
                                                   Quaternion.LookRotation(hit.normal));
                    Destroy(shoot, 2f);
                }
            }

            // Apply bullet velocity only if a bullet was spawned
            if (bullet != null)
            {
                float force = 400f + gun.stats.fireRate;
                bullet.Spawn(bullet.transform.forward * force);
                StartCoroutine(DelayedDisable(2f, bullet));
            }

            // Muzzle flash particles are optional
            if (bulletSpawnLocation != null)
            {
                var parti = bulletSpawnLocation.GetComponent<ParticleSystem>();
                if (parti != null) parti.Play();
            }

            // Fire sound is optional
            if (audioSource != null && sounds != null && sounds.Count > 0 && sounds[0] != null)
            {
                audioSource.clip = sounds[0];
                audioSource.Play();
            }
        }

        CurrentAmmo();
    }

    // ©¤©¤ Ammo ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    public void CurrentAmmo()
    {
        if (reloadNeeded)
        {
            Reload();
            return;
        }
        currentAmmo--;
        if (currentAmmo == 0) reloadNeeded = true;
    }

    public void Reload()
    {
        reloadNeeded = true;

        // Reload sound is optional (sounds[1])
        if (audioSource != null && sounds != null && sounds.Count > 1 && sounds[1] != null)
        {
            audioSource.clip = sounds[1];
            audioSource.Play();
        }

        StartCoroutine(ReloadSpeed(2f));
        SetAmmo();
    }

    private IEnumerator ReloadSpeed(float time)
    {
        yield return new WaitForSeconds(time);
        reloadNeeded = false;
    }

    public void SetAmmo()
    {
        if (gun != null) currentAmmo = gun.stats.ammoCapacity;
    }

    // ©¤©¤ Object pooling ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void ObjectPooling()
    {
        // Only build the pool if a bullet prefab was assigned. Without it the
        // pool would crash on first CreateItem() call.
        if (bulletPrefab == null) return;

        pool = new ObjectPool<Bullet>(
            createFunc: CreateItem,
            actionOnGet: OnGet,
            actionOnRelease: OnRelease,
            maxSize: 5);
    }

    private IEnumerator DelayedDisable(float time, Bullet bullet)
    {
        yield return new WaitForSeconds(time);
        if (pool != null && bullet != null) pool.Release(bullet);
    }

    private Bullet CreateItem() => Instantiate(bulletPrefab);
    private void OnGet(Bullet b) => b.gameObject.SetActive(true);
    private void OnRelease(Bullet b) => b.gameObject.SetActive(false);
}