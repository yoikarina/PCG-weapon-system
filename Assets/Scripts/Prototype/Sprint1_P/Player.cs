using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class Player : MonoBehaviour
{
    public PlayerInput playerControls;

    public int currentHitPoints = 10;
    public int baseHitPoints = 10;

    private bool firstGunInitialized = false;

    public UIStats stats;
    public GunData currentGun;
    public DamageCalculation dmgCalc;

    private void OnEnable()
    {
        playerControls.actions.Enable();
        playerControls.actions["Shoot"].performed += Shooting;
        playerControls.actions["Reload"].performed += Reloading;
        playerControls.actions["SwapGun"].performed += Swapping;
    }

    private void OnDisable()
    {
        playerControls.actions.Disable();
        playerControls.actions["Shoot"].performed -= Shooting;
        playerControls.actions["Reload"].performed -= Reloading;
        playerControls.actions["SwapGun"].performed -= Swapping;
    }

    public void Reloading(InputAction.CallbackContext context)
    {
        if (context.performed) {
            currentGun.Reload();
            stats.UIAmmo(currentGun.currentAmmo);
        }
    }

    public void Swapping(InputAction.CallbackContext context)
    {
        if (context.performed) {
            currentGun.SwapGun();          
            CheckData();
        }
    }

    public void Shooting(InputAction.CallbackContext context)
    {
        if (context.performed) {
            Shoot();
        }
    }

    //int thisWeapon;

    public void Shoot()
    {        
        currentGun.CurrentAmmo();
        int accumulatedDmg = dmgCalc.DamageCalc(currentGun); // Place this later in Bullet when collision is registered
        Debug.Log(accumulatedDmg);
        stats.UIAmmo(currentGun.currentAmmo);
    }

    public void CheckData()
    {
        // UI ammunition counter
        stats.UIMaxAmmo(currentGun.magSize);
        stats.UIAmmo(currentGun.swappedAmmo);
        if (firstGunInitialized) {
            stats.UIAmmo(currentGun.magSize);
            firstGunInitialized = false;       
        }
        
        // Player buff/debuffs
        BuffAttributes();
        DebuffAttributes();

        // Gun attribute buff/debuffs

    }

    void Start()
    {
        stats.UIHealthPoints(baseHitPoints);
        firstGunInitialized = true;
        CheckData();      
    }

    public void BuffAttributes()
    {
        currentHitPoints = baseHitPoints;
        currentHitPoints += currentGun.hitPoints;
        stats.UIHealthPoints(currentHitPoints);
    }

    public void DebuffAttributes()
    {

    }
}
