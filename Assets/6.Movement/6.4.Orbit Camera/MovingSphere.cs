using UnityEngine;

namespace OrbitCamera {
	public class MovingSphere : MonoBehaviour {
		private static readonly int baseColorId = Shader.PropertyToID("_BaseColor");
		
		[SerializeField] private Transform playerInputSpace;
		[SerializeField, Range(0f, 100f)] private float maxSpeed = 10f;
		[SerializeField, Range(0f, 100f)] private float maxAcceleration = 10f, maxAirAcceleration = 1f;
		[SerializeField, Range(0f, 10f)] private float jumpHeight = 2f;
		[SerializeField, Range(0, 5)] private int maxAirJumps;
		[SerializeField, Range(0f, 90f)] private float maxGroundAngle = 25f, maxStairsAngle = 50f;
		[SerializeField, Range(0f, 100f)] private float maxSnapSpeed = 100f;
		[SerializeField, Min(0f)] private float probeDistance = 1f;
		[SerializeField] private LayerMask probeMask = -1, stairsMask = -1;
	
		private Vector3 velocity, desiredVelocity;
		private Rigidbody body;
		private bool desiredJump;
		private int groundContactCount, steepContactCount;
		private int jumpPhase;
		private float minGroundDotProduct, minStairsDotProduct;
		private Vector3 contactNormal, steepNormal;
		private int stepsSinceLastGrounded, stepsSinceLastJump;
		
		private bool OnGround => groundContactCount > 0;
		private bool OnSteep => steepContactCount > 0;

		private void OnValidate () {
			minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
			minStairsDotProduct = Mathf.Cos(maxStairsAngle * Mathf.Deg2Rad);
		}

		private void Awake() {
			body = GetComponent<Rigidbody>();
			OnValidate();
		}

		private void Update() {
			Vector2 playerInput;
			playerInput.x = Input.GetAxis("Horizontal");
			playerInput.y = Input.GetAxis("Vertical");
			playerInput = Vector2.ClampMagnitude(playerInput, 1f);
			if (playerInputSpace) {
				desiredVelocity = playerInputSpace.TransformDirection(playerInput.x, 0f, playerInput.y) * maxSpeed;
			} else {
				desiredVelocity = new Vector3(playerInput.x, 0f, playerInput.y) * maxSpeed;
			}
			desiredJump |= Input.GetButtonDown("Jump");
			GetComponent<Renderer>().material.SetColor(baseColorId, Color.white * (groundContactCount * 0.25f));
		}
		
		private void FixedUpdate() {
			UpdateState();
			AdjustVelocity();
			if (desiredJump) {
				desiredJump = false;
				Jump();
			}
			body.velocity = velocity;
			ClearState();
		}

		private void ClearState() {
			groundContactCount = steepContactCount = 0;
			contactNormal = steepNormal = Vector3.zero;
		}

		private void UpdateState() {
			stepsSinceLastGrounded += 1;
			stepsSinceLastJump += 1;
			velocity = body.velocity;
			if (OnGround || SnapToGround() || CheckSteepContacts()) {
				stepsSinceLastGrounded = 0;
				if (stepsSinceLastJump > 1) {
					jumpPhase = 0;
				}
				if (groundContactCount > 1) {
					contactNormal.Normalize();
				}
			} else {
				contactNormal = Vector3.up;
			}
		}
	
		private void Jump() {
			Vector3 jumpDirection;
			if (OnGround) {
				jumpDirection = contactNormal;
			} else if (OnSteep) {
				jumpDirection = steepNormal;
				jumpPhase = 0;
			} else if (maxAirJumps > 0 && jumpPhase <= maxAirJumps) {
				if (jumpPhase == 0) {
					jumpPhase = 1;
				}
				jumpDirection = contactNormal;
			} else {
				return;
			}
			stepsSinceLastJump = 0;
			jumpPhase += 1;
			float jumpSpeed = Mathf.Sqrt(- 2f * Physics.gravity.y * jumpHeight);
			jumpDirection = (jumpDirection + Vector3.up).normalized;
			float alignedSpeed = Vector3.Dot(velocity, jumpDirection);
			if (alignedSpeed > 0f) {
				jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f);
			}
			velocity += jumpDirection * jumpSpeed;
		}

		private void OnCollisionEnter(Collision collision) {
			EvaluateCollision(collision);
		}

		private void OnCollisionStay(Collision collision) {
			EvaluateCollision(collision);
		}

		private void EvaluateCollision(Collision collision) {
			float minDot = GetMinDot(collision.gameObject.layer);
			for (int i = 0; i < collision.contactCount; i++) {
				Vector3 normal = collision.GetContact(i).normal;
				if (normal.y >= minDot) {
					groundContactCount++;
					contactNormal += normal;
				} else if (normal.y > -0.01f) {
					steepContactCount += 1;
					steepNormal += normal;
				}
			}
		}

		private Vector3 ProjectOnContactPlane(Vector3 vector) {
			return vector - contactNormal * Vector3.Dot(vector, contactNormal);
		}
		
		private void AdjustVelocity () {
			Vector3 xAxis = ProjectOnContactPlane(Vector3.right).normalized;
			Vector3 zAxis = ProjectOnContactPlane(Vector3.forward).normalized;
			
			float currentX = Vector3.Dot(velocity, xAxis);
			float currentZ = Vector3.Dot(velocity, zAxis);

			float acceleration = OnGround ? maxAcceleration : maxAirAcceleration;
			float maxSpeedChange = acceleration * Time.deltaTime;
			
			float newX = Mathf.MoveTowards(currentX, desiredVelocity.x, maxSpeedChange);
			float newZ = Mathf.MoveTowards(currentZ, desiredVelocity.z, maxSpeedChange);
			
			velocity += xAxis * (newX - currentX) + zAxis * (newZ - currentZ);
		}
		
		private bool SnapToGround() {
			if (stepsSinceLastGrounded > 1 || stepsSinceLastJump <= 2) {
				return false;
			}
			float speed = velocity.magnitude;
			if (speed > maxSnapSpeed) {
				return false;
			}
			if (!Physics.Raycast(body.position, Vector3.down, out RaycastHit hit, probeDistance, probeMask)) {
				return false;
			}
			if (hit.normal.y < GetMinDot(hit.collider.gameObject.layer)) {
				return false;
			}
			groundContactCount = 1;
			contactNormal = hit.normal;
			float dot = Vector3.Dot(velocity, hit.normal);
			if (dot > 0f) {
				velocity = (velocity - hit.normal * dot).normalized * speed;
			}
			return true;
		}

		private float GetMinDot(int layer) {
			return (stairsMask & 1 << layer) == 0 ? minGroundDotProduct : minStairsDotProduct;
		}
		
		private bool CheckSteepContacts() {
			if (steepContactCount > 1) {
				steepNormal.Normalize();
				if (steepNormal.y >= minGroundDotProduct) {
					groundContactCount = 1;
					contactNormal = steepNormal;
					return true;
				}
			}
			return false;
		}
	}
}