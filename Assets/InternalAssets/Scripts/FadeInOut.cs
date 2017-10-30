using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;

public class FadeInOut : MonoBehaviour
{
    private enum FadeStatus {fadein, pause,  fadeOut}; // Describing current state of the player : edging <=> grabed the edge of a cliff; pushing <=> pushing up from edging state; etc . jumping can be used pretty much as the default state

    [SerializeField] private float fadeInTime = 1f;
    [SerializeField] private float pauseTime = 2f;
    [SerializeField] private float fadeOutTime = 1f;
    [SerializeField] private UnityEngine.UI.Image blackScreen; 
    private FadeStatus status;
    private Color actualColor;


	// Use this for initialization
	void Start ()
    {
        status = FadeStatus.fadein;
	}
	
	// Update is called once per frame
	void Update ()
    {
        // Fade in
        if (status == FadeStatus.fadein)
        {
            fadeInTime -= Time.deltaTime;

            blackScreen.CrossFadeAlpha(0, fadeInTime, false);

            if (fadeInTime < 0)
                status = FadeStatus.pause;
        }
        // Pause for dramatic effect
        else if (status == FadeStatus.pause)
        {
            pauseTime -= Time.deltaTime;

            if (pauseTime < 0)
                status = FadeStatus.fadeOut;
        }
        // Fade out
        else if (status == FadeStatus.fadeOut)
        {
            fadeOutTime -= Time.deltaTime;

            blackScreen.CrossFadeAlpha(1, fadeOutTime, false);

            if (fadeOutTime < 0)
                SceneManager.LoadScene("MainMenu");
        }
	}
}
