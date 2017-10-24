using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Health : MonoBehaviour {

	[Tooltip("Health of the Object")]
	public float health = 100;

	[Tooltip("If enabled Hit sound will be played")]
	public bool playHitSound = true;

	[Tooltip("Sound when object has been hit")]
	public AudioClip getHitSound;

	AudioSource audio;

	void Start(){
	
		this.audio = this.GetComponent<AudioSource> ();
	}

	//handle damage on the object
	public void ApplyDamage(float damage){

		if (health - damage > 0) {

			if(playHitSound)
				this.audio.PlayOneShot (getHitSound);

			health -= damage;

		} else {

			health = 0;

			Destroy ();
		}
	}
		
	//called when zero health
	void Destroy(){

		Destroy (this.gameObject);
	}
}
