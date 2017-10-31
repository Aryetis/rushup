using System.Collections;
using UnityStandardAssets.Characters.FirstPerson; // only for MouseLook
using UnityStandardAssets.CrossPlatformInput;
using UnityEngine;

/* TODO (after the project is "done") list :
 *      _ Put different factor for walking in reverse
 *      _ rename runningMomentum as it's used for wallrun and slide too (for smoother transition between each state / movements)
 *        same for runningToJumpingImpulse used for wallkick
 *      _ Beautify the whole inspector for this class using [CustomEditor(typeof(parkourFPSController))} and OnInspectorGUI()
 *      _ rework the way the player gain momentum in air as for now he can build up a ton of momentum even tho he is flying toward a wall, use dowallclimbcheck() ? crossProduct ? just make him fall flat as he can pretty much wallrun or wallclimb whatever its speed position and moon position
 *      _ make the height of wallclimb depends of the player's speed when he hits the wall
 *      _ set a minimal wall height for player to be able to wallrun on it
 *      _ probably dozens of little things I can't think about right now
 *      _ check if controls are working on Android ... it shoulds otherwise there's close to no point using CrossPlatformInput
 *      _ try to use controller.isGrounded to update grounded variable every fixedUpdate();
 *      _ find out why wallclimbYMomentum is it ticking slower than it should
 * 
 * TODO (before the end of the project) list :
 *      _ find a more elegant solution to the immediate jump => wall => jump problem
 *      _ smooth camera movements during wallclimb & wallclimbturn
 *      _ move updates() to fixedUpdate()
 */

public class parkourFPSController : MonoBehaviour
{
Ray debugRay, debugRay2;
    /*Player's state variable*/
    private enum PlayerState {running, jumping, wallrunning, wallclimbing, sliding, edging, pushing, attacking, ejecting}; // Describing current state of the player : edging <=> grabed the edge of a cliff; pushing <=> pushing up from edging state; etc . jumping can be used pretty much as the default state
    private bool canWallRun = false;                                                // Describe if player is in a state that allows for him to start wallrunning (can't wallrun during a slide, duh)
    private bool canWallClimb = false;                                              // Describe if player is in a state that allows for him to start wallclimbing 

    [Header("Global Variables")]
    [SerializeField]
    private float gravity = 20f;                                                    // Gravity applied to the vector on the Y axis
    [SerializeField] private float jumpStrength = 20f;                              // Impulse given at the start of a jump
    [SerializeField] private float slopeClimbingPermissionStep = 0.25f;             // Height shift allowed on Y axis between two frames to considere if the player is grounded or not 
    [SerializeField] private float maxNominalSpeed = 50f;                           // Player's max speed without any killSpeedBonus
    private Camera camera = null;                                                   // Player's Camera
    private CharacterController controller;                                         // Player's controller
    private float inputHorizontal;                                                  // [-1;1] horizontal input for strafes (smoothed)
    private float inputVertical;                                                    // [-1;1] horizontal input for running/reversing (smoothed)
    private bool inputJump;                                                         // is jump key pressed ?
    private bool inputSlide;                                                        // is sllding key pressed ?
    private bool inputAttacking;                                                    // Is attacking key pressed ?
    private static PlayerState playerState = PlayerState.running;                   // Describe current player state
    private static float speed;                                                     // Player speed along x and z axis => NOT taking into account Y axis (no falling speed displayed)
    private Vector3 moveDir=Vector3.zero;                                           // Current frame player's movement vector
    private Vector3 prevMoveDir=Vector3.zero;                                       // Previous frame player's movement
    private bool grounded;      // Not using controller.isGrounded value because result is based on the PREVIOUS MOVE state
                                // Resulting in unreliable state when running up on slanted floors
                                // ( https://forum.unity.com/threads/charactercontroller-isgrounded-returning-unreliable-state.494786/ ) 
    private GameObject groundFloor = null;                                          // Gameobject corresponding to what the player is walking on
    private bool hitByBullet = false;
    private Vector3 collisionDirection;
    private float bulletHitMomentum = 0f;
    private Vector3 spawnTransformPosition;
    private Transform spawnCameraTransform;

