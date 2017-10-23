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


        private enum PlayerState {running, jumping, walling};

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
        [SerializeField] private float m_runDecelerationFactor;   // Player loose m_accelerationFactor porcentage of its actual speed per second (on the ground)
        [SerializeField] private float m_jumpAccelerationFactor;      // Player gain m_accelerationFactor porcentage of its actual speed per second (in air)
        [SerializeField] private float m_jumpDecelerationFactor;      // Player loose m_accelerationFactor porcentage of its actual speed per second (in air)
                                                                     // WARNING : affect strongly the curvature of the jump (recommanded value : 0)
        [SerializeField] private float m_wallAccelerationFactor;     // Player gain m_accelerationFactor porcentage of its actual speed per second (during wallrun)
                                                                     // WARNING : why the heck would the player accelerate on wall ? Do whatever you want ....
        [SerializeField] private float m_wallDecelerationFactor;     // Player loose m_accelerationFactor porcentage of its actual speed per second (during wallrun)
        [SerializeField] private float m_wallrunDropSpeed;           // Player will let go of its wallrun if speed goes below m_wallrunDropSpeed
        [SerializeField] private float m_stackSpeedBonus;            // Each monster killed make player goes faster by m_stackSpeedBonus m/s
        private PlayerState playerState = PlayerState.jumping;
        private UnityEngine.UI.Text m_SpeedOMeterText;       // Text printed on the UI containing speed informations
        private Vector3 m_previousNormaliedBaseVector;       //
        private Vector3 m_previousMoveVector;                //
        private Vector3 m_previousMoveDir;
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
            Vector3 desiredMove = transform.forward * m_Input.y + transform.right * m_Input.x;

            /*** SET MoveDir.x & z ***/
            // get a normal for the surface that is being touched to move along it
            RaycastHit hitInfoDown;
            Physics.SphereCast (transform.position, m_CharacterController.radius, Vector3.down, out hitInfoDown,
                m_CharacterController.height / 2f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            desiredMove = Vector3.ProjectOnPlane (desiredMove, hitInfoDown.normal).normalized;

            m_MoveDir.x = desiredMove.x;
            m_MoveDir.z = desiredMove.z;

            Debug.Log(desiredMove);

            /*** SET MoveDir.y ***/
            switch (playerState)
            {
                case PlayerState.running:
                {
                    m_MoveDir.y = -m_StickToGroundForce; // emulate gravity, in case player is walking on a slanted floor
                    if (m_JumpRequest)
                    {
                        // TODO TWEAK IT SO WE  keep a 1,5 factor between standard jump and max speed jump
                        m_MoveDir.y = m_jumpStrength; // Give impulse to the player
                        startJump ();
                    }
                    break;
                }
                case PlayerState.jumping:
                {
                    RaycastHit hitInfoUp;
                    if ( Physics.SphereCast (transform.position, m_CharacterController.radius, Vector3.up, out hitInfoUp,
                        m_CharacterController.height / 2f, Physics.AllLayers, QueryTriggerInteraction.Ignore) ) // player hit its head during a jump     
                        m_MoveDir.y=0;

                    m_MoveDir.x = m_previousMoveDir.x;
                    m_MoveDir.z = m_previousMoveDir.z;
                    m_MoveDir += Physics.gravity * m_GravityMultiplier * Time.fixedDeltaTime;
                    break;
                }
                case PlayerState.walling:
                {
                    break;
                }
            }
                
                
            /*** CREATE FORCE (according to correct acceleration, deceleration factors) ***/
            Vector3 m_normalizeBaseVector;
            Vector3 moveVector;

            // Setting localAccelFactor and localDecelFactor
            float localAccelFactor=0.0f;
            float localDecelFactor=0.0f;

            switch (playerState)
            {
                case PlayerState.running:
                {
                    localAccelFactor = m_runAccelerationFactor;
                    localAccelFactor = m_runDecelerationFactor;
                    break;
                }
                case PlayerState.jumping:
                {
                    localAccelFactor = m_jumpAccelerationFactor;
                    localAccelFactor = m_jumpDecelerationFactor;
                    break;
                }
            case PlayerState.walling:
                {
                    localAccelFactor = m_wallAccelerationFactor;
                    localDecelFactor = m_wallDecelerationFactor;
                    break;
                }
            }

            // Calculating m_SpeedPorcentage
            if (accelerating)
            {
                Debug.Log("bouze de mamouth");
                m_normalizeBaseVector = m_MoveDir * Time.fixedDeltaTime;  // <=> normalized direction vector 
                m_speedPorcentage += localAccelFactor * Time.fixedDeltaTime;
                if (m_speedPorcentage > 100)
                    m_speedPorcentage = 100;
            }
            else // decelerating
            {
                Debug.Log("bite de chat");
                m_normalizeBaseVector = m_previousNormaliedBaseVector; 
                m_speedPorcentage -= localDecelFactor * Time.fixedDeltaTime;
                if (m_speedPorcentage < 0)
                    m_speedPorcentage = 0;
            }

            moveVector = m_minSpeed * m_normalizeBaseVector + // Minimal speed
                        ((m_speedPorcentage * (m_normalizeBaseVector) / 100) * (m_maxNominalSpeed - m_minSpeed)) + // Linear acceleration
                        m_stackSpeedFactor * m_normalizeBaseVector; // stack

            /*** APPLY FORCE ***/
            m_CollisionFlags = m_CharacterController.Move(moveVector);  
           
            /*** SET VARIABLE FROM THE PAST ***/
            m_previousMoveDir = m_MoveDir;
            m_previousNormaliedBaseVector = m_normalizeBaseVector;
            m_previousMoveVector = moveVector;

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
