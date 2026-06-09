using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.Pool;
using UnityEngine.UIElements;

public class Player : MonoBehaviour
{
    public InputActionAsset playerControls;

    public int currentHitPoints = 10;
    public int baseHitPoints = 10;

    private bool firstGunInitialized = false;

    //Camera Look Around Variables
    private Vector2 lookInput = Vector2.zero;
    private float xRotation;
    public float mouseSensitivity = 0.5f;

    [Header("Connection with other objects/scripts")]
    public UIStats stats;
    public WeaponCollection currentWeapon;
    public WeaponCollection nearbyWeapon;
    //public DamageCalculation dmgCalc;
    public Camera playerCamera;
    

    //private bool cooldown = false;
    private ObjectPool<Bullet> pool;
    public Vector3 bulletDirection;
    public float force;

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction shootAction;
    private InputAction swapAction;
    private InputAction reloadAction;
    private InputAction dropAction;

    private Vector2 moveDirection = Vector2.zero;
    public bool useCameraRelativeMovement = true;
    public float adjustedSpeed = 1f;


    private void Awake()
    {
        moveAction = playerControls.FindActionMap("Player").FindAction("Moving");
            moveAction.performed += context => moveDirection = context.ReadValue<Vector2>();
            moveAction.canceled += context => moveDirection = Vector2.zero;
        lookAction = playerControls.FindActionMap("Player").FindAction("lookAround");
            lookAction.performed += context => lookInput = context.ReadValue<Vector2>();
            lookAction.canceled += context => lookInput = Vector2.zero;
        shootAction = playerControls.FindActionMap("Player").FindAction("Shoot");
        swapAction = playerControls.FindActionMap("Player").FindAction("SwapGun");
        reloadAction = playerControls.FindActionMap("Player").FindAction("Reload");
        dropAction = playerControls.FindActionMap("Player").FindAction("Drop");   
    }


    private void OnEnable()
    {
        moveAction.Enable();
        lookAction.Enable();
        shootAction.Enable();
        swapAction.Enable();
        reloadAction.Enable();
        dropAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
        lookAction.Disable();
        shootAction.Disable(); 
        swapAction.Disable();
        reloadAction.Disable();
        dropAction.Disable();
    }

    public void MoveAround()
    {
        Vector3 move = Vector3.zero;
        if (useCameraRelativeMovement) {
            Vector3 forward = playerCamera.transform.forward;
            Vector3 right = playerCamera.transform.right;

            forward.y = 0f;
            right.y = 0f;

            forward.Normalize();
            right.Normalize();
            move = forward * moveDirection.y + right * moveDirection.x;
        } else {
            move = new Vector3(moveDirection.x, 0, moveDirection.y);
        }

        playerRB.linearVelocity = move * adjustedSpeed * 50f;
    }

    public void LookingAround()
    {
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;
        transform.Rotate(0, mouseX, 0);

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80);
        playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0, 0);
    }

    public void Reloading()
    {
        if (reloadAction.triggered && currentWeapon != null) {
            currentWeapon.gunData.Reload();
            stats.UIAmmo(currentWeapon.gunData.currentAmmo);
        }
    }

    public float pickUpRange;

    public void Swapping()
    {
        if (swapAction.triggered) {
            if (nearbyWeapon == null)
                return;

            WeaponCollection oldWeapon = currentWeapon;

            if (oldWeapon != null) {
                oldWeapon.pickUpController.DropGun();
            }

            nearbyWeapon.pickUpController.PickUpGun(pickUpRange);

            currentWeapon = nearbyWeapon;
            nearbyWeapon = null;
            stats.UIAmmo(currentWeapon.gunData.currentAmmo);
            CheckData();
        }
    }

    public void Shooting()
    {
        if (shootAction.triggered && currentWeapon != null) {
            currentWeapon.gunData.ShootingTheGun(playerCamera);
            stats.UIAmmo(currentWeapon.gunData.currentAmmo);
        }
    }

    public void Dropping()
    {
        if (dropAction.triggered && currentWeapon != null) {
            currentWeapon.pickUpController.DropGun();
            currentWeapon = null;
            CheckData();
        }      
    }

    public void CheckData()
    {
        if (currentWeapon == null)
            return;

        if (currentWeapon.gunData == null)
            return;

        // UI ammunition counter
        stats.UIMaxAmmo(currentWeapon.gunData.magSize);
        //stats.UIAmmo(currentWeapon.gunData.swappedAmmo);
        if (firstGunInitialized) {
            stats.UIAmmo(currentWeapon.gunData.magSize);
            firstGunInitialized = false;       
        }
        
        // Player buff/debuffs
        BuffAttributes();
        DebuffAttributes();

        // Gun attribute buff/debuffs

    }
    private Rigidbody playerRB;

    void Start()
    {
        playerRB = GetComponent<Rigidbody>();

        stats.UIHealthPoints(baseHitPoints);
        firstGunInitialized = true;
        CheckData();
    }

    void Update()
    {
        LookingAround();
        Reloading();
        Swapping();
        Shooting();
        Dropping();
    }

    private void FixedUpdate()
    {
        MoveAround();
    }

    public void BuffAttributes()
    {
        currentHitPoints = baseHitPoints;
        currentHitPoints += currentWeapon.gunData.hitPoints;
        stats.UIHealthPoints(currentHitPoints);
    }

    public void DebuffAttributes()
    {

    }
}