    [Space(10)]
    [Header("Running State Variables")]
    [SerializeField]
    private float runningMinSpeed = 10f;                                            // Player will start running at this speed
    [SerializeField] private float runningRampUpTime = 0.2f;                        // Time in seconds for player to reach maxNominalSpeed (in seconds)
    [Range(0.0f, 1.0f)] [SerializeField] private float runningInertiaFactor = 0.9f; // [0;1] the bigger the less current input will impact the outcome / the more slippery the player wil be
    [SerializeField] private float runningDecelerationFactor = 0.5f;                // will decelerate at "runningDecelerationFactor" the speed it accelerates
    [SerializeField] private float jumpHeightSpeedFactor = 1.5f;                    // At full speed player will jump at jumpHeightSpeedFactor * the height of a basic jump
    private float runningMomentum = 0f;                                             // [0;runningRampUpTime] "porcentage" of the current speed wihtout acknoledging minSpeed

    [Space(10)]
    [Header("Airborne State Variables")]
    [SerializeField]
private float airControlFactor = 2.0f;                                              // Determine how much the inputs performed by the player while airborne impact his direction
    private Vector3 runningToJumpingImpulse = Vector3.zero;                         // moveDir vector at the moment of the jump, used to kickstart the direction of the jump
    private Vector3 previousAirControlDir;                                          // direction of the airborne player at the previous frame
    private float wallrunCooldownLock;                                              // player just wallkicked => forbid him to wallrun till ejectTime > 0
    private float wallclimbCooldownLock;

    [Space(10)]
    [Header("Wallrun State Variables")]
    [SerializeField]
    float wallrunMaxSpeed = 50f;                                                    // Max Speed during wallrun (speed will increase over time)
    [SerializeField] private float wallrunningGravityFactor = 2f;                   // The bigger => the less gravity will impact player during wallrun
    [Range(0.0f, 0.1f)] [SerializeField] private float wallrunningDecelerationFactor = 0.025f; // Player's momentum will decrease by deltaTime*wallrunningDecelerationFactor at each frame
    [SerializeField] private float wallrunCoolDown = 0.25f;                         // In seconds, Prevent player from hitting too much wallrun in a row
    [SerializeField] private float wallRunMinSpeed = 20f;                           // If player goes under wallRunMinSpeed he will fall from the wall 
    [SerializeField] private float wallkickHeight = 20f;                            // A wallkick (jump when wallruning) gives the player a boost on the Y axis of wallkickHeight 
    [SerializeField] private float wallrunEnterAngle = 45f;                         // if the player jump on the wall with an angle less than wallrunEnterAngle, he will wallrun it
    [SerializeField] private float wallrunExitAngle = 45f;                          // if the player jump on the wall with an angle less than wallrunEnterAngle, he will wallrun it
    [SerializeField] private float wallrunExitAnimationTime = 0.5f;                 // Time during wich the camera will slerp to the wallkick destination, PLAYERS INPUTS WON'T MATTER during the animation
    private float speedAtWallkick;                                                  // Player's wallrun speed at the moment he triggered the wallkick animation, used to determine wallkick speed
    private Quaternion wallKickRotation;                                            // Describe the camera rotation/angle desired at the end of the wallkick animation
    private float isWallkicking;                                                    // Since how long the player has been wallkicking ?
    private RaycastHit wallHit;                                                     // Target the wall the player is/can currently wallruning on
    private float wallRunTime = 0.0f;                                               // How long player has been wallrunning
    private GameObject previousWallWalltricked = null;                              // keep in memory the last wall that has been wallran to prevent player from wallrunning on it two times in a row
    bool rightImpact;                                                               // Can player wallrun on the right ?
    bool leftImpact;                                                                // Can player wallrun on the left ?

