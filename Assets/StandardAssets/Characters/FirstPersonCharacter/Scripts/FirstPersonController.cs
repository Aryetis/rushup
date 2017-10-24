/**
 * I may have completly broke the original assets, oops..... We don't need it anyways 
 * Reimport it if necessary you fools
 * 
 * @Aryetis
 **/








using System;
using UnityEngine;
using UnityStandardAssets.CrossPlatformInput;
using UnityStandardAssets.Utility;
using Random = UnityEngine.Random;


/*
 * Know bug :  _ moon jump
 *             _ can stop mid air cause movement dependent of input
 *             _ reprise à zero de la momentum mid air après arrêt ? check if falling speed impacted
 * 
 */

namespace UnityStandardAssets.Characters.FirstPerson
{
    [RequireComponent(typeof (CharacterController))]
    [RequireComponent(typeof (AudioSource))]
    public class FirstPersonController : MonoBehaviour
    {
        [SerializeField] private float m_jumpStrength;
        [SerializeField] private float m_StickToGroundForce;
        [SerializeField] private float m_GravityMultiplier;
        [SerializeField] private MouseLook m_MouseLook;
        [SerializeField] private AudioClip[] m_FootstepSounds;    // an array of footstep sounds that will be randomly selected from.
        [SerializeField] private AudioClip m_JumpSound;           // the sound played when character leaves the ground.
        [SerializeField] private AudioClip m_LandSound;           // the sound played when character touches back on ground.


        private enum PlayerState {running, jumping, walling, sliding};

        private Camera m_Camera;
        private bool m_JumpRequest;
        private float m_YRotation;
        private Vector2 m_Input;
        private Vector3 m_MoveDir = Vector3.zero;
        private CharacterController m_CharacterController;
        private CollisionFlags m_CollisionFlags;
        private bool m_PreviouslyGrounded;
        private Vector3 m_OriginalCameraPosition;
        private float m_StepCycle;
        private float m_NextStep;
        private AudioSource m_AudioSource;

        [SerializeField] private float m_minSpeed;                   // Player will start running at m_minSpeed
        [SerializeField] private float m_maxNominalSpeed;            // Player won't be able to go faster than m_minSpeed without killing ennemies
        [SerializeField] private float m_runAccelerationFactor;   // Player gain m_accelerationFactor porcentage of its actual speed per second (on the ground)
        [SerializeField] private float m_runDecelerationFactor;   // Player loose m_runDecelerationFactor porcentage of its actual speed per second (on the ground)
        [SerializeField] private float m_jumpAccelerationFactor;      // Player gain m_jumpAccelerationFactor porcentage of its actual speed per second (in air)
        [SerializeField] private float m_jumpDecelerationFactor;      // Player loose m_jumpDecelerationFactor porcentage of its actual speed per second (in air)
                                                                     // WARNING : affect strongly the curvature of the jump (recommanded value : 0)
        [SerializeField] private float m_wallAccelerationFactor;     // Player gain m_wallAccelerationFactor porcentage of its actual speed per second (during wallrun)
                                                                     // WARNING : why the heck would the player accelerate on wall ? Do whatever you want ....
        [SerializeField] private float m_wallDecelerationFactor;     // Player loose m_wallDecelerationFactor porcentage of its actual speed per second (during wallrun)
        [SerializeField] private float m_slidingAccelerationFactor;      // Player gain m_slidingAccelerationFactor porcentage of its actual speed per second (sliding)
                                                                         // WARNING : makes no sense setting it to something else than 0
        [SerializeField] private float m_slidingDecelerationFactor;      // Player loose m_slidingDecelerationFactor porcentage of its actual speed per second (sliding)
        [SerializeField] private float m_wallrunDropSpeed;           // Player will let go of its wallrun if speed goes below m_wallrunDropSpeed
        [SerializeField] private float m_stackSpeedBonus;            // Each monster killed make player goes faster by m_stackSpeedBonus m/s
        private PlayerState playerState = PlayerState.jumping;
        private UnityEngine.UI.Text m_SpeedOMeterText;       // Text printed on the UI containing speed informations
        private UnityEngine.UI.Text m_DebugZoneText;       // Text printed on the UI containing speed informations
        private Vector3 m_previousTimeNormaliedBaseVector;       //
        private Vector3 m_previousMoveVector;                //
        private Vector3 m_previousMoveDir;
        private Vector3 m_previousPosition;
        private float m_previousSpeed;
        // Speed value is split as the following : speed = m_minSpeed + m_speedporcentage*normalizedSpeedVector + m_stackSpeedFactor*m_stackSpeedBonus
        private float m_stackSpeedFactor; // factor of bonus speed stack
        private float m_speedPorcentage;  // factor of vanilla speed 



