using GunAssemblyTool;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.Pool;
using static UnityEngine.InputSystem.LowLevel.InputStateHistory;

public class GunData : MonoBehaviour
{
    public GameObject bulletSpawnLocation;
    public Bullet bulletPrefab;
    public DamageCalculation dmgCalc;

    public GunRuntimeData runtimeData;

    public List<PartData> equippedParts = new();

    /*[Header("base Stats")]
    public int magSize;
    public int hitPoints;
    public float baseCritDmg;
    public float attackBase;
    public float force;

    [Header("Stats buff")]
    public float critDmg;
    public float critChange;
    public float attackFlat;
    public float attackPercentage;*/

    [Header("Current count ammo")]
    public int currentAmmo;

    private bool reloadNeeded = false;

    private ObjectPool<Bullet> pool;

    //LineRenderer - Temporary
    /*public LineRenderer lineR;
    public float laserWidth = 0.1f;
    public float laserMaxLength = 500f;*/

    //public void Initialize(PartData[] part)
    //{
    //    equippedParts.Clear();

    //    foreach (PartData partData in part) {
    //        if (partData != null)
    //            equippedParts.Add(partData);
    //    }
    //    BuildStats();
    //}

    public float damage;
    private GunInstanceData gun;

    public void Awake()
    {
        //runtimeData = GetComponent<GunRuntimeData>();
        gun = runtimeData.GetGunData();
        damage = gun.stats.damage;
        Debug.Log("Damage: " + damage);

        float critChange = gun.stats.GetFloat("CritChange");
        Debug.Log("critDamage: " + critChange);

        SetAmmo();
        currentAmmo = gun.stats.ammoCapacity;
        Debug.Log("Ammo: " + currentAmmo);

        ObjectPooling();
        
        //force = 200;    
    }

    public void Update()
    {
        //Debug.DrawRay(bulletSpawnLocation.transform.position, transform.forward, Color.green);
        //ShootLaserFromTargetPosition(bulletSpawnLocation.transform.position, transform.forward, laserMaxLength);
    }

    /*void ShootLaserFromTargetPosition(Vector3 targetPosition, Vector3 direction, float length)
    {
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0.05f, 0.05f);
        curve.AddKey(0.05f, 0.05f);
        lineR.widthCurve = curve;
        Vector3 endPosition = targetPosition + (direction * length);
        lineR.SetPosition(0, targetPosition);
        lineR.SetPosition(1, endPosition);
    }*/

    public void ShootingTheGun(Camera playerCamera)
    {
        Bullet bullet = pool.Get();
        bullet.transform.position = bulletSpawnLocation.transform.position;
        bullet.transform.rotation = transform.rotation;

        int accumulatedDmg = dmgCalc.DamageCalc(gun);
        if (!reloadNeeded) {
            bullet.Spawn(bullet.transform.forward * gun.stats.fireRange, accumulatedDmg);
        }
        StartCoroutine(DelayedDisable(2, bullet));
        CurrentAmmo();
    }

    //public void StatReset()
    //{
    //    //Weapon
    //    magSize = 0;

    //    //Player
    //    hitPoints = 0;
    //}

    ////
    //public void BuildStats()
    //{
    //    StatReset();
    //    foreach (PartData part in equippedParts) {
    //        if (part is ReceiverData body) {
    //            baseCritDmg = body.critDmgBase;
    //            critDmg = body.critDmg;
    //            critChange = body.critChange;
    //        }


    //        if (part is MagazineData mag) {
    //            magSize = mag.magSize;
    //            critDmg += mag.critDmg;
    //            critChange += mag.critChange;
    //            attackFlat = mag.attackFlat;
    //            attackPercentage = mag.attackPercentage;
    //            attackBase = mag.attackBase;

    //        }

    //        if (part is BarrelData bar) {
    //            hitPoints = bar.buffHP;
    //        }
    //    }
    //}

    public void CurrentAmmo()
    {
        if (reloadNeeded) {
            SetAmmo();
            reloadNeeded = false;
            return;
        }
        currentAmmo--;
        if (currentAmmo == 0) {
            reloadNeeded = true;
        }     
    }

    public void Reload()
    {
        SetAmmo();
    }

    public void SetAmmo()
    {
        currentAmmo = gun.stats.ammoCapacity;
    }

    /// <summary>
    /// Object Pooling
    /// </summary>
    private void ObjectPooling()
    {
        pool = new ObjectPool<Bullet>(
            createFunc: CreateItem,
            actionOnGet: OnGet,
            actionOnRelease: OnRelease,
            maxSize: 5);
    }

    private IEnumerator DelayedDisable(float Time, Bullet bullet)
    {
        yield return new WaitForSeconds(Time);
        pool.Release(bullet);
    }

    private Bullet CreateItem()
    {
        return Instantiate(bulletPrefab);
    }

    void OnGet(Bullet bullet)
    {
        bullet.gameObject.SetActive(true);
    }

    void OnRelease(Bullet bullet)
    {
        bullet.gameObject.SetActive(false);
    }
}