    [Space(10)]
    [Header("Wallclimb State Variables")]
    [SerializeField] private float wallclimbExitAngle = 60f;                        // "Y axis Angle" the player will exit the wallclimb if he decides to jump 
    [SerializeField] private float wallclimbExitForce = 30f;                        // Impulse force given to the player during a wallclimbturn
    [SerializeField] private float wallclimbDecelerationFactor = 20f;               // Loose wallclimbDecelerationFactor from wallclimbYMomentum (set by wallclimbInitialYMomentum) every second ish .... 
    [SerializeField] private float wallclimbCoolDown = 0.25f;                       // In seconds, Prevent player from wallclimbing too much in a row
    [SerializeField] private float wallturnjumpingExitAnimationTime = 0.5f;         // Time during wich the camera pans out 180° => player inputs will be unresponsive !!!
    [SerializeField] private float wallclimbInitialYMomentum = 20f;                 // When player hits the wall he will immediately start climbing the wall at wallclimbInitialMomentum
    [SerializeField] private float wallclimbMaxSpeed = 10f;                         // Speed the player will be wallclimbing at, decrease over time according to wallclimbDecelerationFactor
    private Quaternion wallturnjumpRotation;                                        // Store the rotation needed to turn the camera during a wallturnjump sequence
    private Vector3 wallclimbturnExitVector;                                        // Direction the player will go when executing a wallclimbturn
    public string _wallClimbAngle_ = "whatever angle is remaining from wallrunEnterAngle"; // Just indicating to LDs that wallClimbAngle is basically whatever angle is remaining 
    private float wallclimbingTime = 0f;                                            // How long the player has been wallclimbing
    private float lastYPosition;                                                    // Vertical position on previous frame
    private float isWallTurnJumping;                                                // Since how long the player has been wallkicking ?
    private float wallclimbYMomentum;                                               // Current Y Momentum during a wallclimb
    private Quaternion wallturnJumpRotation;                                        // Describe the camera rotation/angle desired at the end of the wallkick animation

    [Space(10)]
    [Header("Sliding State Variables")]
    [SerializeField] private float crouchingHeight = 0.3f;
    [SerializeField] private float slidingDecelerationFactor = 0.5f;                // will decelerate at "runningDecelerationFactor" the speed it accelerates
    private float originalHeight;

    [Space(10)]
    [Header("Attacking State Variables")]
    [SerializeField]
    private float attackingImpulse = 50f;
    // TODO
    [SerializeField] private float killSpeedBonus = 5f;
    [SerializeField] private float hoomingRange = 20f;
    private Vector3 attackDirection;
    [SerializeField] private float hoomingAttackMomentum = 1f;
    private float hoomingAttackCurrentMomentum = 0f;

