using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoDestruct : MonoBehaviour {

	[Tooltip("Time after which this object will be destroyed")]
	public float time = 1;

	//destoy object after initialization
	void Start () {
	
		Destroy (this.gameObject, time);
	}
}
