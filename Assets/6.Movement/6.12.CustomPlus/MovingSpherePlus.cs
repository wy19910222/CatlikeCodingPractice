/*
 * @Author: wangyun
 * @CreateTime: 2024-08-12 15:20:07 862
 * @LastEditor: wangyun
 * @EditTime: 2024-08-20 16:43:07 866
 */

using UnityEngine;

namespace CustomPlus {
	public class MovingSpherePlus : MonoBehaviour {
		private static readonly int baseColorId = Shader.PropertyToID("_BaseColor");
		
		[SerializeField] private Transform playerInputSpace;
		[SerializeField, Range(0f, 100f)] private float maxSpeed = 10f;
		[SerializeField, Range(0f, 100f)] private float maxAcceleration = 80f;
		[SerializeField, Range(0f, 100f)] private float maxAirAcceleration = 20f;
		[SerializeField, Range(0f, 10f)] private float jumpHeight = 3f;
		[SerializeField, Range(0f, 10f)] private float airJumpHeight = 2f;
		[SerializeField, Range(0f, 5f)] private float jumpEarlyEndGravityScale = 3f;	// 小跳上升时重力变化
		[SerializeField, Range(0f, 5f)] private float airJumpEarlyEndGravityScale = 1f;	// 二段跳小跳上升时重力变化
		[SerializeField, Range(0, 5)] private int maxAirJumps;
		[SerializeField, Range(0, 1)] private float steepJumpInputModifier = 0.7F;	// 蹬墙跳跳跃方向受输入方向影响程度
		[SerializeField, Range(0f, 90f)] private float maxGroundAngle = 25f;
		[SerializeField, Range(0f, 90f)] private float maxStairsAngle = 50f;
		[SerializeField, Range(0f, 90f)] private float minDetourAngle = 45f;	// 撞墙时最小绕行角度，小于该角度不绕行
		[SerializeField, Range(0f, 100f)] private float maxSnapSpeed = 100f;
		[SerializeField, Range(2f, 100f)] private float maxDropSpeed = 30F;	// 最大掉落速度
		[SerializeField, Range(2f, 100f)] private float maxSteepDropSpeed = 3F;	// 最大擦墙掉落速度
		[SerializeField, Min(0f)] private float probeDistance = 1f;
		[SerializeField] private LayerMask probeMask = -1, stairsMask = -1;
		[SerializeField] private Material normalMaterial;
		
		[Header("Climb")]
		[SerializeField, Range(0f, 100f)] private float maxClimbSpeed = 4f;
		[SerializeField, Range(0f, 100f)] private float maxClimbAcceleration = 40f;
		[SerializeField, Range(90, 180)] private float maxClimbAngle = 140f;
		[SerializeField] private LayerMask climbMask = -1;
		[SerializeField] private Material climbingMaterial;
		
		[Header("Swim")]
		[SerializeField] private float submergenceOffset = 0.5f;
		[SerializeField, Min(0.1f)] private float submergenceRange = 1f;
		[SerializeField, Min(0f)] private float buoyancy = 1f;
		[SerializeField, Range(0.01f, 1f)] private float driftThreshold = 0.3f;	// 漂浮阈值，浸没程度大于该阈值为漂浮
		[SerializeField, Range(0.01f, 1f)] private float diveThreshold = 0.95f;	// 潜水阈值，浸没程度大于该阈值为潜水
		[SerializeField] private bool freeDiving;	// 是否自由潜水（摇杆第3轴控制上下移动）
		[SerializeField, Range(0f, 100f)] private float maxSwimSpeed = 5f;
		[SerializeField, Range(0f, 100f)] private float maxSwimAcceleration = 5f;
		[SerializeField, Range(0f, 10f)] private float divingJumpHeight = 1f;	// 非自由潜水模式下，潜水时跳跃高度（往上游一下高度）
		[SerializeField, Range(0f, 10f)] private float waterDrag = 1f;
		[SerializeField, Range(0f, 1f)] private float waterJumpDrag;	// 在水中跳跃受到的阻力
		[SerializeField, Min(0f)] private float driftAlignMaxSpeed = 100f;	// 漂浮状态下高度修正速度
		[SerializeField, Min(0f)] private float driftAlignAcceleration = 15f;	// 漂浮状态下高度修正加速度
		[SerializeField] private bool divingClimbable;	// 潜水状态是否允许攀爬
		[SerializeField] private LayerMask waterMask = 0;
		[SerializeField] private Material driftingMaterial;
		[SerializeField] private Material divingMaterial;
		
