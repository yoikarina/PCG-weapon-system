using UnityEngine;

public class WeaponDetection : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Player player = other.GetComponent<Player>();

        if (player != null) {
            player.nearbyWeapon = GetComponentInParent<WeaponCollection>();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Player player = other.GetComponent<Player>();

        if (player != null) {
            player.nearbyWeapon = null;
        }
    }
}
