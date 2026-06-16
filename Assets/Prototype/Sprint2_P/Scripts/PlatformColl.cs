using UnityEngine;

public class PlatformColl : MonoBehaviour
{

    public Transform platform;
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject) {
            other.gameObject.transform.parent = platform;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject) {
            other.gameObject.transform.parent = null;
        }
    }
}
