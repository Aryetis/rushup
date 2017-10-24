using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurretController : MonoBehaviour {

	[Tooltip("All the Scripts reference on this object")]
	[Header("Components")]
	public TurretRotation _Rotation;
	public RaycastHandler _Raycast;
	public ShootingSystem _Shooting;
	public TurretHealth _Health;
	public AudioHandler _Audio;

	[Header("Other")]
	[Tooltip("Rigidbody Components of the turret")]
	public Rigidbody [] _Rigidbodies;
	[Tooltip("Mesh Colliders of the turret")]
	public MeshCollider [] _MeshColliders;

	void Awake(){
		//getting all the components on the object. Setting them before RUN state will be a better approach
		_Rotation = this.GetComponent<TurretRotation> ();
		_Raycast = this.GetComponent<RaycastHandler> ();
		_Shooting = this.GetComponent<ShootingSystem> ();
		_Health = this.GetComponent<TurretHealth> ();
		_Audio = this.GetComponent<AudioHandler> ();

	}

	//to enable/disable Rigidbody isKinematic
	public void isKinematicRigidbodies(bool val){
	
		for (int i = 0; i < _Rigidbodies.Length; i++) {

			_Rigidbodies [i].isKinematic = val;
		}
	}

	//to enable/disable Mesh Colliders
	public void MeshCollider_Status(bool val){

		for (int i = 0; i < _MeshColliders.Length; i++) {

			_MeshColliders [i].enabled = val;
		}
	}

}
