using GunAssemblyTool;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class GunData : MonoBehaviour
{
    [Header("Scene References — auto-found if not assigned")]
    public GameObject bulletSpawnLocation;
    public Bullet bulletPrefab;
    public DamageCalculation dmgCalc;
    public GameObject impactEffect;

    public GunRuntimeData runtimeData;

    [Header("Current Ammo")]
    public int currentAmmo;
    public int maxAmmo;

    [Header("Audio")]
    public List<AudioClip> sounds;
    public AudioSource audioSource;

    public float damage;
    public float weight;

    // ── Private state ─────────────────────────────────────────────────────────
    private bool _initialised = false;
    private bool _reloadNeeded = false;
    private ObjectPool<Bullet> _pool;
    private GunInstanceData _gun;

    // ── Initialisation ────────────────────────────────────────────────────────

    /// <summary>
    /// Called by GunSetup (fast path) or lazily on first use.
    /// Safe to call multiple times — subsequent calls are no-ops.
    /// </summary>
    public void Initialise()
    {
        if (_initialised) return;

        if (runtimeData == null)
        {
            Debug.LogWarning($"[GunData] runtimeData not assigned on '{name}'. " +
                             "Stats will be zeroed until it is set.");
            return;
        }

        _gun = runtimeData.GetGunData();
        damage = _gun.stats.damage;
        weight = _gun.stats.weight;

        sounds = _gun.media != null && _gun.media.sfx != null
               ? _gun.media.sfx
               : new List<AudioClip>();

        currentAmmo = _gun.stats.ammoCapacity;
        maxAmmo = currentAmmo;

        BuildPool();

        _initialised = true;
    }

    private void Awake()
    {
        // Attempt early init. If runtimeData is already wired (baked by the
        // workbench wiring pass) this succeeds immediately. If GunSetup hasn't
        // run yet this is a no-op and Initialise() is retried on first use.
        Initialise();
    }

    // ── Scene-reference resolution ────────────────────────────────────────────

    /// <summary>
    /// Fills any still-null scene references. Called by GunSetup and also
    /// lazily before each shot so the gun works even without GunSetup.
    /// </summary>
    public void ResolveSceneRefs()
    {
        if (dmgCalc == null)
            dmgCalc = Object.FindFirstObjectByType<DamageCalculation>();

        if (bulletSpawnLocation == null)
        {
            var spawn = GameObject.Find("RifleBulletSpawn");
            if (spawn != null) bulletSpawnLocation = spawn;
        }

        if (bulletPrefab == null)
        {
            var bulletGO = Resources.Load<GameObject>("Bullet_B");
            if (bulletGO != null) bulletPrefab = bulletGO.GetComponent<Bullet>();
        }

        if (impactEffect == null)
        {
            var impact = GameObject.Find("Impact");
            if (impact != null) impactEffect = impact;
        }

        // Rebuild pool if bullet prefab just became available
        if (bulletPrefab != null && _pool == null)
            BuildPool();
    }

    // ── Shooting ──────────────────────────────────────────────────────────────

    public void ShootingTheGun(Camera playerCamera)
    {
        // Lazy init — covers the case where GunSetup didn't run first
        Initialise();
        ResolveSceneRefs();

        if (_gun == null) return;

        int accumulatedDmg = dmgCalc != null ? dmgCalc.DamageCalc(_gun) : 0;

        if (!_reloadNeeded)
        {
            Bullet bullet = null;
            if (bulletPrefab != null && _pool != null)
            {
                bullet = _pool.Get();
                bullet.transform.position = bulletSpawnLocation != null
                    ? bulletSpawnLocation.transform.position
                    : transform.position;
                bullet.transform.rotation = transform.rotation;
            }

            if (Physics.Raycast(playerCamera.transform.position,
                                playerCamera.transform.forward,
                                out RaycastHit hit,
                                _gun.stats.fireRange))
            {
                Enemy enemy = hit.transform.GetComponent<Enemy>();
                Prop prop = hit.transform.GetComponent<Prop>();
                if (enemy != null) enemy.dealDamage(accumulatedDmg);
                if (prop != null) prop.HitProp(_gun.stats.fireRange, playerCamera);

                if (impactEffect != null)
                {
                    GameObject vfx = Instantiate(impactEffect, hit.point,
                                                 Quaternion.LookRotation(hit.normal));
                    Destroy(vfx, 2f);
                }
            }

            if (bullet != null)
            {
                bullet.Spawn(bullet.transform.forward * (400f + _gun.stats.fireRate));
                StartCoroutine(DelayedRelease(2f, bullet));
            }

            if (bulletSpawnLocation != null)
            {
                var ps = bulletSpawnLocation.GetComponent<ParticleSystem>();
                if (ps != null) ps.Play();
            }

            if (audioSource != null && sounds != null && sounds.Count > 0 && sounds[0] != null)
            {
                audioSource.clip = sounds[0];
                audioSource.Play();
            }
        }

        TickAmmo();
    }

    // ── Ammo ──────────────────────────────────────────────────────────────────

    private void TickAmmo()
    {
        if (_reloadNeeded) { Reload(); return; }
        currentAmmo--;
        if (currentAmmo <= 0) _reloadNeeded = true;
    }

    public void Reload()
    {
        _reloadNeeded = true;
        if (audioSource != null && sounds != null && sounds.Count > 1 && sounds[1] != null)
        {
            audioSource.clip = sounds[1];
            audioSource.Play();
        }
        StartCoroutine(ReloadDelay(2f));
    }

    private IEnumerator ReloadDelay(float time)
    {
        yield return new WaitForSeconds(time);
        if (_gun != null) currentAmmo = _gun.stats.ammoCapacity;
        _reloadNeeded = false;
    }

    // ── Object pooling ────────────────────────────────────────────────────────

    private void BuildPool()
    {
        if (bulletPrefab == null || _pool != null) return;
        _pool = new ObjectPool<Bullet>(
            createFunc: () => Instantiate(bulletPrefab),
            actionOnGet: b => b.gameObject.SetActive(true),
            actionOnRelease: b => b.gameObject.SetActive(false),
            maxSize: 5);
    }

    private IEnumerator DelayedRelease(float time, Bullet bullet)
    {
        yield return new WaitForSeconds(time);
        if (_pool != null && bullet != null) _pool.Release(bullet);
    }
}