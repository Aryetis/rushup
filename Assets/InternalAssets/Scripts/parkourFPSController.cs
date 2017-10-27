using System.Collections;
using UnityStandardAssets.Characters.FirstPerson; // only for MouseLook
using UnityStandardAssets.CrossPlatformInput;     // TODO : check if controls are working on Android ... it shoulds otherwise there's close to no point using CrossPlatformInput
using UnityEngine;

/* TODO (after the project is "done") list :
 *      Put different factor for walking in reverse
 *      
 */

public class parkourFPSController : MonoBehaviour
{
    /* Player's state variable*/
    private enum PlayerState {running, jumping, walling, sliding, edging, pushing, attacking}; // Describing current state of the player : edging <=> grabed the edge of a cliff; pushing <=> pushing up from edging state; etc
    private bool canWallRun = false;                                                // Describe if player is in a state that allows for him to start wallrunning (can't wallrun during a slide, duh)
    private bool canWallClimb = false;                                              // Describe if player is in a state that allows for him to start wallclimbing 
    private bool canAttack = false;                                                 // Describe if player is in a state that allows for him to start attacking
    private bool canSlide = false;                                                  // Describe if player is in a state that allows for him to start sliding

    [Header("Global Variables")]
    [SerializeField] private float gravity = 20f;                                   // Gravity applied to the vector on the Y axis
    [SerializeField] private float jumpStrength = 20f;                              // Impulse given at the start of a jump
    [SerializeField] private float slopeClimbingPermissionStep = 0.25f;             // Height shift allowed on Y axis between two frames to considere if the player is grounded or not 
    [SerializeField] private float maxNominalSpeed = 100f;                          // Player's max speed without any killSpeedBonus
    private Camera camera = null;                                                   // Player's Camera
    private CharacterController controller;                                         // Player's controller
    private float inputHorizontal;                                                  // [-1;1] horizontal input for strafes (smoothed)
    private float inputVertical;                                                    // [-1;1] horizontal input for running/reversing (smoothed)
    private float prevInputHorizontal;                                              // Previous frame's inputHorizontal
    private float prevInputVertical;                                                // Previous frame's inputVertical
    private bool inputJump;                                                         // is jump key pressed ?
    private bool inputSlide;                                                        // is sllding key pressed ?
    private bool inputAttacking;                                                    // TODO is attacking key pressed ?
    private static PlayerState playerState = PlayerState.running;                   // Describe current player state
    private static float speed;                                                     // Player speed along x and z axis => NOT taking into account Y axis (no falling speed displayed)
    private Vector3 moveDir=Vector3.zero;                                           // Current frame player's movement vector
    private Vector3 prevMoveDir=Vector3.zero;                                       // Previous frame player's movement
    private bool prevGroundedState;                                                 // Previous frame's grounded
    private bool grounded;                                                          // Not using controller.isGrounded value because result is based on the PREVIOUS MOVE state
                                                                                    // Resulting in unreliable state when running up on slanted floors
                                                                                    // ( https://forum.unity.com/threads/charactercontroller-isgrounded-returning-unreliable-state.494786/ ) 

    [Space(10)]
    [Header("Running State Variables")]
    [SerializeField] private float runningMinSpeed = 10f;                           // Player will start running at this speed
    [SerializeField] private float runningRampUpTime = 0.2f;                        // Time in seconds for player to reach maxNominalSpeed (in seconds)
    [Range(0.0f, 1.0f)] [SerializeField] private float runningInertiaFactor = 0.9f; // [0;1] the bigger the less current input will impact the outcome / the more slippery the player wil be
    [SerializeField] private float runningDecelerationFactor = 0.5f;                // will decelerate at "runningDecelerationFactor" the speed it accelerates
    [SerializeField] private float jumpHeightSpeedFactor = 1.5f;                    // At full speed player will jump at jumpHeightSpeedFactor * the height of a basic jump
    private float runningMomentum = 0f;                                             // [0;runningRampUpTime] "porcentage" of the current speed wihtout acknoledging minSpeed


    [Space(10)]
    [Header("Airborne State Variables")]
    [SerializeField] private float airControlFactor = 2.0f;                         // Determine how much the inputs performed by the player while airborne impact his direction
    private Vector3 runningToJumpingImpulse = Vector3.zero;                         // moveDir vector at the moment of the jump, used to kickstart the direction of the jump
    private Vector3 previousAirControlDir;                                          // direction of the airborne player at the previous frame

