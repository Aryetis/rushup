using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurretRotation : MonoBehaviour {

	TurretController controller;

	[Tooltip("If enabled then gun will aim automatically")]
	public bool autoRotate = true;
	[Tooltip("Gameobject which should rotate to aim the target")]
	public Transform gunAimPoint;
	[Tooltip("Rotation speed of the Turret")]
	public float rotationSpeed = 1;

	void Start(){

		controller = this.GetComponent<TurretController> ();
	}

	void Update(){

		if (autoRotate && CanTarget()) {
			//Rotate toward the Target with rotation speed
			Quaternion targetRotation = Quaternion.LookRotation (controller._Shooting.target.transform.position - gunAimPoint.transform.position, gunAimPoint.transform.up);
			gunAimPoint.transform.rotation = Quaternion.Lerp (gunAimPoint.transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

		}
	}

	//Either Target is in Range or not -> if in range then Rotate and target else donot Rotate
	bool CanTarget(){
	
		if (controller._Health.isDestroyed)
			return false;

		if (controller._Shooting.target) {
		
			if (Vector3.Distance (this.transform.position, controller._Shooting.target.position) < controller._Shooting.range) {
			
				return true;
			}
		} else {
			
			return false;
		}

		return false;
	}
}
