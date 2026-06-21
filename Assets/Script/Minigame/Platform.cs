using System.Collections;
using UnityEngine;

public class Platform : MonoBehaviour
{
    public GameObject A;
    public GameObject B;
    public GameObject platform;
    public float speed = 10f;
    public float waitDelay = 1f;

    private Vector3 targetPosition;



    private void Start()
    {
        platform.transform.position = A.transform.position;
        targetPosition = B.transform.position;
        StartCoroutine(PlatformMoving());
    }

    IEnumerator PlatformMoving()
    {
        while (true) {
            while ((targetPosition - platform.transform.position).sqrMagnitude > 0.01f) 
            {
                platform.transform.position = Vector3.MoveTowards(platform.transform.position, targetPosition, speed * Time.deltaTime);
                yield return null;
            }
            targetPosition = targetPosition == A.transform.position ? B.transform.position : A.transform.position;

            yield return new WaitForSeconds(waitDelay);
        }
    }
}
