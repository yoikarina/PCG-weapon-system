// Player controller; handles shoot, reload, and swap input and receives HP and ammo updates from the gun system.

using UnityEngine;
using UnityEngine.InputSystem;
using GunAssemblyTool;

public class Player : MonoBehaviour
{
    [Header("Input")]
    public PlayerInput playerControls;

    [Header("Stats")]
    public int baseHitPoints    = 10;
    public int currentHitPoints = 10;

    [Header("References")]
    public UIStats stats;

    // Active gun's runtime data component (set by WeaponWindowTool assembly or manually).
    // Handles ammo counters, shoot, reload, and swap logic.
    public GunData currentGun;

    // Active gun's assembly controller (set by RandomGunSpawner after each spawn).
    // Used to read CurrentAmmoCapacity and to receive HP bonus events.
    [HideInInspector]
    public GunAssemblyController activeGunController;

    // Tracks whether this is the very first gun loaded so the UI can be
    // initialised correctly before any swap has occurred.
    private bool _firstGunInitialized = false;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    // Subscribes to all input actions.
    private void OnEnable()
    {
        playerControls.actions.Enable();
        playerControls.actions["Shoot"].performed   += Shooting;
        playerControls.actions["Reload"].performed  += Reloading;
        playerControls.actions["SwapGun"].performed += Swapping;
    }

    // Unsubscribes from all input actions to prevent memory leaks.
    private void OnDisable()
    {
        playerControls.actions.Disable();
        playerControls.actions["Shoot"].performed   -= Shooting;
        playerControls.actions["Reload"].performed  -= Reloading;
        playerControls.actions["SwapGun"].performed -= Swapping;
    }

    // Initialises the HP display and triggers the first CheckData pass.
    private void Start()
    {
        stats?.UIHealthPoints(baseHitPoints);
        _firstGunInitialized = true;
        CheckData();
    }

    // ── Input callbacks ───────────────────────────────────────────────────────

    // Fires one round and updates the ammo UI.
    public void Shooting(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        Shoot();
    }

    // Refills the magazine and updates the ammo UI.
    public void Reloading(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        if (currentGun == null) return;
        currentGun.Reload();
        stats?.UIAmmo(currentGun.currentAmmo);
    }

    // Swaps to the next gun configuration and refreshes all UI displays.
    public void Swapping(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        currentGun?.SwapGun();
        CheckData();
    }

    // ── Gameplay methods ──────────────────────────────────────────────────────

    // Decrements ammo via GunData and updates the ammo counter display.
    public void Shoot()
    {
        if (currentGun == null) return;
        currentGun.CurrentAmmo();
        stats?.UIAmmo(currentGun.currentAmmo);
    }

    // Refreshes all UI values from the current gun state.
    // Called on swap, on first initialisation, and whenever the gun config changes.
    public void CheckData()
    {
        if (currentGun == null) return;

        stats?.UIMaxAmmo(currentGun.magSize);
        stats?.UIAmmo(currentGun.swappedAmmo);

        // On first load show the full mag rather than the swapped value.
        if (_firstGunInitialized)
        {
            stats?.UIAmmo(currentGun.magSize);
            _firstGunInitialized = false;
        }

        BuffAttributes(currentGun.hitPoints);
        DebuffAttributes();
    }

    // ── Gun system callbacks ──────────────────────────────────────────────────

    // Updates HP from the barrel bonus and refreshes the HP display.
    // Wire GunAssemblyController.onHpBonusChanged → this method in the Inspector
    // so HP updates automatically whenever a barrel is equipped or swapped.
    public void BuffAttributes(int hpBonus)
    {
        currentHitPoints = baseHitPoints + hpBonus;
        stats?.UIHealthPoints(currentHitPoints);
    }

    // Stub for future debuff logic.
    public void DebuffAttributes() { }
}
