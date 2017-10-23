using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RaycastHandler : MonoBehaviour {

	TurretController controller; 
	private LineRenderer lineRenderer; // For laser effect

	[Header("Points")]
	[Tooltip("Point from which raycast should start")]
	public Transform startPoint;

	void Start(){	
		lineRenderer = startPoint.GetComponent<LineRenderer> ();
		controller = this.GetComponent<TurretController> ();
	}

	void FixedUpdate(){

		if (startPoint && lineRenderer && !controller._Health.isDestroyed) {
		
			RaycastHit hit;

			float range = controller._Shooting.range; //get range from the ShootingSystem script

			if (Physics.Raycast (startPoint.position, startPoint.forward, out hit, range)) {

				lineRenderer.SetPosition (1, new Vector3 (0, 0, hit.distance));//if raycast hit somewhere then stop laser effect at that point
				controller._Shooting.Fire (hit.point, hit.collider.gameObject);//if hit some point then shoot through shootingSystem Fire Function

			}else{
				lineRenderer.SetPosition (1, new Vector3 (0, 0, range));//if not hit, laser till range 
			}
		}
	}

	public void TurretLaser_Status(bool val){
	
		lineRenderer.enabled = val;
	}
}