    [Space(10)]
    [Header("Mouse Properties")]
    [SerializeField]
    public MouseLook mouseLook = null;                             // Standard Asset script taking care of moving the camera according to mouse inputs
                                                                   // public because UI must unlock cursor to allow player to click on buttons
                                                                   // Use this for initialization
    void Start()
    {
        camera = Camera.main;
        controller = GetComponent<CharacterController>();
        controller.detectCollisions = true;
        mouseLook.Init(transform , camera.transform);

        originalHeight = controller.height;
        // Teleport Player to the ground to be sure of its playerState at startup
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, 1000))
        {
            transform.position = new Vector3(hit.point.x, hit.point.y + controller.height, hit.point.z);
            grounded = true;
        }
        else
        {
            Debug.LogError("Please put the Player prefab above a floor/closer to it");
        }
        spawnTransformPosition = transform.position;
        spawnCameraTransform = camera.transform;

    }


	
	// Update is called once per frame
	void Update ()
    {
        /*** LOCK mouseLook TO PREVENT UNWANTED INPUTS ***/
        mouseLook.UpdateCursorLock();

        inputHorizontal = CrossPlatformInputManager.GetAxis("Horizontal");
        inputVertical = CrossPlatformInputManager.GetAxis("Vertical");
        inputJump = CrossPlatformInputManager.GetButton("Jump"); // Only capture Down Event for jump to avoid situation like : 
        inputSlide = CrossPlatformInputManager.GetButton("Slide");
        inputAttacking = CrossPlatformInputManager.GetButton("Attack");
    }



    // FixedUpdate is called once per physic cycle
    void FixedUpdate()
    {
        Debug.DrawRay(debugRay.origin, debugRay.direction * 100, Color.red);
        Debug.DrawRay(debugRay2.origin, debugRay2.direction * 100, Color.blue);
            
        /*** UPDATING speed (for UI and various update[State]() ***/
        updateSpeed();

        /*** UPDATING grounded STATE ***/
        RaycastHit hit;
        grounded = Physics.Raycast(controller.transform.position, Vector3.down, out hit, (controller.height / 2f) + controller.skinWidth + slopeClimbingPermissionStep);
        if(grounded)
            groundFloor = hit.collider.gameObject;

        if (inputAttacking && playerState != PlayerState.attacking)
        {
            RaycastHit attackRay;
            if (Physics.Raycast(controller.transform.position, controller.transform.forward, out attackRay, hoomingRange) && attackRay.collider.CompareTag("Enemy"))
            {
                hoomingAttackCurrentMomentum = 0f;
                playerState = PlayerState.attacking;
                attackDirection = controller.transform.forward;
                attackDirection.Normalize();
            }
        }

        /*** CALCULATING FORCE FROM INPUTS & STATE***/
        switch (playerState)
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

        /*** Manage hit by bullet ***/
        if (hitByBullet)
        {
            updateBulletHit();
        }

        /*** APPLYING moveDir FORCE ***/
        controller.Move(moveDir * Time.fixedDeltaTime);

        /*** CONSERVING DATA FOR FUTURE REFERENCES ***/
        prevMoveDir = moveDir;

    }



    void OnCollisionEnter(Collision col)
    {
        if (col.gameObject.CompareTag("Projectile"))
        {
            collisionDirection = col.impulse * -1;
            collisionDirection.Normalize();
            collisionDirection.y = 0;

            hitByBullet = true;
            bulletHitMomentum = 0f;

            Destroy(col.gameObject);
        }
    }



    void OnTriggerEnter(Collider col)
    {
        if (col.CompareTag("DeathZone"))
        {   // "kill" the player and respawn him to the last validated checkpoint
            // reset all variables
            moveDir = Vector3.zero;
            prevMoveDir = Vector3.zero;
            previousAirControlDir = Vector3.zero;
            previousWallWalltricked = null;
            playerState = PlayerState.running;

            // teleport player
            GameObject restartCheckpoint = CheckpointBehavior.getRestartCheckpoint();
            if (restartCheckpoint == null)
            {   // if no checkpoint has been reached => respawn player to his start location
                transform.position = spawnTransformPosition;
                mouseLook.Init(transform, spawnCameraTransform);
            }
            else
            {   // else respawn him to his last validated checkpoint
                transform.position = restartCheckpoint.transform.position;
                mouseLook.Init(transform, restartCheckpoint.transform);
            }
        }
        else if (col.gameObject.CompareTag("Enemy"))
        {
            playerState = PlayerState.jumping;
            Destroy(col.gameObject);
        }
    }



    void updateBulletHit()
    {
        if (bulletHitMomentum < 1.0)
        {
            moveDir += collisionDirection;
            bulletHitMomentum += Time.fixedDeltaTime;
        }
        else
        {
            hitByBullet = false;
        }
    }



    void updateRunning()
    {
        // Update Camera look and freedom according to playerState
        updateCamera();

        // Reset previousWallWallran & previousWallWallclimbed value 

         
        if(grounded)
        {
            // Make sure that our state is set (in case of falling of a clif => no jump but still been airborne for a while)
            playerState = PlayerState.running;

            // Turn on possible moves flags
            canWallRun = true;
            canWallClimb = true;
            if (groundFloor != previousWallWalltricked)
                previousWallWalltricked = null;

            // Reset aircontrol
            previousAirControlDir = Vector3.zero;

            // get direction Vector3 from input
            moveDir = new Vector3(inputHorizontal, 0f, inputVertical);
            moveDir = transform.TransformDirection(moveDir); // Align moveDir vector with localTransform/camera forward vector
            moveDir.Normalize();

            // Correct moveDir according to the floor's slant
            RaycastHit hitInfoDown;
            if (Physics.SphereCast(transform.position, controller.radius, Vector3.down, out hitInfoDown,
               controller.height / 2f, Physics.AllLayers, QueryTriggerInteraction.Ignore))

            {
                moveDir = Vector3.ProjectOnPlane(moveDir, hitInfoDown.normal).normalized;
            }

            // Build up the "momementum" as long as player is pressing "moving forward/strafing"
            bool moving = (inputHorizontal != 0 || inputVertical != 0) ? true : false;
            if (moving && runningMomentum <= runningRampUpTime)
            {
                runningMomentum += Time.fixedDeltaTime; // build up "temporal" momentum 
                if (runningMomentum > runningRampUpTime)
                {  // till we reach rampUpTime
                    runningMomentum = runningRampUpTime;
                }
            }
            else // If Player is letting go of the "forward" key, reduce "momentum"
            {
                runningMomentum -= runningDecelerationFactor * Time.fixedDeltaTime;
                if (runningMomentum < 0)
                {
                    runningMomentum = 0;
                }
            }

            // Compute moveDir according to minSpeed, maxNominalSpeed, deltaTime, inertiaFactor, etc
            if (speed <= 0.5)
            { // if player's speed is below minSpeed => kickstart player to runningMinSpeed// no need to check if player is moving as in this case moveDir will already be at 0
                moveDir *= runningMinSpeed;
            }
            else if (!moving && speed <= runningMinSpeed)
            { // if player is decelerating and going below runningMinSpeed => Stop him immediately
                moveDir = Vector3.zero;
            }
            else // Player is moving beyond runningMinSpeed
            {

                Vector3 foo = moveDir * (runningMinSpeed + ((maxNominalSpeed - runningMinSpeed) * (runningMomentum / runningRampUpTime))); // Calculate current inputs impact on moveDir
                moveDir = foo * (1 - runningInertiaFactor) + prevMoveDir * runningInertiaFactor; // mix current inputs vector and previous one according to runningInertiaFactor
            }

            // Jump Requested 
            if (inputJump)
            {
                runningToJumpingImpulse = moveDir;
                playerState = PlayerState.jumping;
                moveDir.y = jumpStrength + jumpStrength * (speed / maxNominalSpeed) * (jumpHeightSpeedFactor - 1);
                // "standard jump height" + "speed dependent height jump" * (jumpHeightSpeedFactor-1)
            }

            // Slide request
            if (inputSlide && !inputJump)
            {
                playerState = PlayerState.sliding;
                controller.height = crouchingHeight;
                controller.center = new Vector3(controller.center.x,
                    controller.center.y * crouchingHeight / originalHeight,
                    controller.center.z);
            }
        }
        else
        { // Player is running from an edge => change state to "jumping" and override current update()'s cycle result
            runningToJumpingImpulse = moveDir;
            playerState = PlayerState.jumping;
        }

        // Applying gravity
        moveDir.y -= gravity * Time.fixedDeltaTime;
    }



    void updateJumping()
    {
        // Check if we're hitting the floor
        if (grounded)
        {
            playerState = PlayerState.running;
            // reset airBorne specific global values
            previousAirControlDir = Vector3.zero;
            return;
        }

        if(isWallkicking > 0) // Check if player is wallkicking / Iterate over animation and ignore inputs
        {

            // Turn Camera 
            transform.rotation = Quaternion.Slerp(transform.rotation, wallKickRotation, 3.5f * Time.fixedDeltaTime);

            // Reset mouseLook internals quaternion has we indirectly messed our own but not its
            mouseLook.Init(transform, camera.transform);

            // Affect Translation
            moveDir.x = transform.forward.x * speedAtWallkick;
            moveDir.z = transform.forward.z * speedAtWallkick;
            if(isWallkicking == wallrunExitAnimationTime) // first frame of kickglitch => give the player some boost on the Y
                moveDir.y += wallkickHeight;

            // Decrease isWallkicking timer
            isWallkicking -= Time.fixedDeltaTime;

            // Set basic vector for airControlDir for when the wallkick animation will be over (at every frame of the wallkick even tho it will be used only at the end, because why waste time on an if) 
            // this way the player's direction will be the same at the end of the wallkick->start of the fall/jumping state
            previousAirControlDir = transform.forward * speedAtWallkick;

            // DO NOT proceed to continue normal behavior as wallckick state is not user inputs based
            return;
        }
        else
        if(isWallTurnJumping > 0) // Check if player is wallturnjumping / Iterate over animation and ignore inputs
        {
            // Turn Camera 
            transform.rotation = Quaternion.Slerp(transform.rotation, wallturnJumpRotation, 350f * Time.fixedDeltaTime);
            //TODO SMOOTH THIS !

            // Reset mouseLook internals quaternion has we indirectly messed our own but not its
            mouseLook.Init(transform, camera.transform);

            moveDir = wallclimbturnExitVector;
            previousAirControlDir = moveDir;

            // Decrease isWallkicking timer
            isWallTurnJumping -= Time.fixedDeltaTime;

            // DO NOT proceed to continue normal behavior as wallckick state is not user inputs based
            return;
        }
        else // Player is "just" jumping, give him freedom to move the camera
        {
            updateCamera();
        }

        // Update wallrunCooldownLock
        if (wallrunCooldownLock > 0)
        {
            wallrunCooldownLock -= Time.fixedDeltaTime;
        }

        // Update wallclimbCooldownLock
        if (wallclimbCooldownLock > 0)
        {
            wallclimbCooldownLock -= Time.fixedDeltaTime;
        }

        // Do a wall run check and change state if successful.
        wallHit = checkAccessibleWall();
        if (wallHit.collider != null && (leftImpact||rightImpact)  && wallrunCooldownLock <= 0 && wallHit.collider.gameObject != previousWallWalltricked)
        {
            playerState = PlayerState.wallrunning;
            previousWallWalltricked = wallHit.collider.gameObject;
            return;
        }

        // Do a wall climb check and change state
        RaycastHit hit  = walclimbCheck(new Ray(transform.position, transform.TransformDirection(Vector3.forward).normalized * 0.1f));
        if(hit.collider != null && wallclimbCooldownLock <= 0 && hit.collider.gameObject != previousWallWalltricked)
        {
            playerState = PlayerState.wallclimbing;
            wallclimbYMomentum = wallclimbInitialYMomentum;
            moveDir = Vector3.zero; // null out the moveDir as it will be updated from UpdateWallclimbing()
            previousWallWalltricked = hit.collider.gameObject;
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
        airControlDir.x = previousAirControlDir.x + airControlDir.x * airControlFactor;
        airControlDir.z = previousAirControlDir.z + airControlDir.z * airControlFactor;

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
                controller.height / 2f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        { // player hit its head during a jump    
            moveDir.y = 0;
        }
        else
        {
            moveDir.y = prevMoveDir.y;
        }

        // Applying gravity
        moveDir.y -= gravity * Time.fixedDeltaTime;

        // Keeping track of airControlDir as we don't want to be able to immediately transform airControlDir from full left to full right, we must fight it too
        previousAirControlDir = airControlDir;
    }



    RaycastHit checkAccessibleWall()
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

        if (rightImpact && rightAngle > 90 && rightAngle < 90 + wallrunEnterAngle)
        { // check if impact && correct side of the wall && angle not too stiff
            return wallImpactRight;
        }
        else if (leftImpact && leftAngle > 90 && leftAngle < 90 + wallrunEnterAngle)
        {
            wallImpactLeft.normal *= -1; // for crossProduct
            return wallImpactLeft;
        }
        else
        {
            // Make sure we're reseting those
            rightImpact = false;
            leftImpact = false;
            // Just return something empty, because nothing is good for a wall run
            return new RaycastHit();
        }
    }



    RaycastHit walclimbCheck(Ray forwardRay)
    {
        RaycastHit hit;
        Physics.Raycast(forwardRay.origin, forwardRay.direction, out hit, 1f);

        return hit;
    }



    void updateWallrunning()
    {
        // Camera locked during wallrun => no updateCamera()

        if (!grounded && canWallRun)
        {
            // update wallHit, check that we're still riding the wall
            wallHit = checkAccessibleWall();
            if (wallHit.collider == null) // Reached end of the wall 
            {
                stopWallRun();
                return;
            }

            if (speed < wallRunMinSpeed || inputVertical <= 0)
            {
                stopWallRun(); // user decided to stop wallrunning or ran out of speed => don't allow him to wallrun again
            }

            // Make sure we set the state to wallrunning
            playerState = PlayerState.wallrunning;

            // get WallRun direction
            Vector3 crossProduct = Vector3.Cross(Vector3.up, wallHit.normal);

            // slerp camera on place 
            Quaternion lookDirection = Quaternion.LookRotation(crossProduct);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookDirection, 3.5f * Time.fixedDeltaTime);

            // Decrement momentum 
            runningMomentum -= wallrunningDecelerationFactor * Time.fixedDeltaTime;
            if (runningMomentum < 0)
            {
                runningMomentum = 0;
            }

            // Actualize moveDir
            moveDir = crossProduct;
            moveDir.Normalize();
            moveDir *= wallRunMinSpeed + ((wallrunMaxSpeed - wallRunMinSpeed) * (runningMomentum / runningRampUpTime));

            // Set the vertical curve of the wallrun
            moveDir.y = prevMoveDir.y;
            moveDir.y -= (gravity / wallrunningGravityFactor) * Time.fixedDeltaTime;

            // update wallRunTime
            wallRunTime += Time.fixedDeltaTime;

            if (inputJump == true) // player requested a wallkick
            {
                // Prepare wallkick 
                runningToJumpingImpulse = Vector3.zero;                         // reset runningToJumpingImpulse in case player has been chaining the wallkicks
                moveDir = Vector3.zero;                                         // and moveDir too because it's affected by previous runningToJumpingImpulse
                float wallrunExitAngleAdapated = (leftImpact) ? wallrunExitAngle : -1 *wallrunExitAngle;             // Get direction angle from wall 
                wallKickRotation = Quaternion.AngleAxis(wallrunExitAngleAdapated, Vector3.up) * transform.rotation ; // compute wallkick quaternion rotation and store it 
                                                                                                                     // for smooth camera slerp during updateJump()

                // Keep track of player's wallrunning speed and transfer it to wallkick's speed 

                speedAtWallkick = speed;

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
        wallrunCooldownLock = wallrunCoolDown;
        wallRunTime = 0.0f;

        // Change playerState
        playerState = PlayerState.jumping;
    }


    void updateWallclimbing()
    {
        if ( !(inputVertical>0) ) // If player stoped pushing the "forward" button => fall
        {
            stopWallClimb();
            return;
        }

        Ray forwardRay = new Ray(transform.position, transform.TransformDirection(Vector3.forward).normalized);
        forwardRay.direction *= 0.1f;

        RaycastHit hit = walclimbCheck(forwardRay);
        if (canWallClimb && hit.collider != null && Vector3.Angle(forwardRay.direction, hit.normal) > 180-wallrunEnterAngle && wallclimbYMomentum > 0)
        {  // if canWallclimb && collision && attack angle is correct => wallclimb && still got some "runningMomentum" (<=> converted as Y speed) to go

            // Make sure we set the state to wallclimbing 
            playerState = PlayerState.wallclimbing;

            // Update wallclimbingTime
            wallclimbingTime += Time.fixedDeltaTime;

            // Look up
            Quaternion lookDirection = Quaternion.LookRotation(hit.normal * -1);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookDirection, 3.5f * Time.fixedDeltaTime);
            //TODO SMOOTH THIS !

            // Decrement wallclimbYMomentum
            wallclimbYMomentum -= wallclimbDecelerationFactor*Time.fixedDeltaTime;
            if (wallclimbYMomentum < 0)
            {
                wallclimbYMomentum = 0;
            }

            // Move up 
            moveDir.y = transform.TransformDirection(Vector3.up).y * ((wallclimbYMomentum/wallclimbInitialYMomentum) * wallclimbMaxSpeed ); // go up * momentum * predetermined max speed

            if(inputJump)
            {
                // Prepare wallturnjump 
                runningToJumpingImpulse = Vector3.zero;                         // reset runningToJumpingImpulse in case player has been chaining the wallkicks
                moveDir = Vector3.zero;                                         // and moveDir too because it's affected by previous runningToJumpingImpulse

                // Turn the camera
                wallturnJumpRotation = Quaternion.AngleAxis(180f, Vector3.up) * transform.rotation ; // compute wallturnjump quaternion 180° rotation and store it 
                                                                                                     // for smooth camera slerp during updateJump()

                // Setting up the impulse vector for the wallclimbturn jump
                wallclimbturnExitVector = Quaternion.AngleAxis(wallclimbExitAngle, Vector3.Cross(hit.normal, Vector3.up)) * hit.normal * wallclimbExitForce; 
                /**
                 * 
                 * 
                 * 
                 * 
                 * 
                 * 
                 * 
                 * 
                 * 
                 * 
                 * TODO :
Merge                                                           DONE
Menus                                                           DONE
Music                                                           DONE
Tutorial text zone                                              DONE
Curseur UI                                                      DONE        
reset vitesse duruing respawn                                   DONE
nettoyer code, le documenter                                    DOING 
corriger system de jump                                         DONE
doc technique                                                   
doc projet organisation     
mouvement de caméra fluide pour wallclimb et wallturnjump       
TESTER LA BUILD WINDOWS POUR VOIR DIFFERENCE AVEC EDITOR !!!!!! 


fix on checkpoint direction
son de pas, saut, atterissage                                   
killSpeedBonus


                 * 
                 * 
                 * 
                 * 
                 * 
                 * 
                 * 
                 * 
                 * 
                 * 
                 * 
                 * 
                 * 
                 * 
                 * 
                 * 
                 * 
                 * 
                 * 
                 * 
                 * 
                 * 
                 * 
                 * 
                 * 
                 * 
                 * 
                 * 
                 * */
                // Set up the wallkick animation timer for updateJumping()
                isWallTurnJumping = wallturnjumpingExitAnimationTime;
                stopWallClimb();
            }
        }
        else
        {   // If the player wallclimbed all the way to the ledge
            stopWallClimb();
        }
    }



    void stopWallClimb()
    {
        // Reset mouseLook internals quaternion has we indirectly messed our own but not its
        mouseLook.Init(transform, camera.transform);

        // Reset important wallkick specific variables
        wallclimbCooldownLock = wallclimbCoolDown;
        wallclimbingTime = 0.0f;

        // Change playerState
        playerState = PlayerState.jumping;
    }



    void updateSliding()
    {
        // Prevent wallrun and wallclimb until the player is running again
        canWallRun = false;
        canWallClimb = false;

        RaycastHit hit;
        bool canStand = !Physics.Raycast(controller.transform.position, controller.transform.up, out hit, originalHeight + controller.skinWidth + slopeClimbingPermissionStep);

        if (canStand)
        {
            if (!inputSlide)
            {
                playerState = PlayerState.running;
                controller.height = originalHeight;
                controller.center = new Vector3(controller.center.x,
                    controller.center.y * originalHeight / crouchingHeight,
                    controller.center.z);
                camera.transform.localPosition = new Vector3(camera.transform.localPosition.x,
                       camera.transform.localPosition.y * originalHeight / crouchingHeight,
                       camera.transform.localPosition.z);
            }
            else
            {
                moveDir = controller.transform.forward;
                moveDir.Normalize();
                runningMomentum -= slidingDecelerationFactor * Time.fixedDeltaTime;
                if (runningMomentum < 0)
                {
                    runningMomentum = 0;
                }

                Vector3 foo = moveDir * ((maxNominalSpeed - runningMinSpeed) * (runningMomentum / runningRampUpTime)); // Calculate current inputs impact on moveDir
                moveDir = foo * (1 - runningInertiaFactor) + prevMoveDir * runningInertiaFactor; // mix current inputs vector and previous one according to runningInertiaFactor
            }
        }
        else
        {
            moveDir = controller.transform.forward;
            moveDir.Normalize();
            runningMomentum -= slidingDecelerationFactor * Time.fixedDeltaTime;
            if (runningMomentum < 0)
            {
                runningMomentum = 0;
            }

            Vector3 foo = moveDir * (runningMinSpeed + ((maxNominalSpeed - runningMinSpeed) * (runningMomentum / runningRampUpTime))); // Calculate current inputs impact on moveDir
            moveDir = foo * (1 - runningInertiaFactor) + prevMoveDir * runningInertiaFactor; // mix current inputs vector and previous one according to runningInertiaFactor
        }

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
        hoomingAttackCurrentMomentum += Time.deltaTime;
        if(hoomingAttackCurrentMomentum < hoomingAttackMomentum)
        {
            // Need to find a way to prevent player to go beyond target.
            moveDir = attackDirection * attackingImpulse;
        } else
        {
            playerState = PlayerState.running;
        }
        
        // Update Camera look and freedom according to playerState
        updateCamera();
    }


    void updateCamera()
    {
        switch (playerState)
        {

            case PlayerState.sliding:
                camera.transform.localPosition = new Vector3(camera.transform.localPosition.x,
                    camera.transform.localPosition.y * crouchingHeight / originalHeight,
                    camera.transform.localPosition.z);

                break;

            default:
                {   // Allow rotation on every axis by default
                    mouseLook.LookRotation(transform, camera.transform);
                    break;
                }
        }
    }


    private void updateSpeed()
    {
        speed = (float)Mathf.Sqrt(controller.velocity.x * controller.velocity.x +
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
