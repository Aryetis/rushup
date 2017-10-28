using System.Collections;
using UnityStandardAssets.Characters.FirstPerson; // only for MouseLook
using UnityStandardAssets.CrossPlatformInput;     // TODO : check if controls are working on Android ... it shoulds otherwise there's close to no point using CrossPlatformInput
using UnityEngine;
using UnityEditor;

/* TODO (after the project is "done") list :
 *      Put different factor for walking in reverse
 *      rename runningMomentum as it's used for wallrun and slide too (for smoother transition between each state / movements)
 *      same for runningToJumpingImpulse used for wallkick
 */

/*
 * ReadOnlyAttribute class shamelessely stolen from It3ration & scottmontgomerie: 
 * http://answers.unity3d.com/questions/489942/how-to-make-a-readonly-property-in-inspector.html
*  TODO : Beautify the whole inspector for this class using [CustomEditor(typeof(parkourFPSController))} and OnInspectorGUI()
 */
public class ReadOnlyAttribute : PropertyAttribute
{}
[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property,
        GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position,
        SerializedProperty property,
        GUIContent label)
    {
        GUI.enabled = false;
        EditorGUI.PropertyField(position, property, label, true);
        GUI.enabled = true;
    }
}

public class parkourFPSController : MonoBehaviour
{
Ray debugRay;
    /* Player's state variable*/
    private enum PlayerState {running, jumping, wallrunning, wallclimbing, sliding, edging, pushing, attacking, ejecting}; // Describing current state of the player : edging <=> grabed the edge of a cliff; pushing <=> pushing up from edging state; etc
    private bool canWallRun = false;                                                // Describe if player is in a state that allows for him to start wallrunning (can't wallrun during a slide, duh)
    private bool canWallClimb = false;                                              // Describe if player is in a state that allows for him to start wallclimbing 
    private bool canAttack = false;                                                 // Describe if player is in a state that allows for him to start attacking
    private bool canSlide = false;                                                  // Describe if player is in a state that allows for him to start sliding

    [Header("Global Variables")]
    [SerializeField] private float gravity = 20f;                                   // Gravity applied to the vector on the Y axis
    [SerializeField] private float jumpStrength = 20f;                              // Impulse given at the start of a jump
    [SerializeField] private float slopeClimbingPermissionStep = 0.25f;             // Height shift allowed on Y axis between two frames to considere if the player is grounded or not 
    [SerializeField] private float maxNominalSpeed = 50f;                          // Player's max speed without any killSpeedBonus
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
    private float cooldownLock;                                                     // player just wallkicked => forbid him to wallrun till ejectTime > 0
    private float isWallKicking;

    [Space(10)]
    [Header("Wallrun State Variables")]
    [SerializeField] float wallrunMaxSpeed = 50f;                                   // Max Speed during wallrun (speed will increase over time)
    [SerializeField] private float wallrunningGravityFactor = 2f;                   // The bigger => the less gravity will impact player during wallrun
    [Range(0.0f, 0.1f)] [SerializeField] private float wallrunningDecelerationFactor = 0.025f;         // Player's momentum will decrease by deltaTime*wallrunningDecelerationFactor at each frame
    [SerializeField] private float wallrunCoolDown = 0.25f;                         // Prevent player from hitting too much wallrun in a row
    [SerializeField] private float wallRunMinSpeed = 20f;                           // If player goes under wallRunMinSpeed he will fall from the wall 
    [SerializeField] private float wallkickHeight = 20f;                            // A wallkick (jump when wallruning) gives the player a boost on the Y axis of wallkickHeight 
    [SerializeField] private float wallrunEnterAngle = 45f;                         // if the player jump on the wall with an angle less than wallrunEnterAngle, he will wallrun it
    [SerializeField] private float wallrunExitAngle = 45f;                          // if the player jump on the wall with an angle less than wallrunEnterAngle, he will wallrun it
    [SerializeField] private float wallrunExitAnimationTime = 0.5f;                 // Time during wich the camera will slerp to the wallkick destination, PLAYERS INPUTS WON'T MATTER during the animation
    private Quaternion wallKickRotation;                                            // Describe the camera rotation/angle desired at the end of the wallkick animation
    private float isWallkicking;                                                    // Since how long the player has been wallkicking ?
    private RaycastHit wallHit;                                                     // Target the wall the player is/can currently wallruning on
    private float wallRunTime = 0.0f;                                               // How long player has been wallrunning
    private GameObject previousWallWallran = null;                                  // keep in memory the last wall that has been wallran to prevent player from wallrunning on it two times in a row