        // Use this for initialization
        private void Start()
        {
            m_CharacterController = GetComponent<CharacterController>();
            m_Camera = Camera.main;
            m_OriginalCameraPosition = m_Camera.transform.localPosition;
            m_StepCycle = 0f;
            m_NextStep = m_StepCycle/2f;
            m_AudioSource = GetComponent<AudioSource>();
			m_MouseLook.Init(transform , m_Camera.transform);
            m_SpeedOMeterText = GameObject.Find ("SpeedOMeter").GetComponent<UnityEngine.UI.Text>();
            m_DebugZoneText = GameObject.Find ("DebugZone").GetComponent<UnityEngine.UI.Text>();
        }

        // Update is called once per frame
        private void Update()
        {
            // set camera rotation matrix
            m_MouseLook.LookRotation (transform, m_Camera.transform);

            // the jump state needs to read here to make sure it is not missed
            if (!m_JumpRequest && playerState!=PlayerState.jumping)
            {
                m_JumpRequest = CrossPlatformInputManager.GetButtonDown("Jump");
            }

            if (!m_PreviouslyGrounded && m_CharacterController.isGrounded)
            {
                PlayLandingSound();
                m_MoveDir.y = 0f;
                stopJump();
                playerState = PlayerState.running;
            }

            if (m_PreviouslyGrounded && !m_CharacterController.isGrounded && playerState != PlayerState.jumping) // eg : player fell off a cliff
                startJump();

            m_PreviouslyGrounded = m_CharacterController.isGrounded;

            /****** DEBUG DEV ZONE aka CHEAT ZONE******/
            if (Input.GetKeyDown (KeyCode.A))
                // decrease monster stack speed
                decreaseMonsterStackSpeed ();

            if (Input.GetKeyDown(KeyCode.E))
                // increase monster stack speed
                increaseMonsterStackSpeed();
        }


        private void PlayLandingSound()
        {
            m_AudioSource.clip = m_LandSound;
            m_AudioSource.Play();
            m_NextStep = m_StepCycle + .5f;
        }


