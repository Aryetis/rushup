using UnityEngine;

/* -------------------------------------------------------------------------------
	GravityFPSWalker
 
	This component is added to a GameObject with a RigidBody. It allows the player
	to move the RigidBody using the vertical and horizontal inputs, and to jump
	using the jump button.
 
	The RigidBody is pushed towards its own custom Gravity vector. The body will
	rotate to stay upright with the RotationRate.
 
	This component uses a raycast to determine if the RigidBody is standing on 
	the ground. The GroundHeight variable should be the distance between the
	GameObject transform and a little further than the bottom of the RigidBody.
 
	The LookTransform should be a child GameObject which points in the direction
	that the player is looking at. This could for example be a child GameObject 
	with a camera. The LookTransform is used to determine the movement vectors.
 ------------------------------------------------------------------------------ */
[RequireComponent(typeof(Rigidbody))]
public class GravityFPSWalker : MonoBehaviour {

	public Transform LookTransform;ISerializationCallbackReceiver 
	public Vector3 Gravity = Vector3.down * 9.81f;
	public float RotationRate = 0.1f;
	public float Velocity = 8;
	public float GroundControl = 1.0f;
	public float AirControl = 0.2f;
	public float JumpVelocity = 5;
	public float GroundHeight = 1.1f;
	private bool jump;

	void Start() { 
		GetComponent<Rigidbody>().freezeRotation = true;
		GetComponent<Rigidbody>().useGravity = false;
	}

	void Update() {
		jump = jump || Input.GetButtonDown("Jump");
	}

	void FixedUpdate() {
		// Cast a ray towards the ground to see if the Walker is grounded
		bool grounded = Physics.Raycast(transform.position, Gravity.normalized, GroundHeight);

		// Rotate the body to stay upright
		Vector3 gravityForward = Vector3.Cross(Gravity, transform.right);
		Quaternion targetRotation = Quaternion.LookRotation(gravityForward, -Gravity);
		GetComponent<Rigidbody>().rotation = Quaternion.Lerp(GetComponent<Rigidbody>().rotation, targetRotation, RotationRate);

		// Add velocity change for movement on the local horizontal plane
		Vector3 forward = Vector3.Cross(transform.up, -LookTransform.right).normalized;
		Vector3 right = Vector3.Cross(transform.up, LookTransform.forward).normalized;
		Vector3 targetVelocity = (forward * Input.GetAxis("Vertical") + right * Input.GetAxis("Horizontal")) * Velocity;
		Vector3 localVelocity = transform.InverseTransformDirection(GetComponent<Rigidbody>().velocity);
		Vector3 velocityChange = transform.InverseTransformDirection(targetVelocity) - localVelocity;

		// The velocity change is clamped to the control velocity
		// The vertical component is either removed or set to result in the absolute jump velocity
		velocityChange = Vector3.ClampMagnitude(velocityChange, grounded ? GroundControl : AirControl);
		velocityChange.y = jump && grounded ? -localVelocity.y + JumpVelocity : 0;
		velocityChange = transform.TransformDirection(velocityChange);
		GetComponent<Rigidbody>().AddForce(velocityChange, ForceMode.VelocityChange);

		// Add gravity
		GetComponent<Rigidbody>().AddForce(Gravity * GetComponent<Rigidbody>().mass);

		jump = false;
	}

}