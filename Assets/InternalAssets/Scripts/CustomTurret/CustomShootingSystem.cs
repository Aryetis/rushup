using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomShootingSystem : ShootingSystem
{
    [Header("Projectile")]
    [Tooltip("Prefab to be used as ammo")]
    [SerializeField]
    private GameObject _projectile = null;

    [SerializeField]
    private Transform _spawner = null;
    [SerializeField]
    private int _shootingForce = 0;

    public override void Fire(Vector3 hitPoint, GameObject hitObject)
    { //with hit effect
        if (time > fireDelay)
        {
            fireMuzzle.Stop();
            fireMuzzle.Play();
            time = 0;
            controller._Audio.Play_Fire();

            GameObject bullet = Instantiate(_projectile, _spawner.position, Quaternion.identity);
            bullet.GetComponent<Rigidbody>().transform.Rotate(0, 0, 90);
            bullet.GetComponent<Rigidbody>().AddForce(_spawner.transform.forward * _shootingForce);
        }
    }
}
