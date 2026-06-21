using GunAssemblyTool;
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

        [Header("Current count ammo")]
        public int currentAmmo;
        public int maxAmmo;

        private bool reloadNeeded = false;

        private ObjectPool<Bullet> pool;

        public List<AudioClip> sounds;
        public AudioSource audioSource;

        public float damage;
        public float weight;
        private GunInstanceData gun;

        public void Awake()
        {
            gun = runtimeData.GetGunData();
            damage = gun.stats.damage;
            weight = gun.stats.weight;

            sounds = gun.media.sfx;

            float critChange = gun.stats.GetFloat("CritChange");
            float critDmg = gun.stats.GetFloat("CritDamage");

            SetAmmo();
            currentAmmo = gun.stats.ammoCapacity;
            maxAmmo = currentAmmo;
            ObjectPooling(); 
        }

        public GameObject impactEffect;

        public void ShootingTheGun(Camera playerCamera)
        {
            int accumulatedDmg = dmgCalc.DamageCalc(gun);
            RaycastHit hit;
            
            if (!reloadNeeded) {
                Bullet bullet = pool.Get();
                bullet.transform.position = bulletSpawnLocation.transform.position;
                bullet.transform.rotation = transform.rotation;

                if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out hit, gun.stats.fireRange)) {
                    Enemy enemy = hit.transform.GetComponent<Enemy>();
                    Prop prop = hit.transform.GetComponent<Prop>();
                    if (enemy != null) {
                        enemy.dealDamage(accumulatedDmg);
                    }
                    if (prop != null) {
                        prop.HitProp(gun.stats.fireRange, playerCamera);
                    }

                    GameObject Shoot = Instantiate(impactEffect, hit.point, Quaternion.LookRotation(hit.normal));
                    Destroy(Shoot, 2f);
                }

                float force = 400f + gun.stats.fireRate;
                bullet.Spawn(bullet.transform.forward * force);
                ParticleSystem parti = bulletSpawnLocation.GetComponent<ParticleSystem>();
                parti.Play();
                AudioClip clip = sounds[0];
                audioSource.clip = clip;
                audioSource.Play();

                StartCoroutine(DelayedDisable(2, bullet));
            }
            CurrentAmmo();
        }

        public void CurrentAmmo()
        {
            if (reloadNeeded) {
                Reload();
                return;
            }
            currentAmmo--;
            if (currentAmmo == 0) {
                reloadNeeded = true;
            }
        }

        public void Reload()
        {
            reloadNeeded = true;
            AudioClip clip = sounds[1];
            audioSource.clip = clip;
            audioSource.Play();
            StartCoroutine(ReloadSpeed(2f));
            SetAmmo();
        }

        private IEnumerator ReloadSpeed(float Time)
        {
            yield return new WaitForSeconds(Time);
            reloadNeeded = false;
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