		[Header("Rolling")]
		[SerializeField] private Transform ball;
		[SerializeField, Min(0.1f)] private float ballRadius = 0.5f;
		[SerializeField, Min(0f)] private float ballAlignSpeed = 180f;
		[SerializeField, Min(0f)] private float ballAirRotation = 0.5f, ballSwimRotation = 2f;

		private Rigidbody body;
		private MeshRenderer meshRenderer;
		
		// Input
		private Vector3 playerInput;
		private bool desiredJump, desiredJumpHolding, desiresClimbing;
		
		// Space
		private Vector3 upAxis, rightAxis, forwardAxis;
		
		// Status
		private Vector3 velocity;
		private int groundContactCount, steepContactCount, climbContactCount;
		private int jumpPhase;
		private bool jumpRising;
		private float minGroundDotProduct, minStairsDotProduct, minClimbDotProduct, maxDetourDotProduct;
		private Vector3 contactNormal, steepNormal, climbNormal, expectClimbNormal;
		private int stepsSinceLastGrounded, stepsSinceLastJump;
		private float submergence;
		
		// Connect
		private Rigidbody connectedBody, previousConnectedBody;
		private Vector3 connectingSelfWorldPosition, connectionLocalPosition;
		private Vector3 connectionVelocity;
		
		// Rolling
		private Vector3 lastContactNormal, lastSteepNormal, lastConnectionVelocity;
		
		private bool OnGround => groundContactCount > 0;
		private bool OnSteep => steepContactCount > 0;
		private bool Climbing => climbContactCount > 0 && stepsSinceLastJump > 2;
		private bool InWater => submergence > 0f;
		private bool Swimming => Diving || Drifting;
		private bool Drifting => submergence >= driftThreshold && submergence < diveThreshold;
		private bool Diving => submergence >= diveThreshold;

		private void OnValidate () {
			minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
			minStairsDotProduct = Mathf.Cos(maxStairsAngle * Mathf.Deg2Rad);
			minClimbDotProduct = Mathf.Cos(maxClimbAngle * Mathf.Deg2Rad);
			maxDetourDotProduct = Mathf.Cos(minDetourAngle * Mathf.Deg2Rad);
			driftThreshold = Mathf.Min(driftThreshold, diveThreshold);
		}

		private void Awake() {
			body = GetComponent<Rigidbody>();
			body.useGravity = false;
			meshRenderer = ball.GetComponent<MeshRenderer>();
			OnValidate();
		}

		private void Update() {
			playerInput.x = Input.GetAxis("Horizontal");
			playerInput.z = Input.GetAxis("Vertical");
			playerInput.y = freeDiving && Swimming ? Input.GetAxis("UpDown") : 0f;
			playerInput = Vector3.ClampMagnitude(playerInput, 1f);
			if (playerInputSpace) {
				rightAxis = ProjectDirectionOnPlane(playerInputSpace.right, upAxis);
				forwardAxis = ProjectDirectionOnPlane(playerInputSpace.forward, upAxis);
			} else {
				rightAxis = ProjectDirectionOnPlane(Vector3.right, upAxis);
				forwardAxis = ProjectDirectionOnPlane(Vector3.forward, upAxis);
			}
			if (freeDiving && Diving) {
				// 自由潜水模式不跳
				desiredJump = false;
				desiredJumpHolding = false;
			} else {
				desiredJump |= Input.GetButtonDown("Jump");
				desiredJumpHolding = Input.GetButton("Jump");
			}
			if (!divingClimbable && Diving) {
				desiresClimbing = false;
			} else {
				desiresClimbing = Input.GetButton("Climb") || Input.GetAxis("Climb") > 0;
			}
			GetComponent<Renderer>().material.SetColor(baseColorId, Color.white * (groundContactCount * 0.25f));
			
			UpdateBall();
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
			HandleGravity(gravity);
			body.velocity = velocity;
			ClearState();
		}

