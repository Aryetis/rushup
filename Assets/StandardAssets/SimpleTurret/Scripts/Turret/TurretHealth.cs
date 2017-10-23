using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurretHealth : MonoBehaviour {

	TurretController controller;

	[Tooltip("Weather the turret is Destroyed or not")]
	public bool isDestroyed = false;

	[Tooltip("Turret Current Health")]
	public float health = 100;
	[Tooltip("Total Health of the Turret in Start")]
	public float totalHealth = 100; //Should be used when repairing is done

	void Start(){

		controller = this.GetComponent<TurretController> ();
	}

	//Apply damage to Turret
	public void ApplyDamage(float damage){

		if (health - damage > 0) {
		
			health -= damage;
		
		} else {
		
			isDestroyed = true;
			health = 0;

			Destroy ();
		}

		controller._Audio.Play_GetHit ();
	}

	void Destroy(){
	
		controller.MeshCollider_Status (true);
		controller.isKinematicRigidbodies (false);
		controller._Raycast.TurretLaser_Status (false);

		Destroy (this.gameObject, 3);
	}

}
