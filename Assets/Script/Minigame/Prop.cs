using UnityEngine;

public class Prop : MonoBehaviour
{
    private Vector3 addForce;
    public void HitProp(float force, Camera cam)
    {
        Vector3 forwardForce = cam.transform.forward * force;
        //addForce = new Vector3(force, force, force);
        Rigidbody prop = GetComponent<Rigidbody>();
        prop.AddForce(forwardForce);
    }
}
