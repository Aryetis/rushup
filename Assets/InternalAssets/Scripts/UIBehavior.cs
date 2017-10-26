using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIBehavior : MonoBehaviour
{
    private float currentTime = 0;                  // Contains current time since the object creation
                                                    // TODO make a script to add a 3,2,1, GO timer at start 
    private UnityEngine.UI.Text speedOMeterText;    // Text printed on the UI containing speed informations
    private UnityEngine.UI.Text debugZoneText;      // Text printed on the UI containing debug information
    private UnityEngine.UI.Text timerText;          // Text printed on the UI containing timer

    private parkourFPSController fpsConScript;

	// Use this for initialization
	void Start ()
    {
        fpsConScript = GameObject.FindGameObjectWithTag("Player").GetComponent<parkourFPSController>();

        speedOMeterText = GameObject.Find ("SpeedOMeter").GetComponent<UnityEngine.UI.Text>();
        debugZoneText = GameObject.Find ("DebugZone").GetComponent<UnityEngine.UI.Text>();
        timerText = GameObject.Find("Timer").GetComponent<UnityEngine.UI.Text>();
	}
	
	// Update is called once per frame
	void Update ()
    {
        currentTime += Time.deltaTime;

        // Actualize SpeedOMeter UI text
        speedOMeterText.text = fpsConScript.getSpeed() + "m/s";
        // Actualize debugZone text
        debugZoneText.text = "current state : " + fpsConScript.getPlayerState();
        // Actualize timer text
        string minutes = ((int)currentTime / 60).ToString();
        string seconds = (currentTime % 60).ToString("F2");
        timerText.text = minutes + ":" + seconds;
	}

    public float getTime()
    {
        return currentTime;
    }

    public void printSectionTimeTable()
    {
        Debug.Log("game over");
        // freeze the game
        Time.timeScale = 0.05f; // can't set timescale to 0 ... because Unity
        // clean the UI and print each time per section

    }
}
