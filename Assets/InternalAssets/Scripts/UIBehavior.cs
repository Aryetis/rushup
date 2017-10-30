using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIBehavior : MonoBehaviour
{
    private float currentTime = 0;                    // Contains current time since the object creation
                                                      // TODO make a script to add a 3,2,1, GO timer at start 
    private UnityEngine.UI.Text speedOMeterText;      // Text printed on the UI containing speed informations
    private UnityEngine.UI.Text debugZoneText;        // Text printed on the UI containing debug information
    private UnityEngine.UI.Text timerText;            // Text printed on the UI containing timer
    private UnityEngine.UI.Text SectionTimeTableText; // Text printed on the UI containing timer
    private static UnityEngine.UI.Text triggeredZoneText;    // Text printed on the UI containing triggered zone text

    // Use this for initialization
    void Start ()
    {
        speedOMeterText = GameObject.Find ("SpeedOMeter").GetComponent<UnityEngine.UI.Text>();
        debugZoneText = GameObject.Find ("DebugZone").GetComponent<UnityEngine.UI.Text>();
        timerText = GameObject.Find("Timer").GetComponent<UnityEngine.UI.Text>();
        SectionTimeTableText = GameObject.Find("SectionTimeTable").GetComponent<UnityEngine.UI.Text>();
        triggeredZoneText = GameObject.Find("triggeredZoneText").GetComponent<UnityEngine.UI.Text>();
    }
	
	// Update is called once per frame
	void Update ()
    {
        currentTime += Time.deltaTime;

        // Actualize SpeedOMeter UI text
        speedOMeterText.text = parkourFPSController.getSpeed() + "m/s";
        // Actualize debugZone text
        debugZoneText.text = "current state : " + parkourFPSController.getPlayerState();
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
        // freeze/slowDown the game
        Time.timeScale = 0.01f; // can't set timescale to 0 ... because Unity

        // claim back the cursor
        parkourFPSController pkScript = GameObject.Find("FPSController").GetComponent<parkourFPSController>();
        pkScript.mouseLook.lockCursor = false;
        pkScript.mouseLook.XSensitivity = 0;
        pkScript.mouseLook.YSensitivity = 0;

        // clean the UI and print each time per section
        GameObject.Find("SpeedOMeter").SetActive(false);
        GameObject.Find("DebugZone").SetActive(false);
        GameObject.Find("Timer").SetActive(false);

        int sectionEntry = 0;
        foreach(float time in CheckpointBehavior.getCheckpointTimeTable())
        {
            string minutes = ((int)time / 60).ToString();
            string seconds = (time % 60).ToString("F2");
            string timeEntry = "Section"+sectionEntry+" : "+minutes+":"+seconds+"\n";
            SectionTimeTableText.text += timeEntry;
            sectionEntry++;
        }
    }

    public static void setTriggeredZoneText(string text)
    {
        triggeredZoneText.text = text;
    }
}
