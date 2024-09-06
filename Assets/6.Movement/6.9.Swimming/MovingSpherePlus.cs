/*
 * @Author: wangyun
 * @CreateTime: 2024-08-12 15:20:07 862
 * @LastEditor: wangyun
 * @EditTime: 2024-08-20 16:43:07 866
 */

using UnityEngine;

namespace Swimming {
	public class MovingSpherePlus : MonoBehaviour {
		private static readonly int baseColorId = Shader.PropertyToID("_BaseColor");
		
		[SerializeField] private Transform playerInputSpace;
		[SerializeField, Range(0f, 100f)] private float maxSpeed = 10f, maxClimbSpeed = 4f, maxSwimSpeed = 5f;
		[SerializeField, Range(0f, 100f)] private float maxAcceleration = 80f, maxAirAcceleration = 20f, maxClimbAcceleration = 40f, maxSwimAcceleration = 5f;
		[SerializeField, Range(0f, 10f)] private float jumpHeight = 3f;
		[SerializeField, Range(0, 5)] private int maxAirJumps;
		[SerializeField, Range(0f, 90f)] private float maxGroundAngle = 25f, maxStairsAngle = 50f;
		[SerializeField, Range(90, 180)] private float maxClimbAngle = 140f;
		[SerializeField, Range(0f, 100f)] private float maxSnapSpeed = 100f;
		[SerializeField, Min(0f)] private float probeDistance = 1f;
		[SerializeField] private float submergenceOffset = 0.5f;
		[SerializeField, Min(0.1f)] private float submergenceRange = 1f;
		[SerializeField, Min(0f)] private float buoyancy = 1f;
		[SerializeField, Range(0f, 10f)] private float waterDrag = 1f;
		[SerializeField, Range(0.01f, 1f)] private float swimThreshold = 0.5f;
		[SerializeField, Range(2f, 100f)] private float maxDropSpeed = 30F, maxSteepDropSpeed = 3F;
		[SerializeField] private LayerMask probeMask = -1, stairsMask = -1, climbMask = -1, waterMask = 0;
		[SerializeField] private Material normalMaterial, climbingMaterial, swimmingMaterial;
	
		private Vector3 playerInput;
		private Vector3 velocity, connectionVelocity;
		private Rigidbody body, connectedBody, previousConnectedBody;
		private bool desiredJump, desiresClimbing;
		private int groundContactCount, steepContactCount, climbContactCount;
		private int jumpPhase;
		private float minGroundDotProduct, minStairsDotProduct, minClimbDotProduct;
		private Vector3 contactNormal, steepNormal, climbNormal, lastClimbNormal;
		private int stepsSinceLastGrounded, stepsSinceLastJump;
		private Vector3 upAxis, rightAxis, forwardAxis;
		private Vector3 connectionWorldPosition, connectionLocalPosition;
		private MeshRenderer meshRenderer;
		private float submergence;
		
		private bool OnGround => groundContactCount > 0;
		private bool OnSteep => steepContactCount > 0;
		private bool Climbing => climbContactCount > 0 && stepsSinceLastJump > 2;
		private bool InWater => submergence > 0f;
		private bool Swimming => submergence >= swimThreshold;

		private void OnValidate () {
			minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
			minStairsDotProduct = Mathf.Cos(maxStairsAngle * Mathf.Deg2Rad);
			minClimbDotProduct = Mathf.Cos(maxClimbAngle * Mathf.Deg2Rad);
		}

		private void Awake() {
			body = GetComponent<Rigidbody>();
			body.useGravity = false;
			meshRenderer = GetComponent<MeshRenderer>();
			OnValidate();
		}

