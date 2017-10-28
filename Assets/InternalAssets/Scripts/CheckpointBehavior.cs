using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/****
 *  Behavior : Will change Material to activeCheckPointMaterial if it's the next the player has to reach
 *             Save time and add it to the list checkpointTimeTable everytime a checkpoint is reached so 
 *             we can display time per section at the end of the track
 *             User MUST tick the case firstCheckpoint in the editor for the first checkpoint 
 *             AND link the next checkpoint using the "nextCheckpoint" property in the editor as it 
 *             will be used to activate it (except for the final checkpoint)
 */

public class CheckpointBehavior : MonoBehaviour
{
    [SerializeField] private GameObject nextCheckpoint = null;              // Link to next checkpoint
    [SerializeField] private bool firstCheckpoint = false;                  // Is this the first checkpoint the player has to reach ?
    [SerializeField] private Material activeCheckpointMaterial = null;      // Material used when the checkpoint is the one the player has to reach
    [SerializeField] private Material inactiveCheckpointMaterial = null;    // Material used when the checkpoint is not the next one the player has to reach/has been reached
    private static GameObject restartCheckpoint = null;                      // Checkpoint to respawn on if player dies
    private bool active;                                                    // describe current checkpoint state, true if the player has to reach it
    private bool finalCheckpoint;                                           // true if there is no nextCheckpoint linked in the editor
    private bool triggered;                                                 // triggered if a player collide with its trigger
    private static List<float> checkpointTimeTable= new List<float>();      // Contains player's time for each section of the track
    private Renderer renderer = null;                                       // checkpoint's renderer used to change its look according to active state
    private UIBehavior ui;                                                  // ui is holding time value, we need to access it to save time per section value

	// Use this for initialization
	void Start ()
    {
        ui = GameObject.Find("UI").GetComponent<UIBehavior>();
        if (ui == null)
            Debug.LogError("couldn't locate UI");
        renderer = GetComponent<Renderer>();

        active = firstCheckpoint;

        restartCheckpoint = null;

        if(firstCheckpoint)
            renderer.material = activeCheckpointMaterial;

        if(nextCheckpoint == null)
            finalCheckpoint = true;
	}
	
	// Update is called once per frame
	void Update ()
    {
        if(active && triggered)
        {
            // save times
            ui.getTime();
            checkpointTimeTable.Add(ui.getTime()); // get time from UI

            // deactivate this one => change renderer material or stuff
            renderer.material = inactiveCheckpointMaterial;
            active = false;

            // Set this checkpoint as the new respawn point
            restartCheckpoint = this.gameObject;

            // active next checkpoint
            if(!finalCheckpoint)
                nextCheckpoint.GetComponent<CheckpointBehavior>().activate();
            else
                ui.printSectionTimeTable();
        }
	}

    void OnTriggerEnter(Collider col)
    {
        if(col.gameObject.CompareTag("Player") && active)
        {
            triggered = true;
        }
    }

    public void activate()
    {
        renderer.material = activeCheckpointMaterial;
        active = true;
    }

    public static List<float> getCheckpointTimeTable()
    {
        return checkpointTimeTable;
    }

    public static GameObject getRestartCheckpoint()
    {
        return restartCheckpoint;
    }
}
