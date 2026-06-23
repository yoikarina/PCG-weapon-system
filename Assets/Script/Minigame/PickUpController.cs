using UnityEngine;

public class PickUpController : MonoBehaviour
{
    public GunData gun;
    public Rigidbody rb;
    public BoxCollider coll;
    public Transform player, gunContainer, Camera;

    public bool equipped;
    public float dropForwardForce, dropUpwardForce;

    public static bool slotFull;

    private void Awake()
    {
        // Self-wire rb and coll in case the prefab fields were not serialised.
        // This runs before Start() so all references are guaranteed to be valid.
        if (rb == null) rb = GetComponent<Rigidbody>();
        // If Rigidbody is still missing, add it now
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        if (coll == null) coll = GetComponents<BoxCollider>().Length > 0
            ? System.Array.Find(GetComponents<BoxCollider>(), c => c.isTrigger)
            : null;
    }

    private void Start()
    {
        if (!equipped)
        {
            gun.enabled = false;
            rb.isKinematic = false;
            coll.isTrigger = false;
        }

        if (equipped)
        {
            gun.enabled = true;
            rb.isKinematic = true;
            coll.isTrigger = true;
            slotFull = true;
        }
    }

    public void PickUpGun(float pickUpRange)
    {
        Vector3 distanceToPlayer = player.position - transform.position;
        if (!equipped && distanceToPlayer.magnitude <= pickUpRange)
        {
            equipped = true;
            slotFull = true;

            Player playerScript = player.GetComponent<Player>();
            playerScript.currentWeapon = GetComponent<WeaponCollection>();

            transform.SetParent(gunContainer);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.Euler(Vector3.zero);
            transform.localScale = Vector3.one;

            rb.isKinematic = true;
            rb.detectCollisions = false;
            coll.isTrigger = true;
            rb.useGravity = false;

            gun.enabled = true;
            playerScript.CheckData();
        }
    }

    public void DropGun()
    {
        equipped = false;
        slotFull = false;

        transform.SetParent(null);

        rb.isKinematic = false;
        rb.detectCollisions = true;
        coll.isTrigger = false;
        rb.useGravity = true;

        rb.angularVelocity = player.GetComponent<Rigidbody>().angularVelocity;
        rb.AddForce(Camera.forward * dropForwardForce, ForceMode.Impulse);
        rb.AddForce(Camera.up * dropUpwardForce, ForceMode.Impulse);

        float random = Random.Range(-1f, 1f);
        rb.AddTorque(new Vector3(random, random, random));

        gun.enabled = false;
    }
}