		private void UpdateState() {
			stepsSinceLastGrounded += 1;
			stepsSinceLastJump += 1;
			velocity = body.velocity;
			if (CheckClimbing() || OnGround || SnapToGround() || CheckSteepContacts() || CheckSwimming()) {
				if (!Swimming) {
					stepsSinceLastGrounded = 0;
				}
				if (stepsSinceLastJump > 1) {
					jumpPhase = 0;
					// 落地时，跳跃上升阶段结束
					if (!Swimming) {
						jumpRising = false;
					}
				}
				if (groundContactCount > 1) {
					contactNormal.Normalize();
				}
			} else {
				contactNormal = upAxis;
				// 上升速度不大于0时，跳跃上升阶段结束
				if (jumpRising && Vector3.Dot(velocity,upAxis) <= 0) {
					jumpRising = false;
				}
			}
			if (connectedBody) {
				if (connectedBody.isKinematic || connectedBody.mass >= body.mass) {
					UpdateConnectionState();
				}
			}
		}

		#region State Check
		private bool CheckClimbing() {
			if (Climbing) {
				if (climbContactCount > 1) {
					climbNormal.Normalize();
					float upDot = Vector3.Dot(upAxis, climbNormal);
					if (upDot >= minGroundDotProduct) {
						climbNormal = expectClimbNormal;
					}
				}
				groundContactCount = climbContactCount;
				contactNormal = climbNormal;
				return true;
			}
			return false;
		}
		