		private void Update() {
			playerInput.x = Input.GetAxis("Horizontal");
			playerInput.y = Input.GetAxis("Vertical");
			// TODO: 这里设计是在水中上下游动，参考其他游戏：
			// 奥日因为是2D，无操作自然下沉，Y轴控制上下游动，出水面后自动保持水面状态，可跳跃可下潜，
			// 马里奥奥德赛因为是3D，不支持上下游动，无操作自然下沉，跳跃可往上游一小段，可跳出水面。
			playerInput.z = Swimming ? Input.GetAxis("UpDown") : 0f;
			playerInput = Vector3.ClampMagnitude(playerInput, 1f);
			if (playerInputSpace) {
				rightAxis = ProjectDirectionOnPlane(playerInputSpace.right, upAxis);
				forwardAxis = ProjectDirectionOnPlane(playerInputSpace.forward, upAxis);
			} else {
				rightAxis = ProjectDirectionOnPlane(Vector3.right, upAxis);
				forwardAxis = ProjectDirectionOnPlane(Vector3.forward, upAxis);
			}
			if (Swimming) {
				desiresClimbing = false;
			} else {
				// TODO: 这里是在游泳时不允许跳跃，但一般设计应该是允许跳出水面的。
				desiredJump |= Input.GetButtonDown("Jump");
				desiresClimbing = Input.GetButton("Climb") || Input.GetAxis("Climb") > 0;
			}
			GetComponent<Renderer>().material.SetColor(baseColorId, Color.white * (groundContactCount * 0.25f));
			
			meshRenderer.material = Climbing ? climbingMaterial : Swimming ? swimmingMaterial : normalMaterial;
		}
		
		private void FixedUpdate() {
			Vector3 gravity = CustomGravity.GetGravity(body.position, out upAxis);
			UpdateState();
			if (InWater) {
				velocity *= Mathf.Max(1f - waterDrag * submergence * Time.deltaTime, 0);
			}
			AdjustVelocity();
			if (desiredJump) {
				desiredJump = false;
				Jump(gravity);
			}
			if (Climbing) {
				velocity -= contactNormal * (maxClimbAcceleration * 0.9f * Time.deltaTime);
			} else if (InWater) {
				velocity += gravity * ((1f - buoyancy * submergence) * Time.deltaTime);
			} else if (OnGround && velocity.sqrMagnitude < 0.01f) {
				velocity += contactNormal * (Vector3.Dot(gravity, contactNormal) * Time.deltaTime);
			} else if (desiresClimbing && OnGround) {
				velocity += (gravity - contactNormal * (maxClimbAcceleration * 0.9f)) * Time.deltaTime;
			} else {
				velocity += gravity * Time.deltaTime;
				float _maxDropSpeed = OnSteep ? maxSteepDropSpeed : maxDropSpeed;
				float velocityUp = Vector3.Dot(upAxis, velocity);
				if (velocityUp < -_maxDropSpeed) {
					velocity += (-_maxDropSpeed - velocityUp) * upAxis;
				}
			}
			body.velocity = velocity;
			ClearState();
		}

		private void ClearState() {
			groundContactCount = steepContactCount = climbContactCount = 0;
			contactNormal = steepNormal = climbNormal = Vector3.zero;
			connectionVelocity = Vector3.zero;
			previousConnectedBody = connectedBody;
			connectedBody = null;
			submergence = 0f;
		}

