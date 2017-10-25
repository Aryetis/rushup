using System.Collections;
using UnityStandardAssets.Characters.FirstPerson; // only for MouseLook
using UnityStandardAssets.CrossPlatformInput;
using UnityEngine;

/* TODO list :
 *      allow player to accelerate going in reverse ?
 * 
 */

public class parkourFPSController : MonoBehaviour
{
    private enum PlayerState {running, jumping, walling, sliding, edging, pushing};
    // Describing current state of the player : edging <=> grabed the edge of a cliff
    //                                          pushing <=> pushing up from edging state

    [Header("Global Variables")]
    [SerializeField] private float gravity = 9.81f;                             // Gravity applied to the vector on the Y axis
    [SerializeField] private float jumpStrength = 20f;                          // Impulse given at the start of a jump
    [SerializeField] private float minSpeed = 10f;                              // Player will start running at this speed
    [SerializeField] private float maxNominalSpeed = 100f;                      // Player's max speed without any killSpeedBonus
    [SerializeField] private float runninRampUpTime = 3.0f;                     // Time for player to reach maxNominalSpeed (in seconds)
    [SerializeField] private float runningDeceleration = 5f;                    // Factor applied to player's velocity whenever player was running and is letting go of inputs 
    [SerializeField] private float killSpeedBonus = 5f;                         // Speed boost given immediately for each ennemy killed
    [SerializeField] private float slopeClimbingPermissionStep = 0.25f;         // Speed boost given immediately for each ennemy killed
    [Space(10)]
    [Header("Acceleration/Deceleration Factors")]
    [Space(10)]
    [Header("Mouse Properties")]
    [SerializeField] private MouseLook mouseLook = null;


    private Camera camera = null;
    private CharacterController controller;
    private PlayerState playerState = PlayerState.running;
    private Vector3 moveDir=Vector3.zero, prevMoveDir=Vector3.zero;
    private bool forwardKeyDown, prevGroundedState;
    private UnityEngine.UI.Text m_SpeedOMeterText;       // Text printed on the UI containing speed informations
    private UnityEngine.UI.Text m_DebugZoneText;         // Text printed on the UI containing speed informations
    private float forwardKeyDownTime = 0f;
    private float gravityFactor = 1f;                    // gravity doesn't always impact the player the same way (eg : during a wallrun)
    private bool grounded;                              // Not using controller.isGrounded value because result is based on the PREVIOUS MOVE state
                                                        // Resulting in unreliable state when running up on slanted floors
                                                        // ( https://forum.unity.com/threads/charactercontroller-isgrounded-returning-unreliable-state.494786/ ) 
    private float horizontalSpeed;

    private float inputHorizontal;
    private float inputVertical;
    private float prevInputHorizontal;
    private float prevInputVertical;
    private bool inputJump;
    private bool inputSlide;





	// Use this for initialization
	void Start ()
    {
        camera = Camera.main;
        controller = GetComponent<CharacterController>();
        controller.detectCollisions = true;
        mouseLook.Init(transform , camera.transform);
        m_SpeedOMeterText = GameObject.Find ("SpeedOMeter").GetComponent<UnityEngine.UI.Text>();
        m_DebugZoneText = GameObject.Find ("DebugZone").GetComponent<UnityEngine.UI.Text>();

        // Teleport Player to the ground to be sure of its playerState at startup
        RaycastHit hit;
        if(Physics.Raycast(transform.position, Vector3.down, out hit, 1000))
        {
            transform.position = new Vector3(hit.point.x, hit.point.y + controller.height, hit.point.z);
            grounded = true;
        }
        else
        {
            Debug.LogError("Please put the Player prefab above a floor/closer to it");
        }
	}


	
	// Update is called once per frame
	void Update ()
    {
        /*** CAPTURING INPUTS ***/
        //TODO this section to fixedUpdate to be sure we're not missing any inputs in case of lag
        inputHorizontal = CrossPlatformInputManager.GetAxis("Horizontal"); 
        inputVertical = CrossPlatformInputManager.GetAxis("Vertical");
        inputJump = CrossPlatformInputManager.GetButton("Jump");

        /*** UPDATING UI ***/
        updateUI();

        /*** UPDATING grounded STATE ***/
        RaycastHit hit;
        grounded = Physics.Raycast(controller.transform.position, Vector3.down, out hit, (controller.height / 2f) + controller.skinWidth + slopeClimbingPermissionStep);

        /*** CALCULATING FORCE FROM INPUTS & STATE***/ 
        switch(playerState)
        {
            case PlayerState.running:
            {
                updateRunning();
                break; 
            }
            case PlayerState.jumping:
            {
                updateJumping();
                break; 
            }
            case PlayerState.walling:
            {
                updateWalling();
                break; 
            }
            case PlayerState.sliding:
            {
                updateSliding();
                break; 
            }
            case PlayerState.edging:
            {
                updateEdging();
                break; 
            }
            case PlayerState.pushing:
            {
                updatePushing();
                break; 
            }
            default:
            { break; }
        }


        /*** APPLYING moveDir FORCE ***/
        controller.Move(moveDir * Time.deltaTime);

        /*** CONSERVING DATA FOR FUTURE REFERENCES ***/
        prevMoveDir = moveDir;
        prevGroundedState = controller.isGrounded;
        prevInputHorizontal = inputHorizontal;
        prevInputVertical = inputVertical;

        /*** LOCK mouseLook TO PREVENT UNWANTED INPUTS ***/
        mouseLook.UpdateCursorLock();
	}



    // FixedUpdate is called once per physic cycle
    void FixedUpdate ()
    { 

    }
        


