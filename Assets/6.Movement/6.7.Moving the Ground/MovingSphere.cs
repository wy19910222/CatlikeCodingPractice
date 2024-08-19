using UnityEngine;

namespace MovingTheGround {
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
	
		private Vector3 velocity, desiredVelocity, connectionVelocity;
		private Rigidbody body, connectedBody, previousConnectedBody;
		private bool desiredJump;
		private int groundContactCount, steepContactCount;
		private int jumpPhase;
		private float minGroundDotProduct, minStairsDotProduct;
		private Vector3 contactNormal, steepNormal;
		private int stepsSinceLastGrounded, stepsSinceLastJump;
		private Vector3 upAxis, rightAxis, forwardAxis;
		private Vector3 connectionWorldPosition, connectionLocalPosition;
		
		private bool OnGround => groundContactCount > 0;
		private bool OnSteep => steepContactCount > 0;

		private void OnValidate () {
			minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
			minStairsDotProduct = Mathf.Cos(maxStairsAngle * Mathf.Deg2Rad);
		}

		private void Awake() {
			body = GetComponent<Rigidbody>();
			body.useGravity = false;
			OnValidate();
		}

		private void Update() {
			Vector2 playerInput;
			playerInput.x = Input.GetAxis("Horizontal");
			playerInput.y = Input.GetAxis("Vertical");
			playerInput = Vector2.ClampMagnitude(playerInput, 1f);
			if (playerInputSpace) {
				rightAxis = ProjectDirectionOnPlane(playerInputSpace.right, upAxis);
				forwardAxis = ProjectDirectionOnPlane(playerInputSpace.forward, upAxis);
			} else {
				rightAxis = ProjectDirectionOnPlane(Vector3.right, upAxis);
				forwardAxis = ProjectDirectionOnPlane(Vector3.forward, upAxis);
			}
			desiredVelocity = new Vector3(playerInput.x, 0f, playerInput.y) * maxSpeed;
			desiredJump |= Input.GetButtonDown("Jump");
			GetComponent<Renderer>().material.SetColor(baseColorId, Color.white * (groundContactCount * 0.25f));
		}
		
		private void FixedUpdate() {
			Vector3 gravity = CustomGravity.GetGravity(body.position, out upAxis);
			UpdateState();
			AdjustVelocity();
			if (desiredJump) {
				desiredJump = false;
				Jump(gravity);
			}
			velocity += gravity * Time.deltaTime;
			body.velocity = velocity;
			ClearState();
		}

		private void ClearState() {
			groundContactCount = steepContactCount = 0;
			contactNormal = steepNormal = connectionVelocity = Vector3.zero;
			previousConnectedBody = connectedBody;
			connectedBody = null;
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
				contactNormal = upAxis;
			}
			if (connectedBody) {
				if (connectedBody.isKinematic || connectedBody.mass >= body.mass) {
					UpdateConnectionState();
				}
			}
		}

		private void UpdateConnectionState() {
			if (connectedBody == previousConnectedBody) {
				Vector3 connectionMovement = connectedBody.transform.TransformPoint(connectionLocalPosition) - connectionWorldPosition;
				connectionVelocity = connectionMovement / Time.deltaTime;
			}
			connectionWorldPosition = body.position;
			connectionLocalPosition = connectedBody.transform.InverseTransformPoint(connectionWorldPosition);
		}
	
		private void Jump(Vector3 gravity) {
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
			float jumpSpeed = Mathf.Sqrt(2f * gravity.magnitude * jumpHeight);
			jumpDirection = (jumpDirection + upAxis).normalized;
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
				float upDot = Vector3.Dot(upAxis, normal);
				if (upDot >= minDot) {
					groundContactCount++;
					contactNormal += normal;
					connectedBody = collision.rigidbody;
				} else if (upDot > -0.01f) {
					steepContactCount += 1;
					steepNormal += normal;
					if (groundContactCount == 0) {
						connectedBody = collision.rigidbody;
					}
				}
			}
		}

		private Vector3 ProjectDirectionOnPlane(Vector3 direction, Vector3 normal) {
			return (direction - normal * Vector3.Dot(direction, normal)).normalized;
		}
		
		private void AdjustVelocity () {
			Vector3 xAxis = ProjectDirectionOnPlane(rightAxis, contactNormal);
			Vector3 zAxis = ProjectDirectionOnPlane(forwardAxis, contactNormal);

			Vector3 relativeVelocity = velocity - connectionVelocity;
			float currentX = Vector3.Dot(relativeVelocity, xAxis);
			float currentZ = Vector3.Dot(relativeVelocity, zAxis);

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
			if (!Physics.Raycast(body.position, -upAxis, out RaycastHit hit, probeDistance, probeMask)) {
				return false;
			}
			float upDot = Vector3.Dot(upAxis, hit.normal);
			if (upDot < GetMinDot(hit.collider.gameObject.layer)) {
				return false;
			}
			groundContactCount = 1;
			contactNormal = hit.normal;
			float dot = Vector3.Dot(velocity, hit.normal);
			if (dot > 0f) {
				velocity = (velocity - hit.normal * dot).normalized * speed;
			}
			connectedBody = hit.rigidbody;
			return true;
		}

		private float GetMinDot(int layer) {
			return (stairsMask & 1 << layer) == 0 ? minGroundDotProduct : minStairsDotProduct;
		}
		
		private bool CheckSteepContacts() {
			if (steepContactCount > 1) {
				steepNormal.Normalize();
				float upDot = Vector3.Dot(upAxis, steepNormal);
				if (upDot >= minGroundDotProduct) {
					groundContactCount = 1;
					contactNormal = steepNormal;
					return true;
				}
			}
			return false;
		}

		private void OnDrawGizmos() {
			Vector3 p = transform.position;
			Vector3 gravity = CustomGravity.GetGravity(p, out Vector3 up);
			Gizmos.color = Color.green;
			Gizmos.DrawRay(p, up);
			Gizmos.color = Color.red;
			Gizmos.DrawRay(p, gravity * 0.1F);
		}
	}
}