    [Space(10)]
    [Header("Wallrun State Variables")]
    [SerializeField] private float wallRunMaxTime = 5.0f;                           // How long the player can wallrun TODO : change it to minSpeedWallRun
    [SerializeField] float wallrunMaxSpeed = 50f;                                   // Max Speed during wallrun (speed will increase over time)
    [SerializeField] float kickImpulse = 20f;                                       // TODO
    [SerializeField] float kickAngleHorizontal = 30f;                               // TODO
    [SerializeField] float kickAngleVertical = 30f;                                 // TODO
    private RaycastHit wallHit;                                                     // Target the wall the player is/can currently wallruning on
    private float wallRunTime = 0.0f;                                               // How long player has been wallrunning

    [Space(10)]
    [Header("Wallclimb State Variables")]

    [Space(10)]
    [Header("Sliding State Variables")]
    [SerializeField] private float slidingMinSpeed = 10f;                           // TODO

    [Space(10)]
    [Header("Attacking State Variables")]
    [SerializeField] private float impulse = 50f;                                   // TODO
    [SerializeField] private float killSpeedBonus = 5f;                             // TODO Speed boost given immediately for each ennemy killed

    [Space(10)]
    [Header("Mouse Properties")]
    [SerializeField] public MouseLook mouseLook = null;                             // Standard Asset script taking care of moving the camera according to mouse inputs
                                                                                    // public because UI must unlock cursor to allow player to click on buttons