    void updateRunning()
    {
        // Update Camera look and freedom according to playerState
        updateCamera();
         
        Debug.Log("moveDir : " + moveDir);

        if(grounded)
        {
            // Make sure that our state is set (in case of falling of a clif => no jump but still been airborne for a while)
            playerState = PlayerState.running;

            // get direction Vector3 from input
            moveDir = new Vector3(inputHorizontal, 0f, inputVertical);
            moveDir = transform.TransformDirection(moveDir); // Align moveDir vector with localTransform/camera forward vector
            moveDir.Normalize();

            // Correct moveDir according to the floor's slant
            RaycastHit hitInfoDown;
            if(Physics.SphereCast(transform.position, controller.radius, Vector3.down, out hitInfoDown,
               controller.height / 2f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                moveDir = Vector3.ProjectOnPlane(moveDir, hitInfoDown.normal).normalized;
            }

            // Build up the "momementum" as long as player is pressing "forward"
            // WARNING : place after slant correction phase because it uses moveDir to determine wheter the player is insta changing direction
            //           so it can build his momentum or not
            forwardKeyDown = (inputHorizontal>0 || inputVertical!=0) ? true : false;
            if (forwardKeyDown && forwardKeyDownTime <= runninRampUpTime)
            {
                //TODO add conditiion to check that we're going in the same direction 
                // TODO USE inputHorizontalVertical and prevInputs to check that /\
                forwardKeyDownTime += Time.deltaTime; // build up "temporal"    momentum 
                if (forwardKeyDownTime > runninRampUpTime)  // till we reach rampUpTime
                {
                    forwardKeyDownTime = runninRampUpTime;
                }
            }
            else // If Player is letting go of the "forward" key, reduce "momentum"
            {
                forwardKeyDownTime -= Time.deltaTime;
                if (forwardKeyDownTime < 0)
                {
                    forwardKeyDownTime = 0;
                }
            }

            // Compute moveDir according to minSpeed, maxNominalSpeed, deltaTime, killStackSpeed, etc
            moveDir *= minSpeed + ((maxNominalSpeed-minSpeed) * (forwardKeyDownTime / runninRampUpTime)); 

            // Take care of Deceleration, WARNING : place after the input compute phase as 
            // the deceleration process can override inputs value and modify moveDir based upon prevMoveDir
            // TODO : maybe split this section with a if(inputs) then ... would help visibility probably
            if ((moveDir == Vector3.zero)) // <=> if no inputs
            {
                if (horizontalSpeed <= minSpeed + 0.01) // if player is approching the minSpeed, stop him
                {
                    moveDir.x = 0;
                    moveDir.z = 0;
                }
                else // player is decelerating 
                {
                    if (prevMoveDir.x != 0)
                    {
                        moveDir.x = ApplyDeceleration(prevMoveDir.x, runningDeceleration);
                    }

                    if (prevMoveDir.z != 0)
                    {
                        moveDir.z = ApplyDeceleration(prevMoveDir.z, runningDeceleration);
                    }
                }
            }    

            // Jump Requested 
            if(inputJump)
            {   
                playerState = PlayerState.jumping;
                moveDir.y = jumpStrength; // TODO : tweak it so a jump at maxSpeed is 1,5* a basic one 
            }
        }
        else // Player is running from an edge => change state to "jumping" and override current update()'s cycle result
        {
            playerState = PlayerState.jumping;
        }

        // Applying gravity
        moveDir.y -= gravity * Time.deltaTime;
    }



    void updateJumping()
    {
        // Update Camera look and freedom according to playerState
        updateCamera();

        if(grounded) // hitting the ground
        {
            playerState = PlayerState.running;
            return;
        }

        // Check that player isn't bashing its head on the ceiling
        RaycastHit hit;
        if ( Physics.SphereCast (transform.position, controller.radius, Vector3.up, out hit,
            controller.height / 2f, Physics.AllLayers, QueryTriggerInteraction.Ignore) ) // player hit its head during a jump     
            moveDir.y=0;

        // get direction Vector3 from input
//        moveDir = new Vector3(CrossPlatformInputManager.GetAxis("Horizontal"), 0f, CrossPlatformInputManager.GetAxis("Vertical"));
//        moveDir = transform.TransformDirection(moveDir) + prevMoveDir;
//        moveDir.Normalize();

        // Applying gravity
        moveDir.y -= gravity * Time.deltaTime;
    }

    void updateWalling()
    {
        // Update Camera look and freedom according to playerState
        updateCamera();

    }



    void updateSliding()
    {
        // Update Camera look and freedom according to playerState
        updateCamera();

    }



    void updateEdging()
    {
        // Update Camera look and freedom according to playerState
        updateCamera();

    }



    void updatePushing()
    {
        // Update Camera look and freedom according to playerState
        updateCamera();

    }

    void updateCamera()
    {
        switch(playerState)
        {
            default:
            {   // Allow rotation on every axis by default
                mouseLook.LookRotation (transform, camera.transform);
                break;
            }
        }
    }



    void updateUI()
    {
        horizontalSpeed = (float) Mathf.Sqrt(controller.velocity.x * controller.velocity.x +
                                         controller.velocity.z * controller.velocity.z);
        // Actualize SpeedOMeter UI text
        m_SpeedOMeterText.text = horizontalSpeed + "m/s";
//        m_DebugZoneText.text = "m_speedPorcentage : " + m_speedPorcentage;
    }



    float ApplyDeceleration(float lastVelocity, float decelerationFactor)
    {
        if (lastVelocity > 0)
        {
            lastVelocity -= decelerationFactor * Time.deltaTime;
            if (lastVelocity < 0)
                lastVelocity = 0;
        }
        else
        {
            lastVelocity += decelerationFactor * Time.deltaTime;
            if (lastVelocity > 0)
                lastVelocity = 0;
        }
        return lastVelocity;
    }
}
