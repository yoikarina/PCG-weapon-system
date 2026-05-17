using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class Player : MonoBehaviour
{
    public PlayerInput playerControls;

    public int currentHitPoints = 10;
    public int baseHitPoints = 10;

    public UIStats stats;
    //public TestGun equippedGun;

    public GunData currentGun;

    private void OnEnable()
    {
        playerControls.actions.Enable();
        playerControls.actions["Shoot"].performed += Shooting;
        playerControls.actions["Reload"].performed += Reloading;
    }

    private void OnDisable()
    {
        playerControls.actions.Disable();
        playerControls.actions["Shoot"].performed -= Shooting;
        playerControls.actions["Reload"].performed -= Reloading;
    }

    public void Reloading(InputAction.CallbackContext context)
    {
        if (context.performed) {
            //currentGun.Reload();
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
    }

    public void CheckData()
    {
        //thisWeapon = currentGun.magSize;

        // UI ammunition counter
        stats.UIMaxAmmo(currentGun.magSize);
        stats.UIAmmo(currentGun.magSize);

        // Player buff/debuffs
        BuffAttributes();
        DebuffAttributes();

        // Gun attribute buff/debuffs

    }

    void Start()
    {
        stats.UIHealthPoints(baseHitPoints);
        CheckData();      
    }

    public void BuffAttributes(/*int hitPointValue*/)
    {
        currentHitPoints = baseHitPoints;
        currentHitPoints += currentGun.hitPoints;
        Debug.Log(currentHitPoints);
        stats.UIHealthPoints(currentHitPoints);
    }

    public void DebuffAttributes()
    {

    }
}