		private void UpdateState() {
			stepsSinceLastGrounded += 1;
			stepsSinceLastJump += 1;
			velocity = body.velocity;
			if (CheckClimbing() || CheckSwimming() || OnGround || SnapToGround() || CheckSteepContacts()) {
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
			if (velocity.y < 0) {
				velocity.y = 0;
			}
			float jumpSpeed = Mathf.Sqrt(2f * gravity.magnitude * jumpHeight);
			if (InWater) {
				jumpSpeed *= Mathf.Max(0f, 1f - submergence / swimThreshold);
			}
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
			if (Swimming) {
				return;
			}
			Vector3 expectClimbNormal = Vector3.zero;
			float expectClimbRightDot = float.MinValue;
			int layer = collision.gameObject.layer;
			float minDot = GetMinDot(layer);
			for (int i = 0; i < collision.contactCount; i++) {
				Vector3 normal = collision.GetContact(i).normal;
				float upDot = Vector3.Dot(upAxis, normal);
				if (upDot >= minDot) {
					groundContactCount++;
					contactNormal += normal;
					connectedBody = collision.rigidbody;
				} else {
					if (upDot > -0.01f) {
						steepContactCount += 1;
						steepNormal += normal;
						if (groundContactCount == 0) {
							connectedBody = collision.rigidbody;
						}
					}
					if (desiresClimbing && upDot >= minClimbDotProduct && (climbMask & 1 << layer) != 0) {
						climbContactCount += 1;
						climbNormal += normal;
						float climbRightDot = Vector3.Dot(Vector3.Cross(normal, upAxis), rightAxis);
						if (climbRightDot > expectClimbRightDot) {
							expectClimbRightDot = climbRightDot;
							expectClimbNormal = normal;
						}
						connectedBody = collision.rigidbody;
					}
				}
			}
			if (Mathf.Approximately(expectClimbRightDot, float.MinValue)) {
				lastClimbNormal = expectClimbNormal;
			}
		}

		private void OnTriggerEnter(Collider other) {
			if ((waterMask & 1 << other.gameObject.layer) != 0) {
				EvaluateSubmergence(other);
			}
		}

		private void OnTriggerStay(Collider other) {
			if ((waterMask & 1 << other.gameObject.layer) != 0) {
				EvaluateSubmergence(other);
			}
		}

		private void EvaluateSubmergence(Collider other) {
			if (Physics.Raycast(body.position + upAxis * submergenceOffset, -upAxis, out RaycastHit hit,
					submergenceRange + 1f, waterMask, QueryTriggerInteraction.Collide)) {
				submergence = 1f - hit.distance / submergenceRange;
			} else {
				submergence = 1f;
			}
			if (Swimming) {
				connectedBody = other.attachedRigidbody;
			}
		}

		private Vector3 ProjectDirectionOnPlane(Vector3 direction, Vector3 normal) {
			return (direction - normal * Vector3.Dot(direction, normal)).normalized;
		}
		
		private void AdjustVelocity() {
			float acceleration, speed;
			Vector3 xAxis, zAxis;
			if (Climbing) {
				acceleration = maxClimbAcceleration;
				speed = maxClimbSpeed;
				xAxis = Vector3.Cross(contactNormal, upAxis);
				zAxis = upAxis;
			} else if (Swimming) {
				float swimFactor = Mathf.Min(1f, submergence / swimThreshold);
				acceleration = Mathf.LerpUnclamped(OnGround ? maxAcceleration : maxAirAcceleration, maxSwimAcceleration, swimFactor);
				speed = Mathf.LerpUnclamped(maxSpeed, maxSwimSpeed, swimFactor);
				xAxis = rightAxis;
				zAxis = forwardAxis;
			} else {
				acceleration = OnGround ? maxAcceleration : maxAirAcceleration;
				speed = OnGround && desiresClimbing ? maxClimbSpeed : maxSpeed;
				xAxis = rightAxis;
				zAxis = forwardAxis;
			}
			xAxis = ProjectDirectionOnPlane(xAxis, contactNormal);
			zAxis = ProjectDirectionOnPlane(zAxis, contactNormal);

			Vector3 relativeVelocity = velocity - connectionVelocity;
			float currentX = Vector3.Dot(relativeVelocity, xAxis);
			float currentZ = Vector3.Dot(relativeVelocity, zAxis);

			float maxSpeedChange = acceleration * Time.deltaTime;
			float newX = Mathf.MoveTowards(currentX, playerInput.x * speed, maxSpeedChange);
			float newZ = Mathf.MoveTowards(currentZ, playerInput.y * speed, maxSpeedChange);

			velocity += xAxis * (newX - currentX) + zAxis * (newZ - currentZ);
			
			if (Swimming) {
				float currentY = Vector3.Dot(relativeVelocity, upAxis);
				float newY = Mathf.MoveTowards(currentY, playerInput.z * speed, maxSpeedChange);
				velocity += upAxis * (newY - currentY);
			}
		}
		
		private bool SnapToGround() {
			// TODO: 遇到个问题，在浅水处跳起，因为刚离开水，stepsSinceLastGrounded还是1，但stepsSinceLastJump已经大于2了，然后探针射线又太长，这导致跳出水面马上被Snap了。
			if (stepsSinceLastGrounded > 1 || stepsSinceLastJump <= 2) {
				return false;
			}
			float speed = velocity.magnitude;
			if (speed > maxSnapSpeed) {
				return false;
			}
			if (!Physics.Raycast(body.position, -upAxis, out RaycastHit hit, probeDistance, probeMask, QueryTriggerInteraction.Ignore)) {
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
		
		private bool CheckClimbing() {
			if (Climbing) {
				if (climbContactCount > 1) {
					climbNormal.Normalize();
					float upDot = Vector3.Dot(upAxis, climbNormal);
					if (upDot >= minGroundDotProduct) {
						climbNormal = lastClimbNormal;
					}
				}
				groundContactCount = climbContactCount;
				contactNormal = climbNormal;
				return true;
			}
			return false;
		}
		
		private bool CheckSwimming() {
			if (Swimming) {
				groundContactCount = 0;
				contactNormal = upAxis;
				return true;
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