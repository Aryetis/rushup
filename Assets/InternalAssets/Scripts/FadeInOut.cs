using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;

public class FadeInOut : MonoBehaviour
{
    private enum FadeStatus {fadein, fadeOut}; // Describing current state of the player : edging <=> grabed the edge of a cliff; pushing <=> pushing up from edging state; etc . jumping can be used pretty much as the default state

    [SerializeField] private float fadeInTime = 2f;
    [SerializeField] private float fadeOutTime = 2f;
    private FadeStatus status;

	// Use this for initialization
	void Start ()
    {
        status = FadeStatus.fadein;
	}
	
	// Update is called once per frame
	void Update ()
    {
        if (status == FadeStatus.fadein)
        {
            fadeInTime -= Time.deltaTime;
            if (fadeInTime < 0)
                status = FadeStatus.fadeOut;
        }
        else
        {
            fadeOutTime -= Time.deltaTime;
            if (fadeOutTime < 0)
            {
                SceneManager.LoadScene("MainMenu");
            }
        }
	}
}
