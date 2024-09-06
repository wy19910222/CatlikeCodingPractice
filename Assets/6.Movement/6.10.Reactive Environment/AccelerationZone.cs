using UnityEngine;

namespace ReactiveEnvironment {
	public class AccelerationZone : MonoBehaviour {
		[SerializeField, Min(0f)] private float acceleration = 10f, speed = 10f;

		private void OnTriggerEnter(Collider other) {
			Rigidbody body = other.attachedRigidbody;
			if (body && acceleration <= 0f) {
				Accelerate(body);
			}
		}
		
		private void OnTriggerStay(Collider other) {
			Rigidbody body = other.attachedRigidbody;
			if (body && acceleration > 0f) {
				Accelerate(body);
			}
		}

		private void Accelerate(Rigidbody body) {
			Vector3 velocity = transform.InverseTransformDirection(body.velocity);
			if (velocity.y >= speed) {
				return;
			}
			if (acceleration > 0f) {
				velocity.y = Mathf.MoveTowards(velocity.y, speed, acceleration * Time.deltaTime);
			} else {
				velocity.y = speed;
			}
			body.velocity = transform.TransformDirection(velocity);
			if (body.TryGetComponent(out MovingSphere sphere)) {
				sphere.PreventSnapToGround();
			} else if (body.TryGetComponent(out MovingSpherePlus spherePlus)) {
				spherePlus.PreventSnapToGround();
			}
		}
	}
}
