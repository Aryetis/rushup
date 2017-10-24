using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomShootingSystem : ShootingSystem
{

    //Main turret controller Component
    TurretController controller;

    //Random Variables
    bool reloading = false;
    float time = 0;

    [Header("Projectile")]
    [Tooltip("Prefab to be used as ammo")]
    [SerializeField]
    private GameObject projectile;

    [SerializeField]
    private Transform _spawner;

    //Get the component
    void Start()
    {
        controller = this.GetComponent<TurretController>();
    }

    void Update()
    {
        //check FireDelay after fire
        if (time <= fireDelay)
            time += Time.deltaTime;
    }

    public override void Fire(Vector3 hitPoint, GameObject hitObject)
    { //with hit effect
        if (time > fireDelay)
        {
            fireMuzzle.Stop();
            fireMuzzle.Play();
            time = 0;
            controller._Audio.Play_Fire();

            GameObject bullet = Instantiate(projectile, _spawner.position, Quaternion.identity);
            bullet.GetComponent<Rigidbody>().transform.Rotate(0, 0, 90);
            bullet.GetComponent<Rigidbody>().AddForce(_spawner.transform.forward * 1800);
        }
    }
}