    [Space(10)]
    [Header("Wallclimb State Variables")]
    [SerializeField] private float snapCameraSpeed = 3f;                            // The smallest the faster the camera will snap on its wallrun position
    [SerializeField] private float wallclimbImpulse = 50f;                          // TODO
    [SerializeField] private float initialVerticalImpulse = 10f;    
    [ReadOnly] public string wallClimbAngle = "(90-wallrunEnterAngle)*2";           // Just indicating to LDs that wallClimbAngle is basically whatever angle is remaining 

    private float wallclimbingTime = 0f;                                            // How long the player has been wallclimbing
    private float ongoingSnapCameraTime = 0f;
    bool rightImpact;
    bool leftImpact;

    [Space(10)]
    [Header("Sliding State Variables")]
    [SerializeField] private float slidingMinSpeed = 10f;                           // TODO

    [Space(10)]
    [Header("Attacking State Variables")]
    [SerializeField] private float attackingImpulse = 50f;                          // TODO
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

    void OnCollisionEnter(Collision col)
    {
        Debug.Log("collision detected");   
    }
	
	// Update is called once per frame
	void Update ()
    {
Debug.DrawRay(debugRay.origin, debugRay.direction*10);
        /*** CAPTURING INPUTS MOVED INSIDE FixedUpdate() ***/

        /*** UPDATING speed (for UI and various update[State]() ***/
        updateSpeed();

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
            case PlayerState.wallrunning:
            {
                updateWallrunning();
                break; 
            }
            case PlayerState.wallclimbing:
            {
                updateWallclimbing();
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
        /*** CAPTURING INPUTS ***/
        // Doing this inside FixedUpdate to make sure we didn't miss any inputs in case of lag
        inputHorizontal = CrossPlatformInputManager.GetAxis("Horizontal"); 
        inputVertical = CrossPlatformInputManager.GetAxis("Vertical");
        inputJump = CrossPlatformInputManager.GetButtonDown("Jump"); // Only capture Down Event for jump to avoid situation like : 
                                                                     // Player running right next to a wall, hit jump => wallrun and immediately after that wallkick
    }
        


    void updateRunning()
    {
        // Update Camera look and freedom according to playerState
        updateCamera();

        // Reset previousWallWallran value 
        previousWallWallran = null;
         
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
        // Check if we're hitting the floor
        if(grounded) 
        {
            playerState = PlayerState.running;
            // reset airBorne specific global values
            previousAirControlDir = Vector3.zero;
            return;
        }

        if(isWallkicking > 0) // Check if player is wallkicking / Iterage over animation and ignore inputs
        {
            // Turn Camera 
            transform.rotation = Quaternion.Slerp(transform.rotation, wallKickRotation, 3.5f * Time.deltaTime);

            // Reset mouseLook internals quaternion has we indirectly messed our own but not its
            mouseLook.Init(transform, camera.transform);

                // Affect Translation
                runningToJumpingImpulse = Vector3.zero;
//            moveDir = runningToJumpingImpulse;
//            moveDir.y = wallkickHeizht;
                moveDir = transform.forward * 100f;
                controller.Move(Vector3.zero);

            // Decrease isWallkicking timer
            isWallkicking -= Time.deltaTime;

Debug.Log("transform.forward : " + transform.forward);



            if(isWallkicking <= 0)
            {

                runningToJumpingImpulse = Vector3.zero;
//                Debug.Log("hello");
                    previousAirControlDir = transform.forward * 100f;
Debug.Log("transform.forward : " + transform.forward);
Debug.Log("-------------------------------------------------");
//                moveDir = runningToJumpingImpulse;
//                moveDir.y = wallkickHeight; 
//                runningToJumpingImpulse = Vector3.zero;
                return;
            }

            // DO NOT proceed to continue normal behavior as wallckick state is not user inputs based
            return;
        }
        else // Update Camera look and freedom according to playerState
        {
            updateCamera();
        }

        // Update cooldownLock
        if (cooldownLock > 0)
        {
            cooldownLock -= Time.deltaTime;
            return;
        }

        // Do a wall run check and change state if successful.
        wallHit = checkAccessibleWallrun();
        if (wallHit.collider != null && cooldownLock <=0 && wallHit.collider.gameObject != previousWallWallran)
        {
            playerState = PlayerState.wallrunning;
            previousWallWallran = wallHit.collider.gameObject;
            return;
        }

        // Do a wall climb check and I need to clean up these hits.
//        RaycastHit wallClimbHit = DoWallClimbCheck(new Ray(transform.position, 
//            transform.TransformDirection(Vector3.forward).normalized * 0.1f));
//        if (wallClimbHit.collider != null)
//        {
//            playerState = PlayerState.wallclimbing;
//            return;
//        }

        // Set moveDir as impulse given on ground (will be countered as time goes by, by the airControlDir vector)
        moveDir.x = runningToJumpingImpulse.x;
        moveDir.z = runningToJumpingImpulse.z;

        // get direction Vector3 from input
        Vector3 airControlDir = new Vector3(inputHorizontal, 0f, inputVertical);
        airControlDir = transform.TransformDirection(airControlDir);
        airControlDir.Normalize();
       
        // GLUT : hardcoding a airControlFactor to decide how much control the player has over his initial impulse, because lack of time to test (see github for previous attempt, it worked but was pretty unplayable)
        airControlDir.x = previousAirControlDir.x + airControlDir.x*airControlFactor ;
        airControlDir.z = previousAirControlDir.z + airControlDir.z*airControlFactor ;

        //Combine moveDir and airControlDir according to airInertiaFactor factor
        moveDir = moveDir + airControlDir;

        // GLUT Patching speed so it doesn't go above maxSpeed, shouldn't happen but unity magic and probably because speed depends of x AND y and I'm using on x and y independantly $
        if (moveDir.magnitude >= maxNominalSpeed)
        {
            moveDir = moveDir.normalized * maxNominalSpeed;
        }

        // Check that player isn't bashing its head on the ceiling
        RaycastHit ceilingHit;
        if (Physics.SphereCast(transform.position, controller.radius, Vector3.up, out ceilingHit,
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
        // WARNING AT THE MOMENT SOME WALLRUN MIGHT SEEM TO BUG BECAUSE
        // even if this check returns something correct, when the player will start running he will not have enough speed along the wall's axis to be under the wallrunMinSpeed
        // => It's not a bug it's a feature

        Ray rayRight = new Ray(transform.position, transform.TransformDirection(Vector3.right));
        Ray rayLeft = new Ray(transform.position, transform.TransformDirection(Vector3.left));

        RaycastHit wallImpactRight;
        RaycastHit wallImpactLeft;

        rightImpact = Physics.Raycast(rayRight.origin, rayRight.direction, out wallImpactRight, controller.radius+1f);
        leftImpact = Physics.Raycast(rayLeft.origin, rayLeft.direction, out wallImpactLeft, controller.radius+1f);

        float rightAngle = Vector3.Angle(transform.TransformDirection(Vector3.forward), wallImpactRight.normal); // Angle(Forward, innerNormal) => if (Angle == 90) <=> Player looking along the wall) 
        float leftAngle = Vector3.Angle(transform.TransformDirection(Vector3.forward), wallImpactLeft.normal);

        if (rightImpact && rightAngle > 90 && rightAngle < 90+wallrunEnterAngle)
        { // check if impact && correct side of the wall && angle not too stiff
            return wallImpactRight;
        }
        else if (leftImpact && leftAngle > 90 && leftAngle < 90+wallrunEnterAngle)
        {
            wallImpactLeft.normal *= -1; // for crossProduct
            return wallImpactLeft;
        }
        else
        {
            // Just return something empty, because nothing is good for a wall run
            return new RaycastHit();
        }
    }



    void updateWallrunning()
    {
        // Camera locked during wallrun => no updateCamera()

        if (!controller.isGrounded && canWallRun)
        {
            // update wallHit, check that we're still riding the wall
            wallHit = checkAccessibleWallrun();
            if (wallHit.collider == null) // Reached end of the wall
            {
                stopWallRun();
                return;
            }

            // Make sure we set the state to wallrunning
            playerState = PlayerState.wallrunning;

            // get WallRun direction
            Vector3 crossProduct = Vector3.Cross(Vector3.up, wallHit.normal);

            // slerp camera on place 
            Quaternion lookDirection = Quaternion.LookRotation(crossProduct);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookDirection, 3.5f * Time.deltaTime);

            // Decrement momentum 
            runningMomentum -= wallrunningDecelerationFactor*Time.deltaTime;
            if (runningMomentum < 0)
            {
                runningMomentum = 0;
            }

            // Actualize moveDir
            moveDir = crossProduct;
            moveDir.Normalize();
            moveDir *= wallRunMinSpeed + ( (wallrunMaxSpeed-wallRunMinSpeed) * (runningMomentum / runningRampUpTime));

            // Set the vertical curve of the wallrun
            moveDir.y = prevMoveDir.y;
            moveDir.y -= (gravity / wallrunningGravityFactor) * Time.deltaTime;

            // update wallRunTime
            wallRunTime += Time.deltaTime;

            if (speed < wallRunMinSpeed || inputVertical <= 0)
            {
                stopWallRun(); // user decided to stop wallrunning or ran out of speed => don't allow him to wallrun again
            }

            if (inputJump == true) // player requested a wallkick
            {
                // Apply wallkick 
                runningToJumpingImpulse = Vector3.zero;                         // reset runningToJumpingImpulse in case player has been chaining the wallkicks
                moveDir = Vector3.zero;                                         // and moveDir too because it's affected by previous runningToJumpingImpulse
                float wallrunExitAngleAdapated = (leftImpact) ? wallrunExitAngle : -1 *wallrunExitAngle;             // Get direction angle from wall 
                Quaternion originalRotation = transform.rotation;                                                    // store current rotation
                wallKickRotation = Quaternion.AngleAxis(wallrunExitAngleAdapated, Vector3.up) * transform.rotation ; // compute wallkick quaternion rotation and store it
                transform.rotation = wallKickRotation ;                                                              // apply it
                runningToJumpingImpulse = transform.forward * (speed/wallrunMaxSpeed) * speed;                       // compute the wallkick vector
                runningToJumpingImpulse.y = wallkickHeight;                                                          // ....
                transform.rotation = originalRotation;                                                               // restore player's original quaternion rotation as we want a smooth rotation
                                                                                                                     // will be done during updateJumping()

                // Set up the wallkick animation timer for updateJumping()
                isWallkicking = wallrunExitAnimationTime;

                stopWallRun();
            }
        }
        else
        {
            stopWallRun();
        }
    }



    void stopWallRun()
    {
        // Reset mouseLook internals quaternion has we indirectly messed our own but not its
        mouseLook.Init(transform, camera.transform);

        // Reset important wallkick specific variables
        cooldownLock = wallrunCoolDown;
        wallRunTime = 0.0f;

        // Change playerState
        playerState = PlayerState.jumping;
    }


    void updateWallclimbing()
    {
//        Debug.Log("updateWallclimbing()");
//        // Update Camera look and freedom according to playerState
//        updateCamera();
//
//        bool moving = (inputHorizontal!=0 || inputVertical!=0) ? true : false;
//        if (!moving)
//        {
//            wallclimbingTime = 0.0f;
//            if (playerState == PlayerState.wallclimbing)
//                canWallClimb = false;
//            playerState = PlayerState.jumping;
//            return;
//        }
//
//        Ray forwardRay = new Ray(transform.position, transform.TransformDirection(Vector3.forward).normalized);
//        forwardRay.direction *= 0.1f;
//
//        RaycastHit hit = DoWallClimbCheck(forwardRay);
//        if (canWallClimb && hit.collider != null && 
//            wallclimbingTime < 0.5f && Vector3.Angle(forwardRay.direction, hit.normal) > 165)
//        {
//
//            wallclimbingTime += Time.deltaTime;
//
//            // Look up. Disabled for now.
////            Quaternion lookDirection = Quaternion.LookRotation(hit.normal * -1);
////            camera.transform.rotation = Quaternion.Slerp(transform.rotation, lookDirection, 3.5f * Time.deltaTime);
//            camera.transform.Rotate(-85f * (wallclimbingTime / 0.5f), 0f, 0f); //            ^ Magic number for tweaking look time
//
//            // Move up.
//            moveDir += transform.TransformDirection(Vector3.up);
//            moveDir.Normalize();
//            moveDir *= runningMinSpeed;
//
//            playerState = PlayerState.wallclimbing;
//        }
//        else 
//        {
//            if (playerState == PlayerState.wallclimbing)
//                canWallClimb = false;
//            wallclimbingTime = 0f;
//            playerState = PlayerState.jumping;
//        }
    }

    RaycastHit DoWallClimbCheck(Ray forwardRay)
    {
        RaycastHit hit;

        Physics.Raycast(forwardRay.origin, forwardRay.direction, out hit, 1f);

        return hit;

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



    private void updateSpeed()
    {
        speed = (float) Mathf.Sqrt(controller.velocity.x * controller.velocity.x +
            controller.velocity.z * controller.velocity.z);
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
