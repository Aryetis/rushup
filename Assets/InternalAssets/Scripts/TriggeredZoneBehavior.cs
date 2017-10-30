using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriggeredZoneBehavior : MonoBehaviour {

    [SerializeField] private string text;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    void OnTriggerEnter(Collider other)
    {
        if(other.name == "FPSController")
        {
            UIBehavior.setTriggeredZoneText(text);
        }
    }
}