        private void FixedUpdate()
		{
            float speed = (float) Math.Sqrt(m_CharacterController.velocity.x * m_CharacterController.velocity.x +
                                            m_CharacterController.velocity.z * m_CharacterController.velocity.z);
            // Actualize SpeedOMeter UI text
            m_SpeedOMeterText.text = speed + "m/s";
m_DebugZoneText.text = "m_speedPorcentage : " + m_speedPorcentage;


            /*** DEDUCT DESIRED DIRECTION FROM INPUTS ***/
            // Read input
            float horizontal = CrossPlatformInputManager.GetAxis("Horizontal");
            float vertical = CrossPlatformInputManager.GetAxis("Vertical");
            bool accelerating = (horizontal == 0 && vertical == 0)? false : true;
            // Link m_Input
            m_Input = new Vector2(horizontal, vertical);

            // normalize input if it exceeds 1 in combined length:
            if (m_Input.sqrMagnitude > 1)
                m_Input.Normalize();
            
            // always move along the camera forward as it is the direction that it being aimed at
            Vector3 m_desiredMove = transform.forward * m_Input.y + transform.right * m_Input.x;

            Vector3 m_moveVector = new Vector3();
            Vector3 m_timeNormalizedBaseVector = new Vector3();
            Debug.Log("playerState : " + playerState);
            switch (playerState)
            {
                case PlayerState.running:
                {
                    /*** MODIFY desiredMove according to state ***/
                    // get a normal for the surface that is being touched to move along it
                    RaycastHit hitInfoDown;
                    Physics.SphereCast (transform.position, m_CharacterController.radius, Vector3.down, out hitInfoDown,
                        m_CharacterController.height / 2f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                    m_desiredMove = Vector3.ProjectOnPlane (m_desiredMove, hitInfoDown.normal).normalized;

                    // x & z defined, let's define y
                    if(m_JumpRequest)
                    {
                        m_desiredMove.y += m_jumpStrength; // Give impulse to the player, tweak to take into account speed, max speed jump must be 1.5*m_jumpStrength
                        startJump();
                    }
                    else 
                        m_desiredMove.y -= m_StickToGroundForce;

                    /*** UPDATE ACCELERATION ***/
                    //TODO modify m_speedPorcentage : add support for first decelerating m_stackBonus shenanigans
                    if(accelerating && speed>=m_previousSpeed) // double check with speed>=m_previousSpeed in case player is "accelerating" facing a wall
                    {
                        m_timeNormalizedBaseVector = m_desiredMove * Time.fixedDeltaTime;  // <=> "time normalized" direction vector 
                        m_speedPorcentage += m_runAccelerationFactor * Time.fixedDeltaTime;
                        if (m_speedPorcentage > 100)
                            m_speedPorcentage = 100;
                    }
                    else // decelerating
                    {
                        m_timeNormalizedBaseVector = m_previousTimeNormaliedBaseVector; 
                        m_speedPorcentage -= m_runDecelerationFactor * Time.fixedDeltaTime;
                        if(m_speedPorcentage < 0)
                        {
                            m_speedPorcentage = 0;
                            m_moveVector.Set(0,0,m_moveVector.z);
                            break;
                        }
                    }

                    /*** DEFINE moveVector ***/
                    m_moveVector = m_minSpeed * m_timeNormalizedBaseVector + // Minimal speed
                        ((m_speedPorcentage * (m_timeNormalizedBaseVector) / 100) * (m_maxNominalSpeed - m_minSpeed)) + // Linear acceleration
                        m_stackSpeedFactor * m_timeNormalizedBaseVector; // stack

                    break;
                }

                case PlayerState.jumping:
                {
                    RaycastHit hitInfoUp;
                    if ( Physics.SphereCast (transform.position, m_CharacterController.radius, Vector3.up, out hitInfoUp,
                        m_CharacterController.height / 2f, Physics.AllLayers, QueryTriggerInteraction.Ignore) ) // player hit its head during a jump     
                        m_moveVector.y=0;

                    m_moveVector.x = m_previousMoveVector.x;
                    m_moveVector.y += Physics.gravity.y * m_GravityMultiplier * Time.fixedDeltaTime;
                    m_moveVector.z = m_previousMoveVector.z;
                    break;
                }

                case PlayerState.walling:
                {
                    break;
                }

                case PlayerState.sliding:
                {
                    break;
                }
            }

            /*** APPLY FORCE ***/
            m_CollisionFlags = m_CharacterController.Move(m_moveVector);  

            /*** SAVE VECTOR FOR FUTURE FRAMES ***/
            m_previousTimeNormaliedBaseVector = m_timeNormalizedBaseVector;
            m_previousMoveVector = m_moveVector;
            m_previousPosition = m_CharacterController.transform.position;
            m_previousSpeed = speed;

            m_MouseLook.UpdateCursorLock();
        }


        private void PlayJumpSound()
        {
            m_AudioSource.clip = m_JumpSound;
            m_AudioSource.Play();
        }

        private void PlayFootStepAudio()
        {
            // unused atm so who cares !
            if (!m_CharacterController.isGrounded)
            {
                return;
            }
            // pick & play a random footstep sound from the array,
            // excluding sound at index 0
            int n = Random.Range(1, m_FootstepSounds.Length);
            m_AudioSource.clip = m_FootstepSounds[n];
            m_AudioSource.PlayOneShot(m_AudioSource.clip);
            // move picked sound to index 0 so it's not picked next time
            m_FootstepSounds[n] = m_FootstepSounds[0];
            m_FootstepSounds[0] = m_AudioSource.clip;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Rigidbody body = hit.collider.attachedRigidbody;
            // dont move the rigidbody if the character is on top of it
            if (m_CollisionFlags == CollisionFlags.Below)
                return;
            
            if (body == null || body.isKinematic)
                return;

            body.AddForceAtPosition(m_CharacterController.velocity*0.1f, hit.point, ForceMode.Impulse); // basic repulsion force for walls and stuff
        }

        public void increaseMonsterStackSpeed()
        {
            m_stackSpeedFactor += m_stackSpeedBonus;
        }

        public void decreaseMonsterStackSpeed()
        {
            m_stackSpeedFactor -= m_stackSpeedBonus;
            if (m_stackSpeedFactor <=0)
                m_stackSpeedFactor=0;
        }

        private void startJump()
        {
            m_JumpRequest = false;
            playerState = PlayerState.jumping;
        }

        private void stopJump()
        {
            playerState = PlayerState.running;
        }
    }
}
