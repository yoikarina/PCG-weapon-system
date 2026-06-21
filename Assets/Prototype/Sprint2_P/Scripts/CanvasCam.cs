using UnityEngine;

public class CanvasCam : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public Camera cam;

    // Update is called once per frame
    void Update()
    {
        transform.forward = cam.transform.forward;
    }
}
