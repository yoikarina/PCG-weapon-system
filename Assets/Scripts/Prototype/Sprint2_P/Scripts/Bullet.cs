using UnityEngine;

public class Bullet : MonoBehaviour
{
    public Vector3 spawnLocation { get; private set; }
    public Rigidbody Rigidbody { get; private set; }

    private void Awake()
    {
        Rigidbody = GetComponent<Rigidbody>();
    }

    public void Spawn(Vector3 SpawnForce, int damage)
    {
        Rigidbody.AddForce(SpawnForce);
        damageBullet = damage;
        //Debug.Log(damage);
    }

    private void OnDisable()
    {
        Rigidbody.angularVelocity = Vector3.zero;
        Rigidbody.linearVelocity = Vector3.zero;
    }

    private int damageBullet;

    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.TryGetComponent<Enemy>(out Enemy enemyComponent)) {
            enemyComponent.dealDamage(damageBullet);
        }
    }
}