	// Use this for initialization
	void Start ()
    {
        camera = Camera.main;
        controller = GetComponent<CharacterController>();
        controller.detectCollisions = true;
        mouseLook.Init(transform , camera.transform);

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

        /*** UPDATING speed (for UI and various update[State]() ***/
        speed = (float) Mathf.Sqrt(controller.velocity.x * controller.velocity.x +
            controller.velocity.z * controller.velocity.z);

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
            case PlayerState.attacking:
            {
                updateAttacking();
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
         
        if(grounded)
        {
            // Make sure that our state is set (in case of falling of a clif => no jump but still been airborne for a while)
            playerState = PlayerState.running;

            // Turn on possible moves flags
            canWallRun = true;

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

            // Build up the "momementum" as long as player is pressing "moving forward/strafing"
            bool moving = (inputHorizontal!=0 || inputVertical!=0) ? true : false;
            if (moving && runningMomentum <= runningRampUpTime)
            {
                runningMomentum += Time.deltaTime; // build up "temporal" momentum 
                if (runningMomentum > runningRampUpTime)  // till we reach rampUpTime
                {
                    runningMomentum = runningRampUpTime;
                }
            }
            else // If Player is letting go of the "forward" key, reduce "momentum"
            {
                runningMomentum -= runningDecelerationFactor*Time.deltaTime;
                if (runningMomentum < 0)
                {
                    runningMomentum = 0;
                }
            }
                
            // Compute moveDir according to minSpeed, maxNominalSpeed, deltaTime, inertiaFactor, etc
            if (speed <= 0.5) // if player's speed is below minSpeed => kickstart player to runningMinSpeed
            {                 // no need to check if player is moving as in this case moveDir will already be at 0
                moveDir *= runningMinSpeed;
            }
            else
            if (!moving && speed <= runningMinSpeed) // if player is decelerating and going below runningMinSpeed => Stop him immediately
            {
                moveDir = Vector3.zero;
            }
            else // Player is moving beyond runningMinSpeed
            {
                Vector3 foo = moveDir * (runningMinSpeed + ((maxNominalSpeed-runningMinSpeed) * (runningMomentum / runningRampUpTime))); // Calculate current inputs impact on moveDir
                moveDir = foo * (1 - runningInertiaFactor) + prevMoveDir * runningInertiaFactor; // mix current inputs vector and previous one according to runningInertiaFactor
            }

            // Jump Requested 
            if(inputJump)
            {   
                runningToJumpingImpulse = moveDir;
                playerState = PlayerState.jumping;
                moveDir.y = jumpStrength + jumpStrength*(speed/maxNominalSpeed)*(jumpHeightSpeedFactor-1); 
                // "standard jump height" + "speed dependent height jump" * (jumpHeightSpeedFactor-1)
            }
        }
        else // Player is running from an edge => change state to "jumping" and override current update()'s cycle result
        {
            runningToJumpingImpulse = moveDir;
            playerState = PlayerState.jumping;
        }

        // Applying gravity
        moveDir.y -= gravity * Time.deltaTime;
    }



    void updateJumping()
    {
        // Update Camera look and freedom according to playerState
        updateCamera();

        // Check if we're hitting the floor
        if(grounded) 
        {
            playerState = PlayerState.running;
            // reset airBorne specific global values
            previousAirControlDir = Vector3.zero;
            return;
        }

        // Do a wall run check and change state if successful.
        wallHit = checkAccessibleWallrun();
        if (wallHit.collider != null)
        {
            playerState = PlayerState.walling;
            return;
        }

        // Set moveDir as impulse given on ground (will be countered as time goes by, by the airControlDir vector)
        moveDir.x = runningToJumpingImpulse.x;
        moveDir.z = runningToJumpingImpulse.z;

        // get direction Vector3 from input
        Vector3 airControlDir = new Vector3(inputHorizontal, 0f, inputVertical);
        airControlDir = transform.TransformDirection(airControlDir);
        airControlDir.Normalize();
       
        // GLUT : hardcoding a airControlFactor to decide how much control the player has over his initial impulse, because lack of time to test (see github for previous attempt, it worked but was pretty unplayable)
        airControlDir.x = previousAirControlDir.x + airControlDir.x * airControlFactor ;
        airControlDir.z = previousAirControlDir.z + airControlDir.z * airControlFactor ;

        //Combine moveDir and airControlDir according to airInertiaFactor factor
        moveDir = moveDir + airControlDir;

        // GLUT Patching speed so it doesn't go above maxSpeed, shouldn't happen but unity magic and probably because speed depends of x AND y and I'm using on x and y independantly 
        if (speed >= maxNominalSpeed)
            moveDir = moveDir.normalized * maxNominalSpeed;

        // Check that player isn't bashing its head on the ceiling
        RaycastHit hit;
        if (Physics.SphereCast(transform.position, controller.radius, Vector3.up, out hit,
                controller.height / 2f, Physics.AllLayers, QueryTriggerInteraction.Ignore)) // player hit its head during a jump    
        {
            moveDir.y = 0;
        }
        else
        {
            moveDir.y = prevMoveDir.y;
        }

        // Applying gravity
        moveDir.y -= gravity * Time.deltaTime;

        // Keeping track of airControlDir as we don't want to be able to immediately transform airControlDir from full left to full right, we must fight it too
        previousAirControlDir = airControlDir;
    }

    RaycastHit checkAccessibleWallrun()
    {
        Ray rayRight = new Ray(transform.position, transform.TransformDirection(Vector3.right));
        Ray rayLeft = new Ray(transform.position, transform.TransformDirection(Vector3.left));

        RaycastHit wallImpactRight;
        RaycastHit wallImpactLeft;

        bool rightImpact = Physics.Raycast(rayRight.origin, rayRight.direction, out wallImpactRight, 1f);
        bool leftImpact = Physics.Raycast(rayLeft.origin, rayLeft.direction, out wallImpactLeft, 1f);

        if (rightImpact && Vector3.Angle(transform.TransformDirection(Vector3.forward), wallImpactRight.normal) > 90)
        {
            return wallImpactRight;
        }
        else if (leftImpact && Vector3.Angle(transform.TransformDirection(Vector3.forward), wallImpactLeft.normal) > 90)
        {
            wallImpactLeft.normal *= -1;
            return wallImpactLeft;
        }
        else
        {
            // Just return something empty, because nothing is good for a wall run
            return new RaycastHit();
        }
    }

    void updateWalling()
    {
        // Update Camera look and freedom according to playerState
        updateCamera();

        if (!controller.isGrounded && canWallRun && wallRunTime < wallRunMaxTime)
        {
            // Always update the wallhit, because we run past the edge of a wall. This keeps us 
            // from floating off in to the ether.
            wallHit = checkAccessibleWallrun();
            if (wallHit.collider == null)
            {
                stopWallRun();
                return;
            }

            playerState = PlayerState.walling;
            float previousJumpHeight = moveDir.y;

            Vector3 crossProduct = Vector3.Cross(Vector3.up, wallHit.normal);

            Quaternion lookDirection = Quaternion.LookRotation(crossProduct);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookDirection, 3.5f * Time.deltaTime);

            camera.transform.Rotate(new Vector3(0f,0f,800f * Time.deltaTime));
           
            moveDir = crossProduct;
            moveDir.Normalize();
            moveDir *= runningMinSpeed + ( wallrunMaxSpeed * (runningMomentum / runningRampUpTime));

            if (wallRunTime == 0.0f)
            {
                // increase vertical movement.
                moveDir.y = jumpStrength / 4;
            }
            else
            {
                moveDir.y = previousJumpHeight;
                moveDir.y -= (gravity / 4) * Time.deltaTime;
            }

            wallRunTime += Time.deltaTime;
            //Debug.Log("Wall run time: " + wallRunTime);

            if (wallRunTime > wallRunMaxTime)
            {
                canWallRun = false;
                Debug.Log ("Max wall run time hit.");
            }

        }
        else
        {
            stopWallRun();
        }
    }



    void stopWallRun()
    {
        canWallRun = false; // will be reseted once the player hit the floor 
        wallRunTime = 0.0f;
        playerState = PlayerState.jumping;
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



    void updateAttacking()
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



    public static string getPlayerState()
    {
        return playerState.ToString();
    }



    public static string getSpeed()
    {
        return speed.ToString();
    }
}