		private bool SnapToGround() {
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
		
		private bool CheckSwimming() {
			if (Swimming) {
				contactNormal = upAxis;
				return true;
			}
			return false;
		}
		#endregion

		private void UpdateConnectionState() {
			if (connectedBody == previousConnectedBody) {
				Vector3 connectionMovement = connectedBody.transform.TransformPoint(connectionLocalPosition) - connectingSelfWorldPosition;
				connectionVelocity = connectionMovement / Time.deltaTime;
			}
			connectingSelfWorldPosition = body.position;
			connectionLocalPosition = connectedBody.transform.InverseTransformPoint(connectingSelfWorldPosition);
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
				float swimFactor = Mathf.Min(1f, submergence / diveThreshold);
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

			// 撞墙的时候，超过一定角度才绕行
			Vector3 _playerInput = playerInput;
			if (OnSteep) {
				Vector3 inputDirection = xAxis * _playerInput.x + zAxis * _playerInput.z;
				if (freeDiving && Swimming) {
					inputDirection = (inputDirection + upAxis * _playerInput.y).normalized;
				}
				float detourDot = Vector3.Dot(inputDirection, -steepNormal);
				if (detourDot > maxDetourDotProduct) {
					_playerInput = Vector3.zero;
				}
			}
			
			Vector3 relativeVelocity = velocity - connectionVelocity;
			Vector3 adjustment;
			adjustment.x = _playerInput.x * speed - Vector3.Dot(relativeVelocity, xAxis);
			adjustment.z = _playerInput.z * speed - Vector3.Dot(relativeVelocity, zAxis);
			adjustment.y = freeDiving && Swimming ? _playerInput.y * speed - Vector3.Dot(relativeVelocity, upAxis) : 0f;
			adjustment = Vector3.ClampMagnitude(adjustment, acceleration * Time.deltaTime);
			velocity += xAxis * adjustment.x + zAxis * adjustment.z;
			if (freeDiving && Swimming) {
				// 自由潜水，高度摇杆值为期望速度
				velocity += upAxis * adjustment.y;
			}
		}
	
		private void Jump(Vector3 gravity) {
			Vector3 jumpDirection;
			if (OnGround || Swimming) {
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

			float jumpSpeed = Diving ?
					Mathf.Sqrt(2f * gravity.magnitude * (1f - buoyancy) * divingJumpHeight) :
					Mathf.Sqrt(2f * gravity.magnitude * (jumpPhase > 1 ? airJumpHeight : jumpHeight));
			if (InWater) {
				jumpSpeed *= Mathf.Max(0f, 1f - Mathf.Clamp01(submergence / diveThreshold) * waterJumpDrag);
			}
			// 方向键应该影响蹬墙跳的效果
			if (OnSteep) {
				Vector3 direction = rightAxis * playerInput.x + forwardAxis * playerInput.z;
				jumpDirection += jumpDirection * (Vector3.Dot(direction, jumpDirection) * steepJumpInputModifier);
				jumpDirection = (jumpDirection + upAxis).normalized;
			}
			float alignedSpeed = Vector3.Dot(velocity, jumpDirection);
			if (alignedSpeed > 0f) {
				jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f);
			}
			velocity += jumpDirection * jumpSpeed;
			jumpRising = true;
		}

		private void HandleGravity(Vector3 gravity) {
			if (Climbing) {
				velocity -= contactNormal * (maxClimbAcceleration * 0.9f * Time.deltaTime);
			} else if (InWater) {
				if (Drifting) {
					if (freeDiving && playerInput.y >= 0 || !freeDiving && playerInput != Vector3.zero) {
						// 水面漂浮状态，移动可保持漂浮
						float driftRange = diveThreshold - driftThreshold;
						float percent = (submergence - driftThreshold) / driftRange * 2 - 1;
						float desiredUpVelocity = percent * Time.deltaTime * driftAlignMaxSpeed;
						float upVelocity = Vector3.Dot(velocity, upAxis);
						velocity += upAxis * Mathf.MoveTowards(0, desiredUpVelocity - upVelocity, driftAlignAcceleration * Time.deltaTime);
					} else {
						// 水面漂浮状态，静止会缓慢下沉
						velocity += gravity * ((1f - buoyancy * submergence) * Time.deltaTime);
					}
				} else {
					velocity += gravity * ((1f - buoyancy * submergence) * Time.deltaTime);
				}
			} else if (OnGround && velocity.sqrMagnitude < 0.01f) {
				velocity += contactNormal * (Vector3.Dot(gravity, contactNormal) * Time.deltaTime);
			} else if (desiresClimbing && OnGround) {
				velocity += (gravity - contactNormal * (maxClimbAcceleration * 0.9f)) * Time.deltaTime;
			} else {
				// 跳跃没到最高处就放开跳跃键
				if (jumpRising && !desiredJumpHolding) {
					velocity += gravity * (Time.deltaTime * (jumpPhase == 1 ? jumpEarlyEndGravityScale : airJumpEarlyEndGravityScale));
				} else {
					velocity += gravity * Time.deltaTime;
				}
				float _maxDropSpeed = OnSteep ? maxSteepDropSpeed : maxDropSpeed;
				float velocityUp = Vector3.Dot(upAxis, velocity);
				if (velocityUp < -_maxDropSpeed) {
					velocity += (-_maxDropSpeed - velocityUp) * upAxis;
				}
			}
		}

		private void ClearState() {
			lastContactNormal = contactNormal;
			lastSteepNormal = steepNormal;
			lastConnectionVelocity = connectionVelocity;

			groundContactCount = steepContactCount = climbContactCount = 0;
			contactNormal = steepNormal = climbNormal = Vector3.zero;
			connectionVelocity = Vector3.zero;
			previousConnectedBody = connectedBody;
			connectedBody = null;
			submergence = 0f;
		}

		#region Physics Message
		private void OnCollisionEnter(Collision collision) {
			EvaluateCollision(collision);
		}

		private void OnCollisionStay(Collision collision) {
			EvaluateCollision(collision);
		}

		private void EvaluateCollision(Collision collision) {
			Vector3 _expectClimbNormal = Vector3.zero;
			float _expectClimbRightDot = float.MinValue;
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
						if (climbRightDot > _expectClimbRightDot) {
							_expectClimbRightDot = climbRightDot;
							_expectClimbNormal = normal;
						}
						connectedBody = collision.rigidbody;
					}
				}
			}
			if (!Mathf.Approximately(_expectClimbRightDot, float.MinValue)) {
				expectClimbNormal = _expectClimbNormal;
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
		#endregion

		#region Utility
		private Vector3 ProjectDirectionOnPlane(Vector3 direction, Vector3 normal) {
			return (direction - normal * Vector3.Dot(direction, normal)).normalized;
		}

		private float GetMinDot(int layer) {
			return (stairsMask & 1 << layer) == 0 ? minGroundDotProduct : minStairsDotProduct;
		}
		#endregion

		#region Rolling
		private void UpdateBall() {
			Material ballMaterial = normalMaterial;
			Vector3 rotationPlaneNormal = lastContactNormal;
			float rotationFactor = 1f;
			if (Climbing) {
				ballMaterial = climbingMaterial;
			} else if (Drifting) {
				ballMaterial = driftingMaterial;
				rotationFactor = ballSwimRotation;
			} else if (Diving) {
				ballMaterial = divingMaterial;
				rotationFactor = ballSwimRotation;
			} else if (!OnGround) {
				if (OnSteep) {
					rotationPlaneNormal = lastSteepNormal;
				} else {
					rotationFactor = ballAirRotation;
				}
			}
			meshRenderer.material = ballMaterial;
			Vector3 movement = (body.velocity - lastConnectionVelocity) * Time.deltaTime;
			movement -= rotationPlaneNormal * Vector3.Dot(movement, rotationPlaneNormal);
			float distance = movement.magnitude;
			Quaternion rotation = ball.rotation;	// 这里本来就应该用rotation，之所以教程用localRotation是因为父节点rotation是Quaternion.identity
			if (connectedBody && connectedBody == previousConnectedBody) {
				rotation = Quaternion.Euler(connectedBody.angularVelocity * (Mathf.Rad2Deg * Time.deltaTime)) * rotation;
				if (distance < 0.001f) {
					ball.rotation = rotation;
					return;
				}
			} else if (distance < 0.001f) {
				return;
			}
			float angle = distance * rotationFactor * (180f / Mathf.PI) / ballRadius;
			Vector3 rotationAxis = Vector3.Cross(rotationPlaneNormal, movement).normalized;
			rotation = Quaternion.Euler(rotationAxis * angle) * rotation;
			if (ballAlignSpeed > 0f) {
				rotation = AlignBallRotation(rotationAxis, rotation, distance);
			}
			ball.rotation = rotation;
		}

		private Quaternion AlignBallRotation(Vector3 rotationAxis, Quaternion rotation, float traveledDistance) {
			Vector3 ballAxis = ball.up;
			float dot = Mathf.Clamp(Vector3.Dot(ballAxis, rotationAxis), -1f, 1f);
			float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
			float maxAngle = ballAlignSpeed * traveledDistance;

			Quaternion newAlignment = Quaternion.FromToRotation(ballAxis, rotationAxis) * rotation;
			if (angle <= maxAngle) {
				return newAlignment;
			} else {
				return Quaternion.SlerpUnclamped(rotation, newAlignment, maxAngle / angle);
			}
		}
		#endregion

		#region Gizmos
		private void OnDrawGizmos() {
			Vector3 p = transform.position;
			Vector3 gravity = CustomGravity.GetGravity(p, out Vector3 up);
			Gizmos.color = Color.green;
			Gizmos.DrawRay(p, up);
			Gizmos.color = Color.red;
			Gizmos.DrawRay(p, gravity * 0.1F);
		}
		#endregion

		#region Public
		public void SetVelocity(Vector3 value) {
			jumpRising = false;
			stepsSinceLastJump = -1;
			body.velocity = value;
		}
		#endregion
	}
}