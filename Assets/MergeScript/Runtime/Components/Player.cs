// Player controller; handles shoot/reload input and receives HP and ammo updates from the gun system.

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

    // Reference to the GunAssemblyController on the currently active gun.
    // Update this whenever RandomGunSpawner spawns a new gun, or bind it
    // via a UnityEvent on the spawner's onAssemblyChanged event.
    [HideInInspector]
    public GunAssemblyController activeGunController;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    // Subscribes to input actions when the component is enabled.
    private void OnEnable()
    {
        playerControls.actions.Enable();
        playerControls.actions["Shoot"].performed  += OnShoot;
        playerControls.actions["Reload"].performed += OnReload;
    }

    // Unsubscribes from input actions when the component is disabled,
    // preventing memory leaks and stale callbacks.
    private void OnDisable()
    {
        playerControls.actions.Disable();
        playerControls.actions["Shoot"].performed  -= OnShoot;
        playerControls.actions["Reload"].performed -= OnReload;
    }

    // Initialises the HP display on the first frame.
    private void Start() => stats?.UIHealthPoints(currentHitPoints);

    // ── Input callbacks ───────────────────────────────────────────────────────

    // Invoked by the InputSystem when the Shoot action fires.
    // Add your shooting logic here; use activeGunController.CurrentStats
    // for damage, fire rate, and accuracy values.
    private void OnShoot(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        // Implement shoot logic here.
    }

    // Invoked by the InputSystem when the Reload action fires.
    // Updates the UI ammo counter maximum with the current magazine capacity.
    private void OnReload(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        if (stats != null && activeGunController != null)
            stats.UIMaxAmmo(activeGunController.CurrentAmmoCapacity);
    }

    // ── Gun system callbacks ──────────────────────────────────────────────────

    // Receives the updated barrel HP bonus whenever a barrel attachment changes.
    // Wire GunAssemblyController.onHpBonusChanged to this method in the Inspector.
    public void BuffAttributes(int hpBonus)
    {
        currentHitPoints = baseHitPoints + hpBonus;
        stats?.UIHealthPoints(currentHitPoints);
    }

    // Stub for debuff logic. Implement as needed.
    public void DebuffAttributes() { }
}
