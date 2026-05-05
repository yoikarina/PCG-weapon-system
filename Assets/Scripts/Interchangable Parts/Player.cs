using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class Player : MonoBehaviour
{
    public PlayerInput playerControls;

    public int currentHitPoints = 10;
    public int baseHitPoints = 10;

    public UIStats stats;
    public TestGun equippedGun;

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
            equippedGun.Reload();
        }
    }

    public void Shooting(InputAction.CallbackContext context)
    {
        if (context.performed) {
            equippedGun.Shoot();
        }
    }

    void Start()
    {
        stats.UIHealthPoints(baseHitPoints);
    }

    public void BuffAttributes(int hitPointValue)
    {
        currentHitPoints = baseHitPoints;
        currentHitPoints += hitPointValue;
        stats.UIHealthPoints(currentHitPoints);
    }

    public void DebuffAttributes()
    {

    }
